import { Alert, Checkbox, InputNumber } from 'antd'
import { TreeSelect } from '../../classic/TreeSelect'
import { useMemo, useState } from 'react'
import { useItemCategories } from '../../api/masterData'
import { useSupplierCoverage, type SupplierCoverageRow, type SupplierCoverageStatus } from '../../api/reports'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { vnNumberInput } from '../../utils/format'
import { buildCategoryTree } from '../masterData/categoryTree'

/** Mức tối thiểu theo quyết định thành lập Ban cung ứng: mỗi mặt hàng có ít nhất 03 NCC. */
const DEFAULT_MIN = 3

const statusLabels: Record<SupplierCoverageStatus, string> = { Ok: 'Đủ', Short: 'Thiếu', SoleSource: 'Miễn – độc quyền' }
const statusTone: Record<SupplierCoverageStatus, StatusTone> = { Ok: 'success', Short: 'error', SoleSource: 'neutral' }

const columns: GridColumn<SupplierCoverageRow>[] = [
  { key: 'code', title: 'Mã hàng', width: 120, value: (r) => r.itemCode },
  { key: 'name', title: 'Tên hàng', width: 240, value: (r) => r.itemName },
  { key: 'category', title: 'Nhóm hàng', width: 160, value: (r) => r.categoryName, groupable: true },
  { key: 'status', title: 'Tình trạng', width: 130, type: 'status', value: (r) => statusLabels[r.status], tone: (r) => statusTone[r.status], groupable: true },
  { key: 'approved', title: 'NCC đã duyệt', width: 100, type: 'quantity', value: (r) => r.approvedCount },
  { key: 'missing', title: 'Còn thiếu', width: 90, type: 'quantity', value: (r) => r.missing },
  { key: 'potential', title: 'NCC tiềm năng', width: 100, type: 'quantity', value: (r) => r.potentialCount },
  { key: 'suspended', title: 'NCC tạm ngưng', width: 100, type: 'quantity', value: (r) => r.suspendedCount },
  { key: 'suppliers', title: 'Các NCC đã duyệt (ưu tiên đứng đầu)', width: 320, value: (r) => r.approvedSuppliers },
  { key: 'sole', title: 'Phiếu NCC độc quyền', width: 150, value: (r) => r.soleSourceNo ?? '' },
]

/**
 * Giá và NCC → NCC đủ điều kiện (SUPCOV, design.md mục 18.25): mỗi mặt hàng đang dùng đếm NCC Đã duyệt có khai báo NCC – sản phẩm; ít hơn
 * mức tối thiểu (mặc định 3 theo quyết định Ban cung ứng) là thiếu – Văn phòng Ban tìm thêm NCC. Mặt hàng có phiếu NCC độc quyền còn tác
 * dụng ở công ty đang làm việc thì miễn.
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
      title="NCC đủ điều kiện theo mặt hàng"
      exportFileName="ncc-du-dieu-kien"
      exportSubtitle={`Tối thiểu ${minSuppliers} NCC đã duyệt mỗi mặt hàng`}
      canCreate={false}
      rows={data?.items ?? []}
      columns={columns}
      rowKey={(r) => r.itemId}
      loading={report.isLoading}
      isMarked={(r) => r.status === 'Short'}
      notice={
        report.error ? (
          <Alert type="error" showIcon title={report.error.message} />
        ) : (
          <Alert
            type={data && data.shortCount > 0 ? 'warning' : 'info'}
            showIcon
            title={`${(data?.shortCount ?? 0).toLocaleString('vi-VN')} / ${(data?.itemCount ?? 0).toLocaleString('vi-VN')} mặt hàng chưa đủ ${minSuppliers} NCC đã duyệt. Đếm theo danh mục NCC – sản phẩm; NCC tiềm năng là nguồn bổ sung chờ duyệt; mặt hàng có phiếu NCC độc quyền còn tác dụng được miễn.`}
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
                  Chỉ mặt hàng thiếu
                </Checkbox>
              </div>
            )}
          </FilterField>
        </>
      }
    />
  )
}
