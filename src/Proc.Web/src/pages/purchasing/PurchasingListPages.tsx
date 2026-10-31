import { Alert, App, Button, Form, InputNumber, Select } from 'antd'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useRoles } from '../../api/administration'
import type { DateRange } from '../../api/logs'
import {
  priorityLabels,
  purchaseStatusLabels,
  purchaseStatusTone,
  useApprovalRuleMutations,
  useApprovalRules,
  useDeliveryTracking,
  usePendingApprovals,
  usePurchaseDocuments,
  type ApprovalRule,
  type DeliveryTracking,
  type PendingApproval,
  type PurchaseKind,
  type PurchaseListItem,
  type PurchaseStatus,
} from '../../api/purchasing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { vnNumberInput } from '../../utils/format'
import { LogDateRangeFilter } from '../system/logFilters'

type Column = GridColumn<PurchaseListItem>

const statusColumn: Column = {
  key: 'status',
  title: 'Trạng thái',
  width: 110,
  type: 'status',
  value: (r) => purchaseStatusLabels[r.status],
  tone: (r) => purchaseStatusTone[r.status],
  groupable: true,
}

/** Cột theo loại phiếu: PR có bộ phận, mục đích, ưu tiên, % đã đặt; PO có NCC, kho nhận, % đã nhận. */
const columnsByKind: Record<PurchaseKind, Column[]> = {
  'purchase-requests': [
    { key: 'no', title: 'Số PR', width: 150, value: (r) => r.no },
    { key: 'date', title: 'Ngày', width: 90, type: 'date', value: (r) => r.date },
    { key: 'department', title: 'Bộ phận', width: 150, value: (r) => r.description, groupable: true },
    { key: 'purpose', title: 'Mục đích', width: 200, value: (r) => r.purpose },
    { key: 'needed', title: 'Ngày cần', width: 90, type: 'date', value: (r) => r.neededDate },
    { key: 'priority', title: 'Ưu tiên', width: 80, value: (r) => (r.priority ? priorityLabels[r.priority] : ''), groupable: true },
    { key: 'amount', title: 'Giá trị dự kiến', width: 130, type: 'money', value: (r) => r.amount },
    { key: 'progress', title: '% đã đặt', width: 80, type: 'quantity', value: (r) => r.progress },
    { key: 'creator', title: 'Người lập', width: 140, value: (r) => r.createdByName, groupable: true },
    statusColumn,
  ],
  'purchase-orders': [
    { key: 'no', title: 'Số PO', width: 150, value: (r) => r.no },
    { key: 'date', title: 'Ngày', width: 90, type: 'date', value: (r) => r.date },
    { key: 'supplier', title: 'Nhà cung cấp', width: 240, value: (r) => r.description, groupable: true },
    { key: 'warehouse', title: 'Kho nhận', width: 80, value: (r) => r.purpose, groupable: true },
    { key: 'expected', title: 'Ngày giao', width: 90, type: 'date', value: (r) => r.neededDate },
    { key: 'amount', title: 'Tổng thanh toán', width: 130, type: 'money', value: (r) => r.amount },
    { key: 'progress', title: '% đã nhận', width: 85, type: 'quantity', value: (r) => r.progress },
    { key: 'creator', title: 'Người lập', width: 140, value: (r) => r.createdByName, groupable: true },
    statusColumn,
  ],
}

/**
 * Danh sách PR hoặc PO (design.md mục 10.2, 10.3) trong khoảng ngày (mặc định 30 ngày) và trạng thái, lọc ở server.
 * @param kind Loại phiếu.
 * @param title Tiêu đề màn hình.
 * @param path Đường dẫn màn hình (trang phiếu là path/:id).
 * @param exportFileName Tên file CSV.
 */
export function PurchaseListPage({ kind, title, path, exportFileName }: { kind: PurchaseKind; title: string; path: string; exportFileName: string }) {
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const [range, setRange] = useState<DateRange>(() => ({ from: dayjs().subtract(29, 'day').format('YYYY-MM-DD'), to: dayjs().format('YYYY-MM-DD') }))
  const [status, setStatus] = useState<PurchaseStatus | undefined>()
  const documents = usePurchaseDocuments(kind, { ...range, status })
  return (
    <ListPage
      title={title}
      exportFileName={exportFileName}
      rows={documents.data?.items ?? []}
      columns={columnsByKind[kind]}
      rowKey={(r) => r.id}
      loading={documents.isLoading}
      canCreate={hasPermission(kind === 'purchase-requests' ? Permission.PrCreate : Permission.PoCreate)}
      onCreate={() => navigate(`${path}/moi`)}
      onOpenRow={(r) => navigate(`${path}/${r.id}`)}
      onClearFilters={() => setStatus(undefined)}
      notice={documents.data?.truncated && <Alert type="warning" showIcon title="Còn phiếu chưa hiện – thu hẹp khoảng ngày." />}
      filters={
        <>
          <LogDateRangeFilter value={range} onChange={setRange} />
          <FilterField label="Trạng thái" width={130}>
            {(id) => (
              <Select id={id} value={status ?? ''} onChange={(v) => setStatus(v === '' ? undefined : (v as PurchaseStatus))}
                options={[{ value: '', label: 'Tất cả' }, ...Object.entries(purchaseStatusLabels).map(([value, label]) => ({ value, label }))]} />
            )}
          </FilterField>
        </>
      }
    />
  )
}

