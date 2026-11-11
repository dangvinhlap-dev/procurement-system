import { Alert, App, Input, Table } from 'antd'
import { previewCancelChain, runCancelChain, type CancelChainItem } from '../../api/cancelChain'
import { queryClient } from '../../app/queryClient'
import { renderRequiredMark } from '../../components/RequiredMark/requiredMark'

type AppApi = ReturnType<typeof App.useApp>

/**
 * Hộp thoại Hủy phiếu theo dây chuyền (lát Z4b, design.md mục 20.1 R2, 20.2 D3): hỏi máy chủ trình tự sẽ hủy, liệt kê từng phiếu theo thứ tự
 * từ phiếu cuối về phiếu đầu (loại, số, trạng thái, ghi chú), cảnh báo phiếu ngoài chuỗi bị ảnh hưởng, ô lý do bắt buộc. Không đủ quyền thì
 * nêu lý do và khóa nút Hủy. Hủy xong tải lại mọi dữ liệu (SL đã đặt, đã nhận, ngân sách tính lại theo chứng từ chưa hủy).
 * @param modal Modal của App.useApp().
 * @param message Message của App.useApp().
 * @param options.docType Mã loại phiếu được chọn hủy (PR, PO, XNH).
 * @param options.id Id phiếu.
 * @param options.no Số phiếu – tiêu đề hộp thoại.
 * @param options.onError Báo lỗi lên phiếu khi hủy không được.
 */
export async function confirmCancelChain(
  modal: AppApi['modal'],
  message: AppApi['message'],
  options: { docType: string; id: string; no: string; onError?: (text: string) => void },
) {
  let chain
  try {
    chain = await previewCancelChain(options.docType, options.id)
  } catch (e) {
    message.error(e instanceof Error ? e.message : 'Không lấy được trình tự hủy.')
    return
  }
  let reason = ''
  const many = chain.items.length > 1
  modal.confirm({
    title: `Hủy phiếu ${options.no}?`,
    width: many ? 760 : 520,
    content: (
      <div className="cancel-chain">
        {chain.blockedReason && <Alert type="error" showIcon title={chain.blockedReason} style={{ marginBottom: 8 }} />}
        {many ? (
          <p>Hủy theo trình tự từ phiếu cuối về phiếu đầu, trong một lần – một phiếu không hủy được thì không phiếu nào bị hủy:</p>
        ) : (
          <p>Phiếu đã hủy giữ lại số phiếu và lịch sử, không dùng lại được.</p>
        )}
        {many && (
          <Table<CancelChainItem>
            size="small"
            bordered
            pagination={false}
            rowKey="id"
            aria-label="Trình tự hủy"
            dataSource={chain.items}
            columns={[
              { key: 'order', title: 'Thứ tự', dataIndex: 'order', width: 60, align: 'right' },
              { key: 'type', title: 'Loại phiếu', dataIndex: 'docTypeName', width: 150 },
              { key: 'no', title: 'Số phiếu', dataIndex: 'no', width: 170 },
              { key: 'status', title: 'Trạng thái', dataIndex: 'status', width: 110 },
              { key: 'note', title: 'Ghi chú', dataIndex: 'note' },
            ]}
            style={{ marginBottom: 8 }}
          />
        )}
        {chain.warnings.map((w) => (
          <Alert key={w} type="warning" showIcon title={w} style={{ marginBottom: 8 }} />
        ))}
        <label htmlFor="cancel-chain-reason">{renderRequiredMark('Lý do hủy', { required: true })}</label>
        <Input.TextArea
          id="cancel-chain-reason"
          rows={3}
          maxLength={500}
          placeholder="Ví dụ: đổi phương án mua, NCC không giao được"
          onChange={(e) => (reason = e.target.value)}
        />
      </div>
    ),
    okText: many ? `Hủy ${chain.items.length} phiếu` : 'Hủy phiếu',
    okButtonProps: { danger: true, disabled: !!chain.blockedReason },
    cancelText: 'Không',
    onOk: async () => {
      if (!reason.trim()) {
        message.error('Hãy nhập lý do hủy.')
        throw new Error('missing reason')
      }
      try {
        const done = await runCancelChain(options.docType, options.id, reason.trim())
        // Số liệu của nhiều màn hình (PR, PO, nhận hàng, ngân sách, việc cần làm) đổi theo – tải lại toàn bộ.
        await queryClient.invalidateQueries()
        message.success(
          done.items.length > 1 ? `Đã hủy ${done.items.length} phiếu: ${done.items.map((i) => i.no).join(', ')}.` : `Đã hủy phiếu ${options.no}.`,
        )
      } catch (e) {
        const text = e instanceof Error ? e.message : 'Không hủy được phiếu.'
        if (options.onError) options.onError(text)
        else message.error(text)
      }
    },
  })
}
