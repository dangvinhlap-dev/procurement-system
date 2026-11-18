import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiBlob, apiFetch, apiUpload } from './client'

/** Một phiên bản chữ ký – khớp Proc.Application.Documents.SignatureVersionDto (lát IN1, design.md mục 20.3). */
export interface SignatureVersion {
  versionNo: number
  validFrom: string
  /** null = đang dùng. */
  validTo: string | null
  uploadedByName: string | null
  /** Người quản trị tải thay. */
  byAdmin: boolean
  reason: string | null
  retiredByName: string | null
  retireReason: string | null
  width: number
  height: number
}

/** Chữ ký của một người dùng – khớp UserSignatureDto. */
export interface UserSignature {
  userId: string
  userName: string
  fullName: string
  /** Danh xưng in trước họ tên dưới chữ ký (ví dụ "AHLĐ."). */
  honorific: string | null
  isSelf: boolean
  /** Phiên bản đang dùng; null = chưa có chữ ký đang dùng. */
  currentVersionNo: number | null
  /** Mới nhất trước. */
  versions: SignatureVersion[]
}

/**
 * Gốc API chữ ký: của tôi hoặc của người khác (cần quyền "Tải lên, thay chữ ký của người dùng khác").
 * @param userId Người dùng; undefined = chính tôi.
 */
export const signatureBase = (userId?: string) => (userId ? `/api/users/${userId}/signature` : '/api/me/signature')

/**
 * Chữ ký và lịch sử phiên bản.
 * @param userId Người dùng; undefined = chính tôi.
 * @param enabled Có gọi API không (tab chưa mở thì không gọi).
 */
export function useUserSignature(userId?: string, enabled = true) {
  return useQuery({
    queryKey: ['user-signature', userId ?? 'me'],
    queryFn: () => apiFetch<UserSignature>(signatureBase(userId)),
    enabled,
  })
}

/**
 * Ảnh PNG của một phiên bản (mặc định đang dùng) dạng URL blob để hiện trong thẻ img – API cần JWT nên không trỏ thẳng đường dẫn.
 * @param userId Người dùng; undefined = chính tôi.
 * @param versionNo Phiên bản; null = không tải.
 */
export function useSignatureImage(userId: string | undefined, versionNo: number | null) {
  return useQuery({
    queryKey: ['user-signature-image', userId ?? 'me', versionNo],
    queryFn: async () => URL.createObjectURL((await apiBlob(`${signatureBase(userId)}/image?version=${versionNo}`)).blob),
    enabled: versionNo !== null,
    // Ảnh của một phiên bản không đổi; giữ lâu để chuyển dòng qua lại không tải lại.
    staleTime: Infinity,
  })
}

/** Tải chữ ký mới (ảnh PNG đã xử lý) và ngừng dùng chữ ký. */
export function useSignatureMutations(userId?: string) {
  const client = useQueryClient()
  const done = (data: UserSignature) => client.setQueryData(['user-signature', userId ?? 'me'], data)
  const upload = useMutation({
    mutationFn: ({ file, reason }: { file: File; reason?: string }) =>
      apiUpload<UserSignature>(signatureBase(userId), file, { reason: reason?.trim() || undefined }),
    onSuccess: done,
  })
  const retire = useMutation({
    mutationFn: (reason?: string) =>
      apiFetch<UserSignature>(`${signatureBase(userId)}/retire`, { method: 'POST', body: { reason: reason?.trim() || null } }),
    onSuccess: done,
  })
  return { upload, retire }
}

/** Thông tin logo công ty – khớp CompanyLogoDto. */
export interface CompanyLogo {
  companyId: string
  hasLogo: boolean
  contentType: string | null
  width: number
  height: number
  updatedAt: string | null
}

/**
 * Logo công ty in đầu phiếu (lát IN1): thông tin + URL blob ảnh.
 * @param companyId Công ty; null = không tải.
 */
export function useCompanyLogo(companyId: string | null) {
  const info = useQuery({
    queryKey: ['company-logo', companyId],
    queryFn: () => apiFetch<CompanyLogo>(`/api/companies/${companyId}/logo`),
    enabled: companyId !== null,
  })
  const image = useQuery({
    queryKey: ['company-logo-image', companyId, info.data?.updatedAt],
    queryFn: async () => URL.createObjectURL((await apiBlob(`/api/companies/${companyId}/logo/image`)).blob),
    enabled: companyId !== null && info.data?.hasLogo === true,
  })
  return { info, image }
}

/** Tải / xóa logo công ty. */
export function useCompanyLogoMutations(companyId: string) {
  const client = useQueryClient()
  const refresh = () => client.invalidateQueries({ queryKey: ['company-logo', companyId] })
  const upload = useMutation({
    mutationFn: (file: File) => apiUpload<CompanyLogo>(`/api/companies/${companyId}/logo`, file),
    onSuccess: refresh,
  })
  const remove = useMutation({
    mutationFn: () => apiFetch<void>(`/api/companies/${companyId}/logo`, { method: 'DELETE' }),
    onSuccess: refresh,
  })
  return { upload, remove }
}

/** Một lần in – khớp DocumentPrintDto. */
export interface DocumentPrint {
  printNo: number
  kind: 'Print' | 'Download'
  printedAt: string
  printedByName: string | null
  printedByUserName: string | null
  approvalStatus: string
}
