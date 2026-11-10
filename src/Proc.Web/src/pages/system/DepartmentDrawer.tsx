import { App, Checkbox, Form, Input } from 'antd'
import { useState } from 'react'
import { Select } from '../../classic/Select'
import {
  useDepartmentMutations,
  useDivisions,
  useOrganizationUserOptions,
  userSelectOptions,
  type Department,
} from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'

interface DepartmentFormValues {
  code: string
  name: string
  managerName: string
  divisionId: string | null
  managerUserId: string | null
  isActive: boolean
}

/**
 * Ngăn kéo chi tiết bộ phận. Mã không đổi được sau khi tạo (đã nằm trên PR); ngừng dùng thì PR mới không chọn được,
 * PR cũ vẫn hiện tên bộ phận (design.md mục 18.5). Khối và Trưởng phòng ban (tài khoản) là dữ liệu cho người duyệt động của quy trình duyệt
 * (lát O1, mục 19.3).
 * @param department Bộ phận đang mở; null = thêm mới.
 * @param onClose Đóng ngăn kéo.
 */
export function DepartmentDrawer({ department, onClose }: { department: Department | null; onClose: () => void }) {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<DepartmentFormValues>()
  const mutations = useDepartmentMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = department === null
  const editable = hasPermission(Permission.DepartmentManage)
  const divisions = useDivisions()
  const users = useOrganizationUserOptions(editable)

  const initialValues: DepartmentFormValues = {
    code: department?.code ?? '',
    name: department?.name ?? '',
    managerName: department?.managerName ?? '',
    divisionId: department?.divisionId ?? null,
    managerUserId: department?.managerUserId ?? null,
    isActive: department?.isActive ?? true,
  }

  const save = async () => {
    let values: DepartmentFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ.')
      return
    }
    setError(null)
    setSaving(true)
    const input = {
      name: values.name.trim(),
      managerName: values.managerName?.trim() ? values.managerName.trim() : null,
      divisionId: values.divisionId ?? null,
      managerUserId: values.managerUserId ?? null,
    }
    try {
      if (isNew) {
        await mutations.create.mutateAsync({ ...input, code: values.code })
        message.success(`Đã thêm bộ phận ${values.code.toUpperCase()}.`)
      } else {
        await mutations.update.mutateAsync({ id: department.id, input: { ...input, isActive: values.isActive } })
        message.success(`Đã lưu bộ phận ${department.code}.`)
      }
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được bộ phận.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <DetailDrawer
      open
      title={isNew ? 'Thêm bộ phận' : `Bộ phận: ${department.code}`}
      audit={{ entityType: 'Department', id: department?.id }}
      onClose={onClose}
      onSave={editable ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      initialValues={initialValues}
      readOnly={!editable}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              <Form.Item
                label="Mã bộ phận"
                name="code"
                extra={isNew ? '2–20 chữ không dấu hoặc số, ví dụ PX1. Không đổi được sau khi tạo.' : undefined}
                rules={[
                  { required: true, message: 'Hãy nhập mã bộ phận.' },
                  { pattern: /^[A-Za-z0-9]{2,20}$/, message: 'Mã gồm 2–20 chữ không dấu hoặc số.' },
                ]}
                normalize={(v: string) => v.toUpperCase()}
              >
                <Input disabled={!isNew} autoFocus={isNew} autoComplete="off" />
              </Form.Item>
              <Form.Item
                label="Tên bộ phận"
                name="name"
                rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên bộ phận.' }, { max: 200 }]}
              >
                <Input />
              </Form.Item>
              <Form.Item label="Khối" name="divisionId" extra="Giám đốc khối của khối là người duyệt cấp “GĐ khối của phiếu”.">
                <Select
                  allowClear
                  placeholder="Chọn khối"
                  loading={divisions.isLoading}
                  options={(divisions.data ?? [])
                    .filter((d) => d.isActive || d.id === department?.divisionId)
                    .map((d) => ({ value: d.id, label: `${d.code} – ${d.name}` }))}
                />
              </Form.Item>
              <Form.Item
                label="Trưởng phòng ban (tài khoản)"
                name="managerUserId"
                extra="Người duyệt cấp “Trưởng phòng ban của phiếu”. Chỉ chọn được tài khoản có vai trò tại công ty."
              >
                <Select
                  allowClear
                  placeholder="Chọn tài khoản"
                  loading={users.isLoading}
                  options={userSelectOptions(users.data, {
                    id: department?.managerUserId ?? null,
                    name: department?.managerUserName ?? null,
                  })}
                />
              </Form.Item>
              <Form.Item label="Trưởng bộ phận (tên in)" name="managerName" rules={[{ max: 200 }]}>
                <Input />
              </Form.Item>
              {!isNew && (
                <Form.Item
                  name="isActive"
                  valuePropName="checked"
                  extra="Ngừng dùng: PR mới không chọn được bộ phận này; PR đã lập vẫn giữ và hiện tên bộ phận."
                >
                  <Checkbox>Còn sử dụng</Checkbox>
                </Form.Item>
              )}
            </>
          ),
        },
      ]}
    />
  )
}
