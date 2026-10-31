import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, openByCommand, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Đóng kỳ, báo cáo nhập – xuất – tồn, mở lại kỳ (design.md mục 7.2–7.4a, 11.1). Dùng một công ty mới (lịch năm 2025, mở tháng 1,
 * một phiếu nhập 100 cái × 10.000 đã ghi sổ) để đóng/mở kỳ không ảnh hưởng công ty mẫu.
 */
test('đóng kỳ tháng, xem NXT đã chốt, mở lại kỳ có lý do', async ({ page, request }) => {
  const head = await api(request)
  const code = unique('E').slice(0, 8)
  await head.send('post', '/api/companies', {
    code, name: `Công ty thử đóng kỳ ${code}`, shortName: null, taxCode: null, address: null, representative: null,
    startDate: '2025-01-01', mainWarehouseCode: 'KT', mainWarehouseName: 'Kho tổng',
  })
  const company = await api(request, code)
  const calendar = await company.send<{ periods: { id: string; month: number }[] }>('post', '/api/inventory-periods/years', { year: 2025 })
  const jan = calendar.periods.find((p) => p.month === 1)!.id
  await company.send('post', `/api/inventory-periods/${jan}/open`, {})

  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const cai = uoms.find((u) => u.code === 'CAI')!.id
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const category = await head.send<{ id: string; code: string }>('post', '/api/item-categories', { code: null, name: 'Nhóm đóng kỳ', parentId: null, inventoryAccount: null })
  // Mã hàng do hệ thống cấp theo dải đánh số (design.md mục 5.11).
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: 'Vòng bi đóng kỳ', englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: cai,
    specification: null, brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false,
    trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })
  const itemCode = item.code
  const supplier = await head.send<{ id: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: 'NCC đóng kỳ', shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  await company.send('put', `/api/suppliers/${supplier.id}/company`, { localCode: null, startDate: null, isActive: true })
  const warehouses = await company.get<{ id: string }[]>('/api/warehouses')
  const receipt = await company.send<{ id: string }>('post', '/api/goods-receipts', {
    date: '2025-01-10', warehouseId: warehouses[0].id, supplierId: supplier.id, invoiceNo: null, invoiceSeries: null,
    invoiceDate: null, deliveryNoteNo: null, deliverer: null, note: null,
    lines: [{ id: null, itemId: item.id, uomId: cai, qty: 100, unitPrice: 10000, vatCode: '10', lotNo: null, expiryDate: null, note: null }],
  })
  await company.send('post', `/api/inventory-documents/${receipt.id}/post`, {})

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, code)

  // Đóng kỳ 01/2025 từ màn hình Kỳ kho.
  await openByCommand(page, 'PER')
  await expect(page.getByRole('heading', { name: `Kỳ kho – ${code}` })).toBeVisible()
  const janRow = page.getByRole('row', { name: 'Ghim dòng 01/2025' })
  await expect(janRow).toContainText('Đóng kỳ')
  await janRow.dblclick()
  await page.getByRole('button', { name: 'Đóng kỳ' }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Đóng kỳ' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã đóng kỳ 01/2025.')
  await expect(janRow).toContainText('Đã đóng')
  await expect(page.locator('.ant-modal-confirm')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/period-closed.png' })

  // NXT tháng 1: đã chốt, tồn cuối 100 × 10.000.
  await openByCommand(page, 'NXT')
  await expect(page.getByRole('heading', { name: 'Báo cáo nhập – xuất – tồn' })).toBeVisible()
  await expect(page.getByText('Kỳ 01/2025 đã đóng: giá trị theo giá vốn chính thức.')).toBeVisible()
  const row = page.locator('.data-grid-row', { hasText: itemCode })
  await expect(row).toContainText('1.000.000')
  await page.screenshot({ path: 'e2e/screenshots/stock-movement.png' })

  // Mở lại kỳ: nút khóa tới khi nhập lý do.
  await openByCommand(page, 'PER')
  await janRow.dblclick()
  const reopen = page.getByRole('button', { name: 'Mở lại kỳ' })
  await expect(reopen).toBeDisabled()
  await page.getByLabel('Lý do mở lại kỳ (bắt buộc)').fill('Nhập sai đơn giá phiếu nhập')
  await reopen.click()
  await expect(page.locator('.ant-message')).toContainText('Đã mở lại kỳ 01/2025.')
  await expect(janRow).toContainText('Nhập sai đơn giá phiếu nhập')
})
