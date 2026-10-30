import { Alert, Checkbox, Select, TreeSelect } from 'antd'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import { useItemCategories } from '../../api/masterData'
import { useWarehouses } from '../../api/organization'
import { useStockOnHand, type StockOnHandRow } from '../../api/stock'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatQuantity } from '../../utils/format'
import { buildCategoryTree } from '../masterData/categoryTree'

const columns: GridColumn<StockOnHandRow>[] = [
  { key: 'warehouse', title: 'Kho', width: 160, value: (r) => `${r.warehouseCode} – ${r.warehouseName}`, groupable: true },
  { key: 'code', title: 'Mã hàng', width: 130, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên hàng', width: 240, value: (r) => r.itemName },
  { key: 'category', title: 'Nhóm hàng', width: 150, value: (r) => `${r.categoryCode} – ${r.categoryName}`, groupable: true },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.uomCode, groupable: true },
  { key: 'lot', title: 'Số lô', width: 100, value: (r) => r.lotNo },
  { key: 'qty', title: 'Tồn', width: 100, type: 'quantity', value: (r) => r.qty },
  { key: 'unitCost', title: 'Đơn giá BQ', width: 110, type: 'money', value: (r) => r.unitCost },
  { key: 'amount', title: 'Giá trị tồn', width: 130, type: 'money', value: (r) => r.amount },
  { key: 'min', title: 'Tồn tối thiểu', width: 100, type: 'quantity', value: (r) => r.minStock },
  {
    key: 'alert',
    title: 'Cảnh báo',
    width: 130,
    type: 'status',
    value: (r) => (r.qty < 0 ? 'Tồn âm' : r.belowMin ? 'Dưới mức tối thiểu' : 'Đủ'),
    tone: (r) => (r.qty < 0 ? 'error' : r.belowMin ? 'warning' : 'success'),
    groupable: true,
  },
]

/**
 * Màn hình Kho → Tồn kho hiện tại (design.md mục 11): tồn theo kho × mặt hàng × lô của kho, nhóm hàng được phép; giá trị theo sổ
 * kho (giá vốn tạm tính khi kỳ còn mở). Kho, nhóm hàng lọc ở server; ô tìm kiếm lọc tiếp (gõ có/không dấu). Bấm đúp mở thẻ kho.
 */
export function StockOnHandPage() {
  const navigate = useNavigate()
  const [warehouseId, setWarehouseId] = useState<string | undefined>()
  const [categoryId, setCategoryId] = useState<string | undefined>()
  const [includeZero, setIncludeZero] = useState(false)
  const stock = useStockOnHand({ warehouseId, categoryId, includeZero })
  const warehouses = useWarehouses()
  const categories = useItemCategories()
  const tree = useMemo(() => buildCategoryTree(categories.data ?? []), [categories.data])

  return (
    <ListPage
      title="Tồn kho hiện tại"
      exportFileName="ton-kho"
      canCreate={false}
      rows={stock.data?.items ?? []}
      columns={columns}
      rowKey={(r) => `${r.warehouseId}|${r.itemId}|${r.lotNo ?? ''}`}
      loading={stock.isLoading}
      onOpenRow={(r) => navigate(`/kho/the-kho?itemId=${r.itemId}&warehouseId=${r.warehouseId}`)}
      isMarked={(r) => r.belowMin || r.qty < 0}
      notice={
        stock.data?.truncated ? (
          <Alert
            type="warning"
            showIcon
            title={`Có hơn ${formatQuantity(stock.data.limit)} dòng tồn, đang hiện ${formatQuantity(stock.data.limit)} dòng đầu. Chọn kho hoặc nhóm hàng để thu hẹp.`}
          />
        ) : (
          <Alert type="info" showIcon title="Giá trị tồn theo giá vốn tạm tính khi kỳ còn mở; số chính thức chốt khi đóng kỳ. Bấm đúp một dòng để xem thẻ kho." />
        )
      }
      onClearFilters={() => {
        setWarehouseId(undefined)
        setCategoryId(undefined)
        setIncludeZero(false)
      }}
      filters={
        <>
          <FilterField label="Kho" width={200}>
            {(id) => (
              <Select
                id={id}
                value={warehouseId ?? ''}
                onChange={(v) => setWarehouseId(v === '' ? undefined : v)}
                options={[{ value: '', label: 'Tất cả kho được phép' }, ...(warehouses.data ?? []).map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))]}
              />
            )}
          </FilterField>
          <FilterField label="Nhóm hàng (gồm nhóm con)" width={220}>
            {(id) => (
              <TreeSelect
                id={id}
                value={categoryId}
                onChange={setCategoryId}
                treeData={tree}
                allowClear
                showSearch
                treeNodeFilterProp="title"
                placeholder="Tất cả"
              />
            )}
          </FilterField>
          <FilterField label="Dòng tồn 0" width={130}>
            {(id) => (
              <Checkbox id={id} checked={includeZero} onChange={(e) => setIncludeZero(e.target.checked)}>
                Hiện cả tồn 0
              </Checkbox>
            )}
          </FilterField>
        </>
      }
    />
  )
}
