import { expect, type APIRequestContext } from '@playwright/test'
import { api, unique } from './inventoryHelpers.ts'

/**
 * Mời một tài khoản Portal cho NCC, kích hoạt, đăng nhập 2 bước qua API (mã OTP máy dev trả về); trả dữ liệu phiên Portal để gắn vào
 * sessionStorage (`proc.portal.session`).
 * @param request Ngữ cảnh API của Playwright.
 * @param supplierId NCC.
 */
export async function portalSession(request: APIRequestContext, supplierId: string) {
  const head = await api(request)
  const email = `${unique('dh').toLowerCase()}@ncc-thu.vn`
  const invite = await head.send<{ inviteUrl: string }>('post', `/api/suppliers/${supplierId}/portal-users`, {
    email, fullName: 'Lê Bán Hàng', phone: null, position: 'Nhân viên kinh doanh',
  })
  const token = new URL(invite.inviteUrl).searchParams.get('token')!
  expect((await request.post('/api/portal/auth/activate', { data: { token, password: 'ncc12345' } })).status()).toBe(204)
  const challenge = await (await request.post('/api/portal/auth/login', { data: { email, password: 'ncc12345' } })).json()
  const session = await (await request.post('/api/portal/auth/verify-otp', { data: { challengeId: challenge.challengeId, code: challenge.devCode } })).json()
  return session as { accessToken: string }
}
