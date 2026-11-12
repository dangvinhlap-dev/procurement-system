import { expect, test } from '@playwright/test'
import { loginViaApi } from './helpers.ts'
import { api, openByCommand, unique } from './inventoryHelpers.ts'

/**
 * Lát P1 – Portal NCC (design.md 19.5): Phòng CUVT mời quản trị NCC từ ngăn kéo NCC → NCC kích hoạt qua link, đăng nhập mật khẩu + OTP →
 * trang chủ Portal → quản trị NCC mời người dùng con. Máy chủ test không bật email nên link mời hiện cho người mời, OTP hiện trên trang
 * (Portal:ExposeCodesForTesting – chỉ dev/test).
 */
test('Portal NCC: mời, kích hoạt, đăng nhập OTP, mời người dùng con', async ({ page, request }) => {
  test.setTimeout(90_000)
  const head = await api(request)
  const groups = await head.get<{ id: string; code: string }[]>('/api/supplier-groups')
  const name = `NCC Portal ${unique('P')}`
  const supplier = await head.send<{ id: string; code: string }>('post', '/api/suppliers', {
    code: null, groupId: groups.find((g) => g.code === 'TN')!.id, name, shortName: null,
    isForeign: false, taxCode: null, address: 'Số 1 Đường Thử', phone: null, email: null, website: null, paymentTerms: null, supplyScope: null,
    rating: null, status: 'Potential', statusNote: null, internalCompanyId: null, contacts: [], bankAccounts: [],
  })
  const email = `${unique('qt').toLowerCase()}@ncc-thu.vn`

  // Phòng CUVT mời quản trị NCC trong tab "Tài khoản Portal" của ngăn kéo NCC.
  await loginViaApi(page)
  await page.goto('/')
  await openByCommand(page, 'SUP')
  await page.locator('[data-list-search]').fill(supplier.code)
  await page.locator('.data-grid-row', { hasText: supplier.code }).dblclick()
  const drawer = page.locator('.detail-drawer')
  // Chờ ngăn kéo nạp xong NCC. Ngăn kéo có 9 tab nên tab "Tài khoản Portal" nằm sát mép dải tab: Playwright cuộn dải tab bằng cuộn gốc
  // của trình duyệt (antd cuộn bằng transform) nên cú bấm chuột lệch chỗ – gửi thẳng sự kiện click vào tab.
  await expect(drawer.getByLabel('Tên nhà cung cấp')).toHaveValue(name)
  const portalTab = drawer.getByRole('tab', { name: 'Tài khoản Portal' })
  await portalTab.dispatchEvent('click')
  await expect(portalTab).toHaveAttribute('aria-selected', 'true')
  await drawer.getByRole('button', { name: 'Mời quản trị NCC…' }).click()
  const inviteDialog = page.getByRole('dialog', { name: 'Mời quản trị NCC' })
  await inviteDialog.getByLabel('Email (tên đăng nhập)').fill(email)
  await inviteDialog.getByLabel('Họ tên').fill('Nguyễn Quản Trị')
  await inviteDialog.getByLabel('Chức vụ').fill('Giám đốc kinh doanh')
  await inviteDialog.getByRole('button', { name: 'Mời' }).click()
  const result = page.getByRole('dialog', { name: 'Đã tạo lời mời' })
  await expect(result).toContainText('Máy chủ chưa gửi được email')
  const inviteUrl = await result.getByTestId('portal-invite-url').inputValue()
  expect(inviteUrl).toContain('/ncc/kich-hoat?token=')
  await page.screenshot({ path: 'e2e/screenshots/portal-invite.png' })
  await result.getByRole('button', { name: 'Đóng', exact: true }).last().click()
  await expect(drawer.getByTestId('portal-users')).toContainText(email)
  await expect(drawer.getByTestId('portal-users')).toContainText('Đã mời – chưa kích hoạt')

  // NCC mở link kích hoạt, đặt mật khẩu.
  await page.goto(new URL(inviteUrl).pathname + new URL(inviteUrl).search)
  await expect(page.getByText(name)).toBeVisible()
  await page.getByLabel('Mật khẩu mới', { exact: true }).fill('ncc12345')
  await page.getByLabel('Nhập lại mật khẩu mới').fill('ncc12345')
  await page.screenshot({ path: 'e2e/screenshots/portal-activate.png' })
  await page.getByRole('button', { name: 'Kích hoạt' }).click()

  // Đăng nhập hai bước: mật khẩu rồi OTP.
  await expect(page).toHaveURL(/\/ncc\/dang-nhap$/)
  await expect(page.getByText('Đã kích hoạt tài khoản. Hãy đăng nhập.')).toBeVisible()
  await page.getByLabel('Email').fill(email)
  await page.getByLabel('Mật khẩu').fill('ncc12345')
  await page.getByRole('button', { name: 'Tiếp tục' }).click()
  const devCode = page.getByText(/Môi trường thử – mã xác nhận: \d{6}/)
  await expect(devCode).toBeVisible()
  const code = /(\d{6})/.exec((await devCode.textContent()) ?? '')![1]
  await page.getByLabel('Mã xác nhận (OTP)').fill(code)
  await page.screenshot({ path: 'e2e/screenshots/portal-otp.png' })
  await page.getByRole('button', { name: 'Xác nhận' }).click()

  // Trang chủ Portal: thông tin NCC của chính mình.
  await expect(page).toHaveURL(/\/ncc$/)
  await expect(page.getByTestId('portal-supplier')).toContainText(supplier.code)
  await expect(page.getByTestId('portal-supplier')).toContainText('Số 1 Đường Thử')
  await expect(page.locator('.erp-portal-bar')).toContainText('Nguyễn Quản Trị')
  await page.screenshot({ path: 'e2e/screenshots/portal-home.png' })

  // Quản trị NCC mời người dùng con.
  await page.getByRole('link', { name: 'Người dùng' }).click()
  await page.getByRole('button', { name: 'Mời người dùng…' }).click()
  const sub = page.getByRole('dialog', { name: 'Mời người dùng' })
  await sub.getByLabel('Email (tên đăng nhập)').fill(`con.${email}`)
  await sub.getByLabel('Họ tên').fill('Trần Nhân Viên')
  await sub.getByRole('button', { name: 'Mời' }).click()
  const subResult = page.getByRole('dialog', { name: 'Đã tạo lời mời' })
  await expect(subResult.getByTestId('portal-invite-url')).toHaveValue(/\/ncc\/kich-hoat\?token=/)
  await subResult.getByRole('button', { name: 'Đóng', exact: true }).last().click()
  const grid = page.getByTestId('portal-users')
  await expect(grid).toContainText(`con.${email}`)
  await expect(grid).toContainText('Quản trị NCC')
  await expect(grid).toContainText('Người dùng')
  await page.screenshot({ path: 'e2e/screenshots/portal-users.png' })

  // Đăng xuất Portal về trang đăng nhập; trang chủ đòi đăng nhập lại.
  await page.getByRole('button', { name: 'Đăng xuất' }).click()
  await expect(page).toHaveURL(/\/ncc\/dang-nhap$/)
  await page.goto('/ncc')
  await expect(page).toHaveURL(/\/ncc\/dang-nhap$/)
})
