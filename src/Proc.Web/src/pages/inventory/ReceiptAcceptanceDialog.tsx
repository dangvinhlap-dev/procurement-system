import { Alert, App, Form, Input, Modal, Radio } from 'antd'
import { receiptAcceptanceLabels, useConfirmReceiptAcceptance, type InventoryDocument } from '../../api/inventoryDocuments'
import { formatDateTime } from '../../utils/format'

interface AcceptanceValues {
  result: 'Accepted' | 'Disputed'
  note?: string
}

/**
 * Hộp thoại đơn vị đặt hàng xác nhận nghiệm thu phiếu nhập mua đã ghi sổ (lát H4, design.md mục 18.25 – sơ đồ mục 3): Đạt, hoặc Không đạt
 * kèm ý kiến (bắt buộc). Không đổi sổ kho; xác nhận lại được (ví dụ NCC đã đổi hàng) – mọi lần đổi vào nhật ký.
 * @param props.doc Phiếu nhập mua đang mở.
 * @param props.open Hiện hộp thoại.
 * @param props.onClose Đóng hộp thoại (sau khi lưu hoặc bấm Hủy).
 */
export function ReceiptAcceptanceDialog({ doc, open, onClose }: { doc: InventoryDocument; open: boolean; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<AcceptanceValues>()
  const confirm = useConfirmReceiptAcceptance()
  const result = Form.useWatch('result', form)

  const submit = async () => {
    const values = await form.validateFields()
    try {
      await confirm.mutateAsync({ id: doc.id, input: { accepted: values.result === 'Accepted', note: values.note?.trim() || null } })
      message.success(`Đã xác nhận nghiệm thu phiếu ${doc.no}: ${receiptAcceptanceLabels[values.result]}.`)
      onClose()
    } catch (e) {
      message.error((e as Error).message)
    }
  }

  return (
    <Modal
      title="Xác nhận nghiệm thu"
      open={open}
      okText="Xác nhận"
      cancelText="Hủy"
      confirmLoading={confirm.isPending}
      onCancel={onClose}
      onOk={() => void submit()}
      destroyOnHidden
    >
      {doc.acceptance && doc.acceptance !== 'None' && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 8 }}
          title={`Đã xác nhận: ${receiptAcceptanceLabels[doc.acceptance]} – ${doc.acceptedByName ?? ''} lúc ${formatDateTime(doc.acceptedAt ?? null)}. Xác nhận lại sẽ thay kết quả cũ.`}
        />
      )}
      <Form
        form={form}
        name="receiptAcceptance"
        layout="vertical"
        preserve={false}
        initialValues={{ result: doc.acceptance === 'Disputed' ? 'Disputed' : 'Accepted', note: doc.acceptanceNote ?? '' }}
      >
        <Form.Item label="Kết quả nghiệm thu" name="result" rules={[{ required: true }]}>
          <Radio.Group
            options={[
              { value: 'Accepted', label: 'Đạt – đúng số lượng, chất lượng' },
              { value: 'Disputed', label: 'Không đạt – có ý kiến' },
            ]}
          />
        </Form.Item>
        <Form.Item
          label="Ý kiến nghiệm thu"
          name="note"
          rules={[
            { max: 500, message: 'Tối đa 500 ký tự.' },
            { required: result === 'Disputed', whitespace: true, message: 'Không đạt phải ghi ý kiến (thiếu, hỏng, sai quy cách…).' },
          ]}
          extra="Không đạt: Phòng cung ứng xử lý với NCC (trả hàng, giao bù); kế toán thấy trên báo cáo đối chiếu PO – nhận – hóa đơn."
        >
          <Input.TextArea rows={3} maxLength={500} aria-label="Ý kiến nghiệm thu" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
