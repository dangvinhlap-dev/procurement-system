import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { switchCompany } from './inventoryHelpers.ts'
import { portalSession } from './portalHelpers.ts'
import { seedApprovedPo } from './receiptHelpers.ts'

/**
 * Lát P5 (design.md 19.5, P5-1…P5-6): NCC báo giao hàng trên Portal (số phiếu giao, SL, file kèm) → nội bộ thấy ở tab "Giao hàng NCC" của PO,
 * bấm "Lập XNH" được phiếu nhận hàng điền sẵn theo phiếu báo giao → NCC thấy "Bên đặt đang kiểm nhận", không hủy được nữa.
 */
test('Portal: NCC báo giao hàng, nơi nhận lập XNH từ phiếu báo giao', async ({ page, request }) => {
  test.setTimeout(120_000)
  const { po, supplierId } = await seedApprovedPo(request, 12)
  const session = await portalSession(request, supplierId)

  await page.goto('/ncc/dang-nhap')
  await page.evaluate((s) => sessionStorage.setItem('proc.portal.session', JSON.stringify(s)), session)
  await page.goto(`/ncc/don-hang/${po.id}`)
  await expect(page.getByTestId('portal-order-info')).toContainText(po.no)

  // Lập phiếu báo giao: thiếu số phiếu giao → báo lỗi; đủ thì gửi kèm file chờ.
  await page.getByRole('button', { name: 'Báo giao hàng…' }).click()
  const form = page.locator('section', { hasText: 'Lập phiếu báo giao hàng' })
  await form.getByRole('button', { name: 'Gửi phiếu báo giao' }).click()
  await expect(form.getByText('Nhập số phiếu giao hàng')).toBeVisible()
  await form.getByLabel('Số phiếu giao').fill('PGH-E2E-01')
  await form.getByLabel('Người giao, phương tiện').fill('Xe 51C-123.45')
  await form.getByLabel('SL giao dòng 1').fill('8')
  await form.locator('input[type=file]').setInputFiles({
    name: 'PhieuGiao.pdf', mimeType: 'application/pdf', buffer: Buffer.from('%PDF-1.4' + String.fromCharCode(10) + '%%EOF'),
  })
  await expect(form.getByText('PhieuGiao.pdf')).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/portal-delivery-form.png', fullPage: true })
  await form.getByRole('button', { name: 'Gửi phiếu báo giao' }).click()
  await page.locator('.ant-modal-confirm').getByRole('button', { name: 'Gửi' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã gửi phiếu báo giao BGH')
  const notices = page.getByTestId('portal-delivery-notices')
  await expect(notices).toContainText('PGH-E2E-01')
  await expect(notices).toContainText('Đã gửi – chờ nhận')
  await expect(page.getByTestId('portal-delivery-files')).toContainText('PhieuGiao.pdf')
  await expect(page.getByRole('button', { name: 'Hủy phiếu báo giao' })).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/portal-delivery-list.png', fullPage: true })

  // Nội bộ: tab "Giao hàng NCC" → Lập XNH điền sẵn số phiếu giao, người giao; lưu nháp.
  const internal = await page.context().newPage()
  await loginViaApi(internal)
  await internal.goto('/')
  await switchCompany(internal, 'BHA')
  await internal.goto(`/mua-hang/po/${po.id}`)
  await internal.getByRole('tab', { name: /Giao hàng NCC/i }).click()
  const tab = internal.getByTestId('po-delivery-notices')
  await expect(tab).toContainText('PGH-E2E-01')
  await expect(tab).toContainText('Chờ nhận')
  await internal.screenshot({ path: 'e2e/screenshots/po-delivery-notices.png', fullPage: true })
  await tab.getByRole('button', { name: 'Lập XNH' }).click()
  await expect(internal).toHaveURL(/xac-nhan-nhan-hang\/moi\?po=.*&bgh=/)
  await expect(internal.getByLabel('Số phiếu giao NCC')).toHaveValue('PGH-E2E-01')
  await expect(internal.getByLabel('Người giao')).toHaveValue('Xe 51C-123.45')
  await expect(internal.getByText('Theo phiếu báo giao NCC')).toBeVisible()
  await internal.getByRole('button', { name: 'Lưu nháp' }).click()
  await expect(internal.locator('.ant-message')).toContainText('Đã lưu phiếu')
  await expect(internal).toHaveURL(/xac-nhan-nhan-hang\/[0-9a-f-]{36}$/)
  await internal.screenshot({ path: 'e2e/screenshots/receipt-from-delivery-notice.png', fullPage: true })
  await internal.close()

  // NCC: phiếu đang được kiểm nhận – không hủy được nữa.
  await page.reload()
  await expect(notices).toContainText('Bên đặt đang kiểm nhận')
  await expect(page.getByRole('button', { name: 'Hủy phiếu báo giao' })).toHaveCount(0)
})
