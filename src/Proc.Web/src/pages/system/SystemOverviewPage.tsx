import { CheckCircleOutlined, ReloadOutlined, ToolOutlined } from '@ant-design/icons'
import { Alert, App, Button, Form, Input, Modal } from 'antd'
import { useState } from 'react'
import { useMaintenanceMode, useSetMaintenanceMode, type MaintenanceMode } from '../../api/agent'
import { useSystemOverview } from '../../api/operations'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime } from '../../utils/format'
import { overviewColumns } from './overviewColumns'

/** Tên môi trường trên tiêu đề thông báo. */
const environmentLabels: Record<string, string> = { Dev: 'DEV', Test: 'TEST', Prod: 'PROD', None: 'Chưa khai' }

/** Câu thông báo gợi ý khi bật (máy chủ dùng câu mặc định nếu bỏ trống). */
const DEFAULT_MAINTENANCE_MESSAGE = 'Phần mềm đang bảo trì. Vui lòng quay lại sau ít phút.'

/**
 * Hộp thoại bật / tắt chế độ bảo trì (lát QT5): bật thì mọi người dùng thường nhận thông báo bảo trì, chỉ người có quyền quản trị vận hành vào
 * được – bắt buộc lý do (ghi nhật ký vận hành). Bật được sửa câu thông báo người dùng thấy.
 * @param mode Trạng thái hiện tại; null = đóng.
 * @param onClose Đóng.
 */
function MaintenanceModeModal({ mode, onClose }: { mode: MaintenanceMode | null; onClose: () => void }) {
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

/**
 * Màn hình SYS – Tổng quan hệ thống (design.md mục 21.4, lát QT1): số liệu của môi trường đang chạy – phiên bản, migration đã chạy / đang chờ,
 * dung lượng CSDL, số kết nối, chỗ trống ổ đĩa, nhịp dịch vụ nền, cách gửi email; dòng đỏ / vàng ghi lý do và cách xử lý. Chỉ ở tổng công ty,
 * quyền System.Ops.View. Lát QT5: thông báo đỏ khi đang bảo trì, nút "Bật / Tắt chế độ bảo trì…" (quyền System.OpsMaintenanceMode.Manage).
 * Bảng ba môi trường (TEST / PROD) thêm ở lát QT8.
 */
export function SystemOverviewPage() {
  const overview = useSystemOverview()
  const maintenance = useMaintenanceMode()
  const { hasPermission } = useAuth()
  const canToggle = hasPermission(Permission.MaintenanceModeManage)
  const [toggling, setToggling] = useState<MaintenanceMode | null>(null)
  const data = overview.data
  const mode = maintenance.data
  const problems = data?.rows.filter((r) => r.status === 'Error' || r.status === 'Warning').length ?? 0
  return (
    <>
      <ListPage
        title="Tổng quan hệ thống"
        exportFileName="tong-quan-he-thong"
        rows={data?.rows ?? []}
        columns={overviewColumns}
        rowKey={(r) => `${r.group}|${r.item}`}
        loading={overview.isLoading}
        canCreate={false}
        actions={
          <>
            {canToggle && mode && (
              <Button
                icon={mode.isOn ? <CheckCircleOutlined /> : <ToolOutlined />}
                onClick={() => setToggling(mode)}
                title={mode.isOn ? 'Cho người dùng vào lại' : 'Chặn người dùng thường trong lúc cài đặt / khôi phục'}
              >
                {mode.isOn ? 'Tắt chế độ bảo trì…' : 'Bật chế độ bảo trì…'}
              </Button>
            )}
            <Button
              icon={<ReloadOutlined />}
              loading={overview.isFetching}
              onClick={() => {
                void overview.refetch()
                void maintenance.refetch()
              }}
              title="Lấy lại số liệu"
            >
              Làm mới
            </Button>
          </>
        }
        notice={
          <>
            {mode?.isOn && (
              <Alert
                type="error"
                showIcon
                style={{ marginBottom: 4 }}
                data-testid="maintenance-notice"
                title={`Đang bảo trì từ ${mode.startedAt ? formatDateTime(mode.startedAt) : '?'} – ${mode.changedByName ?? ''}: ${mode.message ?? ''}`}
                description={`Lý do: ${mode.reason ?? ''}. Người dùng thường nhận thông báo này khi mở phần mềm; người có quyền quản trị vận hành vẫn vào được.`}
              />
            )}
            {overview.error ? (
              <Alert type="error" showIcon title={overview.error.message} />
            ) : data ? (
              <Alert
                type={problems > 0 ? 'warning' : 'info'}
                showIcon
                title={`Môi trường ${environmentLabels[data.environment] ?? data.environment} · phiên bản ${data.version} · số liệu lúc ${formatDateTime(data.checkedAt)}${
                  problems > 0 ? ` · ${problems} hạng mục cần xem` : ' · mọi hạng mục ổn'
                }`}
              />
            ) : undefined}
          </>
        }
      />
      <MaintenanceModeModal mode={toggling} onClose={() => setToggling(null)} />
    </>
  )
}
