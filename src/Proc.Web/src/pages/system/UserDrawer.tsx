import { KeyOutlined } from '@ant-design/icons'
import { Alert, App, Button, Checkbox, Form, Input, Modal, Radio, Tooltip, Tree } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import {
  useAccessOptions,
  useUser,
  useUserMutations,
  type UserAccess,
  type UserAccessOptions,
  type UserDetail,
} from '../../api/administration'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { formatDateTime } from '../../utils/format'

/** Giá trị của form người dùng (cả hai tab). */
interface UserFormValues {
  userName: string
  fullName: string
  email: string
  password: string
  isActive: boolean
  roleIds: string[]
  warehouseMode: 'all' | 'some'
  warehouseIds: string[]
  categoryMode: 'all' | 'some'
  categoryIds: string[]
}

const emptyValues: UserFormValues = {
  userName: '',
  fullName: '',
  email: '',
  password: '',
  isActive: true,
  roleIds: [],
  warehouseMode: 'all',
  warehouseIds: [],
  categoryMode: 'all',
  categoryIds: [],
}

/** Chuyển chi tiết từ server thành giá trị form; danh sách kho/nhóm rỗng nghĩa là "tất cả" (design.md mục 9.2). */
function toFormValues(user: UserDetail): UserFormValues {
  const { access } = user
  return {
    ...emptyValues,
    userName: user.userName,
    fullName: user.fullName,
    email: user.email ?? '',
    isActive: user.isActive,
    roleIds: access.roleIds,
    warehouseMode: access.warehouseIds.length > 0 ? 'some' : 'all',
    warehouseIds: access.warehouseIds,
    categoryMode: access.itemCategoryIds.length > 0 ? 'some' : 'all',
    categoryIds: access.itemCategoryIds,
  }
}

function toAccess(v: UserFormValues): UserAccess {
  return {
    roleIds: v.roleIds,
    warehouseIds: v.warehouseMode === 'some' ? v.warehouseIds : [],
    itemCategoryIds: v.categoryMode === 'some' ? v.categoryIds : [],
  }
}

const sameSet = (a: string[], b: string[]) => a.length === b.length && a.every((x) => b.includes(x))

/**
 * Ngăn kéo chi tiết người dùng: tab "Thông tin chung" (tài khoản) và tab "Phân quyền" tại công ty đang làm việc.
 * Nút và trường bị khóa theo quyền server trả về (canManageAccount, canAssign, isSelf); server vẫn kiểm tra lại.
 * @param userId Id người dùng; null = tạo mới.
 * @param onClose Đóng ngăn kéo.
 */
