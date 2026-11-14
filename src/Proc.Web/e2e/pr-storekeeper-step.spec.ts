import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Lát Y3c (design.md mục 19.3): cấp "Thủ kho nơi nhận" trong chuỗi duyệt đề nghị mua. Công ty mới (không đụng cấu hình duyệt công ty mẫu): quản trị
 * thấy lựa chọn "Thủ kho nơi nhận" ở APRL cho PR; PR cấp 1 là Thủ kho. Thủ kho (vai trò mẫu KHO, phạm vi kho KT) mở phiếu: chưa có nút Duyệt,
 * nhập tồn xong mới có; duyệt xong phiếu Đã duyệt, lịch sử ghi "Thủ kho KT". Phân quyền, dịch vụ bỏ bước, chặng vượt dự toán kiểm tra ở test tích
 * hợp PurchaseRequestChainTests.
 */
test('bước Thủ kho nơi nhận: nhập tồn rồi mới duyệt đề nghị mua', async ({ page, request }) => {
  test.setTimeout(120_000)
  const head = await api(request)
  await head.send('post', '/api/roles/templates', {})
  const code = unique('TK').slice(0, 8)
  await head.send('post', '/api/companies', {
    code, name: `Công ty thử thủ kho ${code}`, shortName: null, taxCode: null, address: null, representative: null,
    startDate: new Date().toISOString().slice(0, 8) + '01', mainWarehouseCode: 'KT', mainWarehouseName: 'Kho tổng',
  })
  const company = await api(request, code)
  const kt = (await company.get<{ id: string; code: string }[]>('/api/warehouses')).find((w) => w.code === 'KT')!.id
  const roles = await company.get<{ id: string; code: string }[]>('/api/roles')
  await company.send('post', '/api/approval-rules', { docType: 'PR', level: 1, minAmount: 0, roleId: null, approverKind: 'Storekeeper' })
  const keeperName = unique('tk').toLowerCase()
  await company.send('post', '/api/users', {
    userName: keeperName, fullName: `Thủ kho ${keeperName}`, email: null, password: 'Demo@2026',
    access: { roleIds: [roles.find((r) => r.code === 'KHO')!.id], warehouseIds: [kt], itemCategoryIds: [] },
  })

  const cai = (await head.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'CAI')!.id
  const tag = unique('TK')
  const category = await head.send<{ id: string }>('post', '/api/item-categories', {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm vật tư thủ kho ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string }>('post', '/api/items', {
    code: null, name: `Ủng bảo hộ ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: cai, specification: null,
    brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null,
    maxStock: null, conversions: [],
  })
  const dept = await company.send<{ id: string }>('post', '/api/departments', { code: `PX${tag}`.slice(0, 10), name: `Phân xưởng ${tag}`, managerName: null })
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const pr = await company.send<{ id: string; no: string }>('post', '/api/purchase-requests', {
    date: today, departmentId: dept.id, purpose: 'Bảo hộ lao động', neededDate: null, warehouseId: kt, priority: 'Normal', note: null,
    lines: [{ id: null, itemId: item.id, uomId: cai, qty: 6, suggestedSupplierId: null, neededDate: null, note: null }],
  })
  await company.send('post', `/api/purchase-requests/${pr.id}/submit`, {})

  // Quản trị: ô "Người duyệt" của cấp PR có "Thủ kho nơi nhận"; danh sách cấp hiện tên loại người duyệt.
  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, code)
  await page.goto('/he-thong/quy-trinh-duyet')
  await expect(page.locator('.data-grid-row', { hasText: 'Thủ kho nơi nhận' })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/pr-storekeeper-rules.png' })

  // Thủ kho nơi nhận: chưa nhập tồn thì chưa có nút Duyệt.
  const keeperPage = await page.context().browser()!.newPage()
  await loginViaApi(keeperPage, { userName: keeperName, password: 'Demo@2026' })
  await keeperPage.goto(`/mua-hang/pr/${pr.id}`)
  const approve = keeperPage.locator('button.ant-btn', { hasText: /^Duyệt$/ })
  await expect(keeperPage.getByRole('button', { name: 'Nhập tồn' })).toBeVisible()
  await expect(approve).toHaveCount(0)

  await keeperPage.getByRole('button', { name: 'Nhập tồn' }).click()
  const stock = keeperPage.getByRole('dialog', { name: /Nhập tồn kho/ })
  await stock.getByLabel('Tồn dòng 1').fill('2')
  await stock.getByRole('button', { name: 'Xác nhận tồn' }).click()
  await expect(stock).toBeHidden()

  await expect(approve).toBeVisible()
  await approve.click()
  const dialog = keeperPage.getByRole('dialog')
  await dialog.getByRole('button', { name: 'Duyệt' }).click()
  await expect(dialog).toBeHidden()
  await expect(keeperPage.locator('.ant-tag', { hasText: 'Đã duyệt' }).first()).toBeVisible()
  await keeperPage.getByRole('tab', { name: /Lịch sử duyệt/i }).click()
  await expect(keeperPage.getByRole('tabpanel')).toContainText('Thủ kho KT')
  await keeperPage.waitForLoadState('networkidle')
  await keeperPage.screenshot({ path: 'e2e/screenshots/pr-storekeeper-step.png' })
  await keeperPage.close()
})
