import { DownloadOutlined } from '@ant-design/icons'
import { Alert, Button, DatePicker, Form, Input, InputNumber, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useParams } from 'react-router'
import {
  documentStatusLabels,
  useGoodsReturnMutations,
  useInventoryDocument,
  useInventoryDocuments,
  useReturnableLines,
  type InventoryDocument,
  type SaveReturnInput,
} from '../../api/inventoryDocuments'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { formatDate, formatDateTime, formatMoney, formatQuantity, vnNumberInput } from '../../utils/format'
import { documentStatusTone } from './documentStatus'
import { useDocumentCommands } from './useDocumentCommands'

interface ReturnLineValues {
  id: string | null
  sourceLineId?: string
  qty?: number | null
  note?: string
}

interface ReturnFormValues {
  date: Dayjs
  sourceDocumentId?: string
  invoiceNo?: string
  note?: string
  lines: ReturnLineValues[]
}

const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Trang phiếu xuất trả hàng NCC (design.md mục 10.5) trên khuôn DocumentForm. Bắt buộc chọn phiếu nhập gốc đã ghi sổ; kho xuất,
 * NCC lấy theo phiếu gốc; mỗi dòng chọn một dòng của phiếu nhập – mặt hàng, ĐVT, lô, đơn giá (giá nhập gốc) lấy theo dòng đó,
 * chỉ nhập số lượng trả (≤ số còn trả được).
 */
