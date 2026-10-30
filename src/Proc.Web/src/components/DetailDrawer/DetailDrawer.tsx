import { CloseOutlined, SaveOutlined } from '@ant-design/icons'
import { Alert, Button, Drawer, Form, Spin, Tabs, type FormInstance } from 'antd'
import { useEffect, useState, type ReactNode } from 'react'
import './DetailDrawer.css'

/** Một tab của ngăn kéo chi tiết. */
export interface DetailTab {
  key: string
  label: string
  children: ReactNode
}

/** Lớp phủ có thể nằm phía trên ngăn kéo: danh sách thả xuống, lịch, hộp thoại. */
const POPUP_SELECTOR = [
  '.ant-select-dropdown:not(.ant-select-dropdown-hidden)',
  '.ant-picker-dropdown:not(.ant-picker-dropdown-hidden)',
  '.ant-modal-wrap:not([style*="display: none"])',
].join(', ')

/**
 * Có lớp phủ nào đang mở không. Bỏ qua lớp phủ đang chạy hiệu ứng ẩn (lớp *-leave): vừa chọn xong một mục, danh sách thả xuống
 * còn ẩn dần vài trăm mili giây – nếu tính là "đang mở" thì bấm Esc ngay lúc đó không đóng được ngăn kéo.
 * @returns true nếu đang có lớp phủ mở.
 */
function hasOpenPopup(): boolean {
  return [...document.querySelectorAll(POPUP_SELECTOR)].some((e) => !/-leave(\s|$)/.test(e.className))
}

/** Thuộc tính của DetailDrawer. */
export interface DetailDrawerProps {
  open: boolean
  /** Tiêu đề, ví dụ "Người dùng: thukho.bha" hoặc "Thêm người dùng". */
  title: string
  tabs: DetailTab[]
  onClose: () => void
  /** Bấm Lưu hoặc Ctrl+S; không truyền thì chỉ xem (ẩn nút Lưu). */
  onSave?: () => void
  saving?: boolean
  loading?: boolean
  /** Lỗi từ server (tiếng Việt), hiện trên đầu ngăn kéo. */
  error?: string | null
  /** Nút lệnh thêm cạnh Lưu, ví dụ "Đặt lại mật khẩu". */
  extraActions?: ReactNode
  /** Form bao toàn bộ các tab, để trường ở mọi tab cùng một form (kiểm tra và lấy giá trị một lần khi Lưu). */
  form?: FormInstance
  /** Giá trị ban đầu của form (khi tạo mới). */
  initialValues?: object
  /** Chỉ xem: mọi trường bị khóa. */
  readOnly?: boolean
}

/**
 * Ngăn kéo chi tiết của form danh mục (design.md mục 10.9: danh sách + ngăn kéo chi tiết có các tab).
 * Bố cục cố định: hàng nút lệnh (Lưu, Đóng, nút riêng của màn hình), thông báo lỗi, các tab kiểu Windows.
 * Bàn phím: Ctrl+S lưu, Esc đóng.
 */
export function DetailDrawer({
  open,
  title,
  tabs,
  onClose,
  onSave,
  saving = false,
  loading = false,
  error,
  extraActions,
  form,
  initialValues,
  readOnly = false,
}: DetailDrawerProps) {
  const [tab, setTab] = useState(tabs[0]?.key)

  // Ctrl+S lưu khi ngăn kéo đang mở – thao tác bằng bàn phím (CLAUDE.md mục 9).
  useEffect(() => {
    if (!open || !onSave) return
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
        e.preventDefault()
        if (!saving) onSave()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onSave, saving])

  // Esc đóng ngăn kéo, nghe ở cấp cửa sổ: Drawer của antd chỉ nhận Esc khi tiêu điểm nằm trong ngăn kéo, mà chọn xong một mục
  // trong ô chọn thì tiêu điểm có thể rơi ra ngoài (danh sách thả xuống vẽ ở cuối trang) – khi đó Esc không đóng được.
  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape' || e.defaultPrevented) return
      // Đang mở danh sách thả xuống, lịch hay hộp thoại con thì Esc đóng cái đó trước, chưa đóng ngăn kéo.
      if (hasOpenPopup()) return
      onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])

  return (
    <Drawer
      open={open}
      title={title}
      onClose={onClose}
      keyboard={false}
      size={720}
      destroyOnHidden
      className="detail-drawer"
      closeIcon={null}
    >
      <div className="detail-drawer-actions">
        {onSave && (
          <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={onSave} disabled={loading}>
            Lưu
          </Button>
        )}
        {extraActions}
        <Button type="text" icon={<CloseOutlined />} onClick={onClose}>
          Đóng
        </Button>
      </div>
      {error && <Alert type="error" showIcon title={error} className="detail-drawer-error" />}
      <Spin spinning={loading}>
        <Form
          form={form}
          component={false}
          layout="vertical"
          requiredMark={false}
          initialValues={initialValues}
          disabled={readOnly}
        >
          <Tabs
            type="card"
            size="small"
            className="detail-drawer-tabs"
            activeKey={tab}
            onChange={setTab}
            // Vẽ sẵn mọi tab để trường ở tab chưa mở vẫn thuộc form và được kiểm tra khi Lưu.
            items={tabs.map((t) => ({ key: t.key, label: t.label, children: t.children, forceRender: true }))}
          />
        </Form>
      </Spin>
    </Drawer>
  )
}
