import { Alert, App, Button, Form, Input, InputNumber } from 'antd'
import { DeleteOutlined } from '@ant-design/icons'
import { useState } from 'react'
import { useSupplierRatingMutations, useSupplierRatingRules, type SupplierRatingRule } from '../../api/supplierRating'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'
import { vnNumberInput } from '../../utils/format'

const columns: GridColumn<SupplierRatingRule>[] = [
  { key: 'order', title: 'Thứ tự xét', width: 90, type: 'quantity', value: (r) => r.sortOrder },
  { key: 'code', title: 'Mã hạng', width: 90, value: (r) => r.code },
  { key: 'name', title: 'Tên hạng', width: 200, value: (r) => r.name },
  { key: 'onTime', title: 'Đúng hạn tối thiểu (%)', width: 160, type: 'quantity', value: (r) => r.minOnTimePct },
  { key: 'return', title: 'Trả hàng tối đa (%)', width: 150, type: 'quantity', value: (r) => r.maxReturnPct },
]

/**
 * Hệ thống → Xếp loại nhà cung cấp (design.md mục 11.3): ngưỡng xếp hạng do người dùng tự cấu hình, dùng chung toàn tập đoàn, chỉ sửa khi
 * làm việc ở tổng công ty. Báo cáo Đánh giá NCC xét từ hạng có thứ tự nhỏ nhất; đạt cả hai ngưỡng thì nhận hạng đó.
 */
export function SupplierRatingPage() {
  const rules = useSupplierRatingRules()
  const { workingCompany, hasPermission } = useAuth()
  const canManage = workingCompany?.isHeadOffice === true && hasPermission(Permission.ParameterManage)
  // undefined = đóng; null = thêm mới; còn lại = hạng đang mở.
  const [open, setOpen] = useState<SupplierRatingRule | null | undefined>(undefined)
  const rows = rules.data ?? []

  return (
    <>
      <ListPage
        title="Xếp loại nhà cung cấp"
        exportFileName="xep-loai-ncc"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        loading={rules.isLoading}
        canCreate={canManage}
        onCreate={() => setOpen(null)}
        onOpenRow={setOpen}
        notice={
          <Alert
            type="info"
            showIcon
            title={
              rows.length === 0
                ? 'Chưa cấu hình hạng nào – báo cáo Đánh giá NCC chưa xếp loại. Thêm hạng từ tốt nhất (thứ tự 1) trở xuống.'
                : 'NCC nhận hạng đầu tiên (thứ tự nhỏ nhất) mà đạt cả hai ngưỡng; không đạt hạng nào là "Không đạt". Chỉ số không có dữ liệu thì không xét. Dùng chung toàn tập đoàn, chỉ sửa ở tổng công ty.'
            }
          />
        }
      />
      {open !== undefined && (
        <RatingDrawer rule={open} nextOrder={Math.max(0, ...rows.map((r) => r.sortOrder)) + 1} canManage={canManage} onClose={() => setOpen(undefined)} />
      )}
    </>
  )
}

interface RatingForm {
  code: string
  name: string
  sortOrder: number
  minOnTimePct: number
  maxReturnPct: number
}

/**
 * Ngăn kéo chi tiết một hạng.
 * @param rule Hạng đang mở; null = thêm mới.
 * @param nextOrder Thứ tự gợi ý khi thêm mới.
 * @param canManage Được sửa.
 * @param onClose Đóng.
 */
function RatingDrawer({ rule, nextOrder, canManage, onClose }: { rule: SupplierRatingRule | null; nextOrder: number; canManage: boolean; onClose: () => void }) {
  const { message, modal } = App.useApp()
  const [form] = Form.useForm<RatingForm>()
  const mutations = useSupplierRatingMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const save = async () => {
    const values = await form.validateFields().catch(() => null)
    if (!values) return
    setError(null)
    setSaving(true)
    try {
      const input = { ...values, code: rule?.code ?? values.code, name: values.name.trim() }
      if (rule) await mutations.update.mutateAsync({ id: rule.id, input })
      else await mutations.create.mutateAsync(input)
      message.success(`Đã lưu hạng ${input.code.toUpperCase()}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được hạng.')
    } finally {
      setSaving(false)
    }
  }

  const remove = () =>
    rule &&
    modal.confirm({
      title: `Xóa hạng ${rule.code}?`,
      content: 'Báo cáo Đánh giá NCC sẽ xếp loại lại theo các hạng còn lại. Chứng từ không bị ảnh hưởng.',
      okText: 'Xóa',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.remove.mutateAsync(rule.id)
          message.success(`Đã xóa hạng ${rule.code}.`)
          onClose()
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không xóa được hạng.')
        }
      },
    })

  const pct = (label: string, name: keyof RatingForm, extra: string) => (
    <Form.Item label={label} name={name} extra={extra} rules={[{ required: true, message: 'Hãy nhập tỷ lệ.' }]}>
      <InputNumber {...vnNumberInput} min={0} max={100} precision={2} style={{ width: '100%' }} />
    </Form.Item>
  )

  return (
    <DetailDrawer
      open
      title={rule ? `Hạng xếp loại: ${rule.code}` : 'Thêm hạng xếp loại'}
      onClose={onClose}
      onSave={canManage ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      readOnly={!canManage}
      extraActions={
        canManage && rule ? (
          <Button danger icon={<DeleteOutlined />} onClick={remove}>
            Xóa hạng
          </Button>
        ) : undefined
      }
      initialValues={rule ?? { code: '', name: '', sortOrder: nextOrder, minOnTimePct: 0, maxReturnPct: 100 }}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <div className="detail-grid">
              <Form.Item
                label="Mã hạng"
                name="code"
                extra={rule ? undefined : '1–10 chữ không dấu hoặc số, ví dụ A. Không đổi được sau khi tạo.'}
                rules={[
                  { required: true, message: 'Hãy nhập mã hạng.' },
                  { pattern: /^[A-Za-z0-9]{1,10}$/, message: 'Mã gồm 1–10 chữ không dấu hoặc số.' },
                ]}
                normalize={(v: string) => v.toUpperCase()}
              >
                <Input disabled={rule !== null} autoFocus={rule === null} autoComplete="off" />
              </Form.Item>
              <Form.Item label="Tên hạng" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên hạng.' }, { max: 50 }]}>
                <Input />
              </Form.Item>
              <Form.Item
                label="Thứ tự xét"
                name="sortOrder"
                extra="Số nhỏ là hạng tốt hơn, xét trước."
                rules={[{ required: true, message: 'Hãy nhập thứ tự.' }]}
              >
                <InputNumber min={1} max={99} precision={0} style={{ width: '100%' }} />
              </Form.Item>
              {pct('Đúng hạn tối thiểu (%)', 'minOnTimePct', 'Tỷ lệ dòng giao đúng hạn so với ngày giao trên PO phải từ mức này.')}
              {pct('Trả hàng tối đa (%)', 'maxReturnPct', 'Giá trị trả ÷ giá trị nhập không vượt mức này.')}
            </div>
          ),
        },
      ]}
    />
  )
}
