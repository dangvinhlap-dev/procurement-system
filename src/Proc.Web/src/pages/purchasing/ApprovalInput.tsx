import { PaperClipOutlined } from '@ant-design/icons'
import { Button, Input, Upload } from 'antd'
import type { App } from 'antd'
import { useEffect, useState } from 'react'
import { apiFetch } from '../../api/client'
import { attachmentExtensions, checkAttachmentFile, defaultAttachmentMaxMb } from '../../api/attachments'
import { renderRequiredMark } from '../../components/RequiredMark/requiredMark'

/** Giới hạn dung lượng đọc một lần mỗi phiên – hộp thoại nằm ngoài QueryClientProvider nên không dùng hook TanStack Query. */
let cachedMaxMb: number | null = null

/**
 * Ô ý kiến + chọn file của người duyệt trong hộp thoại duyệt / từ chối (design.md mục 20, lát Z1). File chỉ tải lên sau khi duyệt / từ
 * chối thành công và gắn vào đúng bước vừa xử lý (xem approvalDialogs.tsx).
 * @param props.label Nhãn ô ý kiến.
 * @param props.required Ý kiến bắt buộc (từ chối).
 * @param props.placeholder Gợi ý trong ô.
 * @param props.message Message của App.useApp() để báo file không hợp lệ.
 * @param props.onCommentChange Gọi khi ý kiến đổi.
 * @param props.onFilesChange Gọi với danh sách file đã chọn.
 */
export function ApprovalInput({ label, required, placeholder, message, onCommentChange, onFilesChange }: {
  label: string; required: boolean; placeholder: string; message: ReturnType<typeof App.useApp>['message']
  onCommentChange: (comment: string) => void; onFilesChange: (files: File[]) => void
}) {
  const [files, setFiles] = useState<File[]>([])
  const [maxMb, setMaxMb] = useState(cachedMaxMb ?? defaultAttachmentMaxMb)
  useEffect(() => {
    if (cachedMaxMb !== null) return
    apiFetch<{ maxFileSizeMb: number }>('/api/attachments/limits')
      .then((r) => {
        cachedMaxMb = r.maxFileSizeMb
        setMaxMb(r.maxFileSizeMb)
      })
      .catch(() => undefined)
  }, [])
  // Thân hàm trong ngoặc nhọn: effect không được trả về giá trị nào khác hàm dọn dẹp.
  useEffect(() => {
    onFilesChange(files)
  }, [files, onFilesChange])
  return (
    <div className="approval-input">
      <label htmlFor="approval-comment">{renderRequiredMark(label, { required })}</label>
      <Input.TextArea id="approval-comment" rows={3} maxLength={500} placeholder={placeholder} onChange={(e) => onCommentChange(e.target.value)} />
      <Upload
        multiple
        accept={attachmentExtensions.join(',')}
        fileList={files.map((f, i) => ({ uid: String(i), name: f.name, status: 'done' as const }))}
        beforeUpload={(file) => {
          const error = checkAttachmentFile(file, maxMb)
          // Chọn nhiều file một lúc thì beforeUpload gọi lần lượt – cập nhật theo danh sách trước đó.
          if (error) message.error(error)
          else setFiles((prev) => [...prev, file])
          return Upload.LIST_IGNORE
        }}
        onRemove={(item) => setFiles((prev) => prev.filter((_, i) => String(i) !== item.uid))}
      >
        <Button icon={<PaperClipOutlined />} style={{ marginTop: 8 }}>
          Đính kèm file (tối đa {maxMb} MB/file)
        </Button>
      </Upload>
    </div>
  )
}
