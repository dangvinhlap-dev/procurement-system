import { readFileSync } from 'node:fs'
import { expect, test } from '@playwright/test'
import { ADMIN, loginViaApi } from './helpers.ts'
import { api, openByCommand, unique } from './inventoryHelpers.ts'

/**
 * Ánh xạ dữ liệu mua (design.md mục 18.2, lát E2): nhập file mẫu (mã cũ VB-6205), tạo mặt hàng chung tên giống, mở PHMAP → gợi ý →
 * lưu ánh xạ (mọi dòng VB-6205 được gán) → chụp màn hình → bỏ ánh xạ, xóa lô để lần chạy sau bắt đầu lại như cũ.
 */
test('ánh xạ mã vật tư cũ sang mã hàng chung từ gợi ý', async ({ page, request }) => {
  const head = await api(request)
  const tag = unique('SKF')
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const category = await head.send<{ id: string }>('post', '/api/item-categories', { code: null, name: `Nhóm thử ánh xạ ${crypto.randomUUID().slice(0, 8)}`, parentId: null, inventoryAccount: null })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Vòng bi 6205-2RS ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id,
    baseUomId: uoms.find((u) => u.code === 'HOP')!.id, specification: '25x52x15', brand: 'SKF', origin: null, barcode: null, vatCode: '10',
    technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })

  // Nhập file mẫu bằng API ở công ty mặc định (màn hình nhập đã có test riêng – purchase-history.spec.ts).
  const login = await request.post('/api/auth/login', { data: { userName: ADMIN.userName, password: ADMIN.password } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId }
  const imported = await request.post('/api/purchase-history/import?commit=true', {
    headers,
    multipart: {
      file: {
        name: 'du-lieu-mua-lich-su.xlsx',
        mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        buffer: readFileSync('e2e/fixtures/du-lieu-mua-lich-su.xlsx'),
      },
    },
  })
  expect(imported.ok(), await imported.text()).toBeTruthy()
  const { batchId } = (await imported.json()) as { batchId: string }

  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'PHMAP')
  await expect(page.getByRole('heading', { name: 'Ánh xạ dữ liệu mua' })).toBeVisible()
  await page.locator('.data-grid-row', { hasText: 'VB-6205' }).dblclick()

  const drawer = page.getByRole('dialog', { name: /Ánh xạ mã vật tư: VB-6205/ })
  await drawer.getByRole('textbox', { name: 'Tìm mặt hàng chung' }).fill(tag.toLowerCase())
  const option = drawer.getByRole('radio', { name: new RegExp(item.code) })
  await option.check()
  await expect(option).toBeChecked()
  await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
  await page.screenshot({ path: 'e2e/screenshots/purchase-history-mapping-drawer.png' })
  await drawer.getByRole('button', { name: 'Lưu' }).click()
  await expect(page.getByText(/Đã ánh xạ, gán/)).toBeVisible()

  const row = page.locator('.data-grid-row', { hasText: 'VB-6205' })
  await expect(row).toContainText(item.code)
  await expect(row).toContainText('Đã ánh xạ')
  await expect(page.getByRole('dialog')).toHaveCount(0)
  await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
  await page.screenshot({ path: 'e2e/screenshots/purchase-history-mapping.png' })

  await row.dblclick()
  await page.getByRole('dialog', { name: /Ánh xạ mã vật tư: VB-6205/ }).getByRole('button', { name: 'Bỏ ánh xạ' }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Bỏ ánh xạ' }).click()
  await expect(page.getByText(/Đã bỏ ánh xạ/)).toBeVisible()
  await expect(row).not.toContainText(item.code)

  // Dọn lô vừa nhập để dữ liệu test không dồn lên qua các lần chạy.
  expect((await request.delete(`/api/purchase-history/batches/${batchId}`, { headers })).ok()).toBeTruthy()
})
