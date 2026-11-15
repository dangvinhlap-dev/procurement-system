import { expect, test, type Page } from '@playwright/test'
import { ADMIN, loginViaApi, openByCommand } from './helpers.ts'

/**
 * Lát QT8 – màn hình REL (Trung tâm phát hành, design.md mục 21.4, 21.6; ui-style.md mục 5q).
 * - Nhận gói tải lên (gói giả dựng trong test: app\Proc.Api.dll, rollback.json, CHANGELOG.md) → mở phiếu: mục kiểm thử từ CHANGELOG, ghi Đạt /
 *   Không đạt, thêm lỗi, lưu Rủi ro; tab CHANGELOG, Lịch sử cài.
 * - Xác nhận UAT ở chế độ một môi trường; hộp thoại Cài PROD (Q6 "CÀI x.y.z" – gõ sai bị chặn) rồi đóng – KHÔNG cài thật (API test không có bộ
 *   thực thi PROD; phần máy chủ có test tích hợp ReleaseTests). Hủy phiếu cuối test để lần chạy sau nhận lại được cùng phiên bản.
 * API overview được GIẢ LẬP singleEnvironment = true: CSDL test dùng chung với test AGT (có thể đang khai môi trường TEST).
 */

/** CRC32 cho zip (đa thức 0xEDB88320). */
function crc32(data: Uint8Array): number {
  let c = ~0
  for (const b of data) {
    c ^= b
    for (let k = 0; k < 8; k++) c = (c >>> 1) ^ (0xedb88320 & -(c & 1))
  }
  return ~c >>> 0
}

/** Tạo zip tối giản (lưu không nén) từ các file chữ UTF-8 – đủ cho máy chủ đọc bằng System.IO.Compression. */
function makeZip(files: Record<string, string>): Buffer {
  const local: Buffer[] = []
  const central: Buffer[] = []
  let offset = 0
  for (const [name, text] of Object.entries(files)) {
    const nameBuf = Buffer.from(name, 'utf8')
    const data = Buffer.from(text, 'utf8')
    const crc = crc32(data)
    const head = Buffer.alloc(30)
    head.writeUInt32LE(0x04034b50, 0); head.writeUInt16LE(20, 4); head.writeUInt16LE(0x0800, 6); head.writeUInt16LE(0, 8)
    head.writeUInt32LE(0, 10); head.writeUInt32LE(crc, 14); head.writeUInt32LE(data.length, 18); head.writeUInt32LE(data.length, 22)
    head.writeUInt16LE(nameBuf.length, 26); head.writeUInt16LE(0, 28)
    local.push(head, nameBuf, data)
    const dir = Buffer.alloc(46)
    dir.writeUInt32LE(0x02014b50, 0); dir.writeUInt16LE(20, 4); dir.writeUInt16LE(20, 6); dir.writeUInt16LE(0x0800, 8); dir.writeUInt16LE(0, 10)
    dir.writeUInt32LE(0, 12); dir.writeUInt32LE(crc, 16); dir.writeUInt32LE(data.length, 20); dir.writeUInt32LE(data.length, 24)
    dir.writeUInt16LE(nameBuf.length, 28); dir.writeUInt32LE(offset, 42)
    central.push(dir, nameBuf)
    offset += head.length + nameBuf.length + data.length
  }
  const centralBuf = Buffer.concat(central)
  const end = Buffer.alloc(22)
  end.writeUInt32LE(0x06054b50, 0); end.writeUInt16LE(central.length / 2, 8); end.writeUInt16LE(central.length / 2, 10)
  end.writeUInt32LE(centralBuf.length, 12); end.writeUInt32LE(offset, 16)
  return Buffer.concat([...local, centralBuf, end])
}

/** Phiên bản riêng cho mỗi lần chạy (máy chủ chặn trùng phiên bản giữa các phiếu chưa hủy). */
const version = `9.${Math.floor(Date.now() / 1000) % 90000}.${Math.floor(Math.random() * 900) + 100}`

const packageZip = () => makeZip({
  'app/Proc.Api.dll': 'MZ gia',
  'rollback.json': JSON.stringify({ version, minLevel: 1, migrations: [] }),
  'CHANGELOG.md': `# Nhật ký thay đổi\n\n## [${version}] - 2026-11-16\n\n### Thêm mới\n- Màn hình REL – nhận gói, cài TEST / PROD.\n- Màn hình CFG.\n\n### Sửa lỗi\n- Không có.\n\n### Cách quay lại bản trước\n- Mức 1: thay lại gói cũ.\n`,
})

async function mockSingleEnvironment(page: Page) {
  await page.route('**/api/ops/v1/releases/overview', async (route) => {
    const real = (await (await route.fetch()).json()) as Record<string, unknown>
    await route.fulfill({ json: { ...real, singleEnvironment: true } })
  })
}

