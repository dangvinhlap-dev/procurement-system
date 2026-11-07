import { CloseCircleOutlined, PlusCircleOutlined } from '@ant-design/icons'
import { App, Button, DatePicker, Flex, Form, Input, InputNumber, Modal, Table, Typography } from 'antd'
import { Select } from '../../classic/Select'
import dayjs, { type Dayjs } from 'dayjs'
import { useState, type MutableRefObject } from 'react'
import {
  rebateKindLabels,
  rebateMethodLabels,
  rebatePeriodLabels,
  rebatePeriodStatusLabels,
  rebatePeriodStatusTone,
  rebateReceiptFormLabels,
  useContractRebateMutations,
  useContractRebates,
  type Contract,
  type ContractRebatePeriodRow,
  type ContractRebateReceipt,
  type RebateKind,
  type RebateMethod,
  type RebatePeriod,
  type RebateReceiptForm,
} from '../../api/contracts'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { renderRequiredMark } from '../../components/RequiredMark/requiredMark'
import { DATE_INPUT_FORMATS, formatDate, formatDateTime, formatMoney, vnNumberInput } from '../../utils/format'

/** Giá trị form của một cam kết chiết khấu/tài trợ. */
export interface RebateValues {
  id?: string | null
  kind?: RebateKind
  method?: RebateMethod
  value?: number | null
  minPurchaseAmount?: number | null
  period?: RebatePeriod
  description?: string
}

const options = <T extends string>(labels: Record<T, string>) =>
  (Object.keys(labels) as T[]).map((value) => ({ value, label: labels[value] }))

/** Số tiền phông đơn cách, canh phải (docs/ui-style.md mục 3). */
const money = (v: number) => <span style={{ fontFamily: 'var(--erp-font-mono)' }}>{formatMoney(v)}</span>

/**
 * Lưới cam kết chiết khấu doanh số/tài trợ trong form hợp đồng (Form.List `rebates`) – sửa khi hợp đồng còn Nháp (design.md mục 18.21).
 * @param readOnly Hợp đồng chỉ xem.
 * @param rebates Giá trị đang nhập (để đổi nhãn ô mức theo cách tính).
 * @param addRef Nhận hàm thêm dòng.
 */
