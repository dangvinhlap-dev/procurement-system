import { CloseCircleOutlined, PaperClipOutlined, PlusCircleOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Flex, Form, Input, InputNumber, Modal, Table, Typography } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useState } from 'react'
import {
  rebatePeriodStatusLabels,
  rebatePeriodStatusTone,
  rebateReceiptFormLabels,
  type RebatePeriodStatus,
  type RebateReceiptForm,
} from '../../api/contracts'
import {
  useVolumeRebateMutations,
  useVolumeRebateProgress,
  type VolumeRebate,
  type VolumeRebatePeriodRow,
  type VolumeRebateReceipt,
  type VolumeRebateShare,
} from '../../api/volumeRebates'
import { Select } from '../../classic/Select'
import { AttachmentPanel } from '../../components/AttachmentPanel/AttachmentPanel'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { renderRequiredMark } from '../../components/RequiredMark/requiredMark'
import { DATE_INPUT_FORMATS, formatDate, formatDateTime, formatMoney, vnNumberInput } from '../../utils/format'

/** X% cảnh báo sắp chạm bậc dùng khi tải lũy kế cho tab thực nhận (không ảnh hưởng số phải nhận). */
const NEAR = 10

/** Số tiền phông đơn cách, canh phải (docs/ui-style.md mục 3). */
const money = (v: number) => <span style={{ fontFamily: 'var(--erp-font-mono)' }}>{formatMoney(v)}</span>

/** Danh sách lựa chọn hình thức nhận chiết khấu cho ô chọn. */
const formOptions = (Object.keys(rebateReceiptFormLabels) as RebateReceiptForm[]).map((value) => ({ value, label: rebateReceiptFormLabels[value] }))

/** Đèn + chữ tình trạng thực nhận của kỳ (màu không là thông tin duy nhất – docs/ui-style.md mục 2). */
function ReceiptStatus({ status }: { status: RebatePeriodStatus }) {
  const tone = rebatePeriodStatusTone[status]
  const color = tone === 'success' ? 'green' : tone === 'warning' ? 'yellow' : tone === 'error' ? 'red' : null
  return (
    <Flex align="center" gap={4}>
      {color && <span className={`erp-light erp-light--${color}`} aria-hidden />}
      {rebatePeriodStatusLabels[status]}
    </Flex>
  )
}

interface ReceiptValues {
  receivedOn: Dayjs
  amount: number | null
  form: RebateReceiptForm
  refNo?: string
  note?: string
}

/**
 * Hộp thoại ghi nhận thực nhận một kỳ đã kết thúc: ngày nhận (sau cuối kỳ, không sau hôm nay), số tiền, hình thức, số chứng từ, Ghi chú và
 * Đính kèm (file chờ, lưu xong tự tải lên theo Id bản ghi – CLAUDE.md mục 4). Chỉ dựng khi mở để hàng chờ đính kèm làm mới mỗi lần.
 * @param agreementId Thỏa thuận.
 * @param period Kỳ cần ghi.
 * @param onClose Đóng hộp thoại.
 */
