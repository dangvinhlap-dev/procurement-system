import { Alert, Select, TreeSelect } from 'antd'
import { useMemo, useState } from 'react'
import { periodStatusLabels, useInventoryCalendar } from '../../api/inventory'
import { useItemCategories } from '../../api/masterData'
import { useWarehouses } from '../../api/organization'
import { useStockMovement, type StockMovementRow } from '../../api/stock'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { buildCategoryTree } from '../masterData/categoryTree'

const columns: GridColumn<StockMovementRow>[] = [
  { key: 'warehouse', title: 'Kho', width: 150, value: (r) => `${r.warehouseCode} – ${r.warehouseName}`, groupable: true },
  { key: 'code', title: 'Mã hàng', width: 120, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên hàng', width: 200, value: (r) => r.itemName },
  { key: 'category', title: 'Nhóm hàng', width: 140, value: (r) => `${r.categoryCode} – ${r.categoryName}`, groupable: true },
  { key: 'uom', title: 'ĐVT', width: 55, value: (r) => r.uomCode, groupable: true },
  { key: 'openQty', title: 'Tồn đầu SL', width: 90, type: 'quantity', value: (r) => r.openQty },
  { key: 'openAmount', title: 'Tồn đầu GT', width: 120, type: 'money', value: (r) => r.openAmount },
  { key: 'purchaseQty', title: 'Nhập mua SL', width: 90, type: 'quantity', value: (r) => r.purchaseQty },
  { key: 'purchaseAmount', title: 'Nhập mua GT', width: 120, type: 'money', value: (r) => r.purchaseAmount },
  { key: 'transferInQty', title: 'Chuyển đến SL', width: 95, type: 'quantity', value: (r) => r.transferInQty },
  { key: 'inQty', title: 'Tổng nhập SL', width: 95, type: 'quantity', value: (r) => r.inQty },
  { key: 'inAmount', title: 'Tổng nhập GT', width: 120, type: 'money', value: (r) => r.inAmount },
  { key: 'returnQty', title: 'Trả NCC SL', width: 85, type: 'quantity', value: (r) => r.returnQty },
  { key: 'transferOutQty', title: 'Chuyển đi SL', width: 90, type: 'quantity', value: (r) => r.transferOutQty },
  { key: 'outQty', title: 'Tổng xuất SL', width: 95, type: 'quantity', value: (r) => r.outQty },
  { key: 'outAmount', title: 'Tổng xuất GT', width: 120, type: 'money', value: (r) => r.outAmount },
  { key: 'closeQty', title: 'Tồn cuối SL', width: 90, type: 'quantity', value: (r) => r.closeQty },
  { key: 'closeAmount', title: 'Tồn cuối GT', width: 120, type: 'money', value: (r) => r.closeAmount },
]

/**
 * Màn hình Báo cáo → Nhập – xuất – tồn (design.md mục 11, 11.1): theo kỳ tháng, kho × mặt hàng; tồn đầu lấy số dư kỳ đã đóng.
 * Kỳ chưa đóng thì giá trị xuất chuyển kho là tạm tính – báo rõ ở đầu báo cáo. Mặc định kỳ tháng đang mở gần nhất.
 */
export function StockMovementReportPage() {
  const [year, setYear] = useState<number | undefined>()
  const [periodId, setPeriodId] = useState<string | undefined>()
  const [warehouseId, setWarehouseId] = useState<string | undefined>()
  const [categoryId, setCategoryId] = useState<string | undefined>()
  const calendar = useInventoryCalendar(year)
  const months = useMemo(() => (calendar.data?.periods ?? []).filter((p) => p.month > 0 && p.status !== 'NotOpened'), [calendar.data])
  // Chưa chọn kỳ: kỳ đang mở gần nhất, không có thì kỳ đã đóng gần nhất của năm đang xem.
  const selected = periodId ?? (months.filter((p) => p.status === 'Open').at(-1) ?? months.at(-1))?.id
  const report = useStockMovement({ periodId: selected, warehouseId, categoryId })
  const warehouses = useWarehouses()
  const categories = useItemCategories()
  const tree = useMemo(() => buildCategoryTree(categories.data ?? []), [categories.data])
  const data = report.data

  const notice = !selected ? (
    <Alert type="info" showIcon title="Chưa có kỳ kho nào đã mở trong năm đang xem." />
  ) : report.error ? (
    <Alert type="error" showIcon title={report.error.message} />
  ) : (
    data && (
      <Alert
        type={data.isFinal ? 'success' : 'warning'}
        showIcon
        title={
          data.isFinal
            ? `Kỳ ${data.periodLabel} đã đóng: giá trị theo giá vốn chính thức.`
            : `Kỳ ${data.periodLabel} ${periodStatusLabels[data.periodStatus].toLowerCase()}: giá trị xuất chuyển kho là tạm tính, số chính thức có sau khi đóng kỳ.`
        }
      />
    )
  )

  return (
    <ListPage
      title="Báo cáo nhập – xuất – tồn"
      exportFileName={`nxt-${data?.periodLabel.replace('/', '-') ?? ''}`}
      canCreate={false}
      rows={selected ? (data?.items ?? []) : []}
      columns={columns}
      rowKey={(r) => `${r.warehouseId}|${r.itemId}`}
      loading={report.isLoading && !!selected}
      notice={notice}
      onClearFilters={() => {
        setWarehouseId(undefined)
        setCategoryId(undefined)
      }}
      filters={
        <>
          <FilterField label="Năm" width={90}>
            {(id) => (
              <Select
                id={id}
                value={calendar.data?.year ?? undefined}
                onChange={(v) => {
                  setYear(v)
                  setPeriodId(undefined)
                }}
                options={(calendar.data?.years ?? []).map((y) => ({ value: y, label: String(y) }))}
              />
            )}
          </FilterField>
          <FilterField label="Kỳ" width={150}>
            {(id) => (
              <Select
                id={id}
                value={selected}
                onChange={setPeriodId}
                options={months.map((p) => ({ value: p.id, label: `${p.label} – ${periodStatusLabels[p.status]}` }))}
              />
            )}
          </FilterField>
          <FilterField label="Kho" width={190}>
            {(id) => (
              <Select
                id={id}
                value={warehouseId ?? ''}
                onChange={(v) => setWarehouseId(v === '' ? undefined : v)}
                options={[{ value: '', label: 'Tất cả kho được phép' }, ...(warehouses.data ?? []).map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))]}
              />
            )}
          </FilterField>
          <FilterField label="Nhóm hàng (gồm nhóm con)" width={210}>
            {(id) => (
              <TreeSelect id={id} value={categoryId} onChange={setCategoryId} treeData={tree} allowClear showSearch treeNodeFilterProp="title" placeholder="Tất cả" />
            )}
          </FilterField>
        </>
      }
    />
  )
}
