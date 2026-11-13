import { ImportOutlined } from '@ant-design/icons'
import { Alert, Button, ConfigProvider, Flex, Table } from 'antd'
import { useNavigate } from 'react-router'
import {
  deliveryNoticeStatusLabels,
  deliveryNoticeStatusTone,
  useOrderDeliveryNotices,
  type DeliveryNotice,
} from '../../api/receiptConfirmations'
import { StatusLight } from '../../classic/ClassicScreen'
import { DocNo } from '../../classic/DocNo'
import { AttachmentPanel } from '../../components/AttachmentPanel/AttachmentPanel'
import { formatDate, formatDateTime, formatQuantity } from '../../utils/format'
import { RECEIPT_PATH } from './ReceiptConfirmationPages'

/**
 * Tab "Giao hàng NCC" của PO (lát P5 – design.md 19.5, P5-5): các phiếu NCC báo giao trên Portal (mới trước) – ngày giao dự kiến, số phiếu giao,
 * trạng thái theo XNH; mở dòng xem SL từng dòng và file NCC gửi kèm (chỉ xem). Nút **Lập XNH** (icon xanh) mở phiếu nhận hàng điền sẵn số
 * phiếu giao, người giao, SL theo phiếu báo giao – thủ kho sửa theo thực tế rồi lưu.
 * @param orderId PO.
 */
export function DeliveryNoticePanel({ orderId }: { orderId: string }) {
  const navigate = useNavigate()
  const notices = useOrderDeliveryNotices(orderId)

  // PO đã duyệt là form chỉ đọc (khóa mọi ô) – nút Lập XNH không sửa PO nên bật lại trong tab này.
  return (
    <ConfigProvider componentDisabled={false}>
      <Flex vertical gap={8}>
        {notices.error && <Alert type="error" showIcon title={notices.error.message} />}
        <Table<DeliveryNotice>
          aria-label="Phiếu báo giao của NCC"
          data-testid="po-delivery-notices"
          size="small"
          bordered
          pagination={false}
          rowKey="id"
          loading={notices.isFetching}
          scroll={{ x: 'max-content' }}
          dataSource={notices.data ?? []}
          locale={{ emptyText: 'NCC chưa báo giao hàng trên Portal (NCC không dùng Portal thì nhận hàng theo đơn như cũ).' }}
          expandable={{
            expandedRowRender: (n) => (
              <Flex vertical gap={8}>
                <Table
                  aria-label={`Dòng phiếu báo giao ${n.no}`}
                  size="small"
                  bordered
                  pagination={false}
                  rowKey="lineNo"
                  dataSource={n.lines}
                  columns={[
                    { title: 'Dòng PO', key: 'po', width: 70, align: 'right', render: (_, l) => l.poLineNo },
                    { title: 'Mã hàng', key: 'code', width: 110, render: (_, l) => l.itemCode },
                    { title: 'Tên hàng', key: 'name', render: (_, l) => l.itemName },
                    { title: 'ĐVT', key: 'uom', width: 60, render: (_, l) => l.uomCode },
                    {
                      title: 'SL báo giao',
                      key: 'qty',
                      width: 100,
                      align: 'right',
                      render: (_, l) => <span className="erp-number">{formatQuantity(l.qty)}</span>,
                    },
                    { title: 'Ghi chú NCC', key: 'note', width: 220, render: (_, l) => l.note },
                  ]}
                />
                {n.note && <span>Ghi chú của NCC: {n.note}</span>}
                <strong>File NCC gửi kèm</strong>
                <AttachmentPanel ownerType="DeliveryNoticeFile" ownerId={n.id} canManage={false} />
              </Flex>
            ),
          }}
          columns={[
            { title: 'Số phiếu', key: 'no', width: 140, render: (_, n) => <DocNo no={n.no} /> },
            { title: 'Ngày giao dự kiến', key: 'date', width: 110, render: (_, n) => formatDate(n.expectedDate) },
            { title: 'Số phiếu giao', key: 'note', width: 120, render: (_, n) => n.deliveryNoteNo },
            { title: 'Người giao, phương tiện', key: 'by', width: 160, render: (_, n) => n.deliverer },
            {
              title: 'Trạng thái',
              key: 'status',
              width: 170,
              render: (_, n) => (
                <Flex gap={4} align="center">
                  <StatusLight level={deliveryNoticeStatusTone[n.status]} label={deliveryNoticeStatusLabels[n.status]} />
                  {deliveryNoticeStatusLabels[n.status]}
                </Flex>
              ),
            },
            {
              title: 'Phiếu nhận hàng',
              key: 'receipt',
              width: 150,
              render: (_, n) => (n.receiptNo ? <DocNo no={n.receiptNo} docType="XNH" id={n.receiptId} /> : null),
            },
            {
              title: 'NCC gửi',
              key: 'submitted',
              width: 230,
              render: (_, n) => `${formatDateTime(n.submittedAt)} – ${n.submittedByEmail}`,
            },
            {
              title: 'Lý do NCC hủy',
              key: 'cancel',
              width: 240,
              render: (_, n) => (n.cancelledAt ? `${formatDateTime(n.cancelledAt)}: ${n.cancelReason ?? ''}` : null),
            },
            {
              title: '',
              key: 'actions',
              width: 110,
              render: (_, n) =>
                n.canCreateReceipt && (
                  <Button
                    size="small"
                    icon={<ImportOutlined />}
                    onClick={() => navigate(`${RECEIPT_PATH}/moi?po=${orderId}&bgh=${n.id}`)}
                    title="Lập phiếu xác nhận nhận hàng điền sẵn theo phiếu báo giao"
                  >
                    Lập XNH
                  </Button>
                ),
            },
          ]}
        />
      </Flex>
    </ConfigProvider>
  )
}