export function GoodsReturnPage() {
  const { id = 'moi' } = useParams()
  const isNew = id === 'moi'
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<ReturnFormValues>()
  const document = useInventoryDocument(isNew ? null : id)
  const mutations = useGoodsReturnMutations()
  const [error, setError] = useState<string | null>(null)
  const addRef = useRef<(() => void) | null>(null)
  const doc = document.data
  const readOnly = isNew ? !hasPermission(Permission.InventoryDocumentCreate) : !doc?.canEdit

  // Phiếu nhập gốc chọn được: phiếu nhập đã ghi sổ trong 12 tháng gần nhất của kho được phép.
  const receipts = useInventoryDocuments({
    docType: 'GRN',
    status: 'Posted',
    from: dayjs().subtract(365, 'day').format('YYYY-MM-DD'),
    to: dayjs().format('YYYY-MM-DD'),
  })
  const sourceId = Form.useWatch('sourceDocumentId', form) as string | undefined
  const returnable = useReturnableLines(sourceId ?? null, isNew ? null : id)
  const sourceLines = useMemo(() => new Map((returnable.data ?? []).map((l) => [l.sourceLineId, l])), [returnable.data])
  const watchedLines = Form.useWatch('lines', form) as ReturnLineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const receipt = receipts.data?.items.find((r) => r.id === sourceId)

  useEffect(() => {
    if (!doc) return
    form.setFieldsValue({
      date: dayjs(doc.date),
      sourceDocumentId: doc.linkedDocumentId ?? undefined,
      invoiceNo: doc.invoiceNo ?? '',
      note: doc.note ?? '',
      lines: doc.lines.map((l) => ({ id: l.id, sourceLineId: l.sourceLineId ?? undefined, qty: l.qty, note: l.note ?? '' })),
    })
  }, [doc, form])

  /** Thành tiền, VAT của một dòng trả theo đơn giá, thuế suất của dòng nhập gốc (làm tròn như server). */
  const lineMoney = (line: ReturnLineValues | undefined) => {
    const source = line?.sourceLineId ? sourceLines.get(line.sourceLineId) : undefined
    const amount = Math.round((line?.qty ?? 0) * (source?.unitPrice ?? 0) * 100) / 100
    const rate = source?.vatCode && source.vatCode !== 'KCT' ? Number(source.vatCode) : 0
    return { amount, vat: Math.round(amount * rate) / 100 }
  }
  const totals = lines.reduce((sum, l) => {
    const m = lineMoney(l)
    return { amount: sum.amount + m.amount, vat: sum.vat + m.vat }
  }, { amount: 0, vat: 0 })

  const persist = async (): Promise<InventoryDocument | null> => {
    let values: ReturnFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng hàng.')
      return null
    }
    setError(null)
    const input: SaveReturnInput = {
      date: values.date.format('YYYY-MM-DD'),
      sourceDocumentId: values.sourceDocumentId!,
      invoiceNo: orNull(values.invoiceNo),
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({ id: l.id ?? null, sourceLineId: l.sourceLineId!, qty: l.qty ?? 0, note: orNull(l.note) })),
    }
    try {
      return isNew ? await mutations.create.mutateAsync(input) : await mutations.update.mutateAsync({ id, input })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu.')
      return null
    }
  }

  const { save, actions } = useDocumentCommands({
    isNew, doc, readOnly, persist, mutations, listPath: '/kho/rtv', noun: 'phiếu trả NCC', setError,
  })

  /** Điền mọi dòng còn trả được với số lượng = số còn trả được (trả hết), người dùng sửa bớt nếu cần. */
  const takeAll = () =>
    form.setFieldValue(
      'lines',
      (returnable.data ?? []).filter((l) => l.remainingQty > 0).map((l) => ({ id: null, sourceLineId: l.sourceLineId, qty: l.remainingQty, note: '' })),
    )

  const columns: LineColumn[] = [
    {
      key: 'source',
      title: 'Dòng phiếu nhập',
      width: 300,
      render: (field) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'sourceLineId']} noStyle rules={[{ required: true, message: 'Chọn dòng phiếu nhập.' }]}>
            <Select
              variant="borderless"
              style={{ width: '100%' }}
              aria-label={`Dòng phiếu nhập dòng ${field.name + 1}`}
              placeholder={sourceId ? 'Chọn dòng hàng đã nhập' : 'Chọn phiếu nhập gốc trước'}
              options={(returnable.data ?? []).map((l) => ({ value: l.sourceLineId, label: `${l.lineNo}. ${l.itemCode} – ${l.itemName}` }))}
              popupMatchSelectWidth={420}
            />
          </Form.Item>
        </>
      ),
    },
    {
      key: 'uom',
      title: 'ĐVT',
      width: 70,
      render: (field) => <span className="line-grid-readonly">{sourceLines.get(lines[field.name]?.sourceLineId ?? '')?.uomCode}</span>,
    },
    {
      key: 'lot',
      title: 'Số lô',
      width: 90,
      render: (field) => <span className="line-grid-readonly">{sourceLines.get(lines[field.name]?.sourceLineId ?? '')?.lotNo}</span>,
    },
    {
      key: 'remaining',
      // Số tối đa phiếu này được trả = SL nhập − SL các phiếu trả khác đã ghi sổ (không tính chính phiếu này).
      title: 'Trả tối đa',
      width: 100,
      align: 'right',
      render: (field) => {
        const source = sourceLines.get(lines[field.name]?.sourceLineId ?? '')
        return <span className="line-grid-readonly">{source ? formatQuantity(source.remainingQty) : ''}</span>
      },
    },
    {
      key: 'qty',
      title: 'SL trả',
      width: 100,
      align: 'right',
      render: (field) => {
        const source = sourceLines.get(lines[field.name]?.sourceLineId ?? '')
        return (
          <Form.Item
            name={[field.name, 'qty']}
            noStyle
            rules={[
              { required: true, type: 'number', min: 0.0001, message: 'SL > 0.' },
              { type: 'number', max: source?.remainingQty, message: 'Vượt số còn trả được.' },
            ]}
          >
            <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`SL trả dòng ${field.name + 1}`} />
          </Form.Item>
        )
      },
    },
    {
      key: 'price',
      title: 'Đơn giá gốc',
      width: 120,
      align: 'right',
      render: (field) => {
        const source = sourceLines.get(lines[field.name]?.sourceLineId ?? '')
        return <span className="line-grid-readonly">{source ? formatMoney(source.unitPrice) : ''}</span>
      },
    },
    {
      key: 'amount',
      title: 'Thành tiền',
      width: 120,
      align: 'right',
      render: (field) => <span className="line-grid-readonly">{formatMoney(lineMoney(lines[field.name]).amount)}</span>,
    },
    {
      key: 'vat',
      title: 'Tiền VAT',
      width: 100,
      align: 'right',
      render: (field) => <span className="line-grid-readonly">{formatMoney(lineMoney(lines[field.name]).vat)}</span>,
    },
    {
      key: 'note',
      title: 'Ghi chú',
      width: 160,
      render: (field) => (
        <Form.Item name={[field.name, 'note']} noStyle>
          <Input variant="borderless" maxLength={500} aria-label={`Ghi chú dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
  ]

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? documentStatusLabels[doc.status] : '', tone: doc ? documentStatusTone[doc.status] : ('neutral' as const) }
  const receiptOptions = (receipts.data?.items ?? []).map((r) => ({
    value: r.id,
    label: `${r.no} – ${r.supplierCode ?? ''} – ${formatDate(r.date)}`,
  }))
  if (doc?.linkedDocumentId && !receiptOptions.some((o) => o.value === doc.linkedDocumentId))
    receiptOptions.push({ value: doc.linkedDocumentId, label: doc.linkedDocumentNo ?? '' })

  return (
    <DocumentForm
      title="Phiếu xuất trả hàng NCC"
      number={isNew ? null : (doc?.no ?? '')}
      status={status}
      meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
      loading={document.isLoading}
      error={error ?? (document.error ? document.error.message : null)}
      notice={doc?.readOnlyReason && <Alert type="info" showIcon title={doc.readOnlyReason} />}
      form={form}
      initialValues={{ date: dayjs(), lines: [] }}
      readOnly={readOnly}
      onSave={save}
      onAddLine={() => addRef.current?.()}
      actions={actions}
      header={
        <>
          <Form.Item label="Ngày trả" name="date" rules={[{ required: true, message: 'Chọn ngày trả.' }]} extra="Quyết định kỳ kho khi ghi sổ.">
            <DatePicker format="DD/MM/YYYY" style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item
            label="Phiếu nhập gốc"
            name="sourceDocumentId"
            rules={[{ required: true, message: 'Chọn phiếu nhập gốc.' }]}
            extra="Phiếu nhập đã ghi sổ; kho xuất và NCC lấy theo phiếu này."
          >
            <Select
              showSearch={{ optionFilterProp: 'label' }}
              options={receiptOptions}
              onChange={() => form.setFieldValue('lines', [])}
              placeholder="Gõ số phiếu hoặc mã NCC"
            />
          </Form.Item>
          <Form.Item label="Kho xuất / NCC">
            <Input
              disabled
              aria-label="Kho xuất / NCC"
              value={receipt ? `${receipt.warehouseCode ?? ''} / ${receipt.supplierCode ?? ''} – ${receipt.supplierName ?? ''}` : (doc ? `${doc.supplierCode ?? ''} – ${doc.supplierName ?? ''}` : '')}
            />
          </Form.Item>
          <Form.Item label="Số hóa đơn điều chỉnh" name="invoiceNo" rules={[{ max: 50 }]}>
            <Input autoComplete="off" />
          </Form.Item>
          <Form.Item label="Lý do trả" name="note" rules={[{ max: 1000 }]} className="document-form-wide">
            <Input />
          </Form.Item>
        </>
      }
      lines={
        <>
          {!readOnly && (
            <Button icon={<DownloadOutlined />} onClick={takeAll} disabled={!returnable.data} loading={returnable.isFetching} style={{ marginBottom: 6 }}>
              Lấy mọi dòng còn trả được
            </Button>
          )}
          <LineGrid name="lines" columns={columns} readOnly={readOnly} addRef={addRef} newLine={() => ({ id: null, qty: 1 })} />
        </>
      }
      footer={
        <>
          <DocumentTotal label="Tiền hàng" value={formatMoney(totals.amount)} />
          <DocumentTotal label="Tiền VAT" value={formatMoney(totals.vat)} />
          <DocumentTotal label="Tổng giá trị trả" value={formatMoney(totals.amount + totals.vat)} strong />
        </>
      }
    />
  )
}
