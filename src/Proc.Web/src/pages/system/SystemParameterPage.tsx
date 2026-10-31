import { App, Button, Descriptions, Form, InputNumber } from 'antd'
import { useState } from 'react'
import { useSystemParameters, useUpdateSystemParameter, type SystemParameter } from '../../api/systemParameters'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'

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

/** Ngăn kéo xem/sửa một tham số. Hiện chỉ có tham số số nguyên nên ô nhập là InputNumber. */
function ParameterDrawer({ row, onClose }: { row: SystemParameter; onClose: () => void }) {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<{ value: number | null }>()
  const update = useUpdateSystemParameter()
  const [error, setError] = useState<string | null>(null)
  const canEdit = row.editable && hasPermission(Permission.ParameterManage)

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
      onClose={onClose}
      onSave={canEdit ? () => void form.validateFields().then((v) => save(v.value == null ? null : String(v.value))) : undefined}
      saving={update.isPending}
      error={error}
      form={form}
      initialValues={{ value: row.editable ? Number(row.value) : null }}
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
              {row.editable ? (
                <Form.Item
                  label={row.name}
                  name="value"
                  rules={[{ required: true, message: 'Hãy nhập giá trị, hoặc bấm "Về mặc định".' }]}
                  extra={row.defaultValue != null ? `Mặc định: ${row.defaultValue}` : undefined}
                >
                  <InputNumber autoFocus min={1} max={20} precision={0} style={{ width: 120 }} />
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
