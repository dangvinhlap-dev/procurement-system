import { request, type FullConfig } from '@playwright/test'
import { ADMIN } from './helpers.ts'

/**
 * Chuẩn bị chung trước mọi test giao diện: bật tham số tổng công ty "Cho duyệt NCC mới không qua hồ sơ đánh giá (HSN)" (lát G8, design.md
 * mục 18.25). Nhiều test dựng NCC "Đã duyệt" bằng cách tạo NCC trạng thái Đã duyệt rồi duyệt yêu cầu; quy tắc bắt buộc hồ sơ HSN đã được
 * kiểm ở test tích hợp (SupplierOnboardingTests) – test hồ sơ HSN ở giao diện (supplier-onboarding.spec.ts) chạy được cả khi bật.
 * Các test chạy song song nên không bật/tắt qua lại trong từng test.
 * @param config Cấu hình Playwright (lấy baseURL).
 */
export default async function globalSetup(config: FullConfig) {
  const baseURL = config.projects[0].use.baseURL
  const context = await request.newContext({ baseURL })
  const login = await context.post('/api/auth/login', { data: { userName: ADMIN.userName, password: ADMIN.password } })
  if (!login.ok()) throw new Error(`Không đăng nhập được admin: ${await login.text()}`)
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { defaultCompanyId: string } }
  const response = await context.put('/api/system-parameters/SUPPLIER_ONBOARDING_OPTIONAL', {
    headers: { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.defaultCompanyId },
    data: { value: 'true' },
  })
  if (!response.ok()) throw new Error(`Không bật được tham số SUPPLIER_ONBOARDING_OPTIONAL: ${await response.text()}`)
  await context.dispose()
}
