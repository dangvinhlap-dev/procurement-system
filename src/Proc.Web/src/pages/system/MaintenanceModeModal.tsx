import { CheckCircleOutlined, ToolOutlined } from '@ant-design/icons'
import { Alert, App, Form, Input, Modal } from 'antd'
import { useState } from 'react'
import { useSetMaintenanceMode, type MaintenanceMode } from '../../api/agent'
import { formatDateTime } from '../../utils/format'

/** Câu thông báo gợi ý khi bật (máy chủ dùng câu mặc định nếu bỏ trống). */
const DEFAULT_MAINTENANCE_MESSAGE = 'Phần mềm đang bảo trì. Vui lòng quay lại sau ít phút.'

/**
 * Hộp thoại bật / tắt chế độ bảo trì (lát QT5; lát QT7 chuyển nút từ SYS sang tab Bảo trì của CFG): bật thì mọi người dùng thường nhận thông báo bảo trì, chỉ người có quyền quản trị vận hành vào
 * được – bắt buộc lý do (ghi nhật ký vận hành). Bật được sửa câu thông báo người dùng thấy.
 * @param mode Trạng thái hiện tại; null = đóng.
 * @param onClose Đóng.
 */
export function MaintenanceModeModal({ mode, onClose }: { mode: MaintenanceMode | null; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<{ message: string; reason: string }>()
  const [error, setError] = useState<string | null>(null)
  const set = useSetMaintenanceMode()
  const turningOn = !mode?.isOn
  const close = () => {
    setError(null)
    onClose()
  }
  return (
    <Modal
      title={turningOn ? 'Bật chế độ bảo trì' : 'Tắt chế độ bảo trì'}
      open={mode != null}
      onCancel={close}
      onOk={() => form.submit()}
      okText={turningOn ? 'Bật bảo trì' : 'Tắt bảo trì'}
      okButtonProps={turningOn ? { danger: true, icon: <ToolOutlined /> } : { icon: <CheckCircleOutlined /> }}
      cancelText="Bỏ qua"
      confirmLoading={set.isPending}
      destroyOnHidden
      width={520}
    >
      <Alert
        type={turningOn ? 'warning' : 'info'}
        showIcon
        style={{ marginBottom: 8 }}
        title={
          turningOn
            ? 'Mọi người dùng (kể cả NCC trên Portal) không làm việc được cho tới khi tắt; người đang nhập phiếu sẽ không lưu được.'
            : `Đang bảo trì từ ${mode?.startedAt ? formatDateTime(mode.startedAt) : '?'} (${mode?.changedByName ?? ''}) – tắt thì người dùng vào lại bình thường.`
        }
        description={turningOn ? 'Người có quyền quản trị vận hành vẫn vào được để cài đặt, kiểm tra. Việc này ghi vào nhật ký vận hành.' : undefined}
      />
      {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        initialValues={{ message: mode?.message ?? DEFAULT_MAINTENANCE_MESSAGE, reason: '' }}
        onFinish={async (v) => {
          setError(null)
          try {
            await set.mutateAsync({ isOn: turningOn, message: turningOn ? v.message : null, reason: v.reason })
            message.success(turningOn ? 'Đã bật chế độ bảo trì.' : 'Đã tắt chế độ bảo trì.')
            close()
          } catch (e) {
            setError(e instanceof Error ? e.message : 'Không đổi được chế độ bảo trì.')
          }
        }}
      >
        {turningOn && (
          <Form.Item label="Câu thông báo người dùng thấy" name="message">
            <Input.TextArea maxLength={500} autoSize={{ minRows: 2, maxRows: 4 }} />
          </Form.Item>
        )}
        <Form.Item
          label="Lý do"
          name="reason"
          rules={[
            { required: true, message: 'Nhập lý do.' },
            { min: 5, message: 'Lý do ít nhất 5 ký tự.' },
          ]}
        >
          <Input maxLength={500} autoFocus placeholder={turningOn ? 'Ví dụ: Cài bản 0.34.0' : 'Ví dụ: Cài xong bản 0.34.0'} />
        </Form.Item>
      </Form>
    </Modal>
  )
}