export function UserDrawer({ userId, onClose }: { userId: string | null; onClose: () => void }) {
  const { message } = App.useApp()
  const { workingCompany, hasPermission } = useAuth()
  const [form] = Form.useForm<UserFormValues>()
  const canManage = hasPermission(Permission.UserManage)
  const user = useUser(userId)
  const options = useAccessOptions(canManage)
  const mutations = useUserMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [resetOpen, setResetOpen] = useState(false)

  const isNew = userId === null
  const detail = user.data

  // Đổ dữ liệu vào form khi tải xong (setFieldsValue không phải state của React nên không gây vẽ lại vòng lặp).
  useEffect(() => {
    if (detail) form.setFieldsValue(toFormValues(detail))
  }, [detail, form])

  const canEditInfo = canManage && (isNew || detail?.canManageAccount === true || detail?.isSelf === true)
  const canEditAccess = canManage && !detail?.isSelf

  const save = async () => {
    let values: UserFormValues
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
        await mutations.create.mutateAsync({
          userName: values.userName.trim(),
          fullName: values.fullName,
          email: values.email || null,
          password: values.password,
          access: toAccess(values),
        })
        message.success(`Đã tạo người dùng ${values.userName.trim()}.`)
      } else if (detail) {
        const infoChanged =
          values.fullName !== detail.fullName || (values.email || null) !== detail.email || values.isActive !== detail.isActive
        if (canEditInfo && infoChanged) {
          await mutations.update.mutateAsync({
            id: detail.id,
            input: { fullName: values.fullName, email: values.email || null, isActive: values.isActive },
          })
        }
        const access = toAccess(values)
        const accessChanged =
          !sameSet(access.roleIds, detail.access.roleIds) ||
          !sameSet(access.warehouseIds, detail.access.warehouseIds) ||
          !sameSet(access.itemCategoryIds, detail.access.itemCategoryIds)
        if (canEditAccess && accessChanged) await mutations.setAccess.mutateAsync({ id: detail.id, access })
        message.success(`Đã lưu người dùng ${detail.userName}.`)
      }
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được người dùng.')
    } finally {
      setSaving(false)
    }
  }

  const title = isNew ? 'Thêm người dùng' : `Người dùng: ${detail?.userName ?? ''}`

  return (
    <>
      <DetailDrawer
        open
        title={title}
        onClose={onClose}
        onSave={canManage && (canEditInfo || canEditAccess) ? save : undefined}
        saving={saving}
        loading={user.isLoading || (canManage && options.isLoading)}
        error={error ?? (user.error ? user.error.message : null)}
        form={form}
        initialValues={emptyValues}
        readOnly={!canManage}
        extraActions={
          detail?.canManageAccount && (
            <Button type="text" icon={<KeyOutlined />} onClick={() => setResetOpen(true)}>
              Đặt lại mật khẩu
            </Button>
          )
        }
        tabs={[
          {
            key: 'general',
            label: 'Thông tin chung',
            children: <AccountTab isNew={isNew} detail={detail} disabled={!canEditInfo} />,
          },
          {
            key: 'access',
            label: `Phân quyền tại ${workingCompany?.code ?? ''}`,
            children: (
              <AccessTab
                options={options.data}
                canManage={canManage}
                disabled={!canEditAccess}
                isSelf={detail?.isSelf === true}
                companyLabel={`${workingCompany?.code ?? ''} – ${workingCompany?.name ?? ''}`}
              />
            ),
          },
        ]}
      />
      {detail && (
        <ResetPasswordDialog
          open={resetOpen}
          userName={detail.userName}
          onClose={() => setResetOpen(false)}
          onSubmit={async (newPassword) => {
            await mutations.resetPassword.mutateAsync({ id: detail.id, newPassword })
            message.success(`Đã đặt lại mật khẩu cho ${detail.userName}.`)
          }}
        />
      )}
    </>
  )
}

/** Tab thông tin tài khoản. Tên đăng nhập không đổi được sau khi tạo vì nhật ký thao tác ghi theo tên này. */
function AccountTab({ isNew, detail, disabled }: { isNew: boolean; detail?: UserDetail; disabled: boolean }) {
  return (
    <>
      {detail?.lockoutEnd && (
        <Alert
          type="warning"
          showIcon
          className="detail-section"
          title={`Tài khoản đang tạm khóa đến ${formatDateTime(detail.lockoutEnd)} do nhập sai mật khẩu nhiều lần. Đặt lại mật khẩu để mở khóa ngay.`}
        />
      )}
      <Form.Item
        label="Tên đăng nhập"
        name="userName"
        extra={isNew ? 'Chữ thường không dấu, số, dấu chấm, gạch; ví dụ thukho.bha. Không đổi được sau khi tạo.' : undefined}
        rules={[
          { required: true, message: 'Hãy nhập tên đăng nhập.' },
          { pattern: /^[a-z0-9][a-z0-9._-]{2,49}$/, message: 'Tên đăng nhập dài 3–50 ký tự: chữ thường không dấu, số, dấu chấm, gạch.' },
        ]}
      >
        <Input disabled={!isNew} autoFocus={isNew} autoComplete="off" />
      </Form.Item>
      <Form.Item label="Họ tên" name="fullName" rules={[{ required: true, whitespace: true, message: 'Hãy nhập họ tên.' }]}>
        <Input disabled={disabled} />
      </Form.Item>
      <Form.Item label="Email" name="email" rules={[{ type: 'email', message: 'Email không hợp lệ.' }]}>
        <Input disabled={disabled} />
      </Form.Item>
      {isNew ? (
        <Form.Item
          label="Mật khẩu ban đầu"
          name="password"
          extra="Tối thiểu 8 ký tự, có chữ thường và chữ số. Báo người dùng đổi ngay sau lần đăng nhập đầu."
          rules={[
            { required: true, message: 'Hãy nhập mật khẩu ban đầu.' },
            { min: 8, message: 'Mật khẩu phải có ít nhất 8 ký tự.' },
          ]}
        >
          <Input.Password autoComplete="new-password" />
        </Form.Item>
      ) : (
        <Form.Item name="isActive" valuePropName="checked" extra="Bỏ chọn để ngừng tài khoản; tài khoản không xóa để giữ nhật ký thao tác.">
          <Checkbox disabled={disabled || detail?.isSelf}>Còn sử dụng</Checkbox>
        </Form.Item>
      )}
      {detail && (
        <div className="detail-hint">
          Công ty được gán: {detail.companyCodes.length > 0 ? detail.companyCodes.join(', ') : '(chưa có)'} · Tạo lúc{' '}
          {formatDateTime(detail.createdAt)}
        </div>
      )}
      {detail && !detail.canManageAccount && !detail.isSelf && (
        <Alert
          type="info"
          showIcon
          style={{ marginTop: 8 }}
          title="Bạn chỉ sửa được phân quyền tại công ty này. Thông tin tài khoản và mật khẩu do người quản lý mọi công ty của người này sửa."
        />
      )}
    </>
  )
}