const pendingColumns: GridColumn<PendingApproval>[] = [
  { key: 'type', title: 'Loại', width: 120, value: (r) => (r.docType === 'PR' ? 'Đề nghị mua hàng' : 'Đơn mua hàng'), groupable: true },
  { key: 'no', title: 'Số phiếu', width: 150, value: (r) => r.no },
  { key: 'date', title: 'Ngày', width: 90, type: 'date', value: (r) => r.date },
  { key: 'description', title: 'Bộ phận / NCC', width: 240, value: (r) => r.description },
  { key: 'amount', title: 'Giá trị', width: 130, type: 'money', value: (r) => r.amount },
  { key: 'level', title: 'Cấp chờ duyệt', width: 100, type: 'quantity', value: (r) => r.level },
  { key: 'creator', title: 'Người lập', width: 150, value: (r) => r.createdByName, groupable: true },
]

/** Màn hình Việc cần làm: PR, PO đang chờ chính tôi duyệt ở công ty đang làm việc (design.md mục 6.7). Bấm đúp mở phiếu để duyệt. */
export function PendingApprovalsPage() {
  const navigate = useNavigate()
  const pending = usePendingApprovals()
  return (
    <ListPage
      title="Việc cần làm – phiếu chờ tôi duyệt"
      exportFileName="viec-can-lam"
      canCreate={false}
      rows={pending.data ?? []}
      columns={pendingColumns}
      rowKey={(r) => r.documentId}
      loading={pending.isLoading}
      onOpenRow={(r) => navigate(`${r.docType === 'PR' ? '/mua-hang/pr' : '/mua-hang/po'}/${r.documentId}`)}
      notice={<Alert type="info" showIcon title="Phiếu đang chờ cấp duyệt của bạn. Bấm đúp để mở và Duyệt / Từ chối." />}
    />
  )
}

const trackingColumns: GridColumn<DeliveryTracking>[] = [
  {
    key: 'state',
    title: 'Tình trạng',
    width: 100,
    type: 'status',
    value: (r) => (r.overdue ? 'Quá hạn' : r.receivedQty > 0 ? 'Đang giao' : 'Chờ giao'),
    tone: (r) => (r.overdue ? 'error' : r.receivedQty > 0 ? 'warning' : 'neutral'),
    groupable: true,
  },
  { key: 'no', title: 'Số PO', width: 150, value: (r) => r.orderNo },
  { key: 'supplier', title: 'Nhà cung cấp', width: 220, value: (r) => `${r.supplierCode} – ${r.supplierName}`, groupable: true },
  { key: 'item', title: 'Mặt hàng', width: 240, value: (r) => `${r.itemCode} – ${r.itemName}` },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uomCode },
  { key: 'qty', title: 'SL đặt', width: 90, type: 'quantity', value: (r) => r.qty },
  { key: 'received', title: 'Đã nhận', width: 90, type: 'quantity', value: (r) => r.receivedQty },
  { key: 'remaining', title: 'Còn lại', width: 90, type: 'quantity', value: (r) => r.remainingQty },
  { key: 'due', title: 'Hạn giao', width: 90, type: 'date', value: (r) => r.dueDate },
]

/** Màn hình Theo dõi giao hàng: dòng PO đã duyệt còn chưa nhận đủ, hạn giao, quá hạn (design.md mục 6.7). Bấm đúp mở PO. */
export function DeliveryTrackingPage() {
  const navigate = useNavigate()
  const tracking = useDeliveryTracking()
  return (
    <ListPage
      title="Theo dõi giao hàng"
      exportFileName="theo-doi-giao-hang"
      canCreate={false}
      rows={tracking.data ?? []}
      columns={trackingColumns}
      rowKey={(r) => r.lineId}
      loading={tracking.isLoading}
      isMarked={(r) => r.overdue}
      onOpenRow={(r) => navigate(`/mua-hang/po/${r.orderId}`)}
    />
  )
}

const ruleColumns: GridColumn<ApprovalRule>[] = [
  { key: 'type', title: 'Loại phiếu', width: 160, value: (r) => (r.docType === 'PR' ? 'Đề nghị mua hàng (PR)' : 'Đơn mua hàng (PO)'), groupable: true },
  { key: 'level', title: 'Cấp', width: 60, type: 'quantity', value: (r) => r.level },
  { key: 'min', title: 'Từ giá trị', width: 140, type: 'money', value: (r) => r.minAmount },
  { key: 'role', title: 'Vai trò duyệt', width: 240, value: (r) => `${r.roleCode} – ${r.roleName}`, groupable: true },
]

