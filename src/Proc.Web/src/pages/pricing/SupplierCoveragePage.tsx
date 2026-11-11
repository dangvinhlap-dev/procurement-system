import { Alert, Checkbox, InputNumber } from 'antd'
import { TreeSelect } from '../../classic/TreeSelect'
import { useMemo, useState } from 'react'
import { useItemCategories } from '../../api/masterData'
import { useSupplierCoverage, type SupplierCoverageRow, type SupplierCoverageStatus } from '../../api/reports'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { vnNumberInput } from '../../utils/format'
import { buildCategoryTree } from '../masterData/categoryTree'

/** Mức tối thiểu theo quyết định thành lập Ban cung ứng: mỗi nhóm hàng có ít nhất 03 NCC (đếm theo nhóm hàng – Q1, lát G1b). */
const DEFAULT_MIN = 3

const statusLabels: Record<SupplierCoverageStatus, string> = { Ok: 'Đủ', Short: 'Thiếu', SoleSource: 'Miễn – độc quyền' }
const statusTone: Record<SupplierCoverageStatus, StatusTone> = { Ok: 'success', Short: 'error', SoleSource: 'neutral' }

const columns: GridColumn<SupplierCoverageRow>[] = [
  { key: 'code', title: 'Mã nhóm', width: 110, value: (r) => r.categoryCode },
  { key: 'name', title: 'Nhóm hàng', width: 220, value: (r) => r.categoryName },
  { key: 'items', title: 'Số mặt hàng', width: 105, type: 'quantity', value: (r) => r.itemCount },
  { key: 'status', title: 'Tình trạng', width: 130, type: 'status', value: (r) => statusLabels[r.status], tone: (r) => statusTone[r.status], groupable: true },
  { key: 'approved', title: 'NCC đã duyệt', width: 100, type: 'quantity', value: (r) => r.approvedCount },
  { key: 'missing', title: 'Còn thiếu', width: 90, type: 'quantity', value: (r) => r.missing },
  { key: 'potential', title: 'NCC tiềm năng', width: 110, type: 'quantity', value: (r) => r.potentialCount },
  { key: 'suspended', title: 'NCC tạm ngưng', width: 115, type: 'quantity', value: (r) => r.suspendedCount },
  { key: 'suppliers', title: 'Các NCC đã duyệt (ưu tiên đứng đầu)', width: 320, value: (r) => r.approvedSuppliers },
  { key: 'soleItems', title: 'MH độc quyền', width: 100, type: 'quantity', value: (r) => r.soleSourceItemCount },
  { key: 'sole', title: 'Phiếu NCC độc quyền', width: 180, value: (r) => r.soleSourceNo ?? '', docRef: () => ({ docType: 'NDQ', id: null }) },
]

/**
 * Giá và NCC → NCC đủ điều kiện (SUPCOV, design.md mục 18.25): mỗi nhóm hàng có mặt hàng đang dùng đếm NCC Đã duyệt cung cấp nhóm – ngành
 * hàng là nhóm hoặc nhóm cha, hoặc có dòng NCC – sản phẩm của mặt hàng trong nhóm (Q1 08/11/2026 – lát G1b); ít hơn mức tối thiểu (mặc
 * định 3 theo quyết định Ban cung ứng) là thiếu – Văn phòng Ban tìm thêm NCC. Nhóm mà mọi mặt hàng có phiếu NCC độc quyền còn tác dụng ở
 * công ty đang làm việc thì miễn.
 */
export function SupplierCoveragePage() {
  const [minSuppliers, setMinSuppliers] = useState(DEFAULT_MIN)
  const [categoryId, setCategoryId] = useState<string>()
  const [onlyShort, setOnlyShort] = useState(true)
  const categories = useItemCategories()
  const tree = useMemo(() => buildCategoryTree(categories.data ?? []), [categories.data])
  const report = useSupplierCoverage(minSuppliers, categoryId, onlyShort)
  const data = report.data
  return (
    <ListPage
      title="NCC đủ điều kiện theo nhóm hàng"
      exportFileName="ncc-du-dieu-kien"
      exportSubtitle={`Tối thiểu ${minSuppliers} NCC đã duyệt mỗi nhóm hàng`}
      canCreate={false}
      rows={data?.categories ?? []}
      columns={columns}
      rowKey={(r) => r.categoryId}
      loading={report.isLoading}
      isMarked={(r) => r.status === 'Short'}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : (
          <Alert
            type={data && data.shortCount > 0 ? 'warning' : 'info'}
            showIcon
            title={`${(data?.shortCount ?? 0).toLocaleString('vi-VN')} / ${(data?.categoryCount ?? 0).toLocaleString('vi-VN')} nhóm hàng chưa đủ ${minSuppliers} NCC đã duyệt. Đếm theo nhóm hàng: NCC có ngành hàng là nhóm (hoặc nhóm cha) hoặc có dòng NCC – sản phẩm của mặt hàng trong nhóm; NCC tiềm năng là nguồn bổ sung chờ duyệt; nhóm mà mọi mặt hàng có phiếu NCC độc quyền còn tác dụng được miễn.`}
          />
        )
      }
      onClearFilters={() => {
        setMinSuppliers(DEFAULT_MIN)
        setCategoryId(undefined)
        setOnlyShort(true)
      }}
      filters={
        <>
          <FilterField label="Số NCC tối thiểu" width={130}>
            {(id) => (
              <InputNumber
                {...vnNumberInput}
                id={id}
                min={1}
                max={20}
                precision={0}
                value={minSuppliers}
                onChange={(v) => setMinSuppliers(typeof v === 'number' && v >= 1 ? v : DEFAULT_MIN)}
                style={{ width: '100%' }}
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
               
                placeholder="Tất cả"
              />
            )}
          </FilterField>
          <FilterField label="Phạm vi" width={160}>
            {(id) => (
              <div id={id} style={{ display: 'flex', alignItems: 'center', height: '100%' }}>
                <Checkbox checked={onlyShort} onChange={(e) => setOnlyShort(e.target.checked)}>
                  Chỉ nhóm hàng thiếu
                </Checkbox>
              </div>
            )}
          </FilterField>
        </>
      }
    />
  )
}

/** Lưới nhóm hàng NCC đủ điều kiện chỉ xem (cùng cột màn hình SUPCOV) – dùng ở báo cáo tháng cho Ban (lát G12). */
export function SupplierCoverageGrid({ rows }: { rows: SupplierCoverageRow[] }) {
  return <DataGrid rows={rows} columns={columns} rowKey={(r) => r.categoryId} groupBy={null} onGroupByChange={() => {}} />
}