/** Tab phân quyền: vai trò, kho, nhóm hàng tại công ty đang làm việc (design.md mục 9.1, 9.2). */
function AccessTab({
  options,
  canManage,
  disabled,
  isSelf,
  companyLabel,
}: {
  options?: UserAccessOptions
  canManage: boolean
  disabled: boolean
  isSelf: boolean
  companyLabel: string
}) {
  const form = Form.useFormInstance<UserFormValues>()
  const roleIds = Form.useWatch('roleIds', form) ?? []
  const warehouseMode = Form.useWatch('warehouseMode', form)
  const categoryMode = Form.useWatch('categoryMode', form)

  const categoryTree = useMemo(() => buildCategoryTree(options?.itemCategories ?? []), [options?.itemCategories])

  if (!canManage) {
    return <Alert type="info" showIcon title="Cần quyền “Thêm, sửa người dùng và gán vai trò” để xem và sửa phân quyền chi tiết." />
  }
  if (!options) return null

  const noRole = roleIds.length === 0
  return (
    <>
      {isSelf && <Alert type="warning" showIcon className="detail-section" title="Không được tự sửa quyền của chính mình." />}
      <fieldset className="detail-section">
        <legend>Vai trò tại {companyLabel}</legend>
        <Form.Item name="roleIds" noStyle>
          <Checkbox.Group disabled={disabled} className="detail-check-list">
            {options.roles.map((r) => (
              <Tooltip key={r.id} title={r.canAssign ? undefined : 'Vai trò này có quyền mà bạn không có, nên bạn không gán hoặc gỡ được.'}>
                <Checkbox value={r.id} disabled={disabled || !r.canAssign}>
                  {r.name} <span className="detail-hint">({r.code})</span>
                </Checkbox>
              </Tooltip>
            ))}
          </Checkbox.Group>
        </Form.Item>
        <div className="detail-hint">Không chọn vai trò nào = người dùng không làm việc ở công ty này.</div>
      </fieldset>

      <fieldset className="detail-section">
        <legend>Kho được phép</legend>
        <Form.Item name="warehouseMode" noStyle>
          <Radio.Group disabled={disabled || noRole}>
            <Radio value="all" disabled={options.mustLimitWarehouses}>
              Tất cả kho của công ty
            </Radio>
            <Radio value="some">Chỉ các kho chọn dưới đây</Radio>
          </Radio.Group>
        </Form.Item>
        {warehouseMode === 'some' && (
          <Form.Item
            name="warehouseIds"
            style={{ marginTop: 6, marginBottom: 0 }}
            rules={[{ validator: (_, v: string[]) => (noRole || v?.length ? Promise.resolve() : Promise.reject(new Error('Hãy chọn ít nhất một kho.'))) }]}
          >
            <Checkbox.Group disabled={disabled || noRole} className="detail-check-list">
              {options.warehouses.map((w) => (
                <Checkbox key={w.id} value={w.id}>
                  {w.code} – {w.name}
                  {w.type === 'Main' && <span className="detail-hint"> (kho tổng)</span>}
                </Checkbox>
              ))}
            </Checkbox.Group>
          </Form.Item>
        )}
        {options.mustLimitWarehouses && (
          <div className="detail-hint">Bạn chỉ được làm việc với một số kho nên chỉ cấp được các kho đó.</div>
        )}
      </fieldset>

      <fieldset className="detail-section">
        <legend>Nhóm hàng được phép</legend>
        <Form.Item name="categoryMode" noStyle>
          <Radio.Group disabled={disabled || noRole}>
            <Radio value="all" disabled={options.mustLimitItemCategories}>
              Tất cả nhóm hàng
            </Radio>
            <Radio value="some">Chỉ các nhóm chọn dưới đây</Radio>
          </Radio.Group>
        </Form.Item>
        {categoryMode === 'some' && (
          <Form.Item
            name="categoryIds"
            style={{ marginTop: 6, marginBottom: 0 }}
            extra="Chọn nhóm cha là được cả các nhóm con."
            rules={[{ validator: (_, v: string[]) => (noRole || v?.length ? Promise.resolve() : Promise.reject(new Error('Hãy chọn ít nhất một nhóm hàng.'))) }]}
          >
            <CategoryTreeField treeData={categoryTree} disabled={disabled || noRole} />
          </Form.Item>
        )}
      </fieldset>
    </>
  )
}

