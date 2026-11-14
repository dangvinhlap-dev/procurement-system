import { Alert, App, Button, Descriptions, Input, Modal } from 'antd'
import { useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router'
import { useSupplierProfileActions, useSupplierProfileRequest, useSupplierProfileRequests } from '../../api/supplierProfileRequests'
import { Select } from '../../classic/Select'
import { DocNo } from '../../classic/DocNo'
import { AttachmentPanel } from '../../components/AttachmentPanel/AttachmentPanel'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { supplierProfileRequestTone } from '../../components/SupplierProfile/columns'
import { SupplierProfileCompare } from '../../components/SupplierProfile/SupplierProfileCompare'
import { supplierProfileRequestStatusLabels, type SupplierProfileRequest } from '../../portal/portalApi'
import { formatDateTime } from '../../utils/format'
import { SupplierProfile } from './SupplierProfile'

/** Đường dẫn màn hình; trang đầy đủ một đề nghị là `${SUPPLIER_PROFILE_REQUEST_PATH}/:id` (thông báo, ô số chứng từ). */
export const SUPPLIER_PROFILE_REQUEST_PATH = '/gia-ncc/de-nghi-cap-nhat-ncc'

const columns: GridColumn<SupplierProfileRequest>[] = [
  { key: 'no', title: 'Số đề nghị', width: 130, value: (r) => r.no, docRef: (r) => ({ docType: 'HSC', id: r.id }) },
  { key: 'supplier', title: 'Nhà cung cấp', width: 240, value: (r) => `${r.supplierCode} – ${r.supplierName}` },
  { key: 'summary', title: 'Nội dung đề nghị đổi', width: 260, value: (r) => r.summary },
  {
    key: 'status', title: 'Trạng thái', width: 150, type: 'status', value: (r) => supplierProfileRequestStatusLabels[r.status],
    tone: (r) => supplierProfileRequestTone[r.status], groupable: true,
  },
  { key: 'submittedAt', title: 'NCC gửi lúc', width: 130, type: 'datetime', value: (r) => r.submittedAt },
  { key: 'submittedBy', title: 'Người gửi (Portal)', width: 190, value: (r) => r.submittedByName ? `${r.submittedByName} – ${r.submittedByEmail}` : r.submittedByEmail },
  { key: 'reviewedBy', title: 'Người xử lý', width: 150, value: (r) => r.reviewedByName, groupable: true },
  { key: 'closedAt', title: 'Xử lý lúc', width: 130, type: 'datetime', value: (r) => r.closedAt },
  { key: 'bankChange', title: 'Yêu cầu duyệt tài khoản', width: 150, value: (r) => r.bankChangeRequestNo,
    docRef: (r) => (r.bankChangeRequestId ? { docType: 'NCC', id: r.bankChangeRequestId } : null) },
]

/**
 * Màn hình Giá và NCC → Đề nghị cập nhật hồ sơ NCC (lát P2a, design.md 19.5 P2-4): đề nghị quản trị NCC gửi trên Portal. Người sửa được NCC
 * (tổng công ty) xem so sánh rồi chấp nhận / từ chối; mặc định lọc đề nghị đang chờ. Bấm đúp mở ngăn kéo (`?mo=<id>`).
 */
export function SupplierProfileRequestPage() {
  const [status, setStatus] = useState<string>('Submitted')
  const requests = useSupplierProfileRequests(status || null)
  const [params, setParams] = useSearchParams()
  const openId = params.get('mo')
  const open = (id: string | null) => setParams(id ? { mo: id } : {}, { replace: true })
  return (
    <>
      <ListPage
        title="Đề nghị cập nhật hồ sơ NCC"
        exportFileName="de-nghi-cap-nhat-ho-so-ncc"
        canCreate={false}
        rows={requests.data ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        auditEntity="SupplierProfileRequest"
        loading={requests.isLoading}
        isMarked={(r) => r.canProcess}
        onOpenRow={(r) => open(r.id)}
        onClearFilters={() => setStatus('')}
        filters={
          <FilterField label="Trạng thái" width={200}>
            {(id) => (
              <Select
                id={id}
                value={status}
                onChange={(v) => setStatus(v ?? '')}
                options={[
                  { value: '', label: 'Tất cả' },
                  ...Object.entries(supplierProfileRequestStatusLabels).map(([value, label]) => ({ value, label })),
                ]}
              />
            )}
          </FilterField>
        }
        notice={
          <Alert type="info" showIcon
            title="NCC gửi đề nghị trên Portal. Chấp nhận: thông tin chung, người liên hệ áp ngay vào NCC; đổi tài khoản ngân hàng tự lập yêu cầu duyệt NCC (duyệt xong mới áp). Mã số thuế NCC không đề nghị được – Phòng CUVT sửa trên màn hình NCC." />
        }
      />
      {openId && <SupplierProfileRequestDrawer id={openId} onClose={() => open(null)} />}
    </>
  )
}

/** Trang đầy đủ một đề nghị (`/gia-ncc/de-nghi-cap-nhat-ncc/:id`) – mở từ thông báo, ô số chứng từ. */
export function SupplierProfileRequestDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  return <SupplierProfileRequestDrawer id={id} asPage onClose={() => navigate(SUPPLIER_PROFILE_REQUEST_PATH)} />
}

/**
 * Một đề nghị: thông tin chung, so sánh lúc gửi – đề nghị – hiện tại, hồ sơ NCC hiện tại, file NCC gửi kèm (chỉ xem); nút Chấp nhận / Từ chối.
 * @param id Id đề nghị.
 * @param asPage Vẽ thành trang đầy đủ thay cho ngăn kéo.
 * @param onClose Đóng / quay lại danh sách.
 */
function SupplierProfileRequestDrawer({ id, asPage = false, onClose }: { id: string; asPage?: boolean; onClose: () => void }) {
  const { message, modal } = App.useApp()
  const request = useSupplierProfileRequest(id)
  const actions = useSupplierProfileActions()
  const [error, setError] = useState<string | null>(null)
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const r = request.data

  const run = async (action: () => Promise<unknown>, done: string) => {
    setError(null)
    try {
      await action()
      void message.success(done)
      return true
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không thực hiện được.')
      return false
    }
  }

  const accept = () => {
    if (!r) return
    let comment = ''
    modal.confirm({
      title: `Chấp nhận đề nghị ${r.no}?`,
      content: (
        <>
          <p>
            Thông tin chung, người liên hệ được ghi vào NCC {r.supplierCode} ngay.
            {r.proposedBankAccounts && ' Tài khoản ngân hàng thành yêu cầu duyệt NCC – duyệt xong mới áp dụng.'} NCC nhận thư báo kết quả.
          </p>
          <Input.TextArea aria-label="Ý kiến" rows={3} maxLength={1000} placeholder="Ý kiến (không bắt buộc)"
            onChange={(e) => (comment = e.target.value)} />
        </>
      ),
      okText: 'Chấp nhận',
      cancelText: 'Không',
      onOk: () => run(() => actions.accept.mutateAsync({ id, comment: comment.trim() || null }), `Đã chấp nhận đề nghị ${r.no}.`),
    })
  }

  const buttons = r?.canProcess && (
    <>
      <Button type="primary" loading={actions.accept.isPending} onClick={accept}>
        Chấp nhận
      </Button>
      <Button danger onClick={() => setRejecting(true)}>
        Từ chối
      </Button>
    </>
  )

  return (
    <>
      <DetailDrawer
        open
        asPage={asPage}
        title={r ? `Đề nghị ${r.no} – ${supplierProfileRequestStatusLabels[r.status]}` : 'Đề nghị cập nhật hồ sơ NCC'}
        audit={{ entityType: 'SupplierProfileRequest', id: r?.id }}
        onClose={onClose}
        loading={request.isLoading}
        error={error}
        readOnly
        extraActions={buttons}
        tabs={[
          {
            key: 'general',
            label: 'Đề nghị',
            children: r && (
              <>
                <Descriptions column={1} size="small" bordered style={{ marginBottom: 8 }}>
                  <Descriptions.Item label="Nhà cung cấp">{`${r.supplierCode} – ${r.supplierName}`}</Descriptions.Item>
                  <Descriptions.Item label="NCC gửi">
                    {`${r.submittedByName ?? ''} (${r.submittedByEmail}) – ${formatDateTime(r.submittedAt)}`}
                  </Descriptions.Item>
                  <Descriptions.Item label="Ghi chú của NCC">{r.note ?? '—'}</Descriptions.Item>
                  {r.closedAt && (
                    <Descriptions.Item label={r.status === 'Withdrawn' ? 'NCC rút lúc' : 'Xử lý'}>
                      {`${r.reviewedByName ?? ''} ${formatDateTime(r.closedAt)}`.trim()}
                    </Descriptions.Item>
                  )}
                  {r.reviewComment && (
                    <Descriptions.Item label={r.status === 'Rejected' ? 'Lý do từ chối' : r.status === 'Withdrawn' ? 'Lý do rút' : 'Ý kiến'}>
                      {r.reviewComment}
                    </Descriptions.Item>
                  )}
                  {r.bankChangeRequestNo && (
                    <Descriptions.Item label="Yêu cầu duyệt tài khoản">
                      <DocNo no={r.bankChangeRequestNo} docType="NCC" id={r.bankChangeRequestId ?? undefined} />
                    </Descriptions.Item>
                  )}
                </Descriptions>
                <SupplierProfileCompare request={r} showCurrent />
              </>
            ),
          },
          { key: 'profile', label: 'Hồ sơ NCC', children: r && <SupplierProfile supplierId={r.supplierId} /> },
          {
            key: 'attachments',
            label: 'File NCC gửi',
            // File NCC gửi trên Portal: nội bộ chỉ xem, tải xuống (AttachmentService.RequireSupplierProfileFileAsync).
            children: r && <AttachmentPanel ownerType="SupplierProfileFile" ownerId={r.id} canManage={false} />,
          },
        ]}
      />
      <Modal
        title="Từ chối đề nghị"
        open={rejecting}
        okText="Từ chối"
        okButtonProps={{ danger: true, disabled: reason.trim() === '' }}
        cancelText="Hủy"
        confirmLoading={actions.reject.isPending}
        onCancel={() => setRejecting(false)}
        onOk={async () => {
          if (await run(() => actions.reject.mutateAsync({ id, comment: reason.trim() }), 'Đã từ chối đề nghị.')) {
            setRejecting(false)
            setReason('')
          }
        }}
        destroyOnHidden
      >
        <p>Lý do được gửi cho NCC qua email và hiện trên Portal để NCC bổ sung, gửi lại.</p>
        <Input.TextArea aria-label="Lý do từ chối" rows={3} maxLength={1000} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Modal>
    </>
  )
}
