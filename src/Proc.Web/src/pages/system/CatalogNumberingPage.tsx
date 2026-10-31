import { DeleteOutlined } from '@ant-design/icons'
import { Alert, App, Button, Form, Input, InputNumber, Select, TreeSelect } from 'antd'
import { useMemo, useState } from 'react'
import {
  numberedCatalogLabels,
  useCatalogNumberingMutations,
  useCatalogNumberings,
  type CatalogNumbering,
  type NumberedCatalog,
} from '../../api/catalogNumbering'
import { useItemCategories } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'
import { buildCategoryTree } from '../masterData/categoryTree'

const columns: GridColumn<CatalogNumbering>[] = [
  { key: 'catalog', title: 'Danh mục', width: 150, value: (r) => r.catalogName, groupable: true },
  { key: 'category', title: 'Nhóm hàng', width: 200, value: (r) => (r.catalog !== 'item' ? null : r.categoryCode ? `${r.categoryCode} – ${r.categoryName}` : 'Mặc định mọi nhóm') },
  { key: 'prefix', title: 'Tiền tố', width: 110, value: (r) => r.prefix },
  { key: 'digits', title: 'Số chữ số', width: 80, type: 'quantity', value: (r) => r.digits },
  { key: 'from', title: 'Từ số', width: 100, type: 'quantity', value: (r) => r.fromNumber },
  { key: 'to', title: 'Đến số', width: 110, type: 'quantity', value: (r) => r.toNumber },
  { key: 'sample', title: 'Ví dụ mã đầu dải', width: 150, value: (r) => r.sample },
  {
    key: 'next',
    title: 'Mã cấp kế tiếp',
    width: 150,
    type: 'status',
    value: (r) => r.nextCode ?? (r.prefix.includes('{NHOM}') && !r.categoryId ? 'Theo từng nhóm hàng' : 'Hết dải'),
    tone: (r) => (r.nextCode ? 'success' : r.prefix.includes('{NHOM}') && !r.categoryId ? 'neutral' : 'error'),
  },
]

/**
 * Hệ thống → Đánh số danh mục (design.md mục 5.11): dải mã tự cấp cho nhà cung cấp, nhóm NCC, nhóm hàng, hàng hóa – người dùng không tự đặt
 * mã khi thêm mới. Mã = tiền tố + số thứ tự đủ số chữ số; hàng hóa cấu hình riêng từng nhóm hoặc một dòng mặc định dùng {NHOM} (mã nhóm).
 * Dùng chung toàn tập đoàn, chỉ sửa ở tổng công ty.
 */
