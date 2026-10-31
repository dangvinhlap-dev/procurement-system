import { DownOutlined, PrinterOutlined, UpOutlined } from '@ant-design/icons'
import { Alert, App, Button, Form, Spin, Tabs, Tag, type FormInstance } from 'antd'
import { useCallback, useEffect, useState, type ReactNode } from 'react'
import { apiOpenPdf } from '../../api/client'
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
  /** Nút lệnh theo trạng thái và quyền: Lưu nháp, Ghi sổ, Hủy phiếu, Đóng… – phiếu đang sửa thì nằm ở thanh chân, chỉ xem thì ở phải tiêu đề. */
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
  /** API PDF của phiếu đã lưu, ví dụ "/api/purchase-orders/{id}/pdf": hiện nút "In phiếu" (Ctrl+P) – design.md mục 10.10. */
  printPath?: string
}

/**
 * Khuôn chung của mọi chứng từ theo Dynamic Page của Fiori (design.md mục 10.1, ui-guide.md mục 4, 6):
 * phần đầu trang gồm tên loại phiếu, số phiếu, trạng thái (nhãn màu + chữ), người lập, nút In phiếu ở bên phải;
 * nội dung gồm khối "Thông tin phiếu" 3 cột (thu gọn được), lưới dòng nhập như Excel, tổng tiền, tab phụ.
 * Phiếu đang sửa: nút lệnh (Lưu, Gửi duyệt…) ở thanh chân dính đáy; phiếu chỉ xem: nút lệnh ở phải tiêu đề.
 * Bàn phím: Ctrl+S lưu, F2 thêm dòng, Ctrl+P in phiếu PDF.
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
  printPath,
}: DocumentFormProps) {
  const [tab, setTab] = useState(defaultTab)
  const { message } = App.useApp()
  const [printing, setPrinting] = useState(false)
  // Thu gọn khối "Thông tin phiếu" để lưới dòng được nhiều chỗ; chỉ ẩn bằng CSS, các ô vẫn nằm trong form nên kiểm tra dữ liệu vẫn chạy.
  const [headerCollapsed, setHeaderCollapsed] = useState(false)

  /** Mở PDF của phiếu ở tab mới để xem, in hoặc lưu file. */
  const print = useCallback(async () => {
    if (!printPath) return
    setPrinting(true)
    try {
      await apiOpenPdf(printPath)
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không in được phiếu.')
    } finally {
      setPrinting(false)
    }
  }, [printPath, message])

  // Phím tắt của phiếu (design.md mục 10.1). F2 chỉ khi không có danh sách thả xuống đang mở, để không chen vào ô đang chọn.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's' && onSave) {
        e.preventDefault()
        onSave()
      } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'p' && printPath) {
        // Ctrl+P in phiếu PDF thay cho in cả trang web của trình duyệt.
        e.preventDefault()
        void print()
      } else if (e.key === 'F2' && onAddLine && !readOnly && !hasOpenPopup()) {
        e.preventDefault()
        setTab('lines')
        onAddLine()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onSave, onAddLine, readOnly, printPath, print])

  return (
    <div className="document-form">
      <div className="document-form-head">
        <div className="document-form-title">
          <h2>{title}</h2>
          <span className="document-form-number">{number ?? '(số phiếu tự cấp khi lưu)'}</span>
          <Tag color={toneColor[status.tone]}>{status.label}</Tag>
          {meta && <span className="document-form-meta">{meta}</span>}
        </div>
        <div className="document-form-actions">
          {readOnly && actions}
          {printPath && (
            <Button icon={<PrinterOutlined />} onClick={() => void print()} loading={printing} title="In phiếu (Ctrl+P)">
              In phiếu
            </Button>
          )}
        </div>
      </div>
      <div className="document-form-content">
        {error && <Alert type="error" showIcon title={error} className="document-form-alert" />}
        {notice}
        <Spin spinning={loading}>
          <Form form={form} component={false} layout="vertical" requiredMark={false} initialValues={initialValues} disabled={readOnly}>
            <section className={`document-form-header${headerCollapsed ? ' is-collapsed' : ''}`} aria-label="Thông tin phiếu">
              <div className="document-form-header-title">
                <h3>Thông tin phiếu</h3>
                {/* Nút thu gọn không thuộc dữ liệu phiếu: vẫn bấm được khi phiếu chỉ xem (Form disabled). */}
                <Button
                  type="text"
                  size="small"
                  disabled={false}
                  icon={headerCollapsed ? <DownOutlined /> : <UpOutlined />}
                  aria-expanded={!headerCollapsed}
                  onClick={() => setHeaderCollapsed((v) => !v)}
                >
                  {headerCollapsed ? 'Mở rộng' : 'Thu gọn'}
                </Button>
              </div>
              <div className="document-form-grid">{header}</div>
            </section>
            <Tabs
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
      {!readOnly && (
        // Thanh chân (footer bar) khi đang sửa – ui-guide.md mục 4, ý 3.
        <div className="document-form-bar" data-testid="document-form-bar">
          {actions}
        </div>
      )}
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
