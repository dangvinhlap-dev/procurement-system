import { CheckCircleOutlined, CloseOutlined, RollbackOutlined, SaveOutlined, StopOutlined } from '@ant-design/icons'
import { App, Button } from 'antd'
import type { ReactNode } from 'react'
import { useNavigate } from 'react-router'
import type { InventoryDocument } from '../../api/inventoryDocuments'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'

/** Thao tác theo Id phiếu (hủy, ghi sổ, bỏ ghi sổ). */
interface DocumentMutation {
  mutateAsync: (id: string) => Promise<InventoryDocument>
  isPending: boolean
}

/** Phần của useInventoryDocumentMutations mà các lệnh chung dùng – không phụ thuộc kiểu nội dung lưu của từng loại phiếu. */
interface DocumentMutations {
  create: { isPending: boolean }
  update: { isPending: boolean }
  cancel: DocumentMutation
  post: DocumentMutation
  unpost: DocumentMutation
}

/** Tham số của các lệnh trên phiếu kho. */
interface DocumentCommandOptions {
  /** Đang lập mới (chưa có số phiếu). */
  isNew: boolean
  /** Phiếu đã tải (null khi lập mới). */
  doc: InventoryDocument | undefined
  /** Phiếu chỉ xem (không sửa được). */
  readOnly: boolean
  /** Kiểm tra form và lưu nháp; trả về phiếu đã lưu, null nếu lỗi (lỗi tự hiện qua setError). */
  persist: () => Promise<InventoryDocument | null>
  /** Mutations của loại phiếu (để hủy, ghi sổ, bỏ ghi sổ). */
  mutations: DocumentMutations
  /** Đường dẫn danh sách, ví dụ "/kho/grn" – nút Đóng và đổi URL sau khi lưu phiếu mới. */
  listPath: string
  /** Tên loại phiếu trong câu hỏi xác nhận, ví dụ "phiếu nhập". */
  noun: string
  /** Hiện lỗi trên phiếu. */
  setError: (message: string | null) => void
  /** Có nút Hủy phiếu cho phiếu nháp (phiếu nhập mua nội bộ không hủy ở bên nhận). Mặc định có. */
  cancellable?: boolean
}

/**
 * Lệnh chung của phiếu kho trên thanh lệnh của DocumentForm (design.md mục 10.1): Lưu nháp, Ghi sổ (tự lưu nháp trước để ghi sổ
 * đúng nội dung đang thấy), Bỏ ghi sổ, Hủy phiếu, Đóng. Nút hiện/ẩn theo trạng thái phiếu và quyền.
 * @returns Hàm lưu (gắn Ctrl+S) và các nút lệnh.
 */
export function useDocumentCommands({ isNew, doc, readOnly, persist, mutations, listPath, noun, setError, cancellable = true }: DocumentCommandOptions) {
  const navigate = useNavigate()
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)

  const save = async () => {
    if (readOnly) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu phiếu ${saved.no}.`)
    if (isNew) navigate(`${listPath}/${saved.id}`, { replace: true })
  }

  const post = () =>
    modal.confirm({
      title: `Ghi sổ ${noun}?`,
      content: 'Ghi sổ thì cập nhật tồn kho theo phiếu. Sau khi ghi sổ không sửa được; muốn sửa phải bỏ ghi sổ khi kỳ kho còn mở.',
      okText: 'Ghi sổ',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        const saved = readOnly ? doc : await persist()
        if (!saved) return
        try {
          const posted = await mutations.post.mutateAsync(saved.id)
          message.success(`Đã ghi sổ phiếu ${posted.no}.`)
        } catch (e) {
          fail(e, 'Không ghi sổ được phiếu.')
        }
        if (isNew) navigate(`${listPath}/${saved.id}`, { replace: true })
      },
    })

  const unpost = () =>
    doc &&
    modal.confirm({
      title: `Bỏ ghi sổ phiếu ${doc.no}?`,
      content: 'Tồn kho trả lại như trước khi ghi sổ, phiếu về trạng thái nháp để sửa. Chỉ làm được khi kỳ kho của phiếu còn mở.',
      okText: 'Bỏ ghi sổ',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          await mutations.unpost.mutateAsync(doc.id)
          message.success(`Đã bỏ ghi sổ phiếu ${doc.no}.`)
        } catch (e) {
          fail(e, 'Không bỏ ghi sổ được phiếu.')
        }
      },
    })

  const cancel = () =>
    doc &&
    modal.confirm({
      title: `Hủy phiếu ${doc.no}?`,
      content: 'Phiếu đã hủy giữ lại số phiếu và lịch sử, không sửa hay dùng lại được.',
      okText: 'Hủy phiếu',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.cancel.mutateAsync(doc.id)
          message.success(`Đã hủy phiếu ${doc.no}.`)
        } catch (e) {
          fail(e, 'Không hủy được phiếu.')
        }
      },
    })

  const actions: ReactNode = (
    <>
      {!readOnly && (
        <Button type="primary" icon={<SaveOutlined />} onClick={save} loading={mutations.create.isPending || mutations.update.isPending}>
          Lưu nháp
        </Button>
      )}
      {(isNew ? !readOnly && hasPermission(Permission.InventoryDocumentPost) : doc?.canPost) && (
        <Button icon={<CheckCircleOutlined />} onClick={post} loading={mutations.post.isPending}>
          Ghi sổ
        </Button>
      )}
      {doc?.canUnpost && (
        <Button danger icon={<RollbackOutlined />} onClick={unpost} loading={mutations.unpost.isPending}>
          Bỏ ghi sổ
        </Button>
      )}
      {cancellable && !isNew && doc?.status === 'Draft' && doc.canEdit && (
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
