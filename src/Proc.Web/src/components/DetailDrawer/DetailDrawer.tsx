import { CloseOutlined, RollbackOutlined, SaveOutlined } from '@ant-design/icons'
import { Alert, Button, Drawer, Form, Spin, Tabs, Tag, type FormInstance } from 'antd'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { ApprovalCheckButton, ApprovalLocks, type ApprovalTrailInfo } from '../../classic/ApprovalTrail'
import { hasOpenPopup } from '../../utils/popup'
import { useIsActiveTab } from '../../app/screenTabs'
import { auditScopeProps, type AuditScope } from '../FieldHelp/auditScope'
import { RecordHistoryButton } from '../FieldHelp/FieldHelpHost'
import { formRequiredMark } from '../RequiredMark/requiredMark'
import './DetailDrawer.css'

/** Một tab của ngăn kéo chi tiết. */
export interface DetailTab {
  key: string
  label: string
  children: ReactNode
  /**
   * Chỉ vẽ khi mở tab lần đầu. Dùng cho tab không có ô của form (bảng chỉ xem/thao tác riêng): tránh tiêu đề cột trùng nhãn ô form
   * ở tab khác (ví dụ "Họ tên", "Trạng thái") và đỡ gọi API khi không mở tab.
   */
  lazy?: boolean
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
  /**
   * Tên form – làm tiền tố id các ô (ví dụ amendment_paymentTerms). Cần khi ngăn kéo mở trên trang có form trùng tên trường, để nhãn
   * trỏ đúng ô của ngăn kéo.
   */
  formName?: string
  /**
   * Bản ghi đang mở trong nhật ký (design.md mục 9.3a): F1 ở một ô xem lịch sử ô; có id thì hiện nút "Lịch sử thay đổi" (bản ghi và các
   * dòng con như người liên hệ, tài khoản ngân hàng).
   */
  audit?: AuditScope
  /**
   * Vẽ thành **trang đầy đủ** trong AppShell thay cho ngăn kéo – dùng khi mở thẳng phiếu từ thông báo, Việc cần làm, Tra cứu chứng từ
   * (đường dẫn `<màn hình>/:id`). Cùng nội dung, nút lệnh và phím Ctrl+S/F1 như ngăn kéo; Esc không đóng trang (tránh rời trang do lỡ
   * tay), nút "Quay lại" gọi `onClose`.
   */
  asPage?: boolean
  /**
   * Phê duyệt của phiếu (design.md mục 20.1 R3, R4) – như DocumentForm: dải ổ khóa vàng cạnh tiêu đề và nút "Kiểm tra phê duyệt".
   */
  approval?: ApprovalTrailInfo
}

