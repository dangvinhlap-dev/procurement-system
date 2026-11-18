import { expect, test, type Page } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, createLeafCategory, pickOption, switchCompany, unique } from './inventoryHelpers.ts'

/**
 * Lát IN1 (design.md mục 20.4): "Chữ ký của tôi" (CKY) – chọn ảnh chụp chữ ký, phần mềm tách nền / cắt sát / đổi màu mực, lưu thành phiên bản
 * đang dùng; "Xem đề nghị" trên đề nghị mua – hộp thoại xem trang in theo mẫu 1A/1B, Tải PDF tính một lần in.
 */

/** Ảnh "chụp" chữ ký giả lập: nét bút xanh đen trên nền giấy xám nhạt (vẽ bằng canvas của trình duyệt). */
async function fakeSignaturePhoto(page: Page): Promise<Buffer> {
  // Biểu thức chuỗi: tsconfig của e2e không có thư viện DOM, mã này chạy trong trình duyệt.
  const dataUrl = await page.evaluate<string>(`(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 640
    canvas.height = 320
    const ctx = canvas.getContext('2d')
    ctx.fillStyle = '#e8e4d8'
    ctx.fillRect(0, 0, 640, 320)
    ctx.strokeStyle = '#2b2b3a'
    ctx.lineWidth = 6
    ctx.lineCap = 'round'
    ctx.beginPath()
    ctx.moveTo(80, 220)
    ctx.bezierCurveTo(140, 40, 200, 40, 220, 200)
    ctx.bezierCurveTo(240, 280, 320, 80, 360, 160)
    ctx.bezierCurveTo(400, 230, 470, 90, 560, 150)
    ctx.moveTo(150, 250)
    ctx.lineTo(540, 230)
    ctx.stroke()
    return canvas.toDataURL('image/png')
  })()`)
  return Buffer.from(dataUrl.split(',')[1], 'base64')
}

test('Chữ ký của tôi: tải ảnh chụp → tách nền, lưu phiên bản đang dùng', async ({ page }) => {
  await loginViaApi(page)
  await page.goto('/hang-ngay/chu-ky-cua-toi')
  await expect(page.getByRole('heading', { name: 'Chữ ký của tôi' })).toBeVisible()
  await page.getByRole('button', { name: /Tải chữ ký mới/ }).click()
  const dialog = page.getByRole('dialog', { name: 'Tải chữ ký' })
  await expect(dialog.getByRole('button', { name: 'Lưu chữ ký' })).toBeDisabled()
  await page.getByTestId('signature-file').setInputFiles({ name: 'chu-ky.png', mimeType: 'image/png', buffer: await fakeSignaturePhoto(page) })
  await expect(page.getByTestId('signature-processed')).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/signature-upload.png' })
  await dialog.getByRole('button', { name: 'Lưu chữ ký' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã lưu chữ ký mới.')
  await expect(page.getByTestId('signature-current')).toContainText('Đang dùng chữ ký phiên bản')
  await expect(page.locator('.data-grid').first()).toContainText('Đang dùng')
  await page.screenshot({ path: 'e2e/screenshots/signature-mine.png' })
})

test('Đề nghị mua: Xem đề nghị – trang in mẫu 1B, Tải PDF tính một lần in', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const bha = await api(request, 'BHA')
  const cai = (await bha.get<{ id: string; code: string }[]>('/api/uoms')).find((u) => u.code === 'CAI')!.id
  const tag = unique('XD')
  const category = await createLeafCategory<{ id: string }>(head, {
    code: `N${crypto.randomUUID().replace(/-/g, '').slice(0, 9).toUpperCase()}`, name: `Nhóm xem đề nghị ${tag}`, parentId: null, inventoryAccount: null,
  })
  const item = await head.send<{ id: string; code: string }>('post', '/api/items', {
    code: null, name: `Bóng đèn LED ${tag}`, englishName: null, type: 'Inventory', categoryId: category.id, baseUomId: cai, specification: '18W',
    brand: null, origin: null, barcode: null, vatCode: '10', technicalSpecs: null, conversions: [],
  })
  const deptCode = unique('XD').slice(0, 10)
  await bha.send('post', '/api/departments', { code: deptCode, name: `Phòng xem đề nghị ${deptCode}`, managerName: null })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await page.goto('/mua-hang/pr/moi')
  // Phiếu chưa lưu: chưa có nút Xem đề nghị.
  await expect(page.getByRole('button', { name: 'Xem đề nghị' })).toHaveCount(0)
  await chooseOption(page, 'Bộ phận đề nghị', deptCode)
  await page.getByLabel('Mục đích sử dụng').fill('Thay đèn xưởng')
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(item.code.toLowerCase())
  await pickOption(page, item.code)
  await page.getByLabel('SL cần sử dụng dòng 1').fill('12')
  await page.getByLabel('Đơn giá dự kiến dòng 1').fill('85000')
  await page.keyboard.press('Control+s')
  await expect(page.locator('.ant-message')).toContainText(/Đã lưu phiếu PR-BHA-\d{4}-\d{4}\./)

  // Xem đề nghị: hộp thoại xem trang in (không ghi lần in).
  const view = page.waitForResponse((r) => r.url().endsWith('/pdf'))
  await page.getByRole('button', { name: 'Xem đề nghị' }).click()
  expect((await view).headers()['content-type']).toContain('application/pdf')
  const dialog = page.getByRole('dialog', { name: /Xem đề nghị mua PR-BHA-\d{4}-\d{4} – mẫu 1B/ })
  await expect(page.getByTestId('print-preview-frame')).toBeVisible()
  await expect(page.getByTestId('print-count')).toHaveText('Chưa in lần nào')
  await page.screenshot({ path: 'e2e/screenshots/pr-print-preview.png' })

  // Tải PDF: tính một lần in.
  const download = page.waitForEvent('download')
  await dialog.getByRole('button', { name: 'Tải PDF' }).click()
  expect((await download).suggestedFilename()).toMatch(/^PR-BHA-\d{4}-\d{4}\.pdf$/)
  await expect(page.getByTestId('print-count')).toContainText('Đã in 1 lần')
  await dialog.getByRole('button', { name: 'Đóng', exact: true }).click()
  await expect(dialog).toHaveCount(0)
})
