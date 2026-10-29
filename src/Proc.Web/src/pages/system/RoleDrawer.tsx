import { Alert, App, Checkbox, Form, Input } from 'antd'
import { useEffect, useState } from 'react'
import { usePermissionCatalog, useRole, useRoleMutations, type PermissionGroup } from '../../api/administration'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'

interface RoleFormValues {
  code: string
  name: string
  isActive: boolean
  permissionCodes: string[]
}

const emptyValues: RoleFormValues = { code: '', name: '', isActive: true, permissionCodes: [] }

/**
 * Ngăn kéo chi tiết vai trò: tab "Thông tin chung" và tab "Quyền" (tick theo module).
 * Quyền người đang thao tác không có thì không tick được – không cấp được quyền mình không có (design.md mục 9.5).
 * Vai trò ADMIN chỉ xem.
 * @param roleId Id vai trò; null = tạo mới.
 * @param onClose Đóng ngăn kéo.
 */
export function RoleDrawer({ roleId, onClose }: { roleId: string | null; onClose: () => void }) {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<RoleFormValues>()
  const role = useRole(roleId)
  const catalog = usePermissionCatalog()
  const mutations = useRoleMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = roleId === null
  const detail = role.data
  const editable = isNew ? hasPermission(Permission.RoleManage) : detail?.canEdit === true

  useEffect(() => {
    if (detail) form.setFieldsValue({ code: detail.code, name: detail.name, isActive: detail.isActive, permissionCodes: detail.permissionCodes })
  }, [detail, form])

  const save = async () => {
    let values: RoleFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ ở từng tab.')
      return
    }
    setError(null)
    setSaving(true)
    try {
      if (isNew) {
        await mutations.create.mutateAsync({ code: values.code, name: values.name, permissionCodes: values.permissionCodes })
        message.success(`Đã tạo vai trò ${values.code.toUpperCase()}.`)
      } else if (detail) {
        await mutations.update.mutateAsync({
          id: detail.id,
          input: { name: values.name, isActive: values.isActive, permissionCodes: values.permissionCodes },
        })
        message.success(`Đã lưu vai trò ${detail.code}.`)
      }
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được vai trò.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <DetailDrawer
      open
      title={isNew ? 'Thêm vai trò' : `Vai trò: ${detail?.code ?? ''}`}
      onClose={onClose}
      onSave={editable ? save : undefined}
      saving={saving}
      loading={role.isLoading || catalog.isLoading}
      error={error ?? (role.error ? role.error.message : null)}
      form={form}
      initialValues={emptyValues}
      readOnly={!editable}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              {detail?.isSystem && (
                <Alert
                  type="info"
                  showIcon
                  className="detail-section"
                  title="Vai trò hệ thống: luôn có đủ mọi quyền, kể cả quyền thêm ở phiên bản sau, nên không sửa được."
                />
              )}
              {detail && !detail.isSystem && !detail.canEdit && hasPermission(Permission.RoleManage) && (
                <Alert
                  type="info"
                  showIcon
                  className="detail-section"
                  title="Vai trò này có quyền mà bạn không có, nên bạn chỉ xem được."
                />
              )}
              <Form.Item
                label="Mã vai trò"
                name="code"
                extra={isNew ? '2–30 ký tự chữ không dấu hoặc số, ví dụ KETOANKHO. Không đổi được sau khi tạo.' : undefined}
                rules={[
                  { required: true, message: 'Hãy nhập mã vai trò.' },
                  { pattern: /^[A-Za-z0-9]{2,30}$/, message: 'Mã gồm 2–30 ký tự chữ không dấu hoặc số.' },
                ]}
                normalize={(v: string) => v.toUpperCase()}
              >
                <Input disabled={!isNew} autoFocus={isNew} autoComplete="off" />
              </Form.Item>
              <Form.Item label="Tên vai trò" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên vai trò.' }]}>
                <Input />
              </Form.Item>
              {!isNew && (
                <Form.Item name="isActive" valuePropName="checked" extra="Ngừng dùng thì người được gán vai trò này mất các quyền của nó ngay.">
                  <Checkbox disabled={detail?.isSystem}>Còn sử dụng</Checkbox>
                </Form.Item>
              )}
            </>
          ),
        },
        {
          key: 'permissions',
          label: 'Quyền',
          children: (
            <Form.Item name="permissionCodes" noStyle>
              <PermissionChecklist groups={catalog.data ?? []} disabled={!editable} canGrant={hasPermission} />
            </Form.Item>
          ),
        },
      ]}
    />
  )
}

/**
 * Bảng tick quyền theo module, mỗi module một khung có ô "Cả nhóm".
 * @param canGrant Người thao tác có quyền này không – không có thì ô bị khóa.
 */
function PermissionChecklist({
  groups,
  disabled,
  canGrant,
  value = [],
  onChange,
}: {
  groups: PermissionGroup[]
  disabled: boolean
  canGrant: (code: string) => boolean
  value?: string[]
  onChange?: (value: string[]) => void
}) {
  const selected = new Set(value)
  const total = groups.reduce((n, g) => n + g.permissions.length, 0)

  const toggle = (codes: string[], on: boolean) => {
    const next = new Set(selected)
    for (const c of codes) {
      if (on) next.add(c)
      else next.delete(c)
    }
    onChange?.([...next])
  }

  return (
    <>
      <div className="detail-hint" style={{ marginBottom: 6 }}>
        Đã chọn {selected.size}/{total} quyền. Ô bị mờ là quyền bạn không có nên không cấp được.
      </div>
      {groups.map((g) => {
        const grantable = g.permissions.filter((p) => canGrant(p.code)).map((p) => p.code)
        const checkedCount = g.permissions.filter((p) => selected.has(p.code)).length
        return (
          <fieldset key={g.module} className="detail-section" aria-label={g.name}>
            <legend>
              <Checkbox
                disabled={disabled || grantable.length === 0}
                checked={checkedCount === g.permissions.length}
                indeterminate={checkedCount > 0 && checkedCount < g.permissions.length}
                onChange={(e) => toggle(grantable, e.target.checked)}
              >
                {g.name}
              </Checkbox>
            </legend>
            <div className="detail-check-list">
              {g.permissions.map((p) => (
                <Checkbox
                  key={p.code}
                  checked={selected.has(p.code)}
                  disabled={disabled || !canGrant(p.code)}
                  onChange={(e) => toggle([p.code], e.target.checked)}
                  title={p.code}
                >
                  {p.name}
                </Checkbox>
              ))}
            </div>
          </fieldset>
        )
      })}
    </>
  )
}