function RecordDialog({ agreementId, period, onClose }: { agreementId: string; period: VolumeRebatePeriodRow; onClose: () => void }) {
  const { message } = App.useApp()
  const mutations = useVolumeRebateMutations()
  const attachments = useAttachmentTab('VolumeRebateReceipt', undefined, true)
  const [form] = Form.useForm<ReceiptValues>()
  const periodEnd = dayjs(period.periodTo)

  const save = async () => {
    const values = await form.validateFields()
    try {
      const receipt = await mutations.recordReceipt.mutateAsync({
        id: agreementId,
        input: {
          periodFrom: period.periodFrom, receivedOn: values.receivedOn.format('YYYY-MM-DD'), amount: values.amount ?? 0, form: values.form,
          refNo: values.refNo?.trim() || null, note: values.note?.trim() || null,
        },
      })
      await attachments.uploadTo(receipt.id)
      message.success('Đã ghi nhận thực nhận.')
      onClose()
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không ghi nhận được.')
    }
  }

  return (
    <Modal
      open
      width={640}
      title={`Ghi nhận thực nhận – kỳ ${formatDate(period.periodFrom)} – ${formatDate(period.periodTo)}`}
      okText="Ghi nhận"
      cancelText="Đóng"
      onOk={save}
      confirmLoading={mutations.recordReceipt.isPending}
      onCancel={onClose}
    >
      {/* Form riêng, disabled={false}: hộp thoại mở từ form thỏa thuận chỉ đọc (DisabledContext truyền qua portal). name riêng để id ô
          (vr-receipt_note…) không trùng ô Ghi chú của form thỏa thuận phía sau – trùng id thì nhãn gắn nhầm ô. */}
      <Form form={form} name="vr-receipt" layout="vertical" disabled={false}
        initialValues={{ receivedOn: dayjs(), amount: period.shortAmount > 0 ? period.shortAmount : null, form: 'DebtOffset' }}>
        <Typography.Paragraph type="secondary">
          Phải nhận {formatMoney(period.rebateAmount)} đ, đã nhận {formatMoney(period.receivedAmount)} đ. Số ghi nhận được chia cho từng công ty
          theo tỷ lệ doanh số của kỳ tại thời điểm ghi.
        </Typography.Paragraph>
        <Form.Item label="Ngày nhận" name="receivedOn" rules={[{ required: true, message: 'Chọn ngày nhận.' }]}>
          {/* Ngày nhận sau ngày cuối kỳ, không sau hôm nay (design.md mục 18.25 – G6b). */}
          <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }}
            disabledDate={(d) => !d.isAfter(periodEnd, 'day') || d.isAfter(dayjs(), 'day')} />
        </Form.Item>
        <Form.Item label="Số tiền thực nhận (VND)" name="amount" rules={[{ required: true, type: 'number', min: 1, message: 'Số tiền > 0.' }]}>
          <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} precision={0} />
        </Form.Item>
        <Form.Item label="Hình thức" name="form" rules={[{ required: true }]}>
          <Select options={formOptions} />
        </Form.Item>
        <Form.Item label="Số chứng từ đối chiếu" name="refNo" rules={[{ max: 100 }]}>
          <Input placeholder="Biên bản đối chiếu, phiếu kế toán…" />
        </Form.Item>
        <Form.Item label="Ghi chú" name="note" rules={[{ max: 500 }]}>
          <Input.TextArea rows={2} />
        </Form.Item>
      </Form>
      <Typography.Text strong>{attachments.label}</Typography.Text>
      {attachments.children}
    </Modal>
  )
}

/**
 * Tab THỰC NHẬN của thỏa thuận chiết khấu sản lượng gộp (design.md mục 18.25 – lát G6b): mỗi kỳ phải nhận – đã nhận – còn thiếu – tình
 * trạng (hạn ghi nhận cuối kỳ + 30 ngày); mở dòng xem phần từng công ty. Tổng công ty ghi nhận khi **kỳ đã kết thúc** và hủy (bắt buộc lý
 * do); công ty áp dụng chỉ xem phần của mình.
 * @param doc Thỏa thuận đã lưu.
 */
