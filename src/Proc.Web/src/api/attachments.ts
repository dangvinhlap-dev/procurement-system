import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/useAuth'
import { apiDownload, apiFetch, apiUpload } from './client'

/*
 * File đính kèm dùng chung (khớp Proc.Application.Documents – Attachments.cs, design.md mục 18.4).
 */

/** Loại đối tượng được đính kèm (khớp AttachmentOwnerTypes). */
export type AttachmentOwnerType = 'Supplier' | 'SupplierPriceList' | 'Rfq' | 'PurchaseRequest' | 'PurchaseOrder' | 'InventoryDocument' | 'PurchaseContract'
  | 'SoleSourceApproval'

/** Một file đính kèm. */
export interface AttachmentItem {
  id: string
  fileName: string
  contentType: string
  sizeBytes: number
  note: string | null
  uploadedAt: string
  uploadedByName: string | null
}

/** Dung lượng tối đa một file: 20 MB (quyết định Q6) – kiểm tra trước khi gửi để khỏi chờ tải lên rồi mới bị từ chối. */
export const attachmentMaxBytes = 20 * 1024 * 1024

/** Đuôi file được nhận (khớp Attachment.AllowedExtensions) – dùng cho ô chọn file và kiểm tra trước khi gửi. */
export const attachmentExtensions = [
  '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.csv', '.ppt', '.pptx', '.txt', '.jpg', '.jpeg', '.png', '.gif', '.bmp', '.tif', '.tiff',
  '.zip', '.rar', '.7z', '.msg', '.eml', '.xml',
]

const queryKey = (companyId: string, ownerType: AttachmentOwnerType, ownerId: string) => ['attachments', companyId, ownerType, ownerId]

/**
 * File đính kèm (chưa xóa) của một đối tượng.
 * @param ownerType Loại đối tượng.
 * @param ownerId Id đối tượng; rỗng (phiếu chưa lưu) thì không tải.
 */
export function useAttachments(ownerType: AttachmentOwnerType, ownerId: string | undefined) {
  const companyId = useAuth().workingCompany?.id ?? ''
  return useQuery({
    queryKey: queryKey(companyId, ownerType, ownerId ?? ''),
    queryFn: () => apiFetch<AttachmentItem[]>(`/api/attachments?ownerType=${ownerType}&ownerId=${ownerId}`),
    enabled: !!companyId && !!ownerId,
  })
}

/**
 * Tải lên, xóa file đính kèm của một đối tượng; xong thì tải lại danh sách.
 * @param ownerType Loại đối tượng.
 * @param ownerId Id đối tượng.
 */
export function useAttachmentMutations(ownerType: AttachmentOwnerType, ownerId: string) {
  const queryClient = useQueryClient()
  const companyId = useAuth().workingCompany?.id ?? ''
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: queryKey(companyId, ownerType, ownerId) })
  return {
    upload: useMutation({
      mutationFn: ({ file, note }: { file: File; note?: string }) =>
        apiUpload<AttachmentItem>('/api/attachments', file, { ownerType, ownerId, note }),
      onSuccess,
    }),
    remove: useMutation({ mutationFn: (id: string) => apiFetch<void>(`/api/attachments/${id}`, { method: 'DELETE' }), onSuccess }),
  }
}

/**
 * Tải một file đính kèm về máy.
 * @param item File cần tải.
 */
export const downloadAttachment = (item: AttachmentItem) => apiDownload(`/api/attachments/${item.id}/content`, item.fileName)

/**
 * Dung lượng dễ đọc theo kiểu Việt Nam: 512 B, 12,5 KB, 3,2 MB.
 * @param bytes Số byte.
 */
export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  const [value, unit] = bytes < 1024 * 1024 ? [bytes / 1024, 'KB'] : [bytes / 1024 / 1024, 'MB']
  return `${value.toLocaleString('vi-VN', { maximumFractionDigits: 1 })} ${unit}`
}
