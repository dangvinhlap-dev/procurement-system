import { CloseOutlined, DownloadOutlined, PrinterOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, App, Button, Modal, Radio, Spin } from 'antd'
import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { apiBlob, apiFetch } from '../../api/client'
import type { DocumentPrint } from '../../api/signatures'
import { formatDateTime } from '../../utils/format'

/** Thuộc tính hộp thoại xem phiếu trước khi in. */
export interface PrintPreviewProps {
  open: boolean
  /** Tiêu đề hộp thoại, ví dụ "Xem đề nghị mua PR-…". */
  title: string
  /** API bản xem (GET, không ghi nhật ký in), ví dụ "/api/purchase-requests/{id}/pdf". */
  viewPath: string
  /** API In / Tải PDF (POST, ghi nhật ký in), ví dụ "/api/purchase-requests/{id}/print". */
  printPath: string
  /** API danh sách lần in (GET). */
  printsPath: string
  /**
   * Các mẫu của phiếu để chọn xem / in riêng (đề nghị mua loại B: 1B và 2B – design.md 20.4 IN1-13); gửi lên máy chủ thành `?form=<value>`.
   * Bỏ trống = một mẫu, không hiện ô chọn.
   */
  forms?: { value: string; label: ReactNode }[]
  onClose: () => void
}

/**
 * Hộp thoại "Xem đề nghị" (lát IN1 – ui-style.md mục 5s): hiện đúng trang A4 sẽ in (PDF dựng trên máy chủ, giữ nguyên mẫu biểu); **In…** ghi
 * một lần in rồi mở hộp in của trình duyệt, **Tải PDF** cũng tính là một lần in; chỉ xem thì không. Thanh trình xem PDF của trình duyệt được ẩn
 * để mọi lần in đi qua nút In (đếm được số lần in).
 */
export function PrintPreviewModal({ open, title, viewPath, printPath, printsPath, forms, onClose }: PrintPreviewProps) {
  const { message } = App.useApp()
  const [busy, setBusy] = useState<'print' | 'download' | null>(null)
  const [form, setForm] = useState(forms?.[0]?.value)
  const printFrame = useRef<HTMLIFrameElement | null>(null)
  // Mẫu đang chọn đi kèm mọi lần xem / in / tải: người dùng chọn một trong các mẫu để in (ví dụ 1B hoặc 2B).
  const formQuery = form ? `form=${encodeURIComponent(form)}` : ''
  const viewUrlPath = formQuery ? `${viewPath}?${formQuery}` : viewPath
  // Bản xem (không ghi nhật ký in) và số lần in; sau khi In / Tải PDF làm mới để bản xem ghi "Đã in n lần".
  const view = useQuery({
    queryKey: ['print-preview', viewUrlPath],
    queryFn: async () => URL.createObjectURL((await apiBlob(viewUrlPath)).blob),
    enabled: open,
    gcTime: 0,
  })
  const printsQuery = useQuery({ queryKey: ['print-log', printsPath], queryFn: () => apiFetch<DocumentPrint[]>(printsPath), enabled: open, gcTime: 0 })
  const prints = printsQuery.data ?? []
  const loading = view.isFetching
  const error = view.error ? view.error.message : null
  const reload = () => void Promise.all([view.refetch(), printsQuery.refetch()])

  // Bỏ URL blob cũ khi có bản mới / đóng hộp thoại.
  const viewUrl = view.data ?? null
  useEffect(() => () => {
    if (viewUrl) URL.revokeObjectURL(viewUrl)
  }, [viewUrl])
  // Gỡ khung in ẩn khi đóng.
  useEffect(() => () => printFrame.current?.remove(), [])

  const print = async () => {
    setBusy('print')
    try {
      const { blob } = await apiBlob(formQuery ? `${printPath}?${formQuery}` : printPath, 'POST')
      printFrame.current?.remove()
      const frame = document.createElement('iframe')
      frame.className = 'erp-print-frame'
      frame.title = 'Bản in'
      frame.src = URL.createObjectURL(blob)
      frame.onload = () => {
        try {
          frame.contentWindow?.focus()
          frame.contentWindow?.print()
        } catch {
          // Trình duyệt không cho in từ khung ẩn (hiếm): mở bản in ở tab mới để người dùng tự bấm In.
          window.open(frame.src, '_blank')
        }
      }
      document.body.appendChild(frame)
      printFrame.current = frame
      reload()
    } catch (e) {
      void message.error(e instanceof Error ? e.message : 'Không in được.')
    } finally {
      setBusy(null)
    }
  }

  const download = async () => {
    setBusy('download')
    try {
      const { blob, fileName } = await apiBlob(`${printPath}?kind=download${formQuery ? `&${formQuery}` : ''}`, 'POST')
      const url = URL.createObjectURL(blob)
      Object.assign(document.createElement('a'), { href: url, download: fileName ?? 'phieu.pdf' }).click()
      URL.revokeObjectURL(url)
      reload()
    } catch (e) {
      void message.error(e instanceof Error ? e.message : 'Không tải được PDF.')
    } finally {
      setBusy(null)
    }
  }

  const last = prints[0]
  return (
    <Modal open={open} title={title} width={940} onCancel={onClose} footer={null} destroyOnHidden className="erp-print-preview">
      <div className="erp-print-preview-tools">
        {forms && forms.length > 1 && (
          <span className="erp-print-form">
            Mẫu in:
            <Radio.Group
              optionType="button"
              size="small"
              value={form}
              onChange={(e) => setForm(e.target.value as string)}
              options={forms}
              aria-label="Chọn mẫu để xem và in"
              data-testid="print-form"
            />
          </span>
        )}
        <Button icon={<PrinterOutlined />} onClick={() => void print()} loading={busy === 'print'} disabled={loading || !!error}>
          In…
        </Button>
        <Button icon={<DownloadOutlined />} onClick={() => void download()} loading={busy === 'download'} disabled={loading || !!error}>
          Tải PDF
        </Button>
        <Button icon={<ReloadOutlined />} onClick={reload} title="Làm mới bản xem" />
        <span className="erp-print-preview-info" data-testid="print-count">
          {last
            ? `Đã in ${last.printNo} lần · lần gần nhất ${formatDateTime(last.printedAt)} – ${last.printedByName ?? last.printedByUserName ?? ''}`
            : 'Chưa in lần nào'}
        </span>
        <Button icon={<CloseOutlined />} onClick={onClose}>
          Đóng
        </Button>
      </div>
      {error && <Alert type="error" showIcon title={error} />}
      <div className="erp-print-preview-viewer">
        {loading && !viewUrl && <Spin />}
        {viewUrl && (
          // Ẩn thanh công cụ của trình xem PDF: in qua nút "In…" để đếm số lần in.
          <iframe title="Bản xem phiếu" src={`${viewUrl}#toolbar=0&navpanes=0&view=FitH`} data-testid="print-preview-frame" />
        )}
      </div>
    </Modal>
  )
}
