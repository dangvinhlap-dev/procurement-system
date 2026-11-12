import { useSupplierPortalMutations, useSupplierPortalUsers } from '../../api/supplierPortal'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { PortalUserPanel } from '../../components/PortalUserPanel/PortalUserPanel'

/**
 * Tab "Tài khoản Portal" của ngăn kéo NCC (design.md 19.5, lát P1; Q11): Phòng CUVT mời người đại diện NCC làm quản trị NCC trên Portal,
 * gửi lại lời mời, khóa / mở khóa. Quản trị NCC tự mời người dùng con trên Portal.
 * @param supplierId NCC.
 * @param isInternal NCC nội bộ (công ty thành viên) – dùng phần mềm trực tiếp, không mời vào Portal.
 */
export function SupplierPortalTab({ supplierId, isInternal }: { supplierId: string; isInternal: boolean }) {
  const { hasPermission } = useAuth()
  const users = useSupplierPortalUsers(supplierId)
  const m = useSupplierPortalMutations(supplierId)
  return (
    <PortalUserPanel
      rows={users.data ?? []}
      loading={users.isFetching}
      canManage={hasPermission(Permission.SupplierPortal) && !isInternal}
      inviteLabel="Mời quản trị NCC…"
      auditEntity="SupplierPortalUser"
      inviteHint="Người được mời là quản trị NCC trên Portal: tự đặt mật khẩu qua link kích hoạt, tự mời thêm người dùng của NCC mình."
      onInvite={(input) => m.invite.mutateAsync(input)}
      onResend={(id) => m.resend.mutateAsync(id)}
      onDisable={(id) => m.disable.mutateAsync(id)}
      onEnable={(id) => m.enable.mutateAsync(id)}
    />
  )
}
