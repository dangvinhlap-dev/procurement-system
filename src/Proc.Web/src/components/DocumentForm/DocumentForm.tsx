import { Alert, Form, Spin, Tabs, Tag, type FormInstance } from 'antd'
import { useEffect, useState, type ReactNode } from 'react'
import type { StatusTone } from '../DataGrid/types'
import { hasOpenPopup } from '../../utils/popup'
import './DocumentForm.css'

/** Màu thẻ trạng thái phiếu theo sắc thái (màu lấy từ bảng màu có sẵn của antd, không viết màu cứng). */
const toneColor: Record<StatusTone, string> = {
  success: 'success',
  warning: 'warning',
  error: 'error',
  neutral: 'default',
}

/** Một tab phụ của phiếu (Ghi chú, Đính kèm, Nhật ký…). */
export interface DocumentTab {
  key: string
  label: string
  children: ReactNode
}

/** Thuộc tính của DocumentForm. */
export interface DocumentFormProps {
  /** Tên loại phiếu, ví dụ "Phiếu nhập mua từ NCC". */
  title: string
  /** Số phiếu; null khi chưa lưu lần nào. */
  number: string | null
  /** Trạng thái phiếu. */
  status: { label: string; tone: StatusTone }
  /** Dòng thông tin phụ cạnh trạng thái: người lập, ngày lập, kỳ kho… */
  meta?: ReactNode
  /** Nút lệnh theo trạng thái và quyền: Lưu nháp, Ghi sổ, Hủy phiếu, In, Đóng… */
  actions: ReactNode
  /** Lỗi từ server (tiếng Việt). */
  error?: string | null
  /** Thông báo trên đầu phiếu, ví dụ lý do chỉ xem được. */
  notice?: ReactNode
  loading?: boolean
  /** Form bao toàn bộ phiếu: đầu phiếu và các dòng cùng một form. */
  form: FormInstance
  initialValues?: object
  /** Chỉ xem: mọi ô bị khóa. */
  readOnly?: boolean
  /** Các ô đầu phiếu – đặt trong lưới 3 cột (ô rộng dùng lớp document-form-wide). */
  header: ReactNode
  /** Lưới dòng hàng (LineGrid). */
  lines: ReactNode
  /** Chân phiếu: tổng tiền. */
  footer?: ReactNode
  /** Tab phụ sau tab "Dòng hàng". */
  extraTabs?: DocumentTab[]
  /** Nhãn tab dòng (mặc định "Dòng hàng"; ví dụ phiếu tìm nguồn là "NCC ứng viên"). */
  linesLabel?: string
  /** Tab mở sẵn khi vào phiếu (mặc định tab dòng); ví dụ mở thẳng tab so sánh báo giá. */
  defaultTab?: string
  /** Ctrl+S. */
  onSave?: () => void
  /** F2 – thêm dòng. */
  onAddLine?: () => void
}

/**
 * Khuôn chung của mọi chứng từ (design.md mục 10.1, ui-guide.md mục DocumentForm): thanh lệnh của phiếu; thanh trạng thái
 * (số phiếu, trạng thái, người lập); đầu phiếu 3 cột; lưới dòng nhập như Excel; chân phiếu tổng tiền; tab phụ.
 * Bàn phím: Ctrl+S lưu, F2 thêm dòng.
 */
export function DocumentForm({
  title,
  number,
  status,
  meta,
  actions,
  error,
  notice,
  loading = false,
  form,
  initialValues,
  readOnly = false,
  header,
  lines,
  footer,
  extraTabs = [],
  linesLabel = 'Dòng hàng',
  defaultTab = 'lines',
  onSave,
  onAddLine,
}: DocumentFormProps) {
  const [tab, setTab] = useState(defaultTab)

  // Phím tắt của phiếu (design.md mục 10.1). F2 chỉ khi không có danh sách thả xuống đang mở, để không chen vào ô đang chọn.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's' && onSave) {
        e.preventDefault()
        onSave()
      } else if (e.key === 'F2' && onAddLine && !readOnly && !hasOpenPopup()) {
        e.preventDefault()
        setTab('lines')
        onAddLine()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onSave, onAddLine, readOnly])

  return (
    <div className="document-form">
      <div className="document-form-actions">{actions}</div>
      <div className="document-form-title">
        <h2>{title}</h2>
        <span className="document-form-number">{number ?? '(số phiếu tự cấp khi lưu)'}</span>
        <Tag color={toneColor[status.tone]}>{status.label}</Tag>
        {meta && <span className="document-form-meta">{meta}</span>}
      </div>
      {error && <Alert type="error" showIcon title={error} className="document-form-alert" />}
      {notice}
      <Spin spinning={loading}>
        <Form form={form} component={false} layout="vertical" requiredMark={false} initialValues={initialValues} disabled={readOnly}>
          <fieldset className="document-form-header">
            <legend>Thông tin phiếu</legend>
            <div className="document-form-grid">{header}</div>
          </fieldset>
          <Tabs
            type="card"
            size="small"
            className="document-form-tabs"
            activeKey={tab}
            onChange={setTab}
            items={[{ key: 'lines', label: linesLabel, children: lines }, ...extraTabs].map((t) => ({ ...t, forceRender: true }))}
          />
          {footer && <div className="document-form-footer">{footer}</div>}
        </Form>
      </Spin>
    </div>
  )
}

/**
 * Một ô tổng ở chân phiếu: nhãn và số căn phải.
 * @param label Nhãn, ví dụ "Tiền hàng".
 * @param value Giá trị đã định dạng.
 * @param strong In đậm (dòng tổng thanh toán).
 */
export function DocumentTotal({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className={strong ? 'document-total document-total-strong' : 'document-total'}>
      <span>{label}</span>
      <span className="document-total-value">{value}</span>
    </div>
  )
}
