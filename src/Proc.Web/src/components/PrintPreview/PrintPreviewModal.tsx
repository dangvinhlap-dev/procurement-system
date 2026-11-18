import { CloseOutlined, DownloadOutlined, PrinterOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, App, Button, Modal, Spin } from 'antd'
import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
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
  onClose: () => void
}

/**
 * Hộp thoại "Xem đề nghị" (lát IN1 – ui-style.md mục 5s): hiện đúng trang A4 sẽ in (PDF dựng trên máy chủ, giữ nguyên mẫu biểu); **In…** ghi
 * một lần in rồi mở hộp in của trình duyệt, **Tải PDF** cũng tính là một lần in; chỉ xem thì không. Thanh trình xem PDF của trình duyệt được ẩn
 * để mọi lần in đi qua nút In (đếm được số lần in).
 */
export function PrintPreviewModal({ open, title, viewPath, printPath, printsPath, onClose }: PrintPreviewProps) {
  const { message } = App.useApp()
  const [busy, setBusy] = useState<'print' | 'download' | null>(null)
  const printFrame = useRef<HTMLIFrameElement | null>(null)
  // Bản xem (không ghi nhật ký in) và số lần in; sau khi In / Tải PDF làm mới để bản xem ghi "Đã in n lần".
  const view = useQuery({
    queryKey: ['print-preview', viewPath],
    queryFn: async () => URL.createObjectURL((await apiBlob(viewPath)).blob),
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
      const { blob } = await apiBlob(printPath, 'POST')
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
      const { blob, fileName } = await apiBlob(`${printPath}?kind=download`, 'POST')
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
