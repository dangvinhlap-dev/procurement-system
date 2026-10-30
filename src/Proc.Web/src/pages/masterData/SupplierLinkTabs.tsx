import { DeleteOutlined, PlusOutlined, SaveOutlined } from '@ant-design/icons'
import { Alert, App, Button, Checkbox, DatePicker, Form, Input, InputNumber, Modal, Select, Spin } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useState } from 'react'
import {
  supplierStatusLabels,
  useItem,
  useItemSearch,
  useItemSuppliers,
  useSaveSupplierCompanyLink,
  useSupplierCompanyLink,
  useSupplierItemMutations,
  useSupplierItems,
  useUoms,
  type SupplierItemRow,
} from '../../api/masterData'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn } from '../../components/DataGrid/types'

/** Khung cố định chiều cao cho lưới trong tab (DataGrid co giãn theo khung cha). Độ rộng cột cộng lại vừa ngăn kéo 720px. */
const gridBoxStyle = { height: 320, display: 'flex', flexDirection: 'column' } as const

/** Ô số kiểu Việt Nam: dấu phẩy thập phân (CLAUDE.md mục 5). */
const numberProps = { decimalSeparator: ',', style: { width: '100%' } } as const

/**
 * Tab "Áp dụng tại công ty" của ngăn kéo NCC (design.md mục 5.9): công ty đang làm việc khai báo có mua của NCC này không,
 * mã NCC tại công ty, ngày bắt đầu. Lưu riêng bằng nút trong tab vì đây là dữ liệu của công ty, không phải của NCC dùng chung –
 * công ty thành viên lưu được dù không sửa được thông tin NCC.
 * @param supplierId NCC.
 */
export function SupplierCompanyTab({ supplierId }: { supplierId: string }) {
  const { message } = App.useApp()
  const link = useSupplierCompanyLink(supplierId)
  const save = useSaveSupplierCompanyLink()
  const [form] = Form.useForm<{ localCode: string; startDate: Dayjs | null; isActive: boolean }>()
  const [error, setError] = useState<string | null>(null)
  const data = link.data

  useEffect(() => {
    if (data)
      form.setFieldsValue({
        localCode: data.localCode ?? '',
        startDate: data.startDate ? dayjs(data.startDate) : null,
        // Chưa khai báo thì mặc định "đang giao dịch" để bấm Lưu là áp dụng luôn.
        isActive: data.isApplied ? data.isActive : true,
      })
  }, [data, form])

  const submit = async () => {
    const values = await form.validateFields().catch(() => null)
    if (!values) return
    setError(null)
    try {
      await save.mutateAsync({
        supplierId,
        input: {
          localCode: values.localCode?.trim() || null,
          startDate: values.startDate ? values.startDate.format('YYYY-MM-DD') : null,
          isActive: values.isActive,
        },
      })
      message.success(`Đã lưu áp dụng tại ${data?.companyCode ?? ''}.`)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được.')
    }
  }

  return (
    <Spin spinning={link.isLoading}>
      {/* Form riêng, tự bật/tắt theo quyền tại công ty – không theo chế độ chỉ xem của ngăn kéo NCC. */}
      <Form form={form} layout="vertical" requiredMark={false} disabled={!data?.canEdit} component="div">
        {data && !data.isApplied && (
          <Alert type="info" showIcon className="detail-section" title={`${data.companyCode} chưa khai báo mua hàng của NCC này. Bấm "Lưu áp dụng" để thêm.`} />
        )}
        {error && <Alert type="error" showIcon className="detail-section" title={error} />}
        <div className="detail-grid">
          <Form.Item
            label="Mã NCC tại công ty"
            name="localCode"
            extra="Mã đối tượng của NCC trong phần mềm kế toán công ty; để trống nếu dùng mã chung."
            rules={[{ pattern: /^\s*[A-Za-z0-9][A-Za-z0-9._/-]{0,29}\s*$/, message: 'Tối đa 30 ký tự chữ không dấu, số, - . _ /.' }]}
            normalize={(v: string) => v.toUpperCase()}
          >
            <Input autoComplete="off" />
          </Form.Item>
          <Form.Item label="Ngày bắt đầu giao dịch" name="startDate">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
        </div>
        <Form.Item name="isActive" valuePropName="checked" extra="Bỏ chọn khi công ty ngừng mua của NCC này (vẫn giữ lịch sử).">
          <Checkbox>Đang giao dịch</Checkbox>
        </Form.Item>
        {data?.canEdit && (
          <Button icon={<SaveOutlined />} onClick={submit} loading={save.isPending}>
            Lưu áp dụng
          </Button>
        )}
      </Form>
    </Spin>
  )
}

