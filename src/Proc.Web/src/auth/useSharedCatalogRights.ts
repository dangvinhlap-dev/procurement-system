import { Permission } from './permissions'
import { useAuth } from './useAuth'

/**
 * Quyền trên danh mục dùng chung (design.md mục 5.7): thêm mới chỉ khi làm việc ở tổng công ty và có quyền quản lý danh mục; sửa thông tin
 * (trừ mã) khi đó, hoặc ở bất kỳ công ty nào với quyền "Sửa thông tin danh mục dùng chung". API luôn kiểm tra lại.
 * @param managePermission Quyền quản lý của danh mục, ví dụ Permission.ItemManage.
 * @returns canCreate – được thêm mới; canEdit – được sửa bản ghi có sẵn; atHeadOffice – đang làm việc ở tổng công ty.
 */
export function useSharedCatalogRights(managePermission: string) {
  const { workingCompany, hasPermission } = useAuth()
  const atHeadOffice = workingCompany?.isHeadOffice === true
  const canCreate = atHeadOffice && hasPermission(managePermission)
  const canEdit = canCreate || hasPermission(Permission.CatalogEditShared)
  return { canCreate, canEdit, atHeadOffice }
}
