import { Flex, Table } from 'antd'
import { portalEmailKindLabels, portalEmailStateLabels, type PortalEmail } from '../api/sourcing'
import { StatusLight, type StatusLevel } from '../classic/ClassicScreen'
import { formatDateTime } from '../utils/format'

/** Màu đèn theo trạng thái gửi thư: đã gửi xanh, chờ gửi / thôi gửi vàng, lỗi đỏ. */
const portalEmailLevel: Record<PortalEmail['state'], StatusLevel> = { Sent: 'success', Pending: 'warning', Skipped: 'warning', Failed: 'error' }

/**
 * Bảng thư phần mềm đã báo NCC qua Portal (design.md 19.5): tab "Email NCC" của RFQ (lát P3b-3 – thư mời, gia hạn, hủy, trả lời, thông báo)
 * và tab "Xác nhận NCC" của PO (lát P4a – đơn hàng mới, mở lại phản hồi, hủy). Chỉ tài khoản Portal đang dùng mới nhận thư; NCC không có tài
 * khoản thì Phòng CUVT báo theo cách cũ. Chỉ xem – thư do hệ thống soạn, dịch vụ nền gửi.
 * @param items Thư, mới trước.
 * @param loading Đang tải.
 * @param intro Dòng giải thích trên bảng.
 * @param emptyText Chữ khi chưa có thư.
 */
export function PortalEmailView({ items, loading, intro, emptyText }: { items: PortalEmail[]; loading: boolean; intro: string; emptyText: string }) {
  return (
    <Flex vertical gap={4}>
      <span>{intro}</span>
      <Table<PortalEmail>
        aria-label="Email NCC"
        size="small"
        bordered
        loading={loading}
        pagination={false}
        rowKey="id"
        dataSource={items}
        locale={{ emptyText }}
        columns={[
          { title: 'Giờ soạn', key: 'created', width: 120, render: (_, m) => formatDateTime(m.createdAt) },
          { title: 'Loại thư', key: 'kind', width: 130, render: (_, m) => portalEmailKindLabels[m.kind] },
          { title: 'NCC', key: 'supplier', width: 220, render: (_, m) => `${m.supplierCode ?? ''} – ${m.supplierName ?? ''}` },
          { title: 'Người nhận', key: 'to', width: 240, render: (_, m) => `${m.toName} <${m.toAddress}>` },
          {
            title: 'Trạng thái',
            key: 'state',
            width: 110,
            render: (_, m) => (
              <Flex gap={4} align="center">
                <StatusLight level={portalEmailLevel[m.state]} label={portalEmailStateLabels[m.state]} />
                {portalEmailStateLabels[m.state]}
              </Flex>
            ),
          },
          { title: 'Giờ gửi', key: 'sent', width: 120, render: (_, m) => formatDateTime(m.sentAt) },
          { title: 'Ghi chú', key: 'note', render: (_, m) => m.note },
        ]}
      />
    </Flex>
  )
}
