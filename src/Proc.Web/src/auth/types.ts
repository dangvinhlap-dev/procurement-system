/**
 * Kiểu dữ liệu đăng nhập, khớp với Proc.Application.Auth (AuthModels.cs).
 * Viết tay tạm thời; sẽ sinh tự động từ OpenAPI khi số API nhiều lên (design.md mục 3.2).
 */

/** Một công ty người dùng được làm việc. */
export interface UserCompanyInfo {
  id: string
  code: string
  name: string
  shortName: string | null
  isHeadOffice: boolean
  roleCodes: string[]
}

/** Thông tin người dùng đang đăng nhập. */
export interface CurrentUserInfo {
  userId: string
  userName: string
  fullName: string
  defaultCompanyId: string
  companies: UserCompanyInfo[]
}

/** Kết quả POST /api/auth/login. */
export interface LoginResult {
  accessToken: string
  /** ISO 8601, UTC. */
  expiresAt: string
  user: CurrentUserInfo
}

/** Phiên làm việc lưu trong trình duyệt. */
export interface Session {
  accessToken: string
  expiresAt: string
  user: CurrentUserInfo
  /** Công ty đang làm việc – gửi lên API qua header X-Company-Id (design.md mục 9.2). */
  workingCompanyId: string
}
