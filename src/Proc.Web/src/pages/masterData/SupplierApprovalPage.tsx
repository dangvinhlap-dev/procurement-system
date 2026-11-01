import { Alert, App, Button, Descriptions, Input, Modal } from 'antd'
import { useState } from 'react'
import { useSearchParams } from 'react-router'
import {
  changeKindLabels,
  changeStatusLabels,
  useSupplierChangeActions,
  useSupplierChangeRequest,
  useSupplierChangeRequests,
  type SupplierChangeRequest,
  type SupplierChangeStatus,
} from '../../api/supplierApprovals'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime } from '../../utils/format'
import { ApprovalHistory } from '../purchasing/ApprovalHistory'

const statusTone: Record<SupplierChangeStatus, 'success' | 'warning' | 'error' | 'neutral'> = {
  Submitted: 'warning',
  Approved: 'success',
  Rejected: 'error',
  Cancelled: 'neutral',
}

const columns: GridColumn<SupplierChangeRequest>[] = [
  { key: 'no', title: 'Số yêu cầu', width: 130, value: (r) => r.no },
  { key: 'supplier', title: 'Nhà cung cấp', width: 240, value: (r) => `${r.supplierCode} – ${r.supplierName}` },
  { key: 'kind', title: 'Loại', width: 170, value: (r) => changeKindLabels[r.kind], groupable: true },
  { key: 'proposed', title: 'Đề nghị', width: 260, value: (r) => r.proposedSummary },
  {
    key: 'status', title: 'Trạng thái', width: 110, type: 'status', value: (r) => changeStatusLabels[r.status],
    tone: (r) => statusTone[r.status], groupable: true,
  },
  { key: 'waiting', title: 'Đang chờ công ty', width: 140, value: (r) => r.waitingFor },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
  { key: 'created', title: 'Lập lúc', width: 130, type: 'datetime', value: (r) => r.createdAt },
]

/**
 * Màn hình Giá và NCC → Duyệt NCC (design.md mục 18.6): yêu cầu đưa NCC vào Đã duyệt/Danh sách đen và đổi tài khoản ngân hàng. Duyệt
 * hai chặng: các công ty đang mua của NCC duyệt xong mới tới Tổng công ty duyệt lần cuối. Mở từ "Việc cần làm" bằng `?mo=<id>`.
 */
export function SupplierApprovalPage() {
  const requests = useSupplierChangeRequests()
  const [params, setParams] = useSearchParams()
  const openId = params.get('mo')
  const open = (id: string | null) => setParams(id ? { mo: id } : {}, { replace: true })
  return (
    <>
      <ListPage
        title="Yêu cầu duyệt nhà cung cấp"
        exportFileName="yeu-cau-duyet-ncc"
        canCreate={false}
        rows={requests.data ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={requests.isLoading}
        isMarked={(r) => r.canApprove}
        onOpenRow={(r) => open(r.id)}
        notice={
          <Alert type="info" showIcon
            title="Yêu cầu tự lập khi lưu NCC có đổi trạng thái sang Đã duyệt/Danh sách đen hoặc đổi tài khoản ngân hàng. Công ty liên quan duyệt xong mới chuyển Tổng công ty duyệt lần cuối." />
        }
      />
      {openId && <SupplierApprovalDrawer id={openId} onClose={() => open(null)} />}
    </>
  )
}

/**
 * Ngăn kéo một yêu cầu: so sánh hiện tại – đề nghị, lịch sử duyệt theo chặng/công ty, nút Duyệt / Từ chối / Rút yêu cầu.
 * @param id Id yêu cầu.
 * @param onClose Đóng.
 */
function SupplierApprovalDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const { message } = App.useApp()
  const request = useSupplierChangeRequest(id)
  const actions = useSupplierChangeActions()
  const [error, setError] = useState<string | null>(null)
  const [rejecting, setRejecting] = useState(false)
  const [reason, setReason] = useState('')
  const r = request.data

  const run = async (action: () => Promise<unknown>, done: string) => {
    setError(null)
    try {
      await action()
      message.success(done)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không thực hiện được.')
    }
  }

  const buttons = r && (
    <>
      {r.canApprove && (
        <Button type="primary" loading={actions.approve.isPending} onClick={() => run(() => actions.approve.mutateAsync({ id }), 'Đã duyệt.')}>
          Duyệt
        </Button>
      )}
      {r.canApprove && <Button danger onClick={() => setRejecting(true)}>Từ chối</Button>}
      {r.canCancel && (
        <Button loading={actions.cancel.isPending} onClick={() => run(() => actions.cancel.mutateAsync(id), 'Đã rút yêu cầu.')}>
          Rút yêu cầu
        </Button>
      )}
    </>
  )

  return (
    <>
      <DetailDrawer
        open
        title={r ? `Yêu cầu ${r.no} – ${changeStatusLabels[r.status]}` : 'Yêu cầu duyệt NCC'}
        onClose={onClose}
        loading={request.isLoading}
        error={error}
        readOnly
        extraActions={buttons}
        tabs={[
          {
            key: 'general',
            label: 'Thông tin chung',
            children: r && (
              <Descriptions column={1} size="small" bordered>
                <Descriptions.Item label="Nhà cung cấp">{`${r.supplierCode} – ${r.supplierName}`}</Descriptions.Item>
                <Descriptions.Item label="Loại">{changeKindLabels[r.kind]}</Descriptions.Item>
                <Descriptions.Item label="Hiện tại">{r.currentSummary}</Descriptions.Item>
                <Descriptions.Item label="Đề nghị">{r.proposedSummary}</Descriptions.Item>
                <Descriptions.Item label="Đang chờ">{r.waitingFor ?? '—'}</Descriptions.Item>
                <Descriptions.Item label="Người lập">{`${r.createdByName ?? ''} – ${formatDateTime(r.createdAt)}`}</Descriptions.Item>
                {r.rejectReason && <Descriptions.Item label="Lý do từ chối">{r.rejectReason}</Descriptions.Item>}
              </Descriptions>
            ),
          },
          { key: 'history', label: 'Lịch sử duyệt', children: r && <ApprovalHistory steps={r.approvals} byCompany /> },
        ]}
      />
      <Modal
        title="Từ chối yêu cầu"
        open={rejecting}
        okText="Từ chối"
        okButtonProps={{ danger: true, disabled: reason.trim() === '' }}
        cancelText="Hủy"
        confirmLoading={actions.reject.isPending}
        onCancel={() => setRejecting(false)}
        onOk={async () => {
          await run(() => actions.reject.mutateAsync({ id, comment: reason }), 'Đã từ chối yêu cầu.')
          setRejecting(false)
          setReason('')
        }}
        destroyOnHidden
      >
        <p>Từ chối ở một công ty là kết thúc cả yêu cầu; NCC giữ nguyên.</p>
        <Input.TextArea aria-label="Lý do từ chối" rows={3} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
      </Modal>
    </>
  )
}
