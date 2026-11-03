import { useQueryClient } from '@tanstack/react-query'
import { App } from 'antd'
import { useCallback, useEffect, useRef, useState } from 'react'
import { type AttachmentItem, type AttachmentOwnerType } from '../../api/attachments'
import { apiUpload } from '../../api/client'

/** Một file đã chọn khi phiếu chưa lưu, chờ tải lên. */
export interface PendingAttachment {
  /** Khóa tạm trên trình duyệt. */
  key: string
  /** File. */
  file: File
  /** Ghi chú kèm file. */
  note?: string
}

/** Hàng chờ file đính kèm của một phiếu đang tạo. */
export interface PendingAttachments {
  /** File đang chờ. */
  items: PendingAttachment[]
  /** Thêm file vào hàng chờ. */
  add: (file: File, note?: string) => void
  /** Bỏ một file khỏi hàng chờ. */
  remove: (key: string) => void
  /**
   * Tải ngay mọi file đang chờ lên phiếu vừa lưu. Dùng ở form bị dựng lại khi có Id (ngăn kéo có key theo Id) – gọi trong hàm lưu
   * trước khi chuyển sang Id mới; trang giữ nguyên khi lưu thì không cần, hook tự tải khi ownerId có giá trị.
   */
  uploadTo: (ownerId: string) => Promise<void>
}

let nextKey = 0

/**
 * Cho đính kèm ngay khi tạo phiếu (yêu cầu người dùng 03/11/2026, design.md mục 18.4): phiếu chưa lưu chưa có Id nên file được giữ
 * trên trình duyệt; lưu xong (ownerId có giá trị) thì tự tải lên lần lượt. Rời trang trước khi lưu thì hàng chờ mất theo – không có
 * file "mồ côi" trên máy chủ. Hook phải nằm ở cấp trang (không ở trong tab) để hàng chờ còn khi đổi tab hoặc trang chuyển sang URL có Id.
 * @param ownerType Loại đối tượng chủ.
 * @param ownerId Id đối tượng chủ (chưa lưu thì undefined).
 * @returns Hàng chờ và thao tác thêm/bỏ.
 */
export function usePendingAttachments(ownerType: AttachmentOwnerType, ownerId: string | undefined): PendingAttachments {
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const [items, setItems] = useState<PendingAttachment[]>([])
  // Ref song song với state (chỉ ghi trong thao tác, không trong render): uploadTo gọi ngay sau khi thêm vẫn thấy hàng chờ mới nhất,
  // và tránh tải hai lần khi effect chạy lại.
  const itemsRef = useRef<PendingAttachment[]>([])
  const replace = useCallback((list: PendingAttachment[]) => {
    itemsRef.current = list
    setItems(list)
  }, [])

  const uploadTo = useCallback(
    async (target: string) => {
      const queue = itemsRef.current
      if (queue.length === 0) return
      replace([])
      let ok = 0
      // Lần lượt từng file (không song song) để lỗi file nào báo đúng file đó và không dồn nhiều request lớn cùng lúc.
      for (const p of queue) {
        try {
          await apiUpload<AttachmentItem>('/api/attachments', p.file, { ownerType, ownerId: target, note: p.note })
          ok++
        } catch (e) {
          message.error(`Không tải lên được ${p.file.name}: ${e instanceof Error ? e.message : 'lỗi không rõ'}`)
        }
      }
      if (ok > 0) message.success(`Đã tải lên ${ok} file đính kèm.`)
      void queryClient.invalidateQueries({ queryKey: ['attachments'] })
    },
    [ownerType, message, queryClient, replace],
  )

  useEffect(() => {
    if (ownerId && items.length > 0) void uploadTo(ownerId)
  }, [ownerId, items, uploadTo])

  const add = useCallback(
    (file: File, note?: string) => replace([...itemsRef.current, { key: `p${++nextKey}`, file, note }]),
    [replace],
  )
  const remove = useCallback((key: string) => replace(itemsRef.current.filter((p) => p.key !== key)), [replace])
  return { items, add, remove, uploadTo }
}
