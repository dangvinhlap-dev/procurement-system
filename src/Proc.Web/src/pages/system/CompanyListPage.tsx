import { Alert, Select } from 'antd'
import { useMemo, useState } from 'react'
import { useCompanies, type CompanyListItem } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { CompanyDrawer } from './CompanyDrawer'

const columns: GridColumn<CompanyListItem>[] = [
  { key: 'code', title: 'Mã công ty', width: 90, value: (r) => r.code },
  { key: 'name', title: 'Tên công ty', width: 260, value: (r) => r.name },
  { key: 'shortName', title: 'Tên viết tắt', width: 120, value: (r) => r.shortName },
  { key: 'taxCode', title: 'Mã số thuế', width: 120, value: (r) => r.taxCode },
  { key: 'kind', title: 'Loại', width: 110, value: (r) => (r.isHeadOffice ? 'Tổng công ty' : 'Thành viên'), groupable: true },
  { key: 'users', title: 'Số người dùng', width: 100, type: 'quantity', value: (r) => r.userCount },
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
 * Màn hình Danh mục công ty (design.md mục 5.1, 5.6). Ở tổng công ty: thấy, thêm, sửa mọi công ty;
 * ở công ty thành viên: chỉ xem công ty mình (danh mục tổ chức do tổng công ty quản lý).
 */
export function CompanyListPage() {
  const { workingCompany, hasPermission } = useAuth()
  const companies = useCompanies()
  const [status, setStatus] = useState('')
  // undefined = đóng; null = thêm mới; chuỗi = Id công ty đang mở.
  const [openId, setOpenId] = useState<string | null | undefined>(undefined)
  const atHeadOffice = workingCompany?.isHeadOffice === true

  const rows = useMemo(
    () => (companies.data ?? []).filter((c) => !status || (status === 'active') === c.isActive),
    [companies.data, status],
  )

  return (
    <>
      <ListPage
        title="Danh mục công ty"
        exportFileName="cong-ty"
        rows={rows}
        columns={columns}
        rowKey={(r) => r.id}
        loading={companies.isLoading}
        canCreate={atHeadOffice && hasPermission(Permission.CompanyManage)}
        onCreate={() => setOpenId(null)}
        onOpenRow={(r) => setOpenId(r.id)}
        notice={
          !atHeadOffice && (
            <Alert
              type="info"
              showIcon
              title="Đang làm việc ở công ty thành viên nên chỉ xem được công ty mình. Thêm, sửa công ty khi làm việc ở tổng công ty."
            />
          )
        }
        onClearFilters={() => setStatus('')}
        filters={
          <FilterField label="Trạng thái" width={130}>
            {(id) => (
              <Select
                id={id}
                value={status}
                onChange={setStatus}
                options={[
                  { value: '', label: 'Tất cả' },
                  { value: 'active', label: 'Đang dùng' },
                  { value: 'inactive', label: 'Ngừng dùng' },
                ]}
              />
            )}
          </FilterField>
        }
      />
      {openId !== undefined && <CompanyDrawer companyId={openId} onClose={() => setOpenId(undefined)} />}
    </>
  )
}
