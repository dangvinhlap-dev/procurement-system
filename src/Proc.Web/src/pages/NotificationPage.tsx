import { CheckOutlined } from '@ant-design/icons'
import { App, Button, Select } from 'antd'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import { notificationTypeLabels, useNotificationMutations, useNotifications, type AppNotification } from '../api/notifications'
import { screens } from '../app/screens'
import type { GridColumn } from '../components/DataGrid/types'
import { FilterField, ListPage } from '../components/ListPage/ListPage'

const columns: GridColumn<AppNotification>[] = [
  {
    key: 'state',
    title: 'Trạng thái',
    width: 100,
    type: 'status',
    value: (r) => (r.readAt ? 'Đã đọc' : 'Chưa đọc'),
    tone: (r) => (r.readAt ? 'neutral' : 'warning'),
    groupable: true,
  },
  { key: 'createdAt', title: 'Thời gian', width: 120, type: 'datetime', value: (r) => r.createdAt },
  { key: 'type', title: 'Loại', width: 150, value: (r) => notificationTypeLabels[r.type] ?? r.type, groupable: true },
  { key: 'title', title: 'Tiêu đề', width: 280, value: (r) => r.title },
  { key: 'message', title: 'Nội dung', width: 520, value: (r) => r.message },
]

/**
 * Đường dẫn mở khi bấm thông báo: phiếu (PR, PO, ICR…) mở thẳng trang phiếu; kỳ kho (PER) mở màn hình Kỳ kho vì kỳ không có trang
 * riêng. Mã màn hình không còn (đổi menu) thì mở trang chủ.
 */
function targetPath(n: AppNotification): string {
  const screen = screens.find((s) => s.code === n.screenCode)
  if (!screen) return '/'
  return n.targetId && n.screenCode !== 'PER' ? `${screen.path}/${n.targetId}` : screen.path
}

/**
 * Hằng ngày → Thông báo (design.md mục 11.6): thông báo của tôi tại công ty đang làm việc – phiếu chờ tôi duyệt, phiếu tôi lập được
 * duyệt/bị từ chối, hàng nội bộ chờ nhận, kỳ kho mở lại. Bấm đúp/Enter/mũi tên ">" thì đánh dấu đã đọc và mở phiếu.
 */
export function NotificationPage() {
  const notifications = useNotifications()
  const mutations = useNotificationMutations()
  const navigate = useNavigate()
  const { message } = App.useApp()
  const [show, setShow] = useState<'all' | 'unread'>('all')
  const data = notifications.data
  const unread = useMemo(() => (data ?? []).filter((n) => !n.readAt).length, [data])
  const rows = useMemo(() => (show === 'unread' ? (data ?? []).filter((n) => !n.readAt) : (data ?? [])), [data, show])

  const open = (n: AppNotification) => {
    // Đánh dấu đọc chạy nền: mở phiếu ngay, không chờ máy chủ.
    if (!n.readAt) mutations.markRead.mutate(n.id)
    navigate(targetPath(n))
  }

  const markAll = async () => {
    try {
      await mutations.markAllRead.mutateAsync()
      message.success('Đã đánh dấu tất cả là đã đọc.')
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không đánh dấu được.')
    }
  }

  return (
    <ListPage
      title="Thông báo"
      exportFileName="thong-bao"
      rows={rows}
      columns={columns}
      rowKey={(r) => r.id}
      loading={notifications.isLoading}
      canCreate={false}
      onOpenRow={open}
      onClearFilters={() => setShow('all')}
      filters={
        <>
          <FilterField label="Hiển thị" width={160}>
            {(id) => (
              <Select
                id={id}
                value={show}
                onChange={setShow}
                options={[
                  { value: 'all', label: 'Tất cả' },
                  { value: 'unread', label: `Chưa đọc (${unread})` },
                ]}
              />
            )}
          </FilterField>
          <Button icon={<CheckOutlined />} onClick={() => void markAll()} disabled={unread === 0} loading={mutations.markAllRead.isPending}>
            Đánh dấu tất cả đã đọc
          </Button>
        </>
      }
    />
  )
}
