import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, approveSupplier, switchCompany, unique, createLeafCategory } from './inventoryHelpers.ts'

/**
 * Lát R2 (design.md mục 18.26) – chống ghi đè khi hai người cùng sửa: đang mở đơn mua hàng nháp trên trang thì người khác lưu phiếu đó
 * (qua API); bấm Lưu trên trang bản cũ phải báo "người khác thay đổi" (409), không ghi đè thay đổi của người kia.
 */
test('lưu phiếu đã bị người khác sửa thì báo xung đột, không ghi đè', async ({ page, request }) => {
  const head = await api(request)
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const hop = uoms.find((u) => u.code === 'HOP')!.id
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const category = await createLeafCategory<{ id: string }>(head, { code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm xung đột ${crypto.randomUUID().slice(0, 8)}`, parentId: null, inventoryAccount: null })
  const item = await head.send<{ id: string }>('post', '/api/items', {
    code: null, name: 'Hàng thử xung đột', englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: hop,
    specification: null, brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false,
    trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })
  const supplier = await head.send<{ id: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC xung đột ${unique('X')}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  await approveSupplier(request, supplier.id)
  const bha = await api(request, 'BHA')
  await bha.send('put', `/api/suppliers/${supplier.id}/company`, { localCode: null, startDate: null, isActive: true })
  const warehouses = await bha.get<{ id: string; code: string }[]>('/api/warehouses')
  const kt = warehouses.find((w) => w.code === 'KT')!.id
  const today = new Date().toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' })
  // Đơn mua hàng nháp (phiếu nhập kho cũ gỡ ở lát K3).
  const body = (note: string, lineId: string | null) => ({
    date: today, supplierId: supplier.id, warehouseId: kt, expectedDate: today, paymentTerms: null, deliveryTerms: null, deliveryAddress: null,
    receiveTolerancePct: 0, note,
    lines: [{ id: lineId, itemId: item.id, uomId: hop, qty: 3, unitPrice: 10000, discountPct: 0, vatCode: '10', deliveryDate: null, note: null, sourcePrLineId: null }],
  })
  const draft = await bha.send<{ id: string; lines: { id: string }[] }>('post', '/api/purchase-orders', body('Bản đầu', null))

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${draft.id}`)
  await expect(page.getByLabel('Ghi chú', { exact: true })).toHaveValue('Bản đầu')

  // Người khác lưu phiếu trong lúc trang đang mở.
  await bha.send('put', `/api/purchase-orders/${draft.id}`, body('Người khác đã sửa', draft.lines[0].id))

  await page.getByLabel('Ghi chú', { exact: true }).fill('Tôi sửa trên bản cũ')
  await page.keyboard.press('Control+s')
  await expect(page.getByText(/người khác thay đổi/).first()).toBeVisible()
  const saved = await bha.get<{ note: string }>(`/api/purchase-orders/${draft.id}`)
  expect(saved.note).toBe('Người khác đã sửa')
})
