import { App, Button, Descriptions, Form, InputNumber, Switch } from 'antd'
import { useState } from 'react'
import { useSystemParameters, useUpdateSystemParameter, type SystemParameter } from '../../api/systemParameters'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'
import { vnNumberInput } from '../../utils/format'

const columns: GridColumn<SystemParameter>[] = [
  { key: 'group', title: 'Nhóm', width: 100, value: (r) => r.group, groupable: true },
  { key: 'name', title: 'Tham số', width: 240, value: (r) => r.name },
  { key: 'value', title: 'Giá trị', width: 260, value: (r) => r.value },
  {
    key: 'kind',
    title: 'Cấu hình',
    width: 100,
    type: 'status',
    value: (r) => (!r.editable ? 'Chỉ xem' : r.isDefault ? 'Mặc định' : 'Riêng'),
    tone: (r) => (!r.editable || r.isDefault ? 'neutral' : 'success'),
    groupable: true,
  },
  { key: 'note', title: 'Ghi chú', width: 420, value: (r) => r.note },
]

/**
 * Màn hình Tham số hệ thống (SEC – design.md mục 5.4 "Tham số công ty"): tham số của công ty đang làm việc.
 * Tham số sửa được mở ngăn kéo khi bấm đúp; tham số chỉ xem ghi rõ nơi khai báo.
 */
export function SystemParameterPage() {
  const { workingCompany } = useAuth()
  const parameters = useSystemParameters()
  const [openCode, setOpenCode] = useState<string | null>(null)
  const open = parameters.data?.find((r) => r.code === openCode)

  return (
    <>
      <ListPage
        title={`Tham số hệ thống – ${workingCompany?.code ?? ''}`}
        exportFileName="tham-so-he-thong"
        rows={parameters.data ?? []}
        columns={columns}
        rowKey={(r) => r.code}
        loading={parameters.isLoading}
        canCreate={false}
        onOpenRow={(r) => setOpenCode(r.code)}
      />
      {open && <ParameterDrawer row={open} onClose={() => setOpenCode(null)} />}
    </>
  )
}

/**
 * Giới hạn ô nhập theo tham số (khớp kiểm tra ở Proc.Domain.Organization.CompanyParameter): số NCC tối thiểu là số nguyên
 * 1–20; % markup nội bộ 0–1000, tối đa 4 số lẻ (cột numeric(9,4)).
 */
const inputLimits: Record<string, { min: number; max: number; precision: number; suffix?: string }> = {
  RFQ_MIN_SUPPLIERS: { min: 1, max: 20, precision: 0 },
  INTERNAL_MARKUP_PERCENT: { min: 0, max: 1000, precision: 4, suffix: '%' },
  // SLA mua hàng theo ngày làm việc (design.md mục 18.13).
  SLA_PRICED_DAYS: { min: 0, max: 60, precision: 0, suffix: 'ngày' },
  SLA_SOURCING_DAYS: { min: 0, max: 60, precision: 0, suffix: 'ngày' },
  SLA_URGENT_DAYS: { min: 0, max: 60, precision: 0, suffix: 'ngày' },
  // Mục tiêu KPI cung ứng (design.md mục 18.18): tỷ lệ 0–100%, vi phạm là số vụ nguyên.
  KPI_TARGET_SAVING: { min: 0, max: 100, precision: 4, suffix: '%' },
  KPI_TARGET_CONTRACTCOMPLIANCE: { min: 0, max: 100, precision: 4, suffix: '%' },
  KPI_TARGET_PURCHASESLA: { min: 0, max: 100, precision: 4, suffix: '%' },
  KPI_TARGET_ONTIMEDELIVERY: { min: 0, max: 100, precision: 4, suffix: '%' },
  KPI_TARGET_QUALITY: { min: 0, max: 100, precision: 4, suffix: '%' },
  KPI_TARGET_EMERGENCY: { min: 0, max: 100, precision: 4, suffix: '%' },
  KPI_TARGET_SLOWMOVING: { min: 0, max: 100, precision: 4, suffix: '%' },
  KPI_TARGET_SUPPLIERREVIEW: { min: 0, max: 100, precision: 4, suffix: '%' },
  KPI_TARGET_VIOLATION: { min: 0, max: 1000, precision: 0, suffix: 'vụ' },
  KPI_SLOW_MOVING_DAYS: { min: 1, max: 3650, precision: 0, suffix: 'ngày' },
}

