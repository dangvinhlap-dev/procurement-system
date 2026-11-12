import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, approveSupplier, openByCommand, switchCompany, unique } from './inventoryHelpers.ts'
import { approveAsNewAdmin, todayVn } from './receiptHelpers.ts'

/**
 * Lát X7 (design.md mục 19.3): BHA mua nội bộ của NMI bằng PO đặt cho NCC nội bộ của NMI – giá bán nội bộ chỉ là gợi ý (nhập tay được),
 * PO duyệt xong thì NMI thấy ở "Đơn bán nội bộ" (DBN), xác nhận đơn; PO của BHA hiện trạng thái bên bán.
 */
test('Mua nội bộ: PO cho NCC nội bộ, giá gợi ý, bên bán xác nhận ở DBN', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const companies = await head.get<{ id: string; code: string }[]>('/api/companies')
  const bhaId = companies.find((c) => c.code === 'BHA')!.id
  const nmiId = companies.find((c) => c.code === 'NMI')!.id

  // NCC nội bộ của NMI: mỗi công ty tối đa một – dùng lại nếu CSDL test đã có.
  const suppliers = await head.get<{ items: { id: string; internalCompanyCode: string | null }[] }>('/api/suppliers?search=')
  let supplierId = suppliers.items.find((s) => s.internalCompanyCode === 'NMI')?.id
  if (!supplierId) {
    const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
    const created = await head.send<{ id: string }>('post', '/api/suppliers', {
      code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: 'Công ty NMI (NCC nội bộ)', shortName: null,
      isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
      rating: null, status: 'Approved', statusNote: null, internalCompanyId: nmiId, contacts: [], bankAccounts: [],
    })
    await approveSupplier(request, created.id)
    supplierId = created.id
  }
  const bha = await api(request, 'BHA')
  await bha.send('put', `/api/suppliers/${supplierId}/company`, { localCode: null, startDate: null, isActive: true })

  // Mặt hàng mới, giá bán nội bộ NMI → BHA 15.000/hộp.
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const hop = uoms.find((u) => u.code === 'HOP')!.id
  const category = await head.send<{ id: string }>('post', '/api/item-categories', {
    code: null, name: `Nhóm mua nội bộ ${crypto.randomUUID().slice(0, 8)}`, parentId: null, inventoryAccount: null,
  })
  const itemName = `Bu lông nội bộ ${unique('X')}`
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: itemName, englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: hop,
    specification: null, brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false,
    trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })
  const nmi = await api(request, 'NMI')
  await nmi.send('post', '/api/internal-prices', {
    buyerCompanyId: bhaId, itemId: item.id, unitPrice: 15000, vatCode: '10', validFrom: todayVn(), validTo: null, note: null,
  })

  // BHA lập PO, gõ tay 16.000 (khác giá gợi ý) – không bị bắt lý do lệch giá; gửi duyệt và duyệt một cấp.
  const kt = (await bha.get<{ id: string; code: string }[]>('/api/warehouses')).find((w) => w.code === 'KT')!.id
  const po = await bha.send<{ id: string; no: string }>('post', '/api/purchase-orders', {
    date: todayVn(), supplierId, warehouseId: kt, expectedDate: todayVn(), paymentTerms: null, deliveryTerms: null,
    deliveryAddress: null, receiveTolerancePct: 0, note: 'Mua nội bộ thử',
    lines: [{ id: null, itemId: item.id, uomId: hop, qty: 10, unitPrice: 16000, discountPct: 0, vatCode: '10', deliveryDate: null, note: null, sourcePrLineId: null }],
  })

  page.setDefaultTimeout(15_000)
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.locator('.document-form-number')).toHaveText(po.no)
  await expect(page.getByTestId('po-internal-sale')).toHaveValue(/^NMI – Chưa gửi/)
  await expect(page.getByText('Giá nội bộ NMI (gợi ý)').first()).toBeVisible()

  await bha.send('post', `/api/purchase-orders/${po.id}/submit`, {})
  await approveAsNewAdmin(request, bha, po.id)

  // NMI: Đơn bán nội bộ → mở đơn → xác nhận kèm ghi chú.
  await switchCompany(page, 'NMI')
  await openByCommand(page, 'DBN')
  await expect(page.getByRole('heading', { name: 'Đơn bán nội bộ' })).toBeVisible()
  const row = page.locator('.data-grid-row', { hasText: po.no })
  await expect(row).toContainText('Chờ xác nhận')
  await row.dblclick()
  await expect(page.getByText(`Đơn bán nội bộ: ${po.no}`)).toBeVisible()
  await expect(page.getByText(itemName)).toBeVisible()
  await page.getByLabel('Ghi chú xác nhận / lý do từ chối').fill('Giao trong tuần')
  await page.getByRole('button', { name: 'Xác nhận đơn' }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Xác nhận' }).click()
  await expect(page.getByText(`Đã xác nhận đơn ${po.no}.`)).toBeVisible()
  await expect(page.getByText(/^Đã xác nhận – /)).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/internal-sale.png' })

  // BHA thấy bên bán đã xác nhận trên PO.
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/po/${po.id}`)
  await expect(page.getByTestId('po-internal-sale')).toHaveValue(/^NMI – Đã xác nhận · .* · Giao trong tuần$/)
})
