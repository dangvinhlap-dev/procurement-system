import { Alert, DatePicker, Form, Input, InputNumber } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useState } from 'react'
import { useParams } from 'react-router'
import {
  documentStatusLabels,
  receiveStatusLabels,
  useIntercompanyReceiptMutations,
  useInventoryDocument,
  type ConfirmReceiptInput,
  type InventoryDocument,
} from '../../api/inventoryDocuments'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { formatDateTime, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { documentStatusTone } from './documentStatus'
import { useDocumentCommands } from './useDocumentCommands'

interface ReceiveLineValues {
  id: string
  qty?: number | null
  lotNo?: string
  note?: string
}

interface ReceiveFormValues {
  date: Dayjs
  note?: string
  lines: ReceiveLineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Trang phiếu nhập mua nội bộ ở công ty mua (design.md mục 6.3, 10.8). Phiếu do bên bán sinh khi ghi sổ phiếu xuất; bên nhận chỉ
 * chọn ngày nhận, nhập SL thực nhận (≤ SL gửi), lô, ghi chú chênh lệch rồi Ghi sổ (= xác nhận). Đơn giá = giá bán nội bộ, không
 * sửa; không thêm/xóa dòng (nhận 0 nếu không nhận được dòng đó); không hủy phiếu ở bên nhận.
 */
export function IntercompanyReceiptPage() {
  const { id = '' } = useParams()
  const [form] = Form.useForm<ReceiveFormValues>()
  const document = useInventoryDocument(id)
  const mutations = useIntercompanyReceiptMutations()
  const [error, setError] = useState<string | null>(null)
  const doc = document.data
  const readOnly = !doc?.canEdit
  const watchedLines = Form.useWatch('lines', form) as ReceiveLineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      date: dayjs(doc.date),
      note: doc.note ?? '',
      lines: doc.lines.map((l) => ({ id: l.id, qty: l.qty, lotNo: l.lotNo ?? '', note: l.note ?? '' })),
    })
  }, [doc, form])

  const lineOf = (index: number) => doc?.lines[index]
  const amountOf = (index: number) => Math.round((lines[index]?.qty ?? 0) * (lineOf(index)?.unitPrice ?? 0) * 100) / 100
  const vatOf = (index: number) => {
    const vat = lineOf(index)?.vatCode
    const rate = vat && vat !== 'KCT' ? Number(vat) : 0
    return Math.round(amountOf(index) * rate) / 100
  }
  const totals = lines.reduce((sum, _, index) => ({ amount: sum.amount + amountOf(index), vat: sum.vat + vatOf(index) }), { amount: 0, vat: 0 })

  const persist = async (): Promise<InventoryDocument | null> => {
    if (!doc) return null
    let values: ReceiveFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ.')
      return null
    }
    setError(null)
    const input: ConfirmReceiptInput = {
      date: values.date.format('YYYY-MM-DD'),
      note: orNull(values.note),
      lines: values.lines.map((l) => ({ lineId: l.id, receivedQty: l.qty ?? 0, lotNo: orNull(l.lotNo), note: orNull(l.note) })),
    }
    try {
      return await mutations.update.mutateAsync({ id, input })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu.')
      return null
    }
  }

  const { save, actions } = useDocumentCommands({
    isNew: false, doc, readOnly, persist, mutations, listPath: '/kho/icr', noun: 'phiếu nhập mua nội bộ (xác nhận đã nhận hàng)', setError,
    cancellable: false,
  })

  const readonlyCell = (text: string) => <span className="line-grid-readonly">{text}</span>
  const columns: LineColumn[] = [
    {
      key: 'item',
      title: 'Mặt hàng',
      width: 260,
      render: (field, index) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          {readonlyCell(`${lineOf(index)?.itemCode ?? ''} – ${lineOf(index)?.itemName ?? ''}`)}
        </>
      ),
    },
    { key: 'uom', title: 'ĐVT', width: 60, render: (_, index) => readonlyCell(lineOf(index)?.uomCode ?? '') },
    { key: 'sent', title: 'SL gửi', width: 90, align: 'right', render: (_, index) => readonlyCell(formatQuantity(lineOf(index)?.sentQty ?? 0)) },
    {
      key: 'qty',
      title: 'SL thực nhận',
      width: 105,
      align: 'right',
      render: (field, index) => (
        <Form.Item
          name={[field.name, 'qty']}
          noStyle
          rules={[{ required: true, type: 'number', min: 0, max: lineOf(index)?.sentQty ?? undefined, message: 'Từ 0 đến SL gửi.' }]}
        >
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`SL thực nhận dòng ${index + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'diff',
      title: 'Chênh lệch',
      width: 90,
      align: 'right',
      render: (_, index) => {
        const diff = (lines[index]?.qty ?? 0) - (lineOf(index)?.sentQty ?? 0)
        return <span className={diff < 0 ? 'line-grid-readonly line-grid-warning' : 'line-grid-readonly'}>{diff ? formatQuantity(diff) : ''}</span>
      },
    },
    { key: 'price', title: 'Giá bán nội bộ', width: 120, align: 'right', render: (_, index) => readonlyCell(formatMoney(lineOf(index)?.unitPrice ?? 0)) },
    { key: 'amount', title: 'Thành tiền', width: 120, align: 'right', render: (_, index) => readonlyCell(formatMoney(amountOf(index))) },
    {
      key: 'lot',
      title: 'Số lô',
      width: 90,
      render: (field, index) => (
        <Form.Item name={[field.name, 'lotNo']} noStyle>
          <Input variant="borderless" maxLength={50} aria-label={`Số lô dòng ${index + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'note',
      title: 'Ghi chú chênh lệch',
      width: 170,
      render: (field, index) => (
        <Form.Item name={[field.name, 'note']} noStyle>
          <Input variant="borderless" maxLength={500} aria-label={`Ghi chú dòng ${index + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = doc
    ? doc.status === 'Draft'
      ? { label: 'Chờ nhận', tone: 'warning' as const }
      : { label: doc.receiveStatus ? receiveStatusLabels[doc.receiveStatus] : documentStatusLabels[doc.status], tone: documentStatusTone[doc.status] }
    : { label: '', tone: 'neutral' as const }

  return (
    <DocumentForm
      title="Phiếu nhập mua nội bộ"
      number={doc?.no ?? ''}
      printPath={doc ? `/api/inventory-documents/${doc.id}/pdf` : undefined}
      status={status}
      meta={doc && `Sinh từ phiếu xuất ${doc.linkedDocumentNo ?? ''} của ${doc.counterCompanyCode ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={document.isLoading}
      error={error ?? (document.error ? document.error.message : null)}
      notice={
        doc?.status === 'Draft' && (
          <Alert type="info" showIcon title="Kiểm hàng rồi nhập SL thực nhận; bấm Ghi sổ để xác nhận đã nhận – tồn kho tổng tăng theo giá bán nội bộ." />
        )
      }
      form={form}
      initialValues={{ date: dayjs(), lines: [] }}
      readOnly={readOnly}
      onSave={save}
      actions={actions}
      header={
        <>
          <Form.Item label="Ngày nhận" name="date" rules={[{ required: true, message: 'Chọn ngày nhận.' }]} extra="Không trước ngày xuất của bên bán.">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item label="Công ty bán">
            <Input disabled aria-label="Công ty bán" value={doc ? `${doc.counterCompanyCode ?? ''} – ${doc.counterCompanyName ?? ''}` : ''} />
          </Form.Item>
          <Form.Item label="Phiếu xuất bán">
            <Input disabled aria-label="Phiếu xuất bán" value={doc?.linkedDocumentNo ?? ''} />
          </Form.Item>
          <Form.Item label="Số hóa đơn">
            <Input disabled value={doc?.invoiceNo ?? ''} />
          </Form.Item>
          <Form.Item label="Người giao / phương tiện">
            <Input disabled value={doc?.deliverer ?? ''} />
          </Form.Item>
          <Form.Item label="Ghi chú / biên bản chênh lệch" name="note" rules={[{ max: 1000 }]}>
            <Input />
          </Form.Item>
        </>
      }
      lines={<LineGrid name="lines" columns={columns} readOnly={readOnly} fixedRows newLine={() => ({})} />}
      footer={
        <>
          <DocumentTotal label="Tiền hàng" value={formatMoney(totals.amount)} />
          <DocumentTotal label="Tiền VAT" value={formatMoney(totals.vat)} />
          <DocumentTotal label="Tổng giá trị nhận" value={formatMoney(totals.amount + totals.vat)} strong />
        </>
      }
    />
  )
}
