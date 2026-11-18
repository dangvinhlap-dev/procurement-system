import { useUserSignature } from '../../api/signatures'
import { ListPage } from '../../components/ListPage/ListPage'
import { SignatureCurrent } from '../../components/Signature/SignatureParts'
import { signatureColumns } from '../../components/Signature/signatureColumns'
import { useSignatureActions } from '../../components/Signature/useSignatureActions'

/**
 * Màn hình CKY – "Chữ ký của tôi" (lát IN1 – design.md mục 20.4, ui-style.md mục 5s): mỗi người tự tải chữ ký (ảnh chụp / scan trên giấy,
 * phần mềm tách nền, cắt sát, đổi màu mực) để in vào ô ký của phiếu mình gửi duyệt / duyệt. Khuôn `ListPage` + `DataGrid`: lưới là lịch sử
 * phiên bản; thông báo đầu trang là chữ ký đang dùng trên ô ký thử đúng cỡ in. Thay chữ ký mới chỉ có hiệu lực từ lúc thay – phiếu đã duyệt
 * trước đó vẫn in chữ ký cũ (người dùng chốt 18/11/2026).
 */
export function MySignaturePage() {
  const signature = useUserSignature()
  const { buttons, dialogs } = useSignatureActions(undefined, signature.data)
  return (
    <>
      <ListPage
        title="Chữ ký của tôi"
        exportFileName="chu-ky-cua-toi"
        rows={signature.data?.versions ?? []}
        columns={signatureColumns}
        rowKey={(r) => String(r.versionNo)}
        auditEntity="UserSignature"
        loading={signature.isLoading}
        canCreate={false}
        // Một tab duy nhất thay bộ tab mặc định Danh sách / Tổng hợp / Phân tích (như các màn hình QT – ui-style.md mục 5l).
        viewTabs={{ items: [{ key: 'list', label: 'Lịch sử chữ ký' }], activeKey: 'list', onChange: () => undefined }}
        notice={<SignatureCurrent signature={signature.data} />}
        actions={buttons}
      />
      {dialogs}
    </>
  )
}
