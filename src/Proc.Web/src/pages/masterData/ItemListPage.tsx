import { Alert, Select, TreeSelect } from 'antd'
import { useMemo, useState } from 'react'
import { itemTypeLabels, useItemCategories, useItems, type ItemListItem, type ItemType } from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatQuantity } from '../../utils/format'
import { buildCategoryTree } from './categoryTree'
import { useSharedCatalogRights } from '../../auth/useSharedCatalogRights'
import { SharedCatalogNotice } from '../../components/ListPage/SharedCatalogNotice'
import { ItemDrawer } from './ItemDrawer'

const vatLabel = (code: string | null) => (code === null ? '' : code === 'KCT' ? 'KCT' : `${code}%`)

const columns: GridColumn<ItemListItem>[] = [
  { key: 'code', title: 'Mã hàng', width: 130, value: (r) => r.code },
  { key: 'name', title: 'Tên hàng', width: 260, value: (r) => r.name },
  { key: 'type', title: 'Loại', width: 120, value: (r) => itemTypeLabels[r.type], groupable: true },
  { key: 'category', title: 'Nhóm hàng', width: 160, value: (r) => `${r.categoryCode} – ${r.categoryName}`, groupable: true },
  { key: 'uom', title: 'ĐVT', width: 60, value: (r) => r.baseUomCode, groupable: true },
  { key: 'spec', title: 'Quy cách', width: 120, value: (r) => r.specification },
  { key: 'brand', title: 'Thương hiệu', width: 110, value: (r) => r.brand, groupable: true },
  { key: 'origin', title: 'Xuất xứ', width: 100, value: (r) => r.origin, groupable: true },
  { key: 'vat', title: 'VAT', width: 60, value: (r) => vatLabel(r.vatCode), groupable: true },
  { key: 'barcode', title: 'Mã vạch', width: 120, value: (r) => r.barcode },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 110,
    type: 'status',
    value: (r) => (r.isActive ? 'Đang dùng' : 'Ngừng dùng'),
    tone: (r) => (r.isActive ? 'success' : 'neutral'),
    groupable: true,
  },
]

/**
 * Màn hình Danh mục hàng hóa (design.md mục 5.2, 5.7): một mã hàng dùng chung toàn tập đoàn để so sánh giá được.
 * Nhóm hàng và loại hàng lọc ở server (danh mục có thể tới ~100.000 mã, mỗi lần tải tối đa 5.000 dòng);
 * ô tìm kiếm lọc tiếp trên các dòng đã tải. Chỉ thấy hàng thuộc nhóm hàng được phép.
 */
export function ItemListPage() {
  const [categoryId, setCategoryId] = useState<string | undefined>()
  const [type, setType] = useState<ItemType | undefined>()
  const items = useItems({ categoryId, type })
  const categories = useItemCategories()
  const tree = useMemo(() => buildCategoryTree(categories.data ?? []), [categories.data])
  // Thêm mới ở tổng công ty; sửa thông tin (trừ mã) theo quyền – ngăn kéo đọc canEdit từ API (design.md mục 5.7).
  const rights = useSharedCatalogRights(Permission.ItemManage)
  const canManage = rights.canCreate
  // undefined = đóng; null = thêm mới; chuỗi = Id hàng đang mở.
  const [openId, setOpenId] = useState<string | null | undefined>(undefined)

  const notice = items.data?.truncated ? (
    <Alert
      type="warning"
      showIcon
      title={`Có hơn ${formatQuantity(items.data.limit)} mặt hàng khớp điều kiện, đang hiện ${formatQuantity(items.data.limit)} mã đầu tiên. Chọn nhóm hàng hoặc loại hàng để thu hẹp.`}
    />
  ) : (
    <SharedCatalogNotice rights={rights} />
  )

  return (
    <>
      <ListPage
        title="Danh mục hàng hóa"
        exportFileName="hang-hoa"
        excel={{ catalog: 'items', title: 'hàng hóa', canImport: canManage }}
        rows={items.data?.items ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={items.isLoading}
        canCreate={canManage}
        onCreate={() => setOpenId(null)}
        onOpenRow={(r) => setOpenId(r.id)}
        notice={notice}
        onClearFilters={() => {
          setCategoryId(undefined)
          setType(undefined)
        }}
        filters={
          <>
            <FilterField label="Nhóm hàng (gồm nhóm con)" width={240}>
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
            <FilterField label="Loại hàng" width={150}>
              {(id) => (
                <Select
                  id={id}
                  value={type ?? ''}
                  onChange={(v) => setType(v === '' ? undefined : (v as ItemType))}
                  options={[{ value: '', label: 'Tất cả' }, ...Object.entries(itemTypeLabels).map(([value, label]) => ({ value, label }))]}
                />
              )}
            </FilterField>
          </>
        }
      />
      {openId !== undefined && (
        <ItemDrawer itemId={openId} categories={categories.data ?? []} canManage={canManage} onClose={() => setOpenId(undefined)} />
      )}
    </>
  )
}