/**
 * Ngăn kéo chi tiết của form danh mục (design.md mục 10.9: danh sách + ngăn kéo chi tiết có các tab).
 * Bố cục cố định: hàng nút lệnh (Lưu, Đóng, nút riêng của màn hình), thông báo lỗi, các tab kiểu Windows.
 * Bàn phím: Ctrl+S lưu, Esc đóng, F1 lịch sử thay đổi của ô (khai báo audit).
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
  formName,
  audit,
  asPage = false,
  approval,
}: DetailDrawerProps) {
  const rootRef = useRef<HTMLDivElement>(null)
  const [tab, setTab] = useState(tabs[0]?.key)
  // Ngăn kéo ở thẻ màn hình đang ẩn không nhận Ctrl+S, Esc (thẻ màn hình – docs/ui-style.md mục 8a).
  const activeTab = useIsActiveTab()

  // Ctrl+S lưu khi ngăn kéo đang mở – thao tác bằng bàn phím (CLAUDE.md mục 9).
  useEffect(() => {
    if (!open || !onSave || !activeTab) return
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
        e.preventDefault()
        if (!saving) onSave()
      }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onSave, saving, activeTab])

  // Esc đóng ngăn kéo, nghe ở cấp cửa sổ: Drawer của antd chỉ nhận Esc khi tiêu điểm nằm trong ngăn kéo, mà chọn xong một mục
  // trong ô chọn thì tiêu điểm có thể rơi ra ngoài (danh sách thả xuống vẽ ở cuối trang) – khi đó Esc không đóng được.
  // Nghe ở pha capture và giữ onClose trong ref: bộ nghe luôn chạy TRƯỚC hộp thoại con (F1, xác nhận…) nên còn thấy hộp thoại đang mở.
  // Trước đây nghe ở pha bubble, effect đăng ký lại mỗi khi onClose đổi (trang cha vẽ lại khi dữ liệu tải xong) nên có lúc chạy SAU hộp
  // thoại – hộp thoại đã đóng, Esc đóng luôn cả ngăn kéo.
  const onCloseRef = useRef(onClose)
  useEffect(() => {
    onCloseRef.current = onClose
  }, [onClose])
  useEffect(() => {
    if (!open || asPage || !activeTab) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'Escape' || e.defaultPrevented) return
      // Đang mở danh sách thả xuống, lịch hay hộp thoại con thì Esc đóng cái đó trước, chưa đóng ngăn kéo.
      if (hasOpenPopup()) return
      onCloseRef.current()
    }
    window.addEventListener('keydown', onKey, true)
    return () => window.removeEventListener('keydown', onKey, true)
  }, [open, asPage, activeTab])

  const saveButton = onSave && (
    <Button type="primary" icon={<SaveOutlined />} loading={saving} onClick={onSave} disabled={loading}>
      Lưu
    </Button>
  )
  const historyButton = audit?.id && <RecordHistoryButton entityType={audit.entityType} entityId={audit.id} rootRef={rootRef} />
  const locks = approval && <ApprovalLocks steps={approval.steps} byCompany={approval.byCompany} stageLabels={approval.stageLabels} />
  const checkButton = approval && <ApprovalCheckButton {...approval} />
  const body = (
    <>
      {error && <Alert type="error" showIcon title={error} className="detail-drawer-error" />}
      <Spin spinning={loading}>
        <div ref={rootRef} {...auditScopeProps(audit)}>
          <Form
            form={form}
            name={formName}
            component={false}
            layout="vertical"
            requiredMark={formRequiredMark(!readOnly)}
            initialValues={initialValues}
            disabled={readOnly}
          >
            <Tabs
              size="small"
              className="detail-drawer-tabs"
              activeKey={tab}
              onChange={setTab}
              // Vẽ sẵn mọi tab để trường ở tab chưa mở vẫn thuộc form và được kiểm tra khi Lưu (trừ tab `lazy` – không có ô form).
              items={tabs.map((t) => ({ key: t.key, label: t.label, children: t.children, forceRender: !t.lazy }))}
            />
          </Form>
        </div>
      </Spin>
    </>
  )

  if (asPage) {
    // Bố cục trang dùng lại khung tiêu đề + nút lệnh của DocumentForm để trang mở thẳng trông như mọi trang chứng từ (ui-style.md mục 8).
    return (
      <div className="document-form detail-page">
        <div className="document-form-head">
          <div className="document-form-title">
            <h2>{title}</h2>
            {readOnly && <Tag>Chỉ xem</Tag>}
            {locks}
          </div>
          <div className="document-form-actions">
            {saveButton}
            {extraActions}
            {checkButton}
            {historyButton}
            <Button icon={<RollbackOutlined />} onClick={onClose} title="Quay lại danh sách">
              Quay lại
            </Button>
          </div>
        </div>
        <div className="document-form-content">{body}</div>
      </div>
    )
  }

  return (
    <Drawer
      open={open}
      title={
        <span className="detail-drawer-title">
          {title}
          {/* Trạng thái luôn có chữ, không chỉ dựa vào ô bị mờ (ui-guide.md mục 2). */}
          {readOnly && <Tag>Chỉ xem</Tag>}
          {locks}
        </span>
      }
      onClose={onClose}
      keyboard={false}
      size={720}
      destroyOnHidden
      className="detail-drawer"
      closeIcon={null}
    >
      <div className="detail-drawer-actions">
        {saveButton}
        {extraActions}
        {checkButton}
        {historyButton}
        <Button type="text" icon={<CloseOutlined />} onClick={onClose}>
          Đóng
        </Button>
      </div>
      {body}
    </Drawer>
  )
}