/** Tham số bật/tắt (giá trị "Bật"/"Tắt" – design.md mục 18.14): ô nhập là công tắc, gửi "true"/"false". */
const toggleCodes = new Set(['RECEIPT_QC_REQUIRED', 'RECEIPT_SEPARATE_PO_CREATOR', 'RECEIPT_PO_REQUIRED'])

/** Ngăn kéo xem/sửa một tham số: tham số số dùng InputNumber, tham số bật/tắt dùng công tắc. */
function ParameterDrawer({ row, onClose }: { row: SystemParameter; onClose: () => void }) {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<{ value: number | boolean | null }>()
  const isToggle = toggleCodes.has(row.code)
  const update = useUpdateSystemParameter()
  const [error, setError] = useState<string | null>(null)
  const canEdit = row.editable && hasPermission(Permission.ParameterManage)
  const limits = inputLimits[row.code] ?? { min: 0, max: Number.MAX_SAFE_INTEGER, precision: 0 }

  const save = async (value: string | null) => {
    setError(null)
    try {
      const saved = await update.mutateAsync({ code: row.code, value })
      message.success(`Đã lưu "${saved.name}": ${saved.value}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được tham số.')
    }
  }

  return (
    <DetailDrawer
      open
      title={`Tham số: ${row.name}`}
      audit={row.auditField ? { entityType: 'CompanyParameter', id: row.auditId, fields: { value: row.auditField } } : undefined}
      onClose={onClose}
      onSave={canEdit ? () => void form.validateFields().then((v) => save(v.value == null ? null : String(v.value))) : undefined}
      saving={update.isPending}
      error={error}
      form={form}
      // Server trả số kiểu Việt Nam ("12,5") – đổi dấu phẩy về dấu chấm trước khi đọc thành số.
      initialValues={{ value: !row.editable ? null : isToggle ? row.value === 'Bật' : Number(row.value.replace(',', '.')) }}
      readOnly={!canEdit}
      extraActions={
        canEdit &&
        !row.isDefault && (
          <Button type="text" onClick={() => void save(null)}>
            Về mặc định
          </Button>
        )
      }
      tabs={[
        {
          key: 'value',
          label: 'Giá trị',
          children: (
            <>
              {row.editable && isToggle ? (
                <Form.Item label={row.name} name="value" valuePropName="checked" extra={`Mặc định: ${row.defaultValue ?? ''}`}>
                  <Switch checkedChildren="Bật" unCheckedChildren="Tắt" />
                </Form.Item>
              ) : row.editable ? (
                <Form.Item
                  label={row.name}
                  name="value"
                  rules={[{ required: true, message: 'Hãy nhập giá trị, hoặc bấm "Về mặc định".' }]}
                  extra={row.defaultValue != null ? `Mặc định: ${row.defaultValue}` : undefined}
                >
                  <InputNumber
                    {...vnNumberInput}
                    autoFocus
                    min={limits.min}
                    max={limits.max}
                    precision={limits.precision}
                    suffix={limits.suffix}
                    style={{ width: 160 }}
                  />
                </Form.Item>
              ) : (
                <Descriptions size="small" bordered column={1} items={[{ key: 'value', label: 'Giá trị', children: row.value }]} />
              )}
              <fieldset className="detail-section" style={{ marginTop: 8 }}>
                <legend>Giải thích</legend>
                <div className="detail-hint">{row.note}</div>
              </fieldset>
            </>
          ),
        },
      ]}
    />
  )
}