export function VolumeRebateReceiptPanel({ doc }: { doc: VolumeRebate }) {
  const { message, modal } = App.useApp()
  const progress = useVolumeRebateProgress(doc.id, NEAR)
  const mutations = useVolumeRebateMutations()
  const [target, setTarget] = useState<VolumeRebatePeriodRow | null>(null)
  const [filesOf, setFilesOf] = useState<VolumeRebateReceipt | null>(null)
  const data = progress.data
  const canRecord = data?.canRecord ?? false

  const cancel = (receipt: VolumeRebateReceipt) => {
    let reason = ''
    modal.confirm({
      title: `Hủy ghi nhận ${formatMoney(receipt.amount)} đ ngày ${formatDate(receipt.receivedOn)}?`,
      content: (
        <Flex vertical gap={6}>
          <label htmlFor="vr-receipt-cancel-reason">{renderRequiredMark('Lý do hủy', { required: true })}</label>
          <Input.TextArea id="vr-receipt-cancel-reason" rows={3} maxLength={500} onChange={(e) => (reason = e.target.value)} />
        </Flex>
      ),
      okText: 'Hủy ghi nhận',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        if (!reason.trim()) {
          message.error('Hãy nhập lý do hủy.')
          throw new Error('missing reason')
        }
        try {
          await mutations.cancelReceipt.mutateAsync({ id: receipt.id, reason: reason.trim() })
          message.success('Đã hủy ghi nhận.')
        } catch (e) {
          message.error(e instanceof Error ? e.message : 'Không hủy được.')
        }
      },
    })
  }

  return (
    <Flex vertical gap={8} style={{ marginTop: 8 }}>
      <Typography.Text type="secondary">
        Phải nhận = chiết khấu của bậc đạt được trong kỳ. Chỉ ghi thực nhận sau khi kỳ kết thúc; hạn ghi nhận {data?.graceDays ?? 30} ngày sau cuối
        kỳ, quá hạn mà còn thiếu là dấu hiệu Kiểm soát nội bộ. Số ghi nhận chia cho từng công ty theo tỷ lệ doanh số kỳ lúc ghi.
      </Typography.Text>
      {progress.error && <Alert type="error" showIcon title={progress.error.message} />}
      <Table<VolumeRebatePeriodRow>
        size="small"
        pagination={false}
        loading={progress.isLoading}
        rowKey={(r) => r.periodFrom}
        dataSource={data?.periods ?? []}
        tableLayout="fixed"
        scroll={{ x: 1100 }}
        aria-label="Thực nhận theo kỳ"
        expandable={{
          rowExpandable: (r) => r.companies.length > 0,
          expandedRowRender: (r) => (
            <Table<VolumeRebateShare>
              size="small"
              pagination={false}
              rowKey={(s) => s.companyId}
              dataSource={r.companies}
              aria-label={`Thực nhận từng công ty kỳ ${formatDate(r.periodFrom)}`}
              columns={[
                { title: 'Công ty', dataIndex: 'companyCode', width: 110 },
                { title: 'Doanh số', dataIndex: 'amount', width: 130, align: 'right', render: money },
                { title: 'Phải nhận được chia', dataIndex: 'rebateShare', width: 150, align: 'right', render: money },
                { title: 'Đã nhận được chia', dataIndex: 'receivedShare', width: 150, align: 'right', render: money },
              ]}
            />
          ),
        }}
        columns={[
          { title: 'Kỳ', key: 'period', width: 165, render: (_, r) => `${formatDate(r.periodFrom)} – ${formatDate(r.periodTo)}` },
          { title: 'Hạn ghi nhận', dataIndex: 'dueDate', width: 95, render: (v: string) => formatDate(v) },
          { title: 'Doanh số gộp', dataIndex: 'amount', width: 125, align: 'right', render: money },
          { title: 'Bậc đạt', dataIndex: 'tierNo', width: 70, align: 'right', render: (v: number | null) => v ?? 'Chưa đạt' },
          { title: 'Phải nhận', dataIndex: 'rebateAmount', width: 115, align: 'right', render: money },
          { title: 'Đã nhận', dataIndex: 'receivedAmount', width: 115, align: 'right', render: money },
          { title: 'Còn thiếu', dataIndex: 'shortAmount', width: 115, align: 'right', render: money },
          { title: 'Tình trạng', dataIndex: 'receiptStatus', width: 125, render: (v: RebatePeriodStatus) => <ReceiptStatus status={v} /> },
          ...(canRecord
            ? [{
                title: '', key: 'record', width: 100,
                // Chỉ kỳ đã kết thúc mới ghi nhận được (người dùng chốt 08/11/2026) – máy chủ kiểm tra lại.
                render: (_: unknown, r: VolumeRebatePeriodRow) => r.state === 'Ended' && (
                  <Button size="small" disabled={false} icon={<PlusCircleOutlined />} onClick={() => setTarget(r)}
                    aria-label={`Ghi nhận kỳ ${formatDate(r.periodFrom)}`}>
                    Ghi nhận
                  </Button>
                ),
              }]
            : []),
        ]}
      />
      <Typography.Text strong>Các lần ghi nhận thực nhận</Typography.Text>
      <Table<VolumeRebateReceipt>
        size="small"
        pagination={false}
        rowKey={(r) => r.id}
        dataSource={data?.receipts ?? []}
        tableLayout="fixed"
        scroll={{ x: 1400 }}
        aria-label="Các lần ghi nhận thực nhận chiết khấu gộp"
        locale={{ emptyText: 'Chưa ghi nhận lần nào.' }}
        rowClassName={(r) => (r.isCancelled ? 'is-cancelled' : '')}
        columns={[
          { title: 'Kỳ', key: 'period', width: 165, render: (_, r) => `${formatDate(r.periodFrom)} – ${formatDate(r.periodTo)}` },
          { title: 'Ngày nhận', dataIndex: 'receivedOn', width: 95, render: (v: string) => formatDate(v) },
          { title: 'Số tiền', dataIndex: 'amount', width: 115, align: 'right', render: money },
          { title: 'Hình thức', dataIndex: 'form', width: 140, render: (v: RebateReceiptForm) => rebateReceiptFormLabels[v] },
          { title: 'Số chứng từ', dataIndex: 'refNo', width: 110, ellipsis: { showTitle: true } },
          {
            title: 'Phần chia', key: 'shares', width: 220, ellipsis: { showTitle: true },
            render: (_, r) => r.shares.map((s) => `${s.companyCode}: ${formatMoney(s.amount)}`).join('; '),
          },
          { title: 'Ghi chú', dataIndex: 'note', width: 150, ellipsis: { showTitle: true } },
          { title: 'Người ghi', key: 'by', width: 210, ellipsis: { showTitle: true }, render: (_, r) => `${r.createdByName ?? ''} ${formatDateTime(r.createdAt)}` },
          { title: 'Trạng thái', key: 'state', width: 170, ellipsis: { showTitle: true }, render: (_, r) => (r.isCancelled ? `Đã hủy: ${r.cancelReason ?? ''}` : 'Đang tính') },
          {
            title: '', key: 'actions', width: 170,
            render: (_: unknown, r: VolumeRebateReceipt) => (
              <Flex gap={4}>
                {/* Đính kèm thuộc tổng công ty (như thỏa thuận) – công ty áp dụng không mở được. */}
                {canRecord && (
                  <Button size="small" disabled={false} icon={<PaperClipOutlined />} onClick={() => setFilesOf(r)}
                    aria-label={`Đính kèm lần nhận ${formatDate(r.receivedOn)}`}>
                    Đính kèm
                  </Button>
                )}
                {canRecord && !r.isCancelled && (
                  <Button size="small" danger disabled={false} icon={<CloseCircleOutlined />} onClick={() => cancel(r)}>
                    Hủy
                  </Button>
                )}
              </Flex>
            ),
          },
        ]}
      />
      {target && <RecordDialog agreementId={doc.id} period={target} onClose={() => setTarget(null)} />}
      <Modal
        open={!!filesOf}
        width={720}
        title={filesOf ? `Đính kèm – lần nhận ${formatMoney(filesOf.amount)} đ ngày ${formatDate(filesOf.receivedOn)}` : ''}
        footer={null}
        onCancel={() => setFilesOf(null)}
        destroyOnHidden
      >
        {filesOf && <AttachmentPanel ownerType="VolumeRebateReceipt" ownerId={filesOf.id} canManage={canRecord && !filesOf.isCancelled} />}
      </Modal>
    </Flex>
  )
}