export function CatalogNumberingPage() {
  const rows = useCatalogNumberings()
  const { workingCompany, hasPermission } = useAuth()
  const canManage = workingCompany?.isHeadOffice === true && hasPermission(Permission.ParameterManage)
  // undefined = đóng; null = thêm mới; còn lại = dải đang mở.
  const [open, setOpen] = useState<CatalogNumbering | null | undefined>(undefined)

  return (
    <>
      <ListPage
        title="Đánh số danh mục"
        exportFileName="danh-so-danh-muc"
        rows={rows.data ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={rows.isLoading}
        canCreate={canManage}
        onCreate={() => setOpen(null)}
        onOpenRow={setOpen}
        notice={
          <Alert
            type="info"
            showIcon
            title="Mã nhà cung cấp, nhóm NCC, nhóm hàng, hàng hóa do hệ thống tự cấp: số lớn nhất đang có trong dải + 1. Danh mục chưa có dải thì không thêm mới được. Mã hàng dùng {NHOM} để thay bằng mã nhóm hàng."
          />
        }
      />
      {open !== undefined && <NumberingDrawer numbering={open} canManage={canManage} onClose={() => setOpen(undefined)} />}
    </>
  )
}

interface NumberingForm {
  catalog: NumberedCatalog
  categoryId: string | null
  prefix: string
  digits: number
  fromNumber: number
  toNumber: number
}

/**
 * Ngăn kéo chi tiết một dải.
 * @param numbering Dải đang mở; null = thêm mới.
 * @param canManage Được sửa.
 * @param onClose Đóng.
 */
function NumberingDrawer({ numbering, canManage, onClose }: { numbering: CatalogNumbering | null; canManage: boolean; onClose: () => void }) {
  const { message, modal } = App.useApp()
  const [form] = Form.useForm<NumberingForm>()
  const mutations = useCatalogNumberingMutations()
  const categories = useItemCategories()
  const tree = useMemo(() => buildCategoryTree(categories.data ?? []), [categories.data])
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const catalog = Form.useWatch('catalog', form)

  const save = async () => {
    const values = await form.validateFields().catch(() => null)
    if (!values) return
    setError(null)
    setSaving(true)
    try {
      const input = { ...values, prefix: values.prefix ?? '', categoryId: values.catalog === 'item' ? (values.categoryId ?? null) : null }
      if (numbering) await mutations.update.mutateAsync({ id: numbering.id, input })
      else await mutations.create.mutateAsync(input)
      message.success('Đã lưu dải đánh số.')
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được dải đánh số.')
    } finally {
      setSaving(false)
    }
  }

  const remove = () =>
    numbering &&
    modal.confirm({
      title: `Xóa dải đánh số ${numbering.catalogName}${numbering.categoryCode ? ` – nhóm ${numbering.categoryCode}` : ''}?`,
      content: 'Mã đã cấp không đổi. Danh mục (nhóm hàng) này sẽ không thêm mới được cho tới khi cấu hình lại dải.',
      okText: 'Xóa',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          await mutations.remove.mutateAsync(numbering.id)
          message.success('Đã xóa dải đánh số.')
          onClose()
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không xóa được dải đánh số.')
        }
      },
    })

  return (
    <DetailDrawer
      open
      title={numbering ? `Đánh số: ${numbering.catalogName}${numbering.categoryCode ? ` – ${numbering.categoryCode}` : ''}` : 'Thêm dải đánh số'}
      onClose={onClose}
      onSave={canManage ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      readOnly={!canManage}
      extraActions={
        canManage && numbering ? (
          <Button danger icon={<DeleteOutlined />} onClick={remove}>
            Xóa dải
          </Button>
        ) : undefined
      }
      initialValues={
        numbering
          ? { ...numbering }
          : { catalog: 'supplier', categoryId: null, prefix: '', digits: 5, fromNumber: 1, toNumber: 99999 }
      }
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <div className="detail-grid">
              <Form.Item label="Danh mục" name="catalog" rules={[{ required: true, message: 'Hãy chọn danh mục.' }]}>
                <Select disabled={numbering !== null} options={Object.entries(numberedCatalogLabels).map(([value, label]) => ({ value, label }))} />
              </Form.Item>
              {catalog === 'item' && (
                <Form.Item label="Nhóm hàng" name="categoryId" extra="Để trống = dải mặc định cho mọi nhóm chưa có dải riêng.">
                  <TreeSelect disabled={numbering !== null} treeData={tree} allowClear showSearch treeNodeFilterProp="title" placeholder="Mặc định mọi nhóm" />
                </Form.Item>
              )}
              <Form.Item
                label="Tiền tố"
                name="prefix"
                extra={catalog === 'item' ? 'Chữ không dấu, số, - . _ /; {NHOM} thay bằng mã nhóm hàng, ví dụ {NHOM}- → VT-00001.' : 'Chữ không dấu và số (NCC thêm được - . _ /).'}
                normalize={(v: string) => v.toUpperCase()}
                rules={[{ max: 20 }]}
              >
                <Input autoComplete="off" />
              </Form.Item>
              <Form.Item label="Số chữ số" name="digits" rules={[{ required: true, message: 'Hãy nhập số chữ số.' }]}>
                <InputNumber min={1} max={10} precision={0} style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item label="Từ số" name="fromNumber" rules={[{ required: true, message: 'Hãy nhập số đầu dải.' }]}>
                <InputNumber min={1} precision={0} style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item label="Đến số" name="toNumber" rules={[{ required: true, message: 'Hãy nhập số cuối dải.' }]}>
                <InputNumber min={1} precision={0} style={{ width: '100%' }} />
              </Form.Item>
            </div>
          ),
        },
      ]}
    />
  )
}
