import { App, DatePicker, Form, Input, InputNumber, Modal } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useUpdateReceiptInvoice, type InventoryDocument } from '../../api/inventoryDocuments'
import { DATE_INPUT_FORMATS, vnNumberInput } from '../../utils/format'

interface InvoiceValues {
  invoiceNo?: string
  invoiceSeries?: string
  invoiceDate?: Dayjs | null
  invoiceAmount?: number | null
}

/** Chuỗi rỗng hoặc toàn khoảng trắng gửi lên API là null; còn lại bỏ khoảng trắng hai đầu. */
const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Hộp thoại cập nhật hóa đơn NCC cho phiếu nhập mua đã ghi sổ (design.md mục 18.15): hóa đơn thường về sau khi nhập kho;
 * các trường này không đổi sổ kho, giá vốn nên sửa được cả khi kỳ đã đóng – mọi thay đổi vào nhật ký.
 * @param props.doc Phiếu nhập mua đang mở.
 * @param props.open Hiện hộp thoại.
 * @param props.onClose Đóng hộp thoại (sau khi lưu hoặc bấm Hủy).
 */
export function ReceiptInvoiceDialog({ doc, open, onClose }: { doc: InventoryDocument; open: boolean; onClose: () => void }) {
  const { message } = App.useApp()
  const [form] = Form.useForm<InvoiceValues>()
  const update = useUpdateReceiptInvoice()

  const submit = async () => {
    const values = await form.validateFields()
    try {
      await update.mutateAsync({
        id: doc.id,
        input: {
          invoiceNo: orNull(values.invoiceNo),
          invoiceSeries: orNull(values.invoiceSeries),
          invoiceDate: values.invoiceDate ? values.invoiceDate.format('YYYY-MM-DD') : null,
          invoiceAmount: values.invoiceAmount ?? null,
        },
      })
      message.success('Đã cập nhật hóa đơn.')
      onClose()
    } catch (e) {
      message.error((e as Error).message)
    }
  }

  return (
    <Modal
      title="Cập nhật hóa đơn NCC"
      open={open}
      okText="Lưu"
      cancelText="Hủy"
      confirmLoading={update.isPending}
      onCancel={onClose}
      onOk={() => void submit()}
      destroyOnHidden
    >
      <Form
        form={form}
        // Tên form làm tiền tố id ô – không trùng id các ô hóa đơn của form phiếu nhập phía sau.
        name="receiptInvoice"
        layout="vertical"
        preserve={false}
        initialValues={{
          invoiceNo: doc.invoiceNo ?? '',
          invoiceSeries: doc.invoiceSeries ?? '',
          invoiceDate: doc.invoiceDate ? dayjs(doc.invoiceDate) : null,
          invoiceAmount: doc.invoiceAmount ?? null,
        }}
      >
        <Form.Item label="Số hóa đơn" name="invoiceNo" rules={[{ max: 50 }]}>
          <Input autoComplete="off" />
        </Form.Item>
        <Form.Item label="Ký hiệu hóa đơn" name="invoiceSeries" rules={[{ max: 20 }]}>
          <Input autoComplete="off" />
        </Form.Item>
        <Form.Item label="Ngày hóa đơn" name="invoiceDate">
          <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item label="Tiền hóa đơn (gồm VAT)" name="invoiceAmount" extra="Tổng tiền thanh toán ghi trên hóa đơn – để đối chiếu với tiền phiếu nhập.">
          <InputNumber {...vnNumberInput} min={0} precision={0} style={{ width: '100%' }} aria-label="Tiền hóa đơn" />
        </Form.Item>
      </Form>
    </Modal>
  )
}