export function RebateGrid({ readOnly, rebates, addRef }: { readOnly: boolean; rebates: RebateValues[]; addRef?: MutableRefObject<(() => void) | null> }) {
  const columns: LineColumn[] = [
    {
      key: 'kind',
      title: 'Loại', required: true,
      width: 170,
      render: (field) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'kind']} noStyle rules={[{ required: true, message: 'Chọn loại.' }]}>
            <Select variant="borderless" style={{ width: '100%' }} aria-label={`Loại cam kết ${field.name + 1}`} options={options(rebateKindLabels)} />
          </Form.Item>
        </>
      ),
    },
    {
      key: 'method',
      title: 'Cách tính', required: true,
      width: 130,
      render: (field) => (
        <Form.Item name={[field.name, 'method']} noStyle rules={[{ required: true, message: 'Chọn cách tính.' }]}>
          <Select variant="borderless" style={{ width: '100%' }} aria-label={`Cách tính cam kết ${field.name + 1}`} options={options(rebateMethodLabels)} />
        </Form.Item>
      ),
    },
    {
      key: 'value',
      title: 'Mức (% hoặc VND)', required: true,
      width: 130,
      align: 'right',
      render: (field) => {
        const percent = rebates[field.name]?.method !== 'FixedAmount'
        return (
          <Form.Item name={[field.name, 'value']} noStyle
            rules={[{ required: true, type: 'number', min: 0.0001, max: percent ? 100 : undefined, message: percent ? '0 < % ≤ 100.' : 'Số tiền > 0.' }]}>
            <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={percent ? 4 : 0}
              suffix={percent ? '%' : 'đ'} aria-label={`Mức cam kết ${field.name + 1}`} />
          </Form.Item>
        )
      },
    },
    {
      key: 'min',
      title: 'Doanh số tối thiểu (VND)',
      width: 150,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'minPurchaseAmount']} noStyle rules={[{ type: 'number', min: 0, message: '≥ 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={0} placeholder="Không điều kiện"
            aria-label={`Doanh số tối thiểu cam kết ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'period',
      title: 'Kỳ quyết toán', required: true,
      width: 120,
      render: (field) => (
        <Form.Item name={[field.name, 'period']} noStyle rules={[{ required: true, message: 'Chọn kỳ.' }]}>
          <Select variant="borderless" style={{ width: '100%' }} aria-label={`Kỳ quyết toán cam kết ${field.name + 1}`} options={options(rebatePeriodLabels)} />
        </Form.Item>
      ),
    },
    {
      key: 'description',
      title: 'Nội dung cam kết', required: true,
      width: 320,
      render: (field) => (
        <Form.Item name={[field.name, 'description']} noStyle rules={[{ required: true, whitespace: true, message: 'Ghi nội dung.' }, { max: 500 }]}>
          <Input variant="borderless" aria-label={`Nội dung cam kết ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]
  return (
    <LineGrid name="rebates" auditEntity="PurchaseContractRebate" columns={columns} readOnly={readOnly} addRef={addRef}
      newLine={() => ({ id: null, kind: 'SalesVolume', method: 'Percent', period: 'Quarter', minPurchaseAmount: null })} />
  )
}

/** Đèn + chữ tình trạng kỳ (màu không là thông tin duy nhất – docs/ui-style.md mục 2). */
function PeriodStatus({ status }: { status: ContractRebatePeriodRow['status'] }) {
  const tone = rebatePeriodStatusTone[status]
  const color = tone === 'success' ? 'green' : tone === 'warning' ? 'yellow' : tone === 'error' ? 'red' : null
  return (
    <Flex align="center" gap={4}>
      {color && <span className={`erp-light erp-light--${color}`} aria-hidden />}
      {rebatePeriodStatusLabels[status]}
    </Flex>
  )
}

interface ReceiptFormValues {
  receivedOn: Dayjs
  amount: number | null
  form: RebateReceiptForm
  refNo?: string
  note?: string
}

/**
 * Theo dõi chiết khấu/tài trợ thực nhận theo kỳ (lát F4b – design.md mục 18.21): phải nhận – đã nhận – còn thiếu, ghi nhận và hủy thực nhận.
 * Nằm trong form hợp đồng chỉ đọc nên các nút tự đặt disabled={false}, hộp thoại có Form riêng.
 * @param contract Hợp đồng đã hiệu lực.
 */
export function RebateTracking({ contract }: { contract: Contract }) {
  const { message, modal } = App.useApp()
  const tracking = useContractRebates(contract.id)
  const mutations = useContractRebateMutations()
  const [target, setTarget] = useState<ContractRebatePeriodRow | null>(null)
  const [form] = Form.useForm<ReceiptFormValues>()
  const data = tracking.data
  const canRecord = data?.canRecord ?? false

  const openRecord = (row: ContractRebatePeriodRow) => {
    form.setFieldsValue({ receivedOn: dayjs(), amount: row.shortAmount > 0 ? row.shortAmount : null, form: 'DebtOffset', refNo: '', note: '' })
    setTarget(row)
  }

  const record = async () => {
    if (!target) return
    const values = await form.validateFields()
    try {
      await mutations.record.mutateAsync({
        contractId: contract.id,
        input: {
          rebateId: target.rebateId, periodFrom: target.periodFrom, receivedOn: values.receivedOn.format('YYYY-MM-DD'), amount: values.amount ?? 0,
          form: values.form, refNo: values.refNo?.trim() || null, note: values.note?.trim() || null,
        },
      })
      message.success('Đã ghi nhận thực nhận.')
      setTarget(null)
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không ghi nhận được.')
    }
  }

  const cancel = (receipt: ContractRebateReceipt) => {
    let reason = ''
    modal.confirm({
      title: `Hủy ghi nhận ${formatMoney(receipt.amount)} đ ngày ${formatDate(receipt.receivedOn)}?`,
      content: (
        <Flex vertical gap={6}>
          <label htmlFor="rebate-cancel-reason">{renderRequiredMark('Lý do hủy', { required: true })}</label>
          <Input.TextArea id="rebate-cancel-reason" rows={3} maxLength={500} onChange={(e) => (reason = e.target.value)} />
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
          await mutations.cancel.mutateAsync({ id: receipt.id, reason: reason.trim() })
          message.success('Đã hủy ghi nhận.')
        } catch (e) {
          message.error(e instanceof Error ? e.message : 'Không hủy được.')
        }
      },
    })
  }

  return (
    <Flex vertical gap={8} style={{ marginTop: 8 }}>
      <Typography.Text strong>Theo dõi thực nhận theo kỳ</Typography.Text>
      <Typography.Text type="secondary">
        Doanh số = thành tiền trước VAT các PO đã duyệt/đã đóng với NCC trong kỳ. Hạn ghi nhận: {data?.graceDays ?? 30} ngày sau cuối kỳ; quá hạn
        mà còn thiếu là dấu hiệu Kiểm soát nội bộ.
      </Typography.Text>
      <Table<ContractRebatePeriodRow>
        size="small"
        pagination={false}
        loading={tracking.isLoading}
        rowKey={(r) => `${r.rebateId}-${r.periodFrom}`}
        dataSource={data?.periods ?? []}
        tableLayout="fixed"
        scroll={{ x: 1100 }}
        aria-label="Theo dõi chiết khấu tài trợ theo kỳ"
        locale={{ emptyText: 'Hợp đồng chưa có cam kết chiết khấu/tài trợ.' }}
        columns={[
          // Nội dung dài cắt bớt, rê chuột xem đủ – giữ mỗi kỳ một dòng 22px (docs/ui-style.md mục 4).
          { title: 'Cam kết', dataIndex: 'description', width: 200, ellipsis: { showTitle: true }, render: (v: string, r) => `${r.rebateLineNo}. ${v}` },
          { title: 'Kỳ', key: 'period', width: 165, render: (_, r) => `${formatDate(r.periodFrom)} – ${formatDate(r.periodTo)}` },
          { title: 'Hạn ghi nhận', dataIndex: 'dueDate', width: 95, render: (v: string) => formatDate(v) },
          { title: 'Doanh số', dataIndex: 'purchaseAmount', width: 120, align: 'right', render: money },
          { title: 'Phải nhận', dataIndex: 'expectedAmount', width: 105, align: 'right', render: money },
          { title: 'Đã nhận', dataIndex: 'receivedAmount', width: 105, align: 'right', render: money },
          { title: 'Còn thiếu', dataIndex: 'shortAmount', width: 105, align: 'right', render: money },
          { title: 'Tình trạng', dataIndex: 'status', width: 125, render: (v: ContractRebatePeriodRow['status']) => <PeriodStatus status={v} /> },
          ...(canRecord
            ? [{
                title: '', key: 'record', width: 100,
                render: (_: unknown, r: ContractRebatePeriodRow) => (
                  <Button size="small" disabled={false} icon={<PlusCircleOutlined />} onClick={() => openRecord(r)}
                    aria-label={`Ghi nhận kỳ ${formatDate(r.periodFrom)} cam kết ${r.rebateLineNo}`}>
                    Ghi nhận
                  </Button>
                ),
              }]
            : []),
        ]}
      />
      <Typography.Text strong>Các lần ghi nhận thực nhận</Typography.Text>
      <Table<ContractRebateReceipt>
        size="small"
        pagination={false}
        rowKey={(r) => r.id}
        dataSource={data?.receipts ?? []}
        tableLayout="fixed"
        scroll={{ x: 1250 }}
        aria-label="Các lần ghi nhận thực nhận"
        locale={{ emptyText: 'Chưa ghi nhận lần nào.' }}
        rowClassName={(r) => (r.isCancelled ? 'is-cancelled' : '')}
        columns={[
          { title: 'Cam kết', dataIndex: 'rebateLineNo', width: 70 },
          { title: 'Kỳ từ', dataIndex: 'periodFrom', width: 95, render: (v: string) => formatDate(v) },
          { title: 'Ngày nhận', dataIndex: 'receivedOn', width: 95, render: (v: string) => formatDate(v) },
          { title: 'Số tiền', dataIndex: 'amount', width: 120, align: 'right', render: money },
          { title: 'Hình thức', dataIndex: 'form', width: 140, render: (v: RebateReceiptForm) => rebateReceiptFormLabels[v] },
          { title: 'Số chứng từ', dataIndex: 'refNo', width: 120 },
          { title: 'Ghi chú', dataIndex: 'note', width: 160, ellipsis: { showTitle: true } },
          { title: 'Người ghi', key: 'by', width: 230, ellipsis: { showTitle: true }, render: (_, r) => `${r.createdByName ?? ''} ${formatDateTime(r.createdAt)}` },
          { title: 'Trạng thái', key: 'state', width: 200, ellipsis: { showTitle: true }, render: (_, r) => (r.isCancelled ? `Đã hủy: ${r.cancelReason ?? ''}` : 'Đang tính') },
          ...(canRecord
            ? [{
                title: '', key: 'cancel', width: 90,
                render: (_: unknown, r: ContractRebateReceipt) =>
                  !r.isCancelled && (
                    <Button size="small" danger disabled={false} icon={<CloseCircleOutlined />} onClick={() => cancel(r)}>
                      Hủy
                    </Button>
                  ),
              }]
            : []),
        ]}
      />
      <Modal
        open={!!target}
        title={target ? `Ghi nhận thực nhận – kỳ ${formatDate(target.periodFrom)} – ${formatDate(target.periodTo)}` : ''}
        okText="Ghi nhận"
        cancelText="Đóng"
        onOk={record}
        confirmLoading={mutations.record.isPending}
        onCancel={() => setTarget(null)}
        destroyOnHidden
      >
        {target && (
          // Form riêng, disabled={false}: hộp thoại mở từ form hợp đồng chỉ đọc (DisabledContext truyền qua portal).
          <Form form={form} layout="vertical" disabled={false}>
            <Typography.Paragraph type="secondary">
              {target.rebateLineNo}. {target.description} – phải nhận {formatMoney(target.expectedAmount)} đ, đã nhận {formatMoney(target.receivedAmount)} đ.
            </Typography.Paragraph>
            <Form.Item label="Ngày nhận" name="receivedOn" rules={[{ required: true, message: 'Chọn ngày nhận.' }]}>
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Số tiền thực nhận (VND)" name="amount" rules={[{ required: true, type: 'number', min: 1, message: 'Số tiền > 0.' }]}>
              <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} precision={0} />
            </Form.Item>
            <Form.Item label="Hình thức" name="form" rules={[{ required: true }]}>
              <Select options={options(rebateReceiptFormLabels)} />
            </Form.Item>
            <Form.Item label="Số chứng từ đối chiếu" name="refNo" rules={[{ max: 100 }]}>
              <Input placeholder="Biên bản, phiếu kế toán…" />
            </Form.Item>
            <Form.Item label="Ghi chú" name="note" rules={[{ max: 500 }]}>
              <Input.TextArea rows={2} />
            </Form.Item>
          </Form>
        )}
      </Modal>
    </Flex>
  )
}
