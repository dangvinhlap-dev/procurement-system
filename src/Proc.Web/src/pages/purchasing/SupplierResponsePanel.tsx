import { UndoOutlined } from '@ant-design/icons'
import { App, Button, ConfigProvider, Flex, Table } from 'antd'
import {
  supplierOrderResponseLabels,
  usePoPortalEmails,
  useReopenSupplierResponse,
  type SupplierOrderResponseInfo,
  type SupplierOrderResponseItem,
} from '../../api/purchasing'
import { StatusLight } from '../../classic/ClassicScreen'
import { AttachmentPanel } from '../../components/AttachmentPanel/AttachmentPanel'
import { PortalEmailView } from '../../components/PortalEmailView'
import { formatDate, formatDateTime } from '../../utils/format'
import { confirmWithReason } from './useApprovalCommands'

/**
 * Tab "Xác nhận NCC" của PO (lát P4a – design.md 19.5, P4-4, P4-5): các lần NCC phản hồi trên Portal (mới trước), nút **Mở lại phản hồi**
 * (quyền lập PO, PO Đã duyệt – lý do gửi NCC; yêu cầu người dùng 12/11/2026), file NCC gửi kèm (chỉ xem) và thư đã báo NCC.
 * @param orderId PO.
 * @param orderNo Số PO (câu hỏi xác nhận).
 * @param info Phản hồi NCC.
 */
export function SupplierResponsePanel({ orderId, orderNo, info }: { orderId: string; orderNo: string; info: SupplierOrderResponseInfo }) {
  const { message, modal } = App.useApp()
  const reopen = useReopenSupplierResponse()
  const emails = usePoPortalEmails(orderId)
  const level = info.status === 'Confirmed' ? 'success' : info.status === 'Rejected' ? 'error' : 'warning'

  // PO đã duyệt là form chỉ đọc (khóa mọi ô) – lệnh mở lại không sửa PO nên bật lại trong tab này.
  return (
    <ConfigProvider componentDisabled={false}>
      <Flex vertical gap={8}>
        <Flex gap={8} align="center" wrap>
          <StatusLight level={level} label={supplierOrderResponseLabels[info.status]} />
          <strong data-testid="po-supplier-response-status">{supplierOrderResponseLabels[info.status]}</strong>
          {info.canReopen && (
            <Button
              size="small"
              icon={<UndoOutlined />}
              loading={reopen.isPending}
              onClick={() =>
                confirmWithReason(modal, message, {
                  title: `Mở lại phản hồi của NCC với đơn ${orderNo}?`,
                  description:
                    'Phản hồi hiện tại thành lịch sử; NCC xác nhận hoặc từ chối lại trên Portal. Lý do được gửi cho NCC qua thư và hiện trên Portal.',
                  label: 'Lý do (gửi NCC)',
                  placeholder: 'Ví dụ: Bên mua chấp nhận giao chậm 10 ngày, đề nghị xác nhận lại',
                  okText: 'Mở lại',
                  danger: false,
                  onOk: async (reason) => {
                    await reopen.mutateAsync({ id: orderId, reason })
                    void message.success('Đã mở lại phản hồi – NCC được báo qua thư.')
                  },
                })
              }
            >
              Mở lại phản hồi
            </Button>
          )}
        </Flex>
        <Table<SupplierOrderResponseItem>
          aria-label="Phản hồi của NCC"
          size="small"
          bordered
          pagination={false}
          rowKey="respondedAt"
          dataSource={info.responses}
          locale={{ emptyText: info.status === 'NotSent' ? 'PO chưa duyệt – NCC chưa thấy đơn.' : 'NCC chưa phản hồi.' }}
          columns={[
            { title: 'Thời điểm', key: 'at', width: 120, render: (_, r) => formatDateTime(r.respondedAt) },
            { title: 'Phản hồi', key: 'decision', width: 90, render: (_, r) => (r.decision === 'Confirmed' ? 'Xác nhận' : 'Từ chối') },
            { title: 'Ngày giao cam kết', key: 'promised', width: 110, render: (_, r) => formatDate(r.promisedDate) },
            { title: 'Ghi chú / lý do', key: 'note', render: (_, r) => r.note },
            {
              title: 'Người phản hồi (NCC)',
              key: 'by',
              width: 220,
              render: (_, r) => [r.respondedByName, r.respondedByEmail].filter(Boolean).join(' – '),
            },
            {
              title: 'Mở lại',
              key: 'reopen',
              width: 260,
              render: (_, r) =>
                r.reopenedAt ? `${formatDateTime(r.reopenedAt)} – ${r.reopenedByName ?? ''}: ${r.reopenReason ?? ''}` : 'Đang có hiệu lực',
            },
          ]}
        />
        <strong>File NCC gửi kèm</strong>
        <AttachmentPanel ownerType="PoSupplierFile" ownerId={orderId} canManage={false} />
        <PortalEmailView
          items={emails.data ?? []}
          loading={emails.isFetching}
          intro='Thư đã báo NCC qua Portal (tài khoản đang dùng): đơn hàng mới khi PO duyệt xong, đề nghị phản hồi lại, hủy đơn. Email máy chủ đang tắt thì thư ở trạng thái "Chờ gửi".'
          emptyText="Chưa có thư nào (PO chưa duyệt hoặc NCC chưa có tài khoản Portal đang dùng)."
        />
      </Flex>
    </ConfigProvider>
  )
}
