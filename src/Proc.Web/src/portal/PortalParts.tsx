import { DeleteOutlined, DownloadOutlined, UploadOutlined } from '@ant-design/icons'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, App, Button, Input, Upload } from 'antd'
import { useState, type ReactNode } from 'react'
import { DataGrid } from '../components/DataGrid/DataGrid'
import type { GridColumn } from '../components/DataGrid/types'
import { portalFiles, type PortalAttachment } from './portalApi'

/** Khối có dải tiêu đề của trang Portal (ui-style.md mục 5f). */
export function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="erp-portal-section">
      <h2>{title}</h2>
      <div className="erp-portal-section-body">{children}</div>
    </section>
  )
}

const fileColumns: GridColumn<PortalAttachment>[] = [
  { key: 'fileName', title: 'Tên file', width: 260, value: (r) => r.fileName, auditField: false },
  { key: 'sizeBytes', title: 'Dung lượng (KB)', width: 110, type: 'quantity', value: (r) => Math.ceil(r.sizeBytes / 1024), auditField: false },
  { key: 'uploadedByName', title: 'Người tải lên', width: 160, value: (r) => r.uploadedByName, auditField: false },
  { key: 'uploadedAt', title: 'Thời điểm', width: 140, type: 'datetime', value: (r) => r.uploadedAt, auditField: false },
  { key: 'note', title: 'Ghi chú', width: 220, value: (r) => r.note, auditField: false },
]

/** Thuộc tính khối file NCC. */
interface PortalFilesProps {
  /** Loại chứng từ: thư mời báo giá, đơn đặt hàng hoặc phiếu báo giao (lát P5). */
  kind: 'rfqs' | 'orders' | 'delivery-notices'
  /** Id chứng từ (phiếu báo giao: "Id đơn/Id phiếu"). */
  id: string
  /** Tiêu đề khối (thêm số file ở cuối). */
  title: string
  /** Còn thêm file được (từng file vẫn theo cờ canDelete của máy chủ khi xóa). */
  canEdit: boolean
  /** Dung lượng tối đa một file (MB). */
  maxFileSizeMb: number
  /** Dòng hướng dẫn dưới khối. */
  hint: string
  /** data-testid của lưới file. */
  testId: string
}

/**
 * File NCC gửi trên Portal (mọi form có đính kèm – CLAUDE.md mục 4): file gắn với chứng từ có sẵn nên tải lên ngay, không cần hàng chờ;
 * thêm, xóa khi chứng từ còn mở (thư mời: đến giờ hạn nộp – lát P3b-1; đơn hàng: đến khi phản hồi – lát P4a), sau đó chỉ tải xuống.
 * Tối đa theo cấu hình máy chủ (mặc định 2 MB/file); máy chủ kiểm lại dung lượng và nội dung khớp đuôi.
 */
export function PortalFiles({ kind, id, title, canEdit, maxFileSizeMb, hint, testId }: PortalFilesProps) {
  const { message, modal } = App.useApp()
  const queryClient = useQueryClient()
  const api = portalFiles(kind)
  const queryKey = ['portal', `${kind}-files`, id]
  const files = useQuery({ queryKey, queryFn: () => api.list(id) })
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const [selected, setSelected] = useState<PortalAttachment | null>(null)
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const reload = () => queryClient.invalidateQueries({ queryKey })

  const upload = async (file: File) => {
    if (file.size > maxFileSizeMb * 1024 * 1024) {
      void message.error(`File "${file.name}" quá ${maxFileSizeMb} MB. Hãy nén hoặc chia nhỏ file.`)
      return
    }
    setBusy(true)
    try {
      await api.upload(id, file, note.trim() || null)
      setNote('')
      await reload()
      void message.success(`Đã tải lên "${file.name}".`)
    } catch (e) {
      void message.error(e instanceof Error ? e.message : 'Không tải lên được file.')
    } finally {
      setBusy(false)
    }
  }

  const remove = (item: PortalAttachment) =>
    modal.confirm({
      title: 'Xóa file',
      content: `Xóa file "${item.fileName}"?`,
      okText: 'Xóa',
      cancelText: 'Không',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await api.remove(id, item.id)
          setSelected(null)
          await reload()
        } catch (e) {
          void message.error(e instanceof Error ? e.message : 'Không xóa được file.')
        }
      },
    })

  const download = (item: PortalAttachment) => void api.download(id, item).catch((e: unknown) => void message.error(String(e)))
  const rows = files.data ?? []
  return (
    <Section title={`${title} (${rows.length})`}>
      {files.error && <Alert type="error" showIcon title={files.error.message} style={{ marginBottom: 8 }} />}
      <div style={{ height: 160, display: 'flex', flexDirection: 'column' }} data-testid={testId}>
        <DataGrid
          rows={rows}
          columns={fileColumns}
          rowKey={(r) => r.id}
          groupBy={groupBy}
          onGroupByChange={setGroupBy}
          loading={files.isFetching}
          onSelectRow={setSelected}
          onOpenRow={download}
        />
      </div>
      <div className="erp-portal-file-bar">
        {canEdit && (
          <>
            <Input
              value={note}
              onChange={(e) => setNote(e.target.value)}
              maxLength={500}
              placeholder="Ghi chú file (không bắt buộc)"
              aria-label="Ghi chú file"
              style={{ width: 240 }}
            />
            <Upload
              showUploadList={false}
              multiple
              beforeUpload={(file) => {
                void upload(file)
                return false
              }}
            >
              <Button icon={<UploadOutlined />} loading={busy}>
                Tải file lên…
              </Button>
            </Upload>
          </>
        )}
        <Button icon={<DownloadOutlined />} disabled={!selected} onClick={() => selected && download(selected)}>
          Tải xuống
        </Button>
        {canEdit && (
          <Button
            icon={<DeleteOutlined />}
            disabled={!selected?.canDelete}
            title={selected && !selected.canDelete ? (selected.lockReason ?? undefined) : undefined}
            onClick={() => selected && remove(selected)}
          >
            Xóa file
          </Button>
        )}
      </div>
      <p className="erp-portal-hint">{hint}</p>
    </Section>
  )
}
