/** Kết quả một mục health check do API trả về. */
export interface HealthCheckEntry {
  name: string
  status: string
  durationMs: number
  description: string | null
}

/** Kết quả health check tổng hợp của API (GET /health). */
export interface HealthResponse {
  status: string
  version: string
  checks: HealthCheckEntry[]
}

/**
 * Gọi GET /health của API.
 * API trả 503 kèm JSON khi CSDL lỗi, nên vẫn đọc body thay vì ném lỗi theo mã HTTP.
 * @returns Trạng thái hệ thống và phiên bản phần mềm.
 * @throws Error khi không gọi được API hoặc body không phải JSON.
 */
export async function fetchHealth(): Promise<HealthResponse> {
  const response = await fetch('/health')
  if (!response.headers.get('content-type')?.includes('application/json')) {
    throw new Error(`Không gọi được API (HTTP ${response.status}).`)
  }
  return (await response.json()) as HealthResponse
}
