import type { App } from 'antd'
import type { ReactNode } from 'react'
import { apiUpload } from '../../api/client'
import type { AttachmentItem, AttachmentOwnerType } from '../../api/attachments'
import { queryClient } from '../../app/queryClient'
import { ApprovalInput } from './ApprovalInput'

type AppApi = ReturnType<typeof App.useApp>

/** Chứng từ nhận file của người duyệt (design.md mục 20 – mỗi bước duyệt có ý kiến + đính kèm). */
export interface ApprovalAttachTarget {
  ownerType: AttachmentOwnerType
  ownerId: string
}

/**
 * Giá trị người duyệt nhập trong hộp thoại. modal.confirm không giữ state React của nội dung nên ô nhập báo về đối tượng này qua
 * callback, nút đồng ý đọc lại.
 */
export interface ApprovalInputBox {
  comment: string
  files: File[]
}

/**
 * Nội dung ô ý kiến + file gắn với một hộp chứa giá trị (dùng trong modal.confirm).
 * @param box Hộp chứa giá trị.
 * @param message Message của App.useApp().
 * @param options Nhãn, bắt buộc, gợi ý của ô ý kiến.
 * @returns Phần tử React.
 */
export function approvalInputFor(box: ApprovalInputBox, message: AppApi['message'], options: { label: string; required: boolean; placeholder: string }) {
  return (
    <ApprovalInput
      {...options}
      message={message}
      onCommentChange={(comment) => (box.comment = comment)}
      onFilesChange={(files) => (box.files = files)}
    />
  )
}

/**
 * Tải file của người duyệt lên, gắn vào bước vừa duyệt / từ chối (máy chủ tìm bước người dùng vừa xử lý). Lỗi từng file chỉ cảnh báo –
 * việc duyệt đã xong, không hoàn tác.
 * @param target Chứng từ.
 * @param files File đã chọn.
 * @param message Message để báo lỗi.
 */
export async function uploadApprovalFiles(target: ApprovalAttachTarget | undefined, files: File[], message: AppApi['message']) {
  if (!target || files.length === 0) return
  for (const file of files) {
    try {
      await apiUpload<AttachmentItem>('/api/attachments', file, { ownerType: target.ownerType, ownerId: target.ownerId, approvalStep: 'true' })
    } catch (e) {
      message.warning(`Không tải được file "${file.name}": ${e instanceof Error ? e.message : 'lỗi không rõ'}`)
    }
  }
  await queryClient.invalidateQueries({ queryKey: ['attachments'] })
}

/**
 * Hộp thoại duyệt dùng chung (design.md mục 20): nội dung phiếu, ô **Ý kiến người duyệt** (không bắt buộc) và đính kèm file. Duyệt
 * thành công (onOk không trả false) mới tải file lên.
 * @param modal Modal của App.useApp().
 * @param message Message của App.useApp().
 * @param options.title Câu hỏi, ví dụ "Duyệt phiếu PO-0001?".
 * @param options.content Thông tin hiện phía trên ô ý kiến (người duyệt các cấp…).
 * @param options.attach Chứng từ nhận file; bỏ trống thì không cho đính kèm.
 * @param options.onOk Gọi với ý kiến (null nếu bỏ trống); trả false khi duyệt lỗi để khỏi tải file.
 */
export function confirmApprove(
  modal: AppApi['modal'],
  message: AppApi['message'],
  options: { title: string; content?: ReactNode; attach?: ApprovalAttachTarget; onOk: (comment: string | null) => Promise<boolean | void> },
) {
  const box: ApprovalInputBox = { comment: '', files: [] }
  modal.confirm({
    title: options.title,
    width: 560,
    content: (
      <>
        {options.content}
        {approvalInputFor(box, message, { label: 'Ý kiến người duyệt', required: false, placeholder: 'Ý kiến của bạn (không bắt buộc)' })}
      </>
    ),
    okText: 'Duyệt',
    cancelText: 'Không',
    onOk: async () => {
      const ok = await options.onOk(box.comment.trim() || null)
      if (ok !== false) await uploadApprovalFiles(options.attach, box.files, message)
    },
  })
}
