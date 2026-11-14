import { readFileSync } from 'node:fs'
import { expect, test, type APIRequestContext } from '@playwright/test'
import { ADMIN, loginViaApi } from './helpers.ts'
import { api, openByCommand, unique } from './inventoryHelpers.ts'

/** Header gọi API của tài khoản quản trị tại một công ty (cần cho tải file và xóa – helper api chỉ có get/post/put). */
async function headersFor(request: APIRequestContext, companyCode: string) {
  const login = await request.post('/api/auth/login', { data: { userName: ADMIN.userName, password: ADMIN.password } })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  return { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === companyCode)!.id }
}

/** Nhập một file dữ liệu mua lịch sử; trả Id lô để dọn sau test. */
async function importFile(request: APIRequestContext, headers: Record<string, string>, file: string) {
  const response = await request.post('/api/purchase-history/import?commit=true', {
    headers,
    multipart: {
      file: { name: file, mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', buffer: readFileSync(`e2e/fixtures/${file}`) },
    },
  })
  expect(response.ok(), await response.text()).toBeTruthy()
  return ((await response.json()) as { batchId: string }).batchId
}

/**
 * Phân tích giá hợp nhất (design.md mục 18.3, lát E3): hai pháp nhân (TCT, BHA) mua cùng mã cũ PT-BOM, ánh xạ về một mặt hàng chung,
 * mở PHAN năm 2025 toàn tập đoàn → giá mục tiêu 95.000 đ, tiết kiệm 180.000.000 đ như ví dụ của đề xuất → mở chi tiết từng pháp nhân,
 * chụp màn hình → bỏ ánh xạ, xóa lô để lần chạy sau như cũ.
 */
test('phân tích giá hợp nhất hai pháp nhân và tiết kiệm dự kiến', async ({ page, request }) => {
  const head = await api(request)
  const tag = unique('BOM')
  const uoms = await head.get<{ id: string; code: string }[]>('/api/uoms')
  const category = await head.send<{ id: string }>('post', '/api/item-categories', { code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm thử phân tích giá ${crypto.randomUUID().slice(0, 8)}`, parentId: null, inventoryAccount: null })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Bơm ly tâm 2HP ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id,
    baseUomId: uoms.find((u) => u.code === 'HOP')!.id, specification: null, brand: null, origin: null, barcode: null, vatCode: '10',
    technicalSpecs: null, trackLot: false, trackExpiry: false, minStock: null, maxStock: null, conversions: [],
  })

  const companies = [
    { headers: await headersFor(request, 'TCT'), file: 'phan-tich-gia-tct.xlsx' },
    { headers: await headersFor(request, 'BHA'), file: 'phan-tich-gia-bha.xlsx' },
  ]
  const batches: { headers: Record<string, string>; batchId: string }[] = []
  try {
    for (const c of companies) {
      batches.push({ headers: c.headers, batchId: await importFile(request, c.headers, c.file) })
      // Ánh xạ riêng từng pháp nhân (design.md mục 18.2).
      const mapped = await request.put('/api/purchase-history/mappings', { headers: c.headers, data: { kind: 'item', key: 'PT-BOM', targetId: item.id } })
      expect(mapped.ok(), await mapped.text()).toBeTruthy()
    }

    await loginViaApi(page)
    await page.goto('/')
    await openByCommand(page, 'PHAN')
    await expect(page.getByRole('heading', { name: 'Phân tích giá hợp nhất' })).toBeVisible()
    const range = page.getByLabel('Từ ngày – Đến ngày')
    await range.click()
    await range.fill('01/01/2025')
    await range.press('Enter')
    const to = page.locator('.ant-picker-range input').nth(1)
    await to.fill('31/12/2025')
    await to.press('Enter')

    const row = page.locator('.data-grid-row', { hasText: item.code })
    await expect(row).toContainText('95.000')
    await expect(row).toContainText('180.000.000')
    await expect(row).toContainText('8,65')
    await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
    await page.screenshot({ path: 'e2e/screenshots/purchase-analysis.png' })

    await row.dblclick()
    const drawer = page.getByRole('dialog', { name: new RegExp(`Phân tích giá: ${item.code}`) })
    await expect(drawer).toContainText('1.900.000.000')
    await expect(drawer.locator('.data-grid-row', { hasText: 'Hợp nhất' })).toContainText('2.080.000.000')
    await page.waitForFunction("document.getAnimations().every((a) => a.playState !== 'running')")
    await page.screenshot({ path: 'e2e/screenshots/purchase-analysis-drawer.png' })
  } finally {
    // Dọn ánh xạ và lô vừa nhập để lần chạy sau bắt đầu lại như cũ.
    for (const c of companies) await request.delete('/api/purchase-history/mappings?kind=item&key=PT-BOM', { headers: c.headers })
    for (const b of batches) await request.delete(`/api/purchase-history/batches/${b.batchId}`, { headers: b.headers })
  }
})
