import { expect, test } from '@playwright/test'
import { api, approvePriceList, approveSupplier, unique } from './inventoryHelpers.ts'
import { portalSession } from './portalHelpers.ts'
import { todayVn } from './receiptHelpers.ts'

/**
 * Lát P7a (design.md 19.5, P7-1…P7-4): NCC xem bảng giá bên mua đã duyệt trên Portal – danh sách (tình trạng kèm chữ), chi tiết dòng giá sau
 * chiết khấu; bảng nháp không hiện, không lộ ghi chú nội bộ.
 */
test('Portal: NCC xem bảng giá đã duyệt, giá sau chiết khấu', async ({ page, request }) => {
  test.setTimeout(120_000)
  const head = await api(request)
  const hop = (await head.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'HOP')!.id
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const category = await head.send<{ id: string }>('post', '/api/item-categories', {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm bảng giá Portal ${crypto.randomUUID().slice(0, 8)}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: 'Khẩu trang bảng giá', englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: hop,
    specification: null, brand: null, origin: null, barcode: null, vatCode: '8', technicalSpecs: null, trackLot: false,
    trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })
  const supplier = await head.send<{ id: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name: `NCC bảng giá ${unique('G')}`, shortName: null,
    isForeign: false, taxCode: null, address: null, phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Approved', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  await approveSupplier(request, supplier.id)

  const listBody = (title: string) => ({
    supplierId: supplier.id, title, isGroupWide: true, validFrom: todayVn(), validTo: null, deliveryTerms: 'Giao tại kho bên mua',
    note: 'Ghi chú nội bộ bảng giá', lines: [{ id: null, itemId: item.id, uomId: hop, minQty: 0, unitPrice: 25000, discountPct: 4, vatCode: null, note: null }],
  })
  const active = await head.send<{ id: string; no: string }>('post', '/api/price-lists', listBody('Bảng giá khẩu trang 2026'))
  await head.send('post', `/api/price-lists/${active.id}/submit`, {})
  await approvePriceList(request, active.id)
  const draft = await head.send<{ id: string; no: string }>('post', '/api/price-lists', listBody('Bảng giá nháp chưa gửi'))

  const session = await portalSession(request, supplier.id)
  await page.goto('/ncc/dang-nhap')
  await page.evaluate((s) => sessionStorage.setItem('proc.portal.session', JSON.stringify(s)), session)

  // Danh sách: bảng đã duyệt "Đang áp dụng", bảng nháp không hiện.
  await page.goto('/ncc')
  await page.getByRole('link', { name: 'Bảng giá', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Bảng giá', exact: true })).toBeVisible()
  const row = page.locator('.data-grid-row', { hasText: active.no })
  await expect(row).toContainText('Đang áp dụng')
  await expect(row).toContainText('Toàn tập đoàn')
  await expect(page.locator('.data-grid-row', { hasText: draft.no })).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/portal-price-lists.png', fullPage: true })

  // Chi tiết: 25.000 CK 4% → 24.000; thuế suất theo mặt hàng 8%; không có ghi chú nội bộ.
  await row.dblclick()
  await expect(page).toHaveURL(new RegExp(`/ncc/bang-gia/${active.id}$`))
  await expect(page.getByRole('heading', { name: `Bảng giá ${active.no}` })).toBeVisible()
  await expect(page.getByTestId('portal-price-list-header')).toContainText('Giao tại kho bên mua')
  const line = page.getByTestId('portal-price-list-lines').locator('.data-grid-row', { hasText: item.code })
  await expect(line).toContainText('25.000')
  await expect(line).toContainText('24.000')
  await expect(line).toContainText('8%')
  await expect(page.locator('body')).not.toContainText('Ghi chú nội bộ')
  await page.screenshot({ path: 'e2e/screenshots/portal-price-list-detail.png', fullPage: true })
})