interface CategoryNode {
  key: string
  title: string
  children: CategoryNode[]
}

/** Dựng cây nhóm hàng từ danh sách phẳng; nhóm có cha ngoài danh sách thì đứng ở gốc. */
function buildCategoryTree(items: UserAccessOptions['itemCategories']): CategoryNode[] {
  const nodes = new Map(items.map((c) => [c.id, { key: c.id, title: `${c.code} – ${c.name}`, children: [] as CategoryNode[] }]))
  const roots: CategoryNode[] = []
  for (const c of items) {
    const node = nodes.get(c.id)!
    const parent = c.parentId ? nodes.get(c.parentId) : undefined
    if (parent) parent.children.push(node)
    else roots.push(node)
  }
  return roots
}

/**
 * Ô chọn nhóm hàng dạng cây cho Form. Chọn độc lập từng nút (checkStrictly): lưu đúng nhóm được chọn,
 * việc kế thừa xuống nhóm con do server tính (design.md mục 9.2).
 */
function CategoryTreeField({
  treeData,
  disabled,
  value,
  onChange,
}: {
  treeData: CategoryNode[]
  disabled: boolean
  value?: string[]
  onChange?: (value: string[]) => void
}) {
  return (
    <Tree
      checkable
      checkStrictly
      selectable={false}
      defaultExpandAll
      disabled={disabled}
      treeData={treeData}
      checkedKeys={value ?? []}
      onCheck={(checked) => onChange?.((Array.isArray(checked) ? checked : checked.checked).map(String))}
    />
  )
}

/** Hộp thoại quản trị đặt lại mật khẩu cho người dùng (cũng mở khóa tạm). */
function ResetPasswordDialog({
  open,
  userName,
  onClose,
  onSubmit,
}: {
  open: boolean
  userName: string
  onClose: () => void
  onSubmit: (newPassword: string) => Promise<void>
}) {
  const [form] = Form.useForm<{ newPassword: string; confirmPassword: string }>()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const close = () => {
    form.resetFields()
    setError(null)
    onClose()
  }

  return (
    <Modal
      title={`Đặt lại mật khẩu: ${userName}`}
      open={open}
      onCancel={close}
      onOk={() => form.submit()}
      okText="Đặt lại mật khẩu"
      cancelText="Hủy"
      confirmLoading={saving}
      destroyOnHidden
      width={420}
    >
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 12 }} />}
      <Form
        form={form}
        layout="vertical"
        onFinish={async ({ newPassword }) => {
          setSaving(true)
          setError(null)
          try {
            await onSubmit(newPassword)
            close()
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không đặt lại được mật khẩu.')
          } finally {
            setSaving(false)
          }
        }}
      >
        <Form.Item
          label="Mật khẩu mới"
          name="newPassword"
          extra="Tối thiểu 8 ký tự, có chữ thường và chữ số. Tài khoản đang tạm khóa sẽ được mở khóa."
          rules={[{ required: true, message: 'Hãy nhập mật khẩu mới.' }]}
        >
          <Input.Password autoFocus autoComplete="new-password" />
        </Form.Item>
        <Form.Item
          label="Nhập lại mật khẩu mới"
          name="confirmPassword"
          dependencies={['newPassword']}
          rules={[
            { required: true, message: 'Hãy nhập lại mật khẩu mới.' },
            ({ getFieldValue }) => ({
              validator: (_, value) =>
                !value || value === getFieldValue('newPassword')
                  ? Promise.resolve()
                  : Promise.reject(new Error('Mật khẩu nhập lại không khớp.')),
            }),
          ]}
        >
          <Input.Password autoComplete="new-password" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
