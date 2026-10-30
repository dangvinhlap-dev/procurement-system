import { Alert, Select } from 'antd'
import { useMemo, useState } from 'react'
import {
  supplierStatusLabels,
  useSupplierGroups,
  useSuppliers,
  type SupplierListItem,
  type SupplierStatus,
} from '../../api/masterData'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatQuantity } from '../../utils/format'
import { SupplierDrawer } from './SupplierDrawer'

const statusTone: Record<SupplierStatus, StatusTone> = {
  Potential: 'warning',
  Approved: 'success',
  Suspended: 'neutral',
  Blacklisted: 'error',
}

const baseColumns: GridColumn<SupplierListItem>[] = [
  { key: 'code', title: 'Mã NCC', width: 110, value: (r) => r.code },
  { key: 'name', title: 'Tên nhà cung cấp', width: 260, value: (r) => r.name },
  { key: 'shortName', title: 'Tên viết tắt', width: 110, value: (r) => r.shortName },
  { key: 'group', title: 'Nhóm NCC', width: 110, value: (r) => r.groupName, groupable: true },
  { key: 'taxCode', title: 'Mã số thuế', width: 120, value: (r) => r.taxCode },
  { key: 'phone', title: 'Điện thoại', width: 120, value: (r) => r.phone },
  { key: 'contact', title: 'Liên hệ chính', width: 200, value: (r) => r.primaryContact },
  { key: 'rating', title: 'Xếp hạng', width: 80, value: (r) => (r.rating ? '★'.repeat(r.rating) : ''), groupable: true },
  { key: 'internal', title: 'Nội bộ', width: 80, value: (r) => r.internalCompanyCode, groupable: true },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 120,
    type: 'status',
    value: (r) => supplierStatusLabels[r.status],
    tone: (r) => statusTone[r.status],
    groupable: true,
  },
]

/**
 * Màn hình Danh mục nhà cung cấp (design.md mục 5.3, 5.8): NCC dùng chung toàn tập đoàn, kể cả NCC nội bộ (công ty thành viên).
 * Nhóm và trạng thái lọc ở server; ô tìm kiếm (có dấu hay không dấu) lọc tiếp trên các dòng đã tải.
 */
export function SupplierListPage() {
  const { workingCompany, hasPermission } = useAuth()
  const [groupId, setGroupId] = useState<string | undefined>()
  const [status, setStatus] = useState<SupplierStatus | undefined>()
  const [appliedOnly, setAppliedOnly] = useState(false)
  const suppliers = useSuppliers({ groupId, status, appliedOnly })
  const companyCode = workingCompany?.code ?? ''
  // Cột "áp dụng tại" đổi tiêu đề theo công ty đang làm việc – mỗi công ty tự quản lý NCC mình mua (design.md mục 5.9).
  const columns = useMemo<GridColumn<SupplierListItem>[]>(
    () => [
      ...baseColumns,
      { key: 'applied', title: `Mua tại ${companyCode}`, width: 90, value: (r) => (r.appliedAtCompany ? 'Đang mua' : ''), groupable: true },
      { key: 'localCode', title: `Mã tại ${companyCode}`, width: 100, value: (r) => r.localCode },
    ],
    [companyCode],
  )
  const groups = useSupplierGroups()
  const canManage = workingCompany?.isHeadOffice === true && hasPermission(Permission.SupplierManage)
  // undefined = đóng; null = thêm mới; chuỗi = Id NCC đang mở.
  const [openId, setOpenId] = useState<string | null | undefined>(undefined)

  const notice = suppliers.data?.truncated ? (
    <Alert
      type="warning"
      showIcon
      title={`Có hơn ${formatQuantity(suppliers.data.limit)} NCC khớp điều kiện, đang hiện ${formatQuantity(suppliers.data.limit)} mã đầu tiên. Chọn nhóm hoặc trạng thái để thu hẹp.`}
    />
  ) : (
    !workingCompany?.isHeadOffice && (
      <Alert type="info" showIcon title="Danh mục dùng chung toàn tập đoàn: chỉ thêm, sửa khi làm việc ở tổng công ty." />
    )
  )

  return (
    <>
      <ListPage
        title="Danh mục nhà cung cấp"
        exportFileName="nha-cung-cap"
        rows={suppliers.data?.items ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={suppliers.isLoading}
        canCreate={canManage}
        onCreate={() => setOpenId(null)}
        onOpenRow={(r) => setOpenId(r.id)}
        notice={notice}
        onClearFilters={() => {
          setGroupId(undefined)
          setStatus(undefined)
          setAppliedOnly(false)
        }}
        filters={
          <>
            <FilterField label="Phạm vi" width={200}>
              {(id) => (
                <Select
                  id={id}
                  value={appliedOnly ? 'applied' : 'all'}
                  onChange={(v) => setAppliedOnly(v === 'applied')}
                  options={[
                    { value: 'all', label: 'Mọi NCC của tập đoàn' },
                    { value: 'applied', label: `NCC ${companyCode} đang mua` },
                  ]}
                />
              )}
            </FilterField>
            <FilterField label="Nhóm NCC" width={180}>
              {(id) => (
                <Select
                  id={id}
                  value={groupId ?? ''}
                  onChange={(v) => setGroupId(v || undefined)}
                  options={[{ value: '', label: 'Tất cả' }, ...(groups.data ?? []).map((g) => ({ value: g.id, label: g.name }))]}
                />
              )}
            </FilterField>
            <FilterField label="Trạng thái" width={150}>
              {(id) => (
                <Select
                  id={id}
                  value={status ?? ''}
                  onChange={(v) => setStatus(v === '' ? undefined : (v as SupplierStatus))}
                  options={[{ value: '', label: 'Tất cả' }, ...Object.entries(supplierStatusLabels).map(([value, label]) => ({ value, label }))]}
                />
              )}
            </FilterField>
          </>
        }
      />
      {openId !== undefined && (
        <SupplierDrawer supplierId={openId} groups={groups.data ?? []} canManage={canManage} onClose={() => setOpenId(undefined)} />
      )}
    </>
  )
}
