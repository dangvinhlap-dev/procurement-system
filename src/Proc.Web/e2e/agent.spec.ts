import { expect, test, type Page } from '@playwright/test'
import { ADMIN, loginViaApi } from './helpers.ts'
import { openByCommand } from './inventoryHelpers.ts'

/**
 * Lát QT5 – màn hình AGT (Bộ thực thi, docs/ke-hoach-qt.md QT5; ui-style.md mục 5n) và chế độ bảo trì trên SYS.
 * - AGT: khai môi trường PROD (nếu CSDL test chưa có), tạo khóa (Q6 – gõ sai chữ bị chặn), khóa hiện một lần; giả lập bộ thực thi bằng request
 *   có header X-Ops-Agent-Key: hỏi lệnh (204), nhận "Lệnh thử", báo kết quả → tab Lệnh "Xong", ngăn kéo có nhật ký.
 * - SYS: bật chế độ bảo trì → thông báo đỏ trên SYS, dải đỏ "ĐANG BẢO TRÌ"; tắt lại. API chế độ bảo trì được GIẢ LẬP (page.route): test chạy
 *   song song, bật bảo trì thật trên API test sẽ chặn test của người dùng khác đang chạy cùng lúc. Phần máy chủ (503, cho admin qua) có test tích
 *   hợp MaintenanceModeTests.
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

test('SYS: bật chế độ bảo trì – thông báo đỏ, dải ĐANG BẢO TRÌ; tắt lại', async ({ page }) => {
  let mode = { isOn: false, message: null as string | null, reason: null as string | null, startedAt: null as string | null, changedByName: null as string | null, changedAt: null as string | null }
  const sent: { isOn: boolean; message: string | null; reason: string }[] = []
  await page.route('**/api/ops/v1/maintenance-mode', async (route) => {
    if (route.request().method() === 'PUT') {
      const body = route.request().postDataJSON() as { isOn: boolean; message: string | null; reason: string }
      sent.push(body)
      const now = new Date().toISOString()
      mode = { isOn: body.isOn, message: body.isOn ? body.message : null, reason: body.reason, startedAt: body.isOn ? now : null, changedByName: 'admin', changedAt: now }
    }
    await route.fulfill({ json: mode })
  })
  await page.route('**/api/ops/v1/environment', async (route) => {
    const real = (await (await route.fetch()).json()) as Record<string, unknown>
    await route.fulfill({ json: { ...real, maintenance: mode.isOn ? { message: mode.message, startedAt: mode.startedAt } : null } })
  })
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'SYS')
  const toggleOn = page.getByRole('button', { name: 'Bật chế độ bảo trì…' })
  await expect(toggleOn).toBeVisible()
  await toggleOn.click()
  const dialog = page.getByRole('dialog')
  await dialog.getByLabel('Câu thông báo người dùng thấy').fill('Đang cài bản mới (e2e), quay lại sau 5 phút.')
  await dialog.getByRole('button', { name: 'Bật bảo trì' }).click()
  await expect(dialog).toContainText('Nhập lý do.')
  await dialog.getByLabel('Lý do').fill('Thử chế độ bảo trì e2e')
  await dialog.getByRole('button', { name: 'Bật bảo trì' }).click()
  try {
    await expect(page.getByTestId('maintenance-notice')).toContainText('Đang cài bản mới (e2e)')
    await expect(page.getByTestId('environment-banner')).toContainText('ĐANG BẢO TRÌ: Đang cài bản mới (e2e), quay lại sau 5 phút.')
    await page.screenshot({ path: 'e2e/screenshots/maintenance-on.png' })
  } finally {
    await page.getByRole('button', { name: 'Tắt chế độ bảo trì…' }).click()
    const off = page.getByRole('dialog')
    await off.getByLabel('Lý do').fill('Xong thử chế độ bảo trì')
    await off.getByRole('button', { name: 'Tắt bảo trì' }).click()
  }
  await expect(page.getByTestId('maintenance-notice')).toBeHidden()
  await expect(page.getByRole('button', { name: 'Bật chế độ bảo trì…' })).toBeVisible()
  await expect(page.getByTestId('environment-banner')).not.toContainText('ĐANG BẢO TRÌ')
  expect(sent.map((s) => [s.isOn, s.reason])).toEqual([
    [true, 'Thử chế độ bảo trì e2e'],
    [false, 'Xong thử chế độ bảo trì'],
  ])
})
