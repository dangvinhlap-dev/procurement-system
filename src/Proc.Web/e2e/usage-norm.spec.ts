import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, switchCompany, unique, createLeafCategory } from './inventoryHelpers.ts'

/**
 * Định mức sử dụng trên PR (lát G11, design.md mục 18.25): kế hoạch nhu cầu đã chốt của BHA năm sau 10 / 10 cái tháng 1–2; PR 25 cái
 * tháng 2 vượt định mức lũy kế 20 → ô "Định mức LK" có tam giác vàng, đầu phiếu cảnh báo; mặt hàng cùng nhóm không có trong kế hoạch →
 * "Ngoài kế hoạch". Cách tính chi tiết (PR khác, hủy, nhóm cha) ở test tích hợp UsageNormTests.
 */
test('PR vượt định mức sử dụng theo kế hoạch nhu cầu: cảnh báo trên dòng và đầu phiếu', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const bha = await api(request, 'BHA')
  const uoms = await bha.get<{ id: string; code: string }[]>('/api/uoms')
  const cai = uoms.find((u) => u.code === 'CAI')!.id
  const tag = unique('dm')
  // Danh mục nhóm hàng, mặt hàng chỉ thêm ở tổng công ty.
  const category = await createLeafCategory<{ id: string; code: string }>(head, {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm định mức ${tag}`, parentId: null, inventoryAccount: null,
  })
  const newItem = (name: string) =>
    head.send<{ id: string; code: string }>('post', '/api/items', {
      code: null, name: `${name} ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: cai, specification: null,
      brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null,
      maxStock: null, conversions: [],
    })
  const planned = await newItem('Găng tay định mức')
  const outside = await newItem('Khẩu trang ngoài kế hoạch')
  const year = Number(new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' }).slice(0, 4)) + 1
  const months = Array<number>(12).fill(0)
  months[0] = 10
  months[1] = 10
  const plan = await bha.send<{ id: string }>('post', '/api/demand-plans', {
    year, itemCategoryId: category.id, note: null, lines: [{ id: null, itemId: planned.id, uomId: cai, months, note: null }],
  })
  await bha.send('post', `/api/demand-plans/${plan.id}/confirm`, {})

  const department = await bha.send<{ id: string }>('post', '/api/departments', { code: unique('DM'), name: 'Xưởng định mức', managerName: null })
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  const pr = await bha.send<{ id: string }>('post', '/api/purchase-requests', {
    date: today, departmentId: department.id, purpose: 'Thử định mức', neededDate: `${year}-02-15`, warehouseId: null, priority: 'Normal',
    note: null,
    lines: [
      { id: null, itemId: planned.id, uomId: cai, qty: 25, suggestedSupplierId: null, neededDate: null, note: null },
      { id: null, itemId: outside.id, uomId: cai, qty: 3, suggestedSupplierId: null, neededDate: null, note: null },
    ],
  })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto(`/mua-hang/pr/${pr.id}`)
  await expect(page.getByTestId('norm-1')).toHaveText(/25 \/ 20/)
  await expect(page.getByLabel('Định mức dòng 1')).toBeVisible()
  await expect(page.getByTestId('norm-1')).toHaveAttribute('title', new RegExp(`Lũy kế 01–02/${year}: đã đề nghị 25 / định mức 20 CAI`))
  await expect(page.getByTestId('norm-2')).toHaveText(/Ngoài kế hoạch/)
  const notice = page.getByTestId('usage-norm-notice')
  await expect(notice).toContainText(`Vượt định mức sử dụng: dòng 1 ${planned.code} đã đề nghị 25 / định mức 20 CAI (lũy kế 01–02/${year})`)
  await expect(notice).toContainText(`Ngoài kế hoạch nhu cầu: dòng 2 ${outside.code}`)
  await page.waitForLoadState('networkidle')
  await page.screenshot({ path: 'e2e/screenshots/usage-norm.png' })
})
