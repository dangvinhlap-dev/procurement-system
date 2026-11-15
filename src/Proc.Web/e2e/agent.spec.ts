import { expect, test, type Page } from '@playwright/test'
import { ADMIN, loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * Lát QT5 – màn hình AGT (Bộ thực thi, docs/ke-hoach-qt.md QT5; ui-style.md mục 5n). Chế độ bảo trì chuyển sang CFG ở lát QT7 – test ở
 * access-config.spec.ts.
 * - AGT: khai môi trường PROD (nếu CSDL test chưa có), tạo khóa (Q6 – gõ sai chữ bị chặn), khóa hiện một lần; giả lập bộ thực thi bằng request
 *   có header X-Ops-Agent-Key: hỏi lệnh (204), nhận "Lệnh thử", báo kết quả → tab Lệnh "Xong", ngăn kéo có nhật ký.
 */

/** Bộ thực thi giả: hỏi lệnh bằng khóa (không JWT). */
async function agentPoll(page: Page, key: string) {
  return page.request.get('/api/ops/v1/agent/commands?agentVersion=1.0.0&appVersion=e2e&host=E2E-AGENT', {
    headers: { 'X-Ops-Agent-Key': key },
  })
}

test('AGT: khai môi trường, tạo khóa, lệnh thử chạy qua bộ thực thi giả', async ({ page }) => {
  test.setTimeout(120_000)
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'AGT')
  await expect(page.getByRole('heading', { name: /Bộ thực thi/ })).toBeVisible()
  await expect(page.getByRole('tab', { name: 'Môi trường', selected: true })).toBeVisible()

  // CSDL test có thể đã có môi trường Prod (lần chạy trước) – có rồi thì dùng lại.
  await expect(page.getByRole('alert').filter({ hasText: /môi trường/ })).toBeVisible()
  if (!(await page.locator('.data-grid-row').filter({ hasText: 'Prod' }).count())) {
    await page.getByRole('button', { name: 'Khai môi trường…' }).click()
    const dialog = page.getByRole('dialog')
    await dialog.getByLabel('Môi trường').click()
    await page.locator('.ant-select-item-option', { hasText: 'PROD – dùng thật' }).click()
    await dialog.getByLabel('Tên').fill('Máy PROD e2e')
    await dialog.getByRole('button', { name: 'Lưu' }).click()
    await expect(page.locator('.ant-message')).toContainText('Đã khai môi trường Prod')
  }
  const row = page.locator('.data-grid-row').filter({ hasText: 'Prod' }).first()
  await expect(row).toBeVisible()
  await expect(page.getByRole('button', { name: 'Lệnh thử' })).toBeDisabled()
  await row.click()

  await page.getByRole('button', { name: 'Tạo khóa…' }).click()
  const keyDialog = page.getByRole('dialog')
  await keyDialog.getByLabel('Mật khẩu của bạn').fill(ADMIN.password)
  await keyDialog.getByLabel(/Gõ "TẠO KHÓA"/).fill('TAO KHOA')
  await keyDialog.getByRole('button', { name: 'Tạo khóa' }).click()
  await expect(keyDialog).toContainText('Gõ đúng "TẠO KHÓA".')
  await keyDialog.getByLabel(/Gõ "TẠO KHÓA"/).fill('tạo khóa')
  await keyDialog.getByRole('button', { name: 'Tạo khóa' }).click()
  const keyBox = page.getByTestId('agent-key')
  await expect(keyBox).toHaveValue(/^qta_/)
  const key = await keyBox.inputValue()
  await page.screenshot({ path: 'e2e/screenshots/agent-key.png' })
  await page.getByRole('button', { name: 'Đã chép khóa – đóng' }).click()

  expect((await agentPoll(page, 'qta_sai')).status()).toBe(401)
  expect((await agentPoll(page, key)).status()).toBe(204)
  await page.getByRole('button', { name: 'Làm mới' }).click()
  await expect(row).toContainText('Đang liên lạc')
  await expect(row).toContainText('E2E-AGENT')
  await row.click()
  await page.screenshot({ path: 'e2e/screenshots/agent-environments.png' })

  await page.getByRole('button', { name: 'Lệnh thử' }).click()
  const ping = page.getByRole('dialog')
  await expect(ping.getByLabel('Lý do')).toHaveValue('Kiểm tra bộ thực thi sau khi cài')
  await ping.getByRole('button', { name: 'Gửi lệnh' }).click()
  await expect(page.locator('.ant-message')).toContainText('Đã gửi lệnh thử')
  await expect(page.getByRole('tab', { name: 'Lệnh', selected: true })).toBeVisible()
  const command = page.locator('.data-grid-row', { hasText: 'Lệnh thử (Ping)' }).first()
  await expect(command).toContainText('Đang chờ')

  const work = await agentPoll(page, key)
  expect(work.status()).toBe(200)
  const { id } = (await work.json()) as { id: string }
  const result = await page.request.post(`/api/ops/v1/agent/commands/${id}/result`, {
    headers: { 'X-Ops-Agent-Key': key },
    data: { succeeded: true, message: 'Bộ thực thi 1.0.0 trên E2E-AGENT', journal: 'Bộ thực thi: 1.0.0\nMáy: E2E-AGENT' },
  })
  expect(result.status()).toBe(204)
  await expect(command).toContainText('Xong', { timeout: 15_000 })
  await page.screenshot({ path: 'e2e/screenshots/agent-commands.png' })

  await command.dblclick()
  const drawer = page.locator('.ant-drawer')
  await expect(drawer).toContainText('Lệnh: Lệnh thử (Ping) – Prod')
  await drawer.getByRole('tab', { name: /Nhật ký bộ thực thi/i }).click()
  await expect(page.getByTestId('agent-journal')).toContainText('Máy: E2E-AGENT')
  await page.screenshot({ path: 'e2e/screenshots/agent-command-drawer.png' })
})
