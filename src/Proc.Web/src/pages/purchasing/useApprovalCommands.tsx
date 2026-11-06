import { CheckOutlined, CloseCircleOutlined, CloseOutlined, MinusCircleOutlined, SaveOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { App, Button, Input } from 'antd'
import type { ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { previewApproval, type ApprovalStep, type PurchaseActions, type PurchaseStatus } from '../../api/purchasing'
import { renderRequiredMark } from '../../components/RequiredMark/requiredMark'
import { ApprovalChain, ApproveInfo } from './ApprovalHistory'

/** Phiếu mua hàng tối thiểu mà lệnh chung cần. */
interface ApprovableDoc {
  id: string
  no: string
  status: PurchaseStatus
  actions: PurchaseActions
  approvals?: ApprovalStep[]
  createdByName?: string | null
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
  mutations: {
    create: { isPending: boolean }; update: { isPending: boolean }; submit: DocAction<T>; approve: DocAction<T>; reject: DocAction<T>; cancel: DocAction<T>
    /** Đóng phần còn lại (lát R6) – trang không có lệnh này thì bỏ trống. */
    closeRemaining?: DocAction<T>
  }
  /** Đường dẫn danh sách, ví dụ "/mua-hang/pr". */
  listPath: string
  /** Tên loại phiếu trong câu hỏi, ví dụ "đề nghị mua hàng". */
  noun: string
  /** Hiện lỗi trên phiếu. */
  setError: (message: string | null) => void
  /** Loại phiếu duyệt (PR, PO) – để xem trước chuỗi duyệt trước khi gửi. */
  docType: string
}

type AppApi = ReturnType<typeof App.useApp>

/**
 * Hộp thoại hỏi có ô lý do bắt buộc – dùng cho từ chối và đóng phần còn lại (lát R6).
 * @param modal Modal của App.useApp().
 * @param message Message của App.useApp().
 * @param options.title Câu hỏi.
 * @param options.label Nhãn ô lý do.
 * @param options.placeholder Gợi ý trong ô.
 * @param options.okText Chữ nút đồng ý.
 * @param options.description Câu giải thích phía trên ô lý do (tùy chọn).
 * @param options.onOk Gọi với lý do đã cắt khoảng trắng.
 */
export function confirmWithReason(
  modal: AppApi['modal'],
  message: AppApi['message'],
  options: { title: string; label: string; placeholder: string; okText: string; description?: string; onOk: (reason: string) => Promise<void> },
) {
  let reason = ''
  modal.confirm({
    title: options.title,
    content: (
      <>
        {options.description && <p>{options.description}</p>}
        <label htmlFor="action-reason">{renderRequiredMark(options.label, { required: true })}</label>
        <Input.TextArea id="action-reason" rows={3} maxLength={500} placeholder={options.placeholder} onChange={(e) => (reason = e.target.value)} />
      </>
    ),
    okText: options.okText,
    okButtonProps: { danger: true },
    cancelText: 'Không',
    onOk: async () => {
      if (!reason.trim()) {
        message.error(`Hãy nhập ${options.label.toLowerCase()}.`)
        throw new Error('missing reason')
      }
      await options.onOk(reason.trim())
    },
  })
}

/**
 * Hộp thoại từ chối có ô lý do bắt buộc (design.md mục 18.6: từ chối bắt buộc lý do) – dùng chung cho PR, PO, bảng giá NCC.
 * @param modal Modal của App.useApp().
 * @param message Message của App.useApp().
 * @param title Câu hỏi, ví dụ "Từ chối phiếu PR-0001?".
 * @param onReject Gọi với lý do đã cắt khoảng trắng khi người dùng bấm Từ chối.
 */
export function confirmReject(modal: AppApi['modal'], message: AppApi['message'], title: string, onReject: (reason: string) => Promise<void>) {
  let reason = ''
  modal.confirm({
    title,
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
      await onReject(reason.trim())
    },
  })
}

/**
 * Gửi duyệt có xem trước (yêu cầu người dùng 03/11/2026): lưu phiếu, hỏi server chuỗi duyệt dự kiến (chạy thử – không lưu gì) rồi mới hỏi
 * xác nhận, popup liệt kê từng cấp và người duyệt được. Lỗi khiến phiếu không gửi được (như khi gửi thật) báo ngay, không mở popup.
 * @param modal Modal của App.useApp().
 * @param options.title Câu hỏi, ví dụ "Gửi duyệt đơn mua hàng?".
 * @param options.docType Loại phiếu duyệt.
 * @param options.prepare Lưu phiếu; trả Id (và lựa chọn trúng thầu với RFQ), null khi lưu lỗi.
 * @param options.onSubmit Gửi duyệt thật.
 * @param options.onError Báo lỗi (lưu hoặc xem trước).
 */
export async function confirmSubmitWithPreview(
  modal: AppApi['modal'],
  options: {
    title: string
    docType: string
    prepare: () => Promise<{ id: string; award?: unknown } | null>
    onSubmit: (id: string) => Promise<void>
    onError: (e: unknown) => void
    byCompany?: boolean
    okText?: string
  },
) {
  let prepared: { id: string; award?: unknown } | null
  let preview
  try {
    prepared = await options.prepare()
    if (!prepared) return
    preview = await previewApproval(options.docType, prepared.id, prepared.award)
  } catch (e) {
    options.onError(e)
    return
  }
  const id = prepared.id
  modal.confirm({
    title: options.title,
    width: 520,
    content: <ApprovalChain steps={preview.steps} approvedImmediately={preview.approvedImmediately} byCompany={options.byCompany} />,
    okText: options.okText ?? 'Gửi duyệt',
    cancelText: 'Không',
    onOk: () => options.onSubmit(id),
  })
}

/**
 * Nội dung popup Duyệt: người lập và người duyệt tiếp theo sau mình (yêu cầu người dùng 03/11/2026).
 * @param doc Phiếu có lịch sử duyệt và người lập.
 */
export function approveContent(doc: { approvals?: ApprovalStep[]; createdByName?: string | null }) {
  return <ApproveInfo steps={doc.approvals ?? []} createdByName={doc.createdByName} />
}

/**
 * Lệnh chung của phiếu có duyệt trên thanh lệnh DocumentForm (design.md mục 10.1): Lưu nháp · Gửi duyệt (lưu trước rồi gửi) · Duyệt ·
 * Từ chối (bắt buộc lý do) · Hủy · Đóng. Nút hiện theo quyền server trả về (actions) – server vẫn kiểm tra lại khi bấm.
 * @returns Hàm lưu (Ctrl+S) và các nút lệnh.
 */
export function useApprovalCommands<T extends ApprovableDoc>({ isNew, doc, readOnly, persist, mutations, listPath, noun, setError, docType }: ApprovalCommandOptions<T>) {
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

  // Lưu trước rồi xem trước chuỗi duyệt; phiếu mới đã lưu thì chuyển sang trang phiếu dù người dùng chọn "Không".
  const submit = () => {
    setError(null)
    let savedId: string | null = null
    void confirmSubmitWithPreview(modal, {
      title: `Gửi duyệt ${noun}?`,
      docType,
      prepare: async () => {
        const saved = await persist()
        if (!saved) return null
        savedId = saved.id
        if (isNew) navigate(`${listPath}/${saved.id}`, { replace: true })
        return { id: saved.id }
      },
      onSubmit: async (id) => {
        try {
          const sent = await mutations.submit.mutateAsync({ id })
          message.success(sent.status === 'Approved' ? `Phiếu ${sent.no} dưới ngưỡng duyệt – đã duyệt ngay.` : `Đã gửi duyệt phiếu ${sent.no}.`)
        } catch (e) {
          fail(e, 'Không gửi duyệt được.')
        }
      },
      onError: (e) => {
        fail(e, 'Không gửi duyệt được.')
        if (isNew && savedId) navigate(`${listPath}/${savedId}`, { replace: true })
      },
    })
  }

  const approve = () =>
    doc &&
    modal.confirm({
      title: `Duyệt phiếu ${doc.no}?`,
      content: approveContent(doc),
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
    confirmReject(modal, message, `Từ chối phiếu ${doc.no}?`, async (reason) => {
      setError(null)
      try {
        await mutations.reject.mutateAsync({ id: doc.id, comment: reason })
        message.success(`Đã từ chối phiếu ${doc.no}.`)
      } catch (e) {
        fail(e, 'Không từ chối được.')
      }
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

  const closeRemaining = () =>
    doc &&
    mutations.closeRemaining &&
    confirmWithReason(modal, message, {
      title: `Đóng phần còn lại của phiếu ${doc.no}?`,
      description: 'Phần đã đặt/đã nhận giữ nguyên; phần còn lại không đặt hay nhận thêm được nữa.',
      label: 'Lý do đóng',
      placeholder: 'Ví dụ: NCC hết hàng, bộ phận không cần thêm',
      okText: 'Đóng phần còn lại',
      onOk: async (reason) => {
        try {
          await mutations.closeRemaining!.mutateAsync({ id: doc.id, comment: reason })
          message.success(`Đã đóng phần còn lại của phiếu ${doc.no}.`)
        } catch (e) {
          fail(e, 'Không đóng được phần còn lại.')
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
      {!isNew && doc?.actions.canCloseRemaining && mutations.closeRemaining && (
        <Button danger icon={<MinusCircleOutlined />} onClick={closeRemaining} loading={mutations.closeRemaining.isPending}>
          Đóng phần còn lại
        </Button>
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