/**
 * Màn hình Hệ thống → Quy trình duyệt (design.md mục 6.7): các cấp duyệt PR, PO của công ty đang làm việc – cấp, ngưỡng giá trị, vai
 * trò. Chưa có cấp nào cho một loại phiếu thì ai có quyền duyệt cũng duyệt được (một cấp).
 */
export function ApprovalRuleListPage() {
  const { workingCompany, hasPermission } = useAuth()
  const rules = useApprovalRules()
  const canManage = hasPermission(Permission.ParameterManage)
  const [open, setOpen] = useState<ApprovalRule | null | undefined>(undefined)
  return (
    <>
      <ListPage
        title={`Quy trình duyệt – ${workingCompany?.code ?? ''}`}
        exportFileName="quy-trinh-duyet"
        rows={rules.data ?? []}
        columns={ruleColumns}
        rowKey={(r) => r.id}
        loading={rules.isLoading}
        canCreate={canManage}
        onCreate={() => setOpen(null)}
        onOpenRow={setOpen}
        notice={
          <Alert type="info" showIcon
            title="Phiếu phải qua mọi cấp có ngưỡng ≤ tổng giá trị, duyệt từ cấp thấp lên. Loại phiếu chưa có cấp nào: ai có quyền duyệt cũng duyệt được." />
        }
      />
      {open !== undefined && <ApprovalRuleDrawer rule={open} canManage={canManage} onClose={() => setOpen(undefined)} />}
    </>
  )
}

/**
 * Ngăn kéo thêm/sửa/xóa một cấp duyệt.
 * @param rule Cấp đang mở; null = thêm mới.
 * @param canManage Có quyền sửa.
 * @param onClose Đóng.
 */
function ApprovalRuleDrawer({ rule, canManage, onClose }: { rule: ApprovalRule | null; canManage: boolean; onClose: () => void }) {
  const { message, modal } = App.useApp()
  const [form] = Form.useForm<{ docType: string; level: number; minAmount: number; roleId?: string }>()
  const mutations = useApprovalRuleMutations()
  const roles = useRoles()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const save = async () => {
    const values = await form.validateFields().catch(() => null)
    if (!values) return
    setError(null)
    setSaving(true)
    const input = { docType: values.docType, level: values.level, minAmount: values.minAmount ?? 0, roleId: values.roleId! }
    try {
      if (rule) await mutations.update.mutateAsync({ id: rule.id, input })
      else await mutations.create.mutateAsync(input)
      message.success('Đã lưu cấp duyệt.')
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được cấp duyệt.')
    } finally {
      setSaving(false)
    }
  }

  const remove = () =>
    rule &&
    modal.confirm({
      title: `Xóa cấp ${rule.level} của ${rule.docType}?`,
      content: 'Phiếu đang chờ duyệt giữ các bước đã sinh; cấu hình mới áp cho lần gửi duyệt sau.',
      okText: 'Xóa',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.remove.mutateAsync(rule.id)
          message.success('Đã xóa cấp duyệt.')
          onClose()
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không xóa được.')
        }
      },
    })

  return (
    <DetailDrawer
      open
      title={rule ? `Cấp duyệt ${rule.docType} – cấp ${rule.level}` : 'Thêm cấp duyệt'}
      onClose={onClose}
      onSave={canManage ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      readOnly={!canManage}
      initialValues={rule ? { docType: rule.docType, level: rule.level, minAmount: rule.minAmount, roleId: rule.roleId } : { docType: 'PR', level: 1, minAmount: 0 }}
      extraActions={canManage && rule ? <Button danger onClick={remove}>Xóa cấp này</Button> : undefined}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <div className="detail-grid">
              <Form.Item label="Loại phiếu" name="docType">
                <Select disabled={rule !== null} options={[{ value: 'PR', label: 'Đề nghị mua hàng (PR)' }, { value: 'PO', label: 'Đơn mua hàng (PO)' }]} />
              </Form.Item>
              <Form.Item label="Cấp" name="level" rules={[{ required: true, type: 'number', min: 1, max: 9, message: 'Cấp từ 1 đến 9.' }]}>
                <InputNumber style={{ width: '100%' }} min={1} max={9} precision={0} />
              </Form.Item>
              <Form.Item label="Từ giá trị (đ)" name="minAmount" extra="Phiếu có tổng giá trị từ số này trở lên mới phải qua cấp này.">
                <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} />
              </Form.Item>
              <Form.Item label="Vai trò duyệt" name="roleId" rules={[{ required: true, message: 'Chọn vai trò.' }]}
                extra="Người có vai trò này tại công ty và có quyền duyệt loại phiếu.">
                <Select showSearch={{ optionFilterProp: 'label' }} options={(roles.data ?? []).map((r) => ({ value: r.id, label: `${r.code} – ${r.name}` }))} />
              </Form.Item>
            </div>
          ),
        },
      ]}
    />
  )
}
