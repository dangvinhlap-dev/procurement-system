import { CheckCircleOutlined, DownloadOutlined, FileExcelOutlined, InboxOutlined } from '@ant-design/icons'
import { useQueryClient } from '@tanstack/react-query'
import { Alert, App, Button, Modal, Upload } from 'antd'
import { useState } from 'react'
import { apiDownload, apiUpload } from '../../api/client'
import { DataGrid } from '../DataGrid/DataGrid'
import type { GridColumn } from '../DataGrid/types'

/** Một lỗi nhập Excel – khớp Proc.Application.Excel.ExcelImportError. */
interface ImportError {
  row: number
  code: string | null
  message: string
}

/** Kết quả nhập Excel – khớp Proc.Application.Excel.ExcelImportResult. */
interface ImportResult {
  catalog: string
  totalRows: number
  created: number
  updated: number
  errors: ImportError[]
  committed: boolean
}

const errorColumns: GridColumn<ImportError & { key: string }>[] = [
  { key: 'row', title: 'Dòng', width: 60, type: 'quantity', value: (r) => (r.row > 0 ? r.row : null) },
  { key: 'code', title: 'Mã', width: 120, value: (r) => r.code },
  { key: 'message', title: 'Lỗi', width: 420, value: (r) => r.message },
]

/**
 * Hộp thoại nhập danh mục từ Excel (design.md mục 5.10): tải file mẫu → chọn file → "Kiểm tra thử" (không lưu gì, báo lỗi
 * theo dòng) → "Nhập thật" (chỉ lưu khi mọi dòng đúng). Nhập xong tải lại mọi danh sách đang mở.
 * @param catalog Mã danh mục trên API, ví dụ "items".
 * @param title Tên danh mục hiển thị.
 * @param onClose Đóng hộp thoại.
 */
export function ExcelImportDialog({ catalog, title, onClose }: { catalog: string; title: string; onClose: () => void }) {
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const [file, setFile] = useState<File | null>(null)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState<'check' | 'commit' | 'template' | null>(null)

  const run = async (commit: boolean) => {
    if (!file) return
    setBusy(commit ? 'commit' : 'check')
    setError(null)
    try {
      const r = await apiUpload<ImportResult>(`/api/excel/${catalog}/import?commit=${commit}`, file)
      setResult(r)
      if (r.committed) {
        message.success(`Đã nhập ${r.created} dòng mới, cập nhật ${r.updated} dòng.`)
        void queryClient.invalidateQueries()
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không nhập được file.')
    } finally {
      setBusy(null)
    }
  }

  const downloadTemplate = async () => {
    setBusy('template')
    try {
      await apiDownload(`/api/excel/${catalog}?template=true`, `mau-${catalog}.xlsx`)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không tải được file mẫu.')
    } finally {
      setBusy(null)
    }
  }

  const checkedOk = result !== null && result.errors.length === 0 && !result.committed

  return (
    <Modal
      open
      title={`Nhập ${title} từ Excel`}
      onCancel={onClose}
      width={720}
      destroyOnHidden
      footer={
        <>
          <Button onClick={onClose}>Đóng</Button>
          <Button icon={<FileExcelOutlined />} disabled={!file || result?.committed} loading={busy === 'check'} onClick={() => run(false)}>
            Kiểm tra thử
          </Button>
          <Button type="primary" icon={<CheckCircleOutlined />} disabled={!checkedOk} loading={busy === 'commit'} onClick={() => run(true)}>
            Nhập thật
          </Button>
        </>
      }
    >
      <div className="detail-hint" style={{ marginBottom: 8 }}>
        Mã đã có thì cập nhật, chưa có thì thêm mới; không xóa bản ghi nào. Bấm "Kiểm tra thử" trước – "Nhập thật" chỉ lưu khi mọi
        dòng đều đúng. Dễ nhất: bấm "Xuất Excel" trên danh sách, sửa file đó rồi nhập lại.
      </div>
      <Button icon={<DownloadOutlined />} onClick={downloadTemplate} loading={busy === 'template'} style={{ marginBottom: 8 }}>
        Tải file mẫu
      </Button>
      <Upload.Dragger
        accept=".xlsx"
        maxCount={1}
        // Giữ file ở trình duyệt, chỉ gửi khi bấm Kiểm tra thử / Nhập thật.
        beforeUpload={(f) => {
          setFile(f)
          setResult(null)
          return false
        }}
        onRemove={() => {
          setFile(null)
          setResult(null)
        }}
        fileList={file ? [{ uid: 'file', name: file.name, status: 'done' }] : []}
      >
        <p className="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p>Kéo file .xlsx vào đây hoặc bấm để chọn file</p>
      </Upload.Dragger>

      {error && <Alert type="error" showIcon title={error} style={{ marginTop: 8 }} />}
      {result && (
        <Alert
          style={{ marginTop: 8 }}
          showIcon
          type={result.committed ? 'success' : result.errors.length > 0 ? 'error' : 'info'}
          title={
            result.committed
              ? `Đã nhập xong ${result.totalRows} dòng: ${result.created} dòng mới, ${result.updated} dòng cập nhật.`
              : result.errors.length > 0
                ? `Có ${result.errors.length} lỗi – chưa lưu dòng nào. Sửa file rồi kiểm tra lại.`
                : `File hợp lệ: ${result.totalRows} dòng (${result.created} dòng mới, ${result.updated} dòng cập nhật). Bấm "Nhập thật" để lưu.`
          }
        />
      )}
      {result && result.errors.length > 0 && (
        <div style={{ height: 240, display: 'flex', flexDirection: 'column', marginTop: 8 }}>
          <DataGrid
            rows={result.errors.map((e, i) => ({ ...e, key: String(i) }))}
            columns={errorColumns}
            rowKey={(r) => r.key}
            groupBy={null}
            onGroupByChange={() => {}}
          />
        </div>
      )}
    </Modal>
  )
}
