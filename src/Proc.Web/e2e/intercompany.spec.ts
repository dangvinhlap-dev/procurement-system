import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, chooseOption, openByCommand, pickOption, seedPostedReceipt, switchCompany } from './inventoryHelpers.ts'

/**
 * Bán nội bộ BHA → NMI (design.md mục 6.3, 6.6, 10.7, 10.8): BHA có 10 hộp ở kho tổng (qua API) và giá bán nội bộ cho NMI 60.000 đ/hộp.
 * BHA lập phiếu xuất bán (giá tự lấy), ghi sổ → NMI thấy phiếu nhập chờ nhận, nhận thiếu 1 hộp rồi ghi sổ.
 */
test('xuất bán nội bộ, bên mua xác nhận nhận thiếu', async ({ page, request }) => {
  const { itemCode, itemId } = await seedPostedReceipt(request)
  const bha = await api(request, 'BHA')
  const companies = await bha.get<{ id: string; code: string }[]>('/api/group-companies')
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'Asia/Ho_Chi_Minh' })
  await bha.send('post', '/api/internal-prices', {
    buyerCompanyId: companies.find((c) => c.code === 'NMI')!.id, itemId, unitPrice: 60000, vatCode: '10', validFrom: today, validTo: null, note: null,
  })

  await loginViaApi(page)
  await page.goto('/')
  await switchCompany(page, 'BHA')
  await openByCommand(page, 'IPR')
  await expect(page.locator('.data-grid-row', { hasText: itemCode })).toContainText('60.000')

  await openByCommand(page, 'ICS')
  await expect(page.getByRole('heading', { name: 'Phiếu xuất bán nội bộ (ICS)' })).toBeVisible()
  await page.getByRole('button', { name: 'Tạo mới' }).click()
  await expect(page).toHaveURL(/\/kho\/ics\/moi$/)
  await chooseOption(page, 'Công ty mua', 'NMI – ')
  await page.keyboard.press('F2')
  await page.getByLabel('Mặt hàng dòng 1').fill(itemCode.toLowerCase())
  await pickOption(page, itemCode)
  await page.getByLabel('SL xuất dòng 1').fill('6')
  await page.getByLabel('SL xuất dòng 1').press('Tab')
  const row = page.locator('.line-grid-row').first()
  await expect(row).toContainText('60.000')
  await expect(row).toContainText('360.000')
  await expect(page.locator('.document-form-footer')).toContainText('396.000')

  await page.getByRole('button', { name: 'Ghi sổ' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Ghi sổ' }).click()
  await expect(page.locator('.ant-message')).toContainText(/Đã ghi sổ phiếu ICS-BHA-\d{4}-\d{4}\./)
  await expect(page.getByLabel('Phiếu nhập bên mua')).toHaveValue(/^ICR-NMI-\d{4}-\d{4} – Chờ nhận$/)
  const receiptNo = (await page.getByLabel('Phiếu nhập bên mua').inputValue()).split(' – ')[0]
  await page.screenshot({ path: 'e2e/screenshots/intercompany-sale.png' })

  // Công ty mua: phiếu chờ nhận, nhận 5/6 hộp.
  await switchCompany(page, 'NMI')
  await openByCommand(page, 'ICR')
  await expect(page.getByRole('heading', { name: 'Phiếu nhập mua nội bộ (ICR)' })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Tạo mới' })).toHaveCount(0)
  const listRow = page.locator('.data-grid-row', { hasText: receiptNo })
  await expect(listRow).toContainText('Chờ nhận')
  await listRow.dblclick()
  await expect(page.getByLabel('Phiếu xuất bán')).toHaveValue(/^ICS-BHA-/)
  await expect(page.getByRole('button', { name: 'Hủy phiếu' })).toHaveCount(0)
  await page.getByLabel('SL thực nhận dòng 1').fill('5')
  await page.getByLabel('SL thực nhận dòng 1').press('Tab')
  await expect(page.locator('.line-grid-row').first().locator('.line-grid-warning')).toHaveText('-1')
  await page.getByLabel('Ghi chú dòng 1').fill('Móp 1 hộp')
  await page.getByRole('button', { name: 'Ghi sổ' }).click()
  await page.getByRole('dialog').getByRole('button', { name: 'Ghi sổ' }).click()
  await expect(page.locator('.ant-message')).toContainText(`Đã ghi sổ phiếu ${receiptNo}.`)
  await expect(page.locator('.document-form-title')).toContainText('Nhận thiếu')
  await expect(page.locator('.ant-modal-confirm')).toHaveCount(0)
  await page.screenshot({ path: 'e2e/screenshots/intercompany-receipt.png' })
})
