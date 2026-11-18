import { StopOutlined, UploadOutlined } from '@ant-design/icons'
import { App, Button, Input } from 'antd'
import { useState, type ReactNode } from 'react'
import { useSignatureMutations, type UserSignature } from '../../api/signatures'
import { signatureDisplayName } from './signatureColumns'
import { SignatureUploadDialog } from './SignatureUploadDialog'

/**
 * Nút "Tải chữ ký mới…", "Ngừng dùng chữ ký…" và hộp thoại đi kèm – dùng chung cho màn hình CKY (chữ ký của tôi) và tab CHỮ KÝ của ngăn kéo
 * người dùng (người quản trị tải thay: bắt buộc lý do, người được tải thay nhận thông báo).
 * @param userId Người dùng; undefined = chính tôi.
 * @param signature Dữ liệu chữ ký.
 * @returns Các nút và phần tử hộp thoại để đặt vào trang.
 */
export function useSignatureActions(userId: string | undefined, signature: UserSignature | undefined): { buttons: ReactNode; dialogs: ReactNode } {
  const { message, modal } = App.useApp()
  const { upload, retire } = useSignatureMutations(userId)
  const [open, setOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const forOther = signature !== undefined && !signature.isSelf

  const save = async (file: File, reason: string | undefined) => {
    setError(null)
    try {
      await upload.mutateAsync({ file, reason })
      setOpen(false)
      void message.success(forOther ? `Đã tải chữ ký mới cho ${signature?.fullName}.` : 'Đã lưu chữ ký mới.')
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được chữ ký.')
    }
  }

  const askRetire = () => {
    let reason = ''
    modal.confirm({
      title: forOther ? `Ngừng dùng chữ ký của ${signature?.fullName}?` : 'Ngừng dùng chữ ký của bạn?',
      content: (
        <div>
          <p>Phiếu duyệt sau lúc này in họ tên kèm "Đã duyệt điện tử" thay cho chữ ký. Phiếu đã duyệt trước đó vẫn in chữ ký cũ.</p>
          {forOther && <Input placeholder="Lý do (bắt buộc)" maxLength={500} onChange={(e) => (reason = e.target.value)} />}
        </div>
      ),
      okText: 'Ngừng dùng',
      cancelText: 'Không',
      okButtonProps: { danger: true, icon: <StopOutlined /> },
      onOk: async () => {
        if (forOther && !reason.trim()) {
          void message.warning('Nhập lý do ngừng dùng chữ ký.')
          throw new Error('Thiếu lý do')
        }
        try {
          await retire.mutateAsync(reason)
          void message.success('Đã ngừng dùng chữ ký.')
        } catch (e) {
          void message.error(e instanceof Error ? e.message : 'Không ngừng dùng được chữ ký.')
        }
      },
    })
  }

  const buttons = (
    <>
      <Button icon={<UploadOutlined />} onClick={() => (setError(null), setOpen(true))} disabled={!signature}>
        {forOther ? 'Tải chữ ký thay…' : 'Tải chữ ký mới…'}
      </Button>
      <Button
        icon={<StopOutlined />}
        onClick={askRetire}
        disabled={!signature?.currentVersionNo}
        title={signature?.currentVersionNo ? undefined : 'Chưa có chữ ký đang dùng'}
      >
        Ngừng dùng chữ ký…
      </Button>
    </>
  )
  const dialogs = open && (
    <SignatureUploadDialog
      open
      displayName={signatureDisplayName(signature)}
      requireReason={forOther}
      saving={upload.isPending}
      error={error}
      onCancel={() => setOpen(false)}
      onSave={(file, reason) => void save(file, reason)}
    />
  )
  return { buttons, dialogs }
}
