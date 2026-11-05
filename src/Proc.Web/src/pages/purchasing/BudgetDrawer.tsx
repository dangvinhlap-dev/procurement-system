import { App, Button, Form, InputNumber } from 'antd'
import { CopyOutlined } from '@ant-design/icons'
import { useState } from 'react'
import { useSaveBudget, type DepartmentBudgetYear } from '../../api/purchasing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { formatMoney, vnNumberInput } from '../../utils/format'

/** Giá trị form: m1…m12, null = tháng không kiểm soát ngân sách. */
type BudgetFormValues = Record<string, number | null>

const MONTHS = Array.from({ length: 12 }, (_, i) => i + 1)

/**
 * Ngăn kéo khai ngân sách 12 tháng của một bộ phận (design.md mục 18.24). Mỗi ô tháng kèm số đã dùng và còn lại (tính lúc mở).
 * Ô bỏ trống = tháng đó không kiểm soát ngân sách. PR/PO đã gửi giữ số liệu ngân sách lúc gửi; phiếu nháp tính lại khi lưu/gửi.
 * @param year Năm ngân sách.
 * @param row Bộ phận đang mở.
 * @param onClose Đóng ngăn kéo.
 */
export function BudgetDrawer({ year, row, onClose }: { year: number; row: DepartmentBudgetYear; onClose: () => void }) {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<BudgetFormValues>()
  const save = useSaveBudget()
  const [error, setError] = useState<string | null>(null)
  const editable = hasPermission(Permission.BudgetManage)

  const initialValues: BudgetFormValues = Object.fromEntries(row.months.map((m) => [`m${m.month}`, m.budget]))

  const submit = async () => {
    let values: BudgetFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ.')
      return
    }
    setError(null)
    try {
      await save.mutateAsync({ year, departmentId: row.departmentId, months: MONTHS.map((m) => values[`m${m}`] ?? null) })
      message.success(`Đã lưu ngân sách ${year} của bộ phận ${row.departmentCode}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được ngân sách.')
    }
  }

  /** Chép số của tháng 1 sang 11 tháng còn lại – khai nhanh ngân sách đều các tháng. */
  const copyFirstMonth = () => {
    const first = form.getFieldValue('m1') as number | null
    form.setFieldsValue(Object.fromEntries(MONTHS.slice(1).map((m) => [`m${m}`, first])))
  }

  return (
    <DetailDrawer
      open
      title={`Ngân sách ${year}: ${row.departmentCode} – ${row.departmentName}`}
      onClose={onClose}
      onSave={editable ? submit : undefined}
      saving={save.isPending}
      error={error}
      form={form}
      initialValues={initialValues}
      readOnly={!editable}
      extraActions={
        editable ? (
          <Button icon={<CopyOutlined />} onClick={copyFirstMonth} title="Chép ngân sách tháng 01 sang các tháng còn lại">
            Chép T01 cho cả năm
          </Button>
        ) : undefined
      }
      tabs={[
        {
          key: 'months',
          label: 'Ngân sách theo tháng',
          children: (
            <>
              {row.months.map((m) => (
                <Form.Item
                  key={m.month}
                  label={`Tháng ${String(m.month).padStart(2, '0')}/${year}`}
                  name={`m${m.month}`}
                  rules={[{ type: 'number', min: 0, message: 'Ngân sách không được âm.' }]}
                  extra={`Đã dùng ${formatMoney(m.used)}${m.remaining == null ? '' : ` – còn lại ${formatMoney(m.remaining)}`}`}
                >
                  <InputNumber {...vnNumberInput} min={0} precision={0} style={{ width: 200 }} placeholder="Không kiểm soát" />
                </Form.Item>
              ))}
            </>
          ),
        },
      ]}
    />
  )
}