const supplierItemColumns: GridColumn<SupplierItemRow>[] = [
  { key: 'item', title: 'Mã hàng', width: 100, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên hàng', width: 170, value: (r) => r.itemName },
  { key: 'code', title: 'Mã hàng của NCC', width: 110, value: (r) => r.supplierItemCode },
  { key: 'uom', title: 'ĐVT mua', width: 60, value: (r) => r.purchaseUomCode },
  { key: 'moq', title: 'MOQ', width: 70, type: 'quantity', value: (r) => r.moq },
  { key: 'lead', title: 'Giao (ngày)', width: 70, type: 'quantity', value: (r) => r.leadTimeDays },
  { key: 'preferred', title: 'Ưu tiên', width: 60, value: (r) => (r.isPreferred ? '✓' : '') },
]

/**
 * Tab "Sản phẩm/dịch vụ" của ngăn kéo NCC (design.md mục 5.9): mặt hàng NCC bán, mỗi dòng sửa riêng trong hộp thoại.
 * Chỉ thấy hàng trong phạm vi nhóm hàng; sửa ở tổng công ty.
 * @param supplierId NCC.
 * @param canManage Được sửa.
 */
export function SupplierItemsTab({ supplierId, canManage }: { supplierId: string; canManage: boolean }) {
  const rows = useSupplierItems(supplierId)
  // undefined = đóng; null = thêm mới; còn lại = dòng đang sửa.
  const [editing, setEditing] = useState<SupplierItemRow | null | undefined>(undefined)

  return (
    <>
      <div className="detail-hint" style={{ marginBottom: 6 }}>
        Bấm đúp một dòng để sửa. Mỗi mặt hàng có tối đa một NCC ưu tiên – chọn NCC này làm ưu tiên thì NCC ưu tiên cũ tự bỏ.
      </div>
      {canManage && (
        <Button icon={<PlusOutlined />} onClick={() => setEditing(null)} style={{ marginBottom: 6 }}>
          Thêm mặt hàng
        </Button>
      )}
      <div style={gridBoxStyle}>
        <DataGrid
          rows={rows.data ?? []}
          columns={supplierItemColumns}
          rowKey={(r) => r.id}
          groupBy={null}
          onGroupByChange={() => {}}
          onOpenRow={canManage ? setEditing : undefined}
          loading={rows.isLoading}
        />
      </div>
      {editing !== undefined && <SupplierItemDialog supplierId={supplierId} row={editing} onClose={() => setEditing(undefined)} />}
    </>
  )
}

interface SupplierItemFormValues {
  itemId: string | undefined
  supplierItemCode: string
  purchaseUomId: string | undefined
  moq: number | null
  leadTimeDays: number | null
  isPreferred: boolean
}

/**
 * Hộp thoại thêm/sửa một dòng NCC – sản phẩm. ĐVT mua chỉ chọn được ĐVT cơ sở và ĐVT quy đổi của mặt hàng.
 * @param supplierId NCC.
 * @param row Dòng đang sửa; null = thêm mới.
 * @param onClose Đóng.
 */
function SupplierItemDialog({ supplierId, row, onClose }: { supplierId: string; row: SupplierItemRow | null; onClose: () => void }) {
  const { message, modal } = App.useApp()
  const [form] = Form.useForm<SupplierItemFormValues>()
  const mutations = useSupplierItemMutations()
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const found = useItemSearch(search)
  const itemId = Form.useWatch('itemId', form) ?? row?.itemId ?? null
  const item = useItem(itemId)
  const uoms = useUoms()

  const itemOptions = row
    ? [{ value: row.itemId, label: `${row.itemCode} – ${row.itemName}` }]
    : (found.data?.items ?? []).map((i) => ({ value: i.id, label: `${i.code} – ${i.name}` }))
  const uomOptions = useMemo(() => {
    if (!item.data) return []
    const ids = [item.data.baseUomId, ...item.data.conversions.map((c) => c.uomId)]
    return ids.map((id) => {
      const u = uoms.data?.find((x) => x.id === id)
      const factor = item.data!.conversions.find((c) => c.uomId === id)?.factor
      return { value: id, label: u ? `${u.code} – ${u.name}${factor ? ` (= ${factor.toLocaleString('vi-VN')} ĐVT cơ sở)` : ' (ĐVT cơ sở)'}` : id }
    })
  }, [item.data, uoms.data])

  const submit = async () => {
    const values = await form.validateFields().catch(() => null)
    if (!values) return
    setError(null)
    const input = {
      itemId: values.itemId!,
      supplierItemCode: values.supplierItemCode?.trim() || null,
      purchaseUomId: values.purchaseUomId!,
      moq: values.moq ?? null,
      leadTimeDays: values.leadTimeDays ?? null,
      isPreferred: values.isPreferred ?? false,
    }
    try {
      if (row) await mutations.update.mutateAsync({ id: row.id, input })
      else await mutations.create.mutateAsync({ supplierId, input })
      message.success('Đã lưu mặt hàng của NCC.')
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được.')
    }
  }

  const remove = () =>
    row &&
    modal.confirm({
      title: `Xóa ${row.itemCode} khỏi danh sách hàng NCC bán?`,
      content: 'Nhật ký thao tác vẫn giữ thông tin cũ.',
      okText: 'Xóa',
      cancelText: 'Không',
      onOk: async () => {
        await mutations.remove.mutateAsync(row.id)
        message.success(`Đã xóa ${row.itemCode}.`)
        onClose()
      },
    })

  return (
    <Modal
      open
      title={row ? `Sửa mặt hàng ${row.itemCode}` : 'Thêm mặt hàng NCC bán'}
      onCancel={onClose}
      onOk={submit}
      okText="Lưu"
      cancelText="Đóng"
      confirmLoading={mutations.create.isPending || mutations.update.isPending}
      footer={(_, { OkBtn, CancelBtn }) => (
        <>
          {row && (
            <Button danger icon={<DeleteOutlined />} onClick={remove} style={{ float: 'left' }}>
              Xóa
            </Button>
          )}
          <CancelBtn />
          <OkBtn />
        </>
      )}
      destroyOnHidden
    >
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        requiredMark={false}
        disabled={false}
        initialValues={
          row
            ? { itemId: row.itemId, supplierItemCode: row.supplierItemCode ?? '', purchaseUomId: row.purchaseUomId, moq: row.moq, leadTimeDays: row.leadTimeDays, isPreferred: row.isPreferred }
            : { itemId: undefined, supplierItemCode: '', purchaseUomId: undefined, moq: null, leadTimeDays: null, isPreferred: false }
        }
      >
        <Form.Item label="Mặt hàng" name="itemId" rules={[{ required: true, message: 'Hãy chọn mặt hàng.' }]}>
          <Select
            showSearch={{ filterOption: false, onSearch: setSearch }}
            options={itemOptions}
            disabled={row !== null}
            placeholder="Gõ ít nhất 2 ký tự mã, tên hoặc mã vạch"
            notFoundContent={found.isFetching ? <Spin size="small" /> : search.trim().length < 2 ? 'Gõ để tìm' : 'Không có'}
            onChange={() => form.setFieldValue('purchaseUomId', undefined)}
          />
        </Form.Item>
        <div className="detail-grid">
          <Form.Item label="Mã hàng của NCC" name="supplierItemCode" rules={[{ max: 50 }]}>
            <Input autoComplete="off" />
          </Form.Item>
          <Form.Item label="ĐVT mua" name="purchaseUomId" rules={[{ required: true, message: 'Hãy chọn ĐVT mua.' }]}>
            <Select options={uomOptions} loading={item.isLoading} placeholder={itemId ? undefined : 'Chọn mặt hàng trước'} />
          </Form.Item>
          <Form.Item label="Số lượng đặt tối thiểu (MOQ)" name="moq" rules={[{ type: 'number', min: 0.0001, message: 'MOQ phải lớn hơn 0.' }]}>
            <InputNumber {...numberProps} precision={4} />
          </Form.Item>
          <Form.Item label="Thời gian giao hàng (ngày)" name="leadTimeDays">
            <InputNumber {...numberProps} min={0} max={365} precision={0} />
          </Form.Item>
        </div>
        <Form.Item name="isPreferred" valuePropName="checked">
          <Checkbox>NCC ưu tiên của mặt hàng này</Checkbox>
        </Form.Item>
      </Form>
    </Modal>
  )
}

const itemSupplierColumns: GridColumn<SupplierItemRow>[] = [
  { key: 'preferred', title: 'Ưu tiên', width: 60, value: (r) => (r.isPreferred ? '✓' : '') },
  { key: 'supplier', title: 'Mã NCC', width: 90, value: (r) => r.supplierCode },
  { key: 'name', title: 'Tên NCC', width: 150, value: (r) => r.supplierName },
  { key: 'status', title: 'Trạng thái NCC', width: 90, value: (r) => supplierStatusLabels[r.supplierStatus] },
  { key: 'code', title: 'Mã hàng của NCC', width: 100, value: (r) => r.supplierItemCode },
  { key: 'uom', title: 'ĐVT mua', width: 60, value: (r) => r.purchaseUomCode },
  { key: 'moq', title: 'MOQ', width: 70, type: 'quantity', value: (r) => r.moq },
  { key: 'lead', title: 'Giao (ngày)', width: 60, type: 'quantity', value: (r) => r.leadTimeDays },
]

/**
 * Tab "Nhà cung cấp" của ngăn kéo hàng hóa: các NCC đang bán mặt hàng này (chỉ xem; sửa ở ngăn kéo NCC).
 * @param itemId Mặt hàng.
 */
export function ItemSuppliersTab({ itemId }: { itemId: string }) {
  const rows = useItemSuppliers(itemId)
  return (
    <>
      <div className="detail-hint" style={{ marginBottom: 6 }}>NCC ưu tiên đứng đầu. Thêm, sửa ở màn hình Nhà cung cấp → tab Sản phẩm/dịch vụ.</div>
      <div style={gridBoxStyle}>
        <DataGrid rows={rows.data ?? []} columns={itemSupplierColumns} rowKey={(r) => r.id} groupBy={null} onGroupByChange={() => {}} loading={rows.isLoading} />
      </div>
    </>
  )
}
