import { useState } from 'react'
import { useSignatureImage, useUserSignature, type UserSignature } from '../../api/signatures'
import { formatDateTime } from '../../utils/format'
import { DataGrid } from '../DataGrid/DataGrid'
import { signatureColumns, signatureDisplayName } from './signatureColumns'
import { SignatureSample } from './SignatureUploadDialog'
import { useSignatureActions } from './useSignatureActions'

/**
 * Chữ ký đang dùng: ô ký thử đúng cỡ in + dòng tình trạng.
 * @param userId Người dùng; undefined = chính tôi.
 * @param signature Dữ liệu chữ ký.
 */
export function SignatureCurrent({ userId, signature }: { userId?: string; signature: UserSignature | undefined }) {
  const image = useSignatureImage(userId, signature?.currentVersionNo ?? null)
  const current = signature?.versions.find((v) => v.versionNo === signature.currentVersionNo)
  return (
    <div className="erp-signature-current" data-testid="signature-current">
      <SignatureSample url={image.data ?? null} name={signatureDisplayName(signature)} />
      <div className="erp-signature-current-text">
        {current ? (
          <>
            <div>
              <span className="erp-signature-light is-on" aria-hidden /> Đang dùng chữ ký phiên bản {current.versionNo} – từ{' '}
              {formatDateTime(current.validFrom)}
            </div>
            <div className="erp-signature-hint">
              Chữ ký in vào ô ký của phiếu bạn gửi duyệt / duyệt từ lúc này. Phiếu đã duyệt trước khi thay chữ ký vẫn in chữ ký cũ.
            </div>
          </>
        ) : (
          <>
            <div>
              <span className="erp-signature-light" aria-hidden /> Chưa có chữ ký đang dùng
            </div>
            <div className="erp-signature-hint">Phiếu đã duyệt in họ tên kèm "Đã duyệt điện tử" thay cho chữ ký.</div>
          </>
        )}
      </div>
    </div>
  )
}

/**
 * Tab CHỮ KÝ của ngăn kéo người dùng (người có quyền "Tải lên, thay chữ ký của người dùng khác"): chữ ký đang dùng, lịch sử phiên bản, nút
 * tải thay / ngừng dùng.
 * @param userId Người dùng của ngăn kéo.
 */
export function SignaturePanel({ userId }: { userId: string }) {
  const signature = useUserSignature(userId)
  const { buttons, dialogs } = useSignatureActions(userId, signature.data)
  const [groupBy, setGroupBy] = useState<string | null>(null)
  return (
    <div data-testid="signature-panel">
      {signature.data && !signature.data.isSelf && (
        <div className="erp-signature-warn">
          Bạn đang quản lý chữ ký của <b>{signature.data.fullName}</b>. Tải thay / ngừng dùng bắt buộc lý do, ghi nhật ký và gửi thông báo cho người này.
        </div>
      )}
      <SignatureCurrent userId={userId} signature={signature.data} />
      <div className="erp-signature-grid">
        <DataGrid
          rows={signature.data?.versions ?? []}
          columns={signatureColumns}
          rowKey={(r) => String(r.versionNo)}
          groupBy={groupBy}
          onGroupByChange={setGroupBy}
          loading={signature.isLoading}
          auditEntity="UserSignature"
        />
      </div>
      <div className="erp-signature-buttons">{buttons}</div>
      {dialogs}
    </div>
  )
}
