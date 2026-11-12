import { expect, type APIRequestContext } from '@playwright/test'
import { api, unique } from './inventoryHelpers.ts'

/**
 * Đưa mọi dòng còn cần mua của một đề nghị mua đã duyệt vào bảng tổng hợp 2B, gửi duyệt và duyệt qua API – mọi công ty bắt buộc qua bảng tổng
 * hợp đã duyệt trước khi lên RFQ/PO (design.md mục 19.3, lát Y5c). Người lập bảng là `creator`, quản trị duyệt (người lập không
 * tự duyệt).
 * @param request Ngữ cảnh API của Playwright.
 * @param creator Tài khoản lập bảng (khác quản trị).
 * @param companyCode Mã công ty.
 * @param prId Id đề nghị đã duyệt.
 * @param departmentId Bộ phận đề xuất của đề nghị.
 * @returns Số bảng tổng hợp.
 */
export async function approveSummaryForPr(
  request: APIRequestContext, creator: { userName: string; password: string }, companyCode: string, prId: string, departmentId: string,
) {
  const login = await request.post('/api/auth/login', { data: creator })
  const { accessToken, user } = (await login.json()) as { accessToken: string; user: { companies: { id: string; code: string }[] } }
  const headers = { Authorization: `Bearer ${accessToken}`, 'X-Company-Id': user.companies.find((c) => c.code === companyCode)!.id }
  const post = async <T,>(url: string, data: unknown) => {
    const response = await request.post(url, { headers, data })
    expect(response.ok(), await response.text()).toBeTruthy()
    return (await response.json()) as T
  }
  const unit = await post<{ id: string }>('/api/purchasing-units', { code: unique('DV'), name: 'Phòng CUVT', note: null })
  const candidates = (await (await request.get(`/api/purchase-summaries/candidates?form=2B&departmentId=${departmentId}`, { headers })).json()) as {
    requestId: string
    requestLineId: string
  }[]
  const lines = candidates.filter((c) => c.requestId === prId).map((c) => ({ requestLineId: c.requestLineId, purchasingUnitId: unit.id, note: null }))
  expect(lines.length).toBeGreaterThan(0)
  const summary = await post<{ id: string; no: string }>('/api/purchase-summaries', {
    date: new Date().toISOString().slice(0, 10), form: '2B', departmentId, constructionProjectId: null, note: null, lines,
  })
  await post(`/api/purchase-summaries/${summary.id}/submit`, {})
  const admin = await api(request, companyCode)
  const approved = await admin.send<{ status: string }>('post', `/api/purchase-summaries/${summary.id}/approve`, { comment: null })
  expect(approved.status).toBe('Approved')
  return summary.no
}