test('REL: nhận gói tải lên, kiểm thử, lỗi, UAT, hộp thoại cài PROD (Q6), hủy phiếu', async ({ page }) => {
  test.setTimeout(120_000)
  await mockSingleEnvironment(page)
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'REL')
  await expect(page.getByRole('heading', { name: /Trung tâm phát hành/ })).toBeVisible()
  await expect(page.getByTestId('release-notice')).toContainText('Máy này:')

  await page.getByRole('button', { name: 'Nhận gói tải lên…' }).click()
  const receive = page.getByRole('dialog', { name: 'Nhận gói tải lên' })
  await receive.locator('input[type=file]').setInputFiles({ name: `procurement-${version}.zip`, mimeType: 'application/zip', buffer: packageZip() })
  await expect(receive).toContainText(`procurement-${version}.zip`)
  await page.screenshot({ path: 'e2e/screenshots/release-receive.png' })
  await receive.getByRole('button', { name: 'Nhận gói' }).click()

  // Mở thẳng phiếu vừa nhận.
  await expect(page.getByText('Phiếu phát hành').first()).toBeVisible()
  await expect(page.locator('.data-grid-row', { hasText: 'Màn hình REL – nhận gói' })).toContainText('Chưa kiểm thử')
  await expect(page.locator('.data-grid-row')).toHaveCount(2)

  await page.locator('.data-grid-row', { hasText: 'Màn hình REL – nhận gói' }).click()
  // Nút dưới lưới: Đạt, Không đạt…, Chưa kiểm thử (tên đọc được kèm tên icon nên chọn theo thứ tự).
  const testButtons = page.locator('.erp-rel-grid-buttons').first().getByRole('button')
  await testButtons.nth(0).click()
  await expect(page.locator('.data-grid-row', { hasText: 'Màn hình REL – nhận gói' })).toContainText('Đạt')
  await page.locator('.data-grid-row', { hasText: 'Màn hình CFG' }).click()
  await page.getByRole('button', { name: 'Không đạt…' }).click()
  const fail = page.getByRole('dialog', { name: /Không đạt/ })
  await fail.getByLabel(/Ý kiến/).fill('Tab Bảo mật thiếu dòng HSTS')
  await fail.getByRole('button', { name: 'Ghi Không đạt' }).click()
  await expect(page.locator('.data-grid-row', { hasText: 'Màn hình CFG' })).toContainText('Không đạt')

  await page.getByLabel('Rủi ro').fill('Cài ngoài giờ')
  await page.getByRole('button', { name: /Lưu$/ }).click()
  await expect(page.getByText('Đã lưu phiếu phát hành.')).toBeVisible()
  await page.screenshot({ path: 'e2e/screenshots/release-ticket.png' })

  await page.getByRole('tab', { name: /Lỗi phát hiện/ }).click()
  await page.getByRole('button', { name: 'Thêm lỗi…' }).click()
  const defect = page.getByRole('dialog', { name: 'Thêm lỗi phát hiện' })
  await defect.getByLabel('Tiêu đề').fill('Chữ tab lệch')
  await defect.getByRole('button', { name: 'Thêm lỗi' }).click()
  await expect(page.locator('.data-grid-row', { hasText: 'Chữ tab lệch' })).toContainText('Chưa xử lý')
  await page.getByRole('tab', { name: 'CHANGELOG' }).click()
  await expect(page.getByTestId('release-changelog')).toContainText(`## [${version}]`)

  // UAT còn vướng → phải ghi lý do.
  await page.getByRole('button', { name: 'Xác nhận UAT đạt…' }).click()
  const signOff = page.getByRole('dialog', { name: /Xác nhận UAT đạt/ })
  await expect(signOff).toContainText('1 mục không đạt')
  await signOff.getByRole('button', { name: 'Xác nhận UAT đạt' }).click()
  await expect(signOff).toContainText('Ghi lý do')
  await signOff.getByLabel('Ý kiến').fill('Lỗi nhỏ, sửa ở bản sau')
  await signOff.getByRole('button', { name: 'Xác nhận UAT đạt' }).click()
  await expect(page.getByRole('button', { name: 'Cài PROD…' })).toBeVisible()

  // Cài PROD: Q6 – gõ sai chữ bị chặn; không cài thật.
  await page.getByRole('button', { name: 'Cài PROD…' }).click()
  const deploy = page.getByRole('dialog', { name: /Cài bản .* lên PROD/ })
  await deploy.getByLabel('Lý do').fill('Cài thử e2e')
  await deploy.getByLabel('Mật khẩu của bạn').fill(ADMIN.password)
  await deploy.getByLabel(/Gõ "CÀI/).fill('CÀI 0.0.0')
  await deploy.getByRole('button', { name: 'Cài PROD' }).click()
  await expect(deploy).toContainText(`Gõ đúng "CÀI ${version}".`)
  await page.screenshot({ path: 'e2e/screenshots/release-deploy.png' })
  await deploy.getByRole('button', { name: 'Bỏ qua' }).click()

  await page.getByRole('button', { name: 'Hủy phiếu…' }).click()
  const cancel = page.getByRole('dialog', { name: /Hủy phiếu phát hành/ })
  await cancel.getByLabel('Lý do').fill('Kết thúc test e2e')
  await cancel.getByRole('button', { name: 'Hủy phiếu' }).click()
  await expect(page.getByText('Đã hủy phiếu.')).toBeVisible()
})
