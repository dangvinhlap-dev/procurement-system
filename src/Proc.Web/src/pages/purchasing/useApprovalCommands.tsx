import { CheckOutlined, CloseCircleOutlined, CloseOutlined, SaveOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { App, Button, Input } from 'antd'
import type { ReactNode } from 'react'
import { useNavigate } from 'react-router'
import type { PurchaseActions, PurchaseStatus } from '../../api/purchasing'
import { renderRequiredMark } from '../../components/RequiredMark/requiredMark'

/** Phiếu mua hàng tối thiểu mà lệnh chung cần. */
interface ApprovableDoc {
  id: string
  no: string
  status: PurchaseStatus
  actions: PurchaseActions
}

/** Một lệnh theo Id phiếu (gửi duyệt, duyệt, từ chối, hủy). */
interface DocAction<T> {
  mutateAsync: (args: { id: string; comment?: string | null }) => Promise<T>
  isPending: boolean
}

/** Tham số của các lệnh trên phiếu có duyệt. */
interface ApprovalCommandOptions<T extends ApprovableDoc> {
  /** Đang lập mới. */
  isNew: boolean
  /** Phiếu đã tải. */
  doc: T | undefined
  /** Phiếu chỉ xem. */
  readOnly: boolean
  /** Kiểm tra form và lưu nháp; trả về phiếu đã lưu, null nếu lỗi. */
  persist: () => Promise<T | null>
  /** Các lệnh của loại phiếu. */
  mutations: { create: { isPending: boolean }; update: { isPending: boolean }; submit: DocAction<T>; approve: DocAction<T>; reject: DocAction<T>; cancel: DocAction<T> }
  /** Đường dẫn danh sách, ví dụ "/mua-hang/pr". */
  listPath: string
  /** Tên loại phiếu trong câu hỏi, ví dụ "đề nghị mua hàng". */
  noun: string
  /** Hiện lỗi trên phiếu. */
  setError: (message: string | null) => void
}

/**
 * Lệnh chung của phiếu có duyệt trên thanh lệnh DocumentForm (design.md mục 10.1): Lưu nháp · Gửi duyệt (lưu trước rồi gửi) · Duyệt ·
 * Từ chối (bắt buộc lý do) · Hủy · Đóng. Nút hiện theo quyền server trả về (actions) – server vẫn kiểm tra lại khi bấm.
 * @returns Hàm lưu (Ctrl+S) và các nút lệnh.
 */
export function useApprovalCommands<T extends ApprovableDoc>({ isNew, doc, readOnly, persist, mutations, listPath, noun, setError }: ApprovalCommandOptions<T>) {
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu phiếu ${saved.no}.`)
    if (isNew) navigate(`${listPath}/${saved.id}`, { replace: true })
  }

  const submit = () =>
    modal.confirm({
      title: `Gửi duyệt ${noun}?`,
      content: 'Phiếu chuyển sang Chờ duyệt, không sửa được nữa trừ khi bị từ chối.',
      okText: 'Gửi duyệt',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        const saved = await persist()
        if (!saved) return
        try {
          const sent = await mutations.submit.mutateAsync({ id: saved.id })
          message.success(sent.status === 'Approved' ? `Phiếu ${sent.no} dưới ngưỡng duyệt – đã duyệt ngay.` : `Đã gửi duyệt phiếu ${sent.no}.`)
        } catch (e) {
          fail(e, 'Không gửi duyệt được.')
        }
        if (isNew) navigate(`${listPath}/${saved.id}`, { replace: true })
      },
    })

  const approve = () =>
    doc &&
    modal.confirm({
      title: `Duyệt phiếu ${doc.no}?`,
      okText: 'Duyệt',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          const result = await mutations.approve.mutateAsync({ id: doc.id })
          message.success(result.status === 'Approved' ? `Đã duyệt xong phiếu ${doc.no}.` : `Đã duyệt cấp của bạn – phiếu ${doc.no} chuyển cấp tiếp theo.`)
        } catch (e) {
          fail(e, 'Không duyệt được.')
        }
      },
    })

  const reject = () => {
    if (!doc) return
    let reason = ''
    modal.confirm({
      title: `Từ chối phiếu ${doc.no}?`,
      content: (
        <>
          <label htmlFor="reject-reason">{renderRequiredMark('Lý do từ chối', { required: true })}</label>
          <Input.TextArea
            id="reject-reason"
            rows={3}
            maxLength={500}
            placeholder="Người lập sẽ thấy lý do để sửa"
            onChange={(e) => (reason = e.target.value)}
          />
        </>
      ),
      okText: 'Từ chối',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        if (!reason.trim()) {
          message.error('Hãy nhập lý do từ chối.')
          throw new Error('missing reason')
        }
        setError(null)
        try {
          await mutations.reject.mutateAsync({ id: doc.id, comment: reason.trim() })
          message.success(`Đã từ chối phiếu ${doc.no}.`)
        } catch (e) {
          fail(e, 'Không từ chối được.')
        }
      },
    })
  }

  const cancel = () =>
    doc &&
    modal.confirm({
      title: `Hủy phiếu ${doc.no}?`,
      content: 'Phiếu đã hủy giữ lại số phiếu và lịch sử, không dùng lại được.',
      okText: 'Hủy phiếu',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.cancel.mutateAsync({ id: doc.id })
          message.success(`Đã hủy phiếu ${doc.no}.`)
        } catch (e) {
          fail(e, 'Không hủy được phiếu.')
        }
      },
    })

  const canEdit = isNew ? !readOnly : doc?.actions.canEdit === true
  const actions: ReactNode = (
    <>
      {canEdit && (
        <Button type="primary" icon={<SaveOutlined />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending}>
          Lưu nháp
        </Button>
      )}
      {(isNew ? !readOnly : doc?.actions.canSubmit) && (
        <Button icon={<SendOutlined />} onClick={submit} loading={mutations.submit.isPending}>
          Gửi duyệt
        </Button>
      )}
      {doc?.actions.canApprove && (
        <>
          <Button type="primary" icon={<CheckOutlined />} onClick={approve} loading={mutations.approve.isPending}>
            Duyệt
          </Button>
          <Button danger icon={<CloseCircleOutlined />} onClick={reject} loading={mutations.reject.isPending}>
            Từ chối
          </Button>
        </>
      )}
      {!isNew && doc?.actions.canCancel && (
        <Button danger icon={<StopOutlined />} onClick={cancel}>
          Hủy phiếu
        </Button>
      )}
      <Button type="text" icon={<CloseOutlined />} onClick={() => navigate(listPath)}>
        Đóng
      </Button>
    </>
  )

  return { save, actions }
}
