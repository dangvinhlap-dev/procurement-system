import { useDocumentFormSync } from '../../components/DocumentForm/useDocumentFormSync'
import { DollarOutlined, FileAddOutlined, FileDoneOutlined, InboxOutlined, WarningFilled } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Form, Input, InputNumber, Modal, Space, Table, Tooltip } from 'antd'
import { Select } from '../../classic/Select'
import dayjs, { type Dayjs } from 'dayjs'
import { Fragment, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useItemLookup, useSupplierLookup } from '../../api/inventoryDocuments'
import { supplierItemKey, useSupplierItemLookup, vatOptions } from '../../api/masterData'
import { useDepartments, useWarehouses } from '../../api/organization'
import { aboveGroupMinPct, pickSupplierPrice, useGroupMinPrices, useSupplierPrices } from '../../api/pricing'
import {
  priceSourceLabels,
  purchaseStatusText,
  withPendingApprovers,
  purchaseTypeLabels,
  useCompletePoDocs,
  purchaseStatusTone,
  useOpenRequestLines,
  usePurchaseMutations,
  usePurchaseOrder,
  type OpenRequestLine,
  type PurchaseOrder,
  type PurchaseType,
  type SavePurchaseOrderInput,
  PURCHASE_STAGE_LABELS,
} from '../../api/purchasing'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DocumentForm, DocumentTotal } from '../../components/DocumentForm/DocumentForm'
import { LineGrid, type LineColumn } from '../../components/DocumentForm/LineGrid'
import { formatDate, formatDateTime, formatMoney, formatQuantity, vnNumberInput, DATE_INPUT_FORMATS } from '../../utils/format'
import { ApprovalHistory } from './ApprovalHistory'
import { BudgetNotice } from './BudgetNotice'
import { ClosedRemainingNotice } from './ClosedRemainingNotice'
import { useApprovalCommands } from './useApprovalCommands'
import { DocNo } from '../../classic/DocNo'

interface PoLineValues {
  id: string | null
  itemId?: string
  uomId?: string
  qty?: number | null
  unitPrice?: number | null
  discountPct?: number | null
  vatCode?: string | null
  deliveryDate?: Dayjs | null
  note?: string
  sourcePrLineId?: string | null
  priceDeviationReason?: string
  sourceRfqLineId?: string | null
}

/**
 * Giá sau chiết khấu, làm tròn 6 số như server (PurchaseOrderLine.NetUnitPrice): giá × (100 − %CK) / 100.
 * Ví dụ 50.000 − 5% → 47.500.
 */
const netPrice = (price: number, discountPct: number) => Math.round(price * (100 - discountPct) * 10_000) / 1_000_000

interface PoFormValues {
  date: Dayjs
  departmentId?: string | null
  purchaseType: PurchaseType
  emergencyReason?: string
  docsDueDate?: Dayjs | null
  supplierId?: string
  warehouseId?: string
  expectedDate?: Dayjs | null
  paymentTerms?: string
  deliveryTerms?: string
  deliveryAddress?: string
  receiveTolerancePct?: number
  note?: string
  lines: PoLineValues[]
}

/** Chuỗi rỗng hoặc toàn khoảng trắng gửi lên API là null; còn lại bỏ khoảng trắng hai đầu. */
const orNull = (value: string | null | undefined) => (value?.trim() ? value.trim() : null)

/**
 * Tính một dòng giống server (PurchaseOrderLine.Set): tiền hàng = SL × giá; CK = tiền hàng × %; thành tiền = tiền hàng − CK;
 * VAT = thành tiền × thuế suất; mỗi bước làm tròn 2 số. Ví dụ 10 × 100.000, CK 5%, VAT 10% → 950.000; 95.000.
 */
function computeLine(line: PoLineValues | undefined) {
  const gross = Math.round((line?.qty ?? 0) * (line?.unitPrice ?? 0) * 100) / 100
  const discount = Math.round(gross * (line?.discountPct ?? 0)) / 100
  const amount = Math.round((gross - discount) * 100) / 100
  const rate = line?.vatCode && line.vatCode !== 'KCT' ? Number(line.vatCode) : 0
  return { gross, discount, amount, vat: Math.round(amount * rate) / 100 }
}

/**
 * Trang đơn mua hàng (design.md mục 10.3) trên khuôn DocumentForm: NCC đã duyệt và áp dụng tại công ty, kho nhận (mặc định kho tổng),
 * dòng nhập tay hoặc "Lấy dòng từ PR" (PR đã duyệt còn chưa đặt), chiết khấu, VAT, dung sai nhận hàng. Chỉ VND ở giai đoạn này.
 * Phiếu đã duyệt hiện SL đã nhận, còn lại theo phiếu nhập kho. Giá đã duyệt (design.md mục 18.10): cột "Giá đã duyệt" và "Nguồn giá"
 * theo kết quả chọn thầu / hợp đồng nguyên tắc / bảng giá NCC hiệu lực; nút "Áp giá đã duyệt" chép giá vào các dòng. Người lập vẫn sửa
 * được đơn giá, nhưng dòng lệch giá đã duyệt (tam giác vàng) phải ghi lý do và PO qua thêm chặng duyệt ngoại lệ giá.
 */
export function PurchaseOrderPage() {
  const { id = 'moi' } = useParams()
  const navigate = useNavigate()
  const isNew = id === 'moi'
  const { hasPermission } = useAuth()
  const { message, modal } = App.useApp()
  const [form] = Form.useForm<PoFormValues>()
  const order = usePurchaseOrder(isNew ? null : id)
  const mutations = usePurchaseMutations<PurchaseOrder, SavePurchaseOrderInput>('purchase-orders')
  const warehouses = useWarehouses()
  const [error, setError] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [picking, setPicking] = useState(false)
  const addRef = useRef<(() => void) | null>(null)
  const doc = order.data
  const readOnly = isNew ? !hasPermission(Permission.PoCreate) : !doc?.actions.canEdit
  // Đính kèm được cả khi phiếu đã duyệt/ghi sổ (scan hóa đơn, biên bản) – quyền theo quyền lập phiếu, design.md mục 18.4.
  const attachmentTab = useAttachmentTab('PurchaseOrder', doc?.id, hasPermission(Permission.PoCreate))
  const suppliers = useSupplierLookup(doc?.supplierId ? [doc.supplierId] : [])
  // Ô NCC chỉ liệt kê NCC đã duyệt và đang áp dụng tại công ty (design.md mục 5.9): không có NCC nào thì nói rõ lý do và chỗ xử lý,
  // tránh người dùng tưởng lỗi vì đã tạo NCC nhưng chưa duyệt hoặc chưa áp dụng cho công ty này.
  const noEligibleSupplier = !readOnly && suppliers.isSuccess && suppliers.data.length === 0
  const supplierHint = (
    <>
      Chưa có nhà cung cấp chọn được. Chỉ NCC <b>đã duyệt</b> và <b>đang áp dụng tại công ty đang làm việc</b> mới đặt hàng được – mở{' '}
      <Link to="/gia-ncc/nha-cung-cap">Danh mục nhà cung cấp</Link> để duyệt NCC và thêm công ty áp dụng.
    </>
  )

  const watchedLines = Form.useWatch('lines', form) as PoLineValues[] | undefined
  const lines = useMemo(() => watchedLines ?? [], [watchedLines])
  const itemIds = useMemo(() => lines.map((l) => l?.itemId).filter((x): x is string => !!x), [lines])
  const lookup = useItemLookup(search, itemIds)
  const itemsById = useMemo(() => new Map((lookup.data ?? []).map((i) => [i.id, i])), [lookup.data])
  const mainWarehouse = warehouses.data?.find((w) => w.type === 'Main' && w.isActive)?.id
  const supplierId = Form.useWatch('supplierId', form) as string | undefined
  const orderDate = Form.useWatch('date', form) as Dayjs | undefined
  const departmentId = Form.useWatch('departmentId', form) as string | null | undefined
  const purchaseType = Form.useWatch('purchaseType', form) as PurchaseType | undefined
  const completeDocs = useCompletePoDocs()

  /** Hộp thoại xác nhận đã bổ sung hồ sơ PO khẩn cấp: ghi chú bắt buộc (hồ sơ đã đính kèm ở tab Đính kèm). */
  const confirmCompleteDocs = () => {
    if (!doc) return
    let note = ''
    modal.confirm({
      title: `Xác nhận đã bổ sung hồ sơ đơn ${doc.no}?`,
      content: (
        <>
          <label htmlFor="docs-note">Hồ sơ đã bổ sung (bắt buộc)</label>
          <Input.TextArea id="docs-note" rows={3} maxLength={500} placeholder="Ví dụ: biên bản sự cố, 3 báo giá so sánh – đã đính kèm"
            onChange={(e) => (note = e.target.value)} />
        </>
      ),
      okText: 'Xác nhận',
      cancelText: 'Không',
      onOk: async () => {
        if (!note.trim()) {
          message.error('Hãy ghi hồ sơ đã bổ sung.')
          throw new Error('missing note')
        }
        try {
          await completeDocs.mutateAsync({ id: doc.id, note: note.trim() })
          message.success(`Đã xác nhận bổ sung hồ sơ đơn ${doc.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không xác nhận được.')
        }
      },
    })
  }
  const supplierPrices = useSupplierPrices(supplierId, orderDate?.format('YYYY-MM-DD'), itemIds, departmentId)
  const supplierItems = useSupplierItemLookup(supplierId ? [supplierId] : [])
  const groupMin = useGroupMinPrices(itemIds, doc?.id)
  const departments = useDepartments()
  // Chỉ chọn bộ phận đang dùng; bộ phận phiếu đã chọn nay ngừng dùng vẫn hiện tên (như PR – design.md mục 18.5).
  const departmentOptions = useMemo(
    () =>
      (departments.data ?? [])
        .filter((d) => d.isActive || d.id === doc?.departmentId)
        .map((d) => ({ value: d.id, label: `${d.code} – ${d.name}${d.isActive ? '' : ' (ngừng dùng)'}` })),
    [departments.data, doc?.departmentId],
  )

  /** Giá gợi ý của dòng theo bảng giá hợp đồng/bảng giá NCC: đúng ĐVT và bậc SL (xem pickSupplierPrice). */
  const suggestedPrice = (line: PoLineValues | undefined) => {
    if (!line?.itemId || !line.uomId) return undefined
    const tiers = (supplierPrices.data ?? []).filter((t) => t.itemId === line.itemId)
    const factor = itemsById.get(line.itemId)?.uoms.find((u) => u.uomId === line.uomId)?.factor ?? 1
    return tiers.length ? pickSupplierPrice(tiers, line.uomId, factor, line.qty ?? 0) : undefined
  }

  /**
   * Giá đã duyệt của dòng để hiện và so lệch: phiếu chỉ đọc lấy giá server đã chốt; dòng từ kết quả chọn thầu giữ giá trúng thầu đã lưu;
   * còn lại theo bảng giá hợp đồng/bảng giá NCC hiện hành (server tính lại khi lưu, gửi).
   */
  const approvedPrice = (index: number) => {
    const saved = savedLine(index)
    const line = lines[index]
    if (readOnly || (line?.sourceRfqLineId && saved?.priceSource === 'Rfq')) {
      if (!saved) return undefined
      if (saved.approvedUnitPrice == null)
        return saved.priceSource === 'None' ? { unitPrice: null, discountPct: 0, sourceText: priceSourceLabels.None } : undefined
      // Phiếu gốc của nguồn giá đã lưu – hiện bằng ô số chứng từ (bấm sao chép, bấm đúp mở – design.md mục 20.1 R5).
      const ref =
        saved.priceSource === 'Rfq' ? { no: saved.sourceRfqNo, docType: 'RFQ', id: saved.sourceRfqId }
        : saved.priceSource === 'Contract' ? { no: saved.contractNo, docType: 'CTR', id: saved.contractId }
        : saved.priceSource === 'MonthlyApproval' ? { no: saved.monthlyApprovalNo, docType: 'GTH', id: saved.monthlyApprovalId }
        : { no: saved.priceListNo, docType: 'PRL', id: saved.priceListId }
      return {
        unitPrice: saved.approvedUnitPrice,
        discountPct: saved.approvedDiscountPct ?? 0,
        sourceText: `${priceSourceLabels[saved.priceSource]} ${ref.no ?? ''}`.trim(),
        sourceLabel: priceSourceLabels[saved.priceSource],
        ref,
      }
    }
    if (!line?.itemId || !line.uomId || !supplierId) return undefined
    const p = suggestedPrice(line)
    if (!p) return supplierPrices.isSuccess ? { unitPrice: null, discountPct: 0, sourceText: priceSourceLabels.None } : undefined
    const sourceText =
      p.source === 'MonthlyApproval' ? `Duyệt tháng ${p.priceListNo}` : p.source === 'Contract' ? `Hợp đồng ${p.contractNo ?? ''}` : `Bảng giá ${p.priceListNo}`
    return { unitPrice: p.unitPrice, discountPct: p.discountPct, sourceText }
  }

  /** Dòng có giá đã duyệt mà giá sau CK khác – cần lý do lệch giá (so như server: sau chiết khấu, 6 số lẻ). */
  const isDeviation = (index: number) => {
    const a = approvedPrice(index)
    const line = lines[index]
    if (!a || a.unitPrice == null || !line) return false
    return netPrice(line.unitPrice ?? 0, line.discountPct ?? 0) !== netPrice(a.unitPrice, a.discountPct)
  }

  /**
   * So giá dòng với giá thấp nhất tập đoàn (design.md mục 8.5, 10.3): chỉ cảnh báo, không chặn. Cao hơn quá % cảnh báo của công ty
   * (tham số SEC, mặc định 10%) thì tam giác vàng; so giá sau CK quy về ĐVT cơ sở.
   */
  const highPrice = (index: number) => {
    const line = lines[index]
    if (!line?.itemId || line.unitPrice == null || !groupMin.data) return undefined
    const min = groupMin.data.items.find((m) => m.itemId === line.itemId)
    if (!min) return undefined
    const factor = savedLine(index)?.uomFactor ?? itemsById.get(line.itemId)?.uoms.find((u) => u.uomId === line.uomId)?.factor ?? 1
    const pct = aboveGroupMinPct(netPrice(line.unitPrice, line.discountPct ?? 0), factor, min.basePrice)
    return { min, factor, pct, warn: pct != null && pct > groupMin.data.warnPercent, warnPercent: groupMin.data.warnPercent }
  }

  /** Chép giá đã duyệt (đơn giá, % CK, thuế suất) vào mọi dòng có giá hợp đồng/bảng giá (dòng từ kết quả chọn thầu giữ nguyên). */
  const applyPriceList = () => {
    const current = (form.getFieldValue('lines') as PoLineValues[] | undefined) ?? []
    let count = 0
    const next = current.map((l) => {
      if (l.sourceRfqLineId) return l
      const p = suggestedPrice(l)
      if (!p) return l
      count++
      return { ...l, unitPrice: p.unitPrice, discountPct: p.discountPct, vatCode: p.vatCode ?? l.vatCode }
    })
    form.setFieldValue('lines', next)
    if (count) message.success(`Đã áp giá đã duyệt cho ${count} dòng.`)
    else message.info('Không có dòng nào có giá trong hợp đồng/bảng giá đang hiệu lực của NCC này.')
  }

  /**
   * Dòng vừa chọn mặt hàng / đổi ĐVT chờ tự điền giá đã duyệt (yêu cầu người dùng 11/11/2026 – design.md mục 18.10): chỉ số dòng →
   * mặt hàng, ĐVT và đơn giá lúc đổi. Giá đã duyệt của mặt hàng mới về sau (API tải lại theo danh sách mặt hàng) nên điền trong effect;
   * bỏ qua nếu dòng đã đổi mặt hàng/ĐVT khác hoặc người dùng đã tự gõ đơn giá trong lúc chờ.
   */
  const pendingAutoPrice = useRef(new Map<number, { itemId: string; uomId: string; unitPrice: number | null | undefined }>())
  const queueAutoPrice = (index: number, itemId: string | undefined, uomId: string | undefined) => {
    if (!itemId || !uomId) return
    const line = form.getFieldValue(['lines', index]) as PoLineValues | undefined
    pendingAutoPrice.current.set(index, { itemId, uomId, unitPrice: line?.unitPrice })
  }

  useEffect(() => {
    const pending = pendingAutoPrice.current
    // Chờ giá của đúng danh sách mặt hàng hiện tại (không dùng dữ liệu cũ giữ tạm khi đang tải lại).
    if (readOnly || pending.size === 0 || !supplierPrices.isSuccess || supplierPrices.isFetching || supplierPrices.isPlaceholderData) return
    for (const [index, want] of pending) {
      pending.delete(index)
      const line = form.getFieldValue(['lines', index]) as PoLineValues | undefined
      if (!line || line.itemId !== want.itemId || line.uomId !== want.uomId || line.unitPrice !== want.unitPrice || line.sourceRfqLineId) continue
      const p = suggestedPrice(line)
      if (!p) continue
      form.setFieldValue(['lines', index, 'unitPrice'], p.unitPrice)
      form.setFieldValue(['lines', index, 'discountPct'], p.discountPct)
      if (p.vatCode) form.setFieldValue(['lines', index, 'vatCode'], p.vatCode)
    }
    // suggestedPrice đọc supplierPrices.data – chạy lại khi dữ liệu giá về.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [supplierPrices.data, supplierPrices.isFetching, supplierPrices.isPlaceholderData, readOnly, form, lines])

  useEffect(() => {
    if (isNew && mainWarehouse && !form.getFieldValue('warehouseId')) form.setFieldValue('warehouseId', mainWarehouse)
  }, [isNew, mainWarehouse, form])

  const { loadSaved, targetId } = useDocumentFormSync(form, id, doc, (doc) => ({
    date: dayjs(doc.date),
    departmentId: doc.departmentId,
    purchaseType: doc.purchaseType,
    emergencyReason: doc.emergencyReason ?? '',
    docsDueDate: doc.docsDueDate ? dayjs(doc.docsDueDate) : null,
    supplierId: doc.supplierId,
    warehouseId: doc.warehouseId,
    expectedDate: doc.expectedDate ? dayjs(doc.expectedDate) : null,
    paymentTerms: doc.paymentTerms ?? '',
    deliveryTerms: doc.deliveryTerms ?? '',
    deliveryAddress: doc.deliveryAddress ?? '',
    receiveTolerancePct: doc.receiveTolerancePct,
    note: doc.note ?? '',
    lines: doc.lines.map((l) => ({
      id: l.id, itemId: l.itemId, uomId: l.uomId, qty: l.qty, unitPrice: l.unitPrice, discountPct: l.discountPct, vatCode: l.vatCode,
      deliveryDate: l.deliveryDate ? dayjs(l.deliveryDate) : null, note: l.note ?? '', sourcePrLineId: l.sourcePrLineId,
      priceDeviationReason: l.priceDeviationReason ?? '', sourceRfqLineId: l.sourceRfqLineId,
    })),
  }))

  const totals = lines.reduce(
    (sum, l) => {
      const c = computeLine(l)
      return { gross: sum.gross + c.gross, discount: sum.discount + c.discount, amount: sum.amount + c.amount, vat: sum.vat + c.vat }
    },
    { gross: 0, discount: 0, amount: 0, vat: 0 },
  )

  const persist = async (): Promise<PurchaseOrder | null> => {
    let values: PoFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở đầu phiếu và dòng hàng.')
      return null
    }
    setError(null)
    const input: SavePurchaseOrderInput = {
      date: values.date.format('YYYY-MM-DD'),
      departmentId: values.departmentId ?? null,
      purchaseType: values.purchaseType ?? 'Normal',
      emergencyReason: values.purchaseType === 'Emergency' ? orNull(values.emergencyReason) : null,
      docsDueDate: values.purchaseType === 'Emergency' && values.docsDueDate ? values.docsDueDate.format('YYYY-MM-DD') : null,
      supplierId: values.supplierId!,
      warehouseId: values.warehouseId!,
      expectedDate: values.expectedDate ? values.expectedDate.format('YYYY-MM-DD') : null,
      paymentTerms: orNull(values.paymentTerms),
      deliveryTerms: orNull(values.deliveryTerms),
      deliveryAddress: orNull(values.deliveryAddress),
      receiveTolerancePct: values.receiveTolerancePct ?? 0,
      note: orNull(values.note),
      lines: (values.lines ?? []).map((l) => ({
        id: l.id ?? null, itemId: l.itemId!, uomId: l.uomId!, qty: l.qty ?? 0, unitPrice: l.unitPrice ?? 0, discountPct: l.discountPct ?? 0,
        vatCode: l.vatCode ?? null, deliveryDate: l.deliveryDate ? l.deliveryDate.format('YYYY-MM-DD') : null, note: orNull(l.note),
        sourcePrLineId: l.sourcePrLineId ?? null, priceDeviationReason: orNull(l.priceDeviationReason), sourceRfqLineId: l.sourceRfqLineId ?? null,
      })),
    }
    try {
      const target = targetId()
      const saved = target ? await mutations.update.mutateAsync({ id: target, input }) : await mutations.create.mutateAsync(input)
      loadSaved(saved)
      return saved
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu.')
      return null
    }
  }

  const { save, actions } = useApprovalCommands({
    isNew, doc, readOnly, persist, mutations, listPath: '/mua-hang/po', noun: 'đơn mua hàng', setError, docType: 'PO',
  })

  /** Thêm các dòng PR đã chọn: SL = SL còn chưa đặt, đơn giá = giá tham khảo của PR, thuế suất theo mặt hàng. */
  const addFromRequests = (picked: OpenRequestLine[]) => {
    const current = (form.getFieldValue('lines') as PoLineValues[] | undefined) ?? []
    const existing = new Set(current.map((l) => l.sourcePrLineId).filter(Boolean))
    const added = picked
      .filter((p) => !existing.has(p.prLineId))
      .map((p) => ({
        id: null, itemId: p.itemId, uomId: p.uomId, qty: p.remainingQty, unitPrice: p.estPrice, discountPct: 0, vatCode: p.vatCode,
        deliveryDate: p.neededDate ? dayjs(p.neededDate) : null, note: '', sourcePrLineId: p.prLineId,
      }))
    form.setFieldValue('lines', [...current, ...added])
    if (!form.getFieldValue('supplierId')) {
      const suggested = picked.find((p) => p.suggestedSupplierId)?.suggestedSupplierId
      if (suggested && (suppliers.data ?? []).some((s) => s.id === suggested)) form.setFieldValue('supplierId', suggested)
    }
    setPicking(false)
  }

  const savedLine = (index: number) => (lines[index]?.id ? doc?.lines.find((l) => l.id === lines[index].id) : undefined)
  // Mã hàng theo NCC: PO nháp hiện mã đang khai báo ở NCC – sản phẩm (máy chủ chép đúng mã này khi lưu/gửi); PO đã gửi hiện mã đã chép.
  const supplierItemCode = (index: number) =>
    !doc || doc.status === 'Draft'
      ? (lines[index]?.itemId ? supplierItems.data?.get(supplierItemKey(supplierId ?? '', lines[index].itemId!))?.supplierItemCode : null)
      : savedLine(index)?.supplierItemCode
  const hasDeviation = lines.some((_, i) => isDeviation(i))
  const readonlyCell = (text: string) => <span className="line-grid-readonly">{text}</span>
  const showReceiving = doc && doc.status !== 'Draft'

  const columns: LineColumn[] = [
    {
      key: 'item',
      title: 'Mặt hàng', required: true,
      width: 250,
      render: (field) => (
        <>
          <Form.Item name={[field.name, 'id']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'sourcePrLineId']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'sourceRfqLineId']} hidden>
            <Input />
          </Form.Item>
          <Form.Item name={[field.name, 'itemId']} noStyle rules={[{ required: true, message: 'Chọn mặt hàng.' }]}>
            <Select
              variant="borderless"
              style={{ width: '100%' }}
              aria-label={`Mặt hàng dòng ${field.name + 1}`}
              disabled={!!lines[field.name]?.sourcePrLineId}
              showSearch={{ filterOption: false, onSearch: setSearch }}
              placeholder="Gõ mã hoặc tên hàng"
              options={(lookup.data ?? []).map((i) => ({ value: i.id, label: `${i.code} – ${i.name}` }))}
              onChange={(v: string) => {
                // ĐVT mua khai báo ở NCC – sản phẩm đi trước ĐVT cơ sở (design.md mục 5.9); ĐVT đó đã bị gỡ khỏi mặt hàng thì về ĐVT cơ sở.
                const item = itemsById.get(v)
                const purchaseUomId = supplierId ? supplierItems.data?.get(supplierItemKey(supplierId, v))?.purchaseUomId : undefined
                const uomId = purchaseUomId && item?.uoms.some((u) => u.uomId === purchaseUomId) ? purchaseUomId : item?.baseUomId
                form.setFieldValue(['lines', field.name, 'uomId'], uomId)
                form.setFieldValue(['lines', field.name, 'vatCode'], item?.vatCode)
                queueAutoPrice(field.name, v, uomId)
              }}
              popupMatchSelectWidth={420}
            />
          </Form.Item>
        </>
      ),
    },
    { key: 'supplierItemCode', title: 'Mã hàng NCC', width: 100, render: (field) => readonlyCell(supplierItemCode(field.name) ?? '') },
    {
      key: 'uom',
      title: 'ĐVT', required: true,
      width: 75,
      render: (field) => {
        const item = lines[field.name]?.itemId ? itemsById.get(lines[field.name].itemId!) : undefined
        return (
          <Form.Item name={[field.name, 'uomId']} noStyle rules={[{ required: true, message: 'Chọn ĐVT.' }]}>
            <Select variant="borderless" style={{ width: '100%' }} aria-label={`ĐVT dòng ${field.name + 1}`}
              onChange={(uomId: string) => queueAutoPrice(field.name, lines[field.name]?.itemId ?? undefined, uomId)}
              options={(item?.uoms ?? []).map((u) => ({ value: u.uomId, label: u.code }))} />
          </Form.Item>
        )
      },
    },
    {
      key: 'qty',
      title: 'SL đặt', required: true,
      width: 90,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'qty']} noStyle rules={[{ required: true, type: 'number', min: 0.0001, message: 'SL > 0.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} precision={4} aria-label={`SL đặt dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    // SL thực nhận (từ phiếu XNH đã xác nhận – design.md mục 19.4) đặt ngay sau SL đặt: để cuối lưới 17 cột thì bị đẩy khỏi khung nhìn,
    // người dùng tưởng PO chưa ghi nhận nhận hàng (phản ánh khi test 0.22.0, 11/11/2026).
    ...(showReceiving
      ? ([
          { key: 'received', title: 'SL thực nhận', width: 90, align: 'right', render: (_, index) => readonlyCell(formatQuantity(savedLine(index)?.receivedQty ?? 0)) },
          { key: 'remaining', title: 'Còn lại', width: 80, align: 'right', render: (_, index) => readonlyCell(formatQuantity(savedLine(index)?.remainingQty ?? 0)) },
        ] as LineColumn[])
      : []),
    {
      key: 'price',
      title: 'Đơn giá', required: true,
      width: 115,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'unitPrice']} noStyle rules={[{ required: true, type: 'number', min: 0, message: 'Nhập đơn giá.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} aria-label={`Đơn giá dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'discount',
      title: '% CK',
      width: 65,
      align: 'right',
      render: (field) => (
        <Form.Item name={[field.name, 'discountPct']} noStyle rules={[{ type: 'number', min: 0, max: 100, message: '0–100.' }]}>
          <InputNumber {...vnNumberInput} variant="borderless" style={{ width: '100%' }} min={0} max={100} aria-label={`Chiết khấu dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'vat',
      title: 'VAT',
      width: 75,
      render: (field) => (
        <Form.Item name={[field.name, 'vatCode']} noStyle>
          <Select variant="borderless" style={{ width: '100%' }} allowClear aria-label={`VAT dòng ${field.name + 1}`}
            options={vatOptions.map((o) => ({ value: o.value, label: o.value === 'KCT' ? 'KCT' : o.label }))} />
        </Form.Item>
      ),
    },
    { key: 'amount', title: 'Thành tiền', width: 115, align: 'right', render: (field) => readonlyCell(formatMoney(computeLine(lines[field.name]).amount)) },
    {
      key: 'approvedPrice',
      title: 'Giá đã duyệt',
      width: 120,
      align: 'right',
      render: (field) => {
        const p = approvedPrice(field.name)
        const deviation = isDeviation(field.name)
        return (
          <span className="line-grid-readonly">
            {deviation && (
              <Tooltip title="Đơn giá khác giá đã duyệt – phải ghi lý do lệch giá; PO qua thêm chặng duyệt ngoại lệ giá.">
                <WarningFilled className="line-grid-alert-icon" aria-label={`Lệch giá đã duyệt dòng ${field.name + 1}`} />{' '}
              </Tooltip>
            )}
            {p?.unitPrice != null ? `${formatMoney(p.unitPrice)}${p.discountPct ? ` −${p.discountPct}%` : ''}` : ''}
          </span>
        )
      },
    },
    {
      key: 'groupMin',
      title: 'Giá thấp nhất TĐ',
      width: 125,
      align: 'right',
      render: (field) => {
        const h = highPrice(field.name)
        if (!h) return readonlyCell('')
        const m = h.min
        const where = m.companyCode
          ? `${m.source === 'Receipt' ? 'Phiếu nhập' : 'PO'} ${m.documentNo ?? ''} – ${m.companyCode}, NCC ${m.supplierCode ?? ''}, ngày ${formatDate(m.date)}`
          : `ngày ${formatDate(m.date)} (công ty, NCC chỉ người có quyền xem giá toàn tập đoàn thấy)`
        // Một chú thích cho cả ô (cảnh báo đứng trước) – hai Tooltip lồng nhau sẽ hiện chồng lên nhau.
        const warning = h.warn
          ? `Đơn giá cao hơn giá thấp nhất tập đoàn ${formatQuantity(h.pct)}% (ngưỡng cảnh báo ${formatQuantity(h.warnPercent)}%). ` +
            'Chỉ cảnh báo – kiểm tra lại giá trước khi gửi duyệt. '
          : ''
        return (
          <Tooltip title={`${warning}Giá mua thấp nhất tập đoàn 12 tháng (sau CK, trước VAT, theo ĐVT dòng): ${where}.`}>
            <span className="line-grid-readonly">
              {h.warn && (
                <>
                  <WarningFilled className="line-grid-alert-icon" aria-label={`Giá cao dòng ${field.name + 1}`} />{' '}
                </>
              )}
              {formatMoney(Math.round(m.basePrice * h.factor * 1_000_000) / 1_000_000)}
            </span>
          </Tooltip>
        )
      },
    },
    {
      key: 'priceSource',
      title: 'Nguồn giá',
      width: 150,
      // Phiếu NCC độc quyền, mua ngoài danh sách phiếu tháng ghi nhận lúc lưu (design.md mục 18.10, 18.25) – hiện kèm nguồn giá.
      render: (field, index) => {
        const saved = savedLine(index)
        const price = approvedPrice(field.name)
        const parts: ReactNode[] = []
        // Nguồn giá đã lưu có số phiếu gốc → ô số chứng từ; dòng đang nhập (giá gợi ý) chỉ có chữ.
        if (price && 'ref' in price && price.ref?.no)
          parts.push(<>{price.sourceLabel} <DocNo no={price.ref.no} docType={price.ref.docType} id={price.ref.id} /></>)
        else if (price?.sourceText) parts.push(price.sourceText)
        if (saved?.isOffMonthlyList)
          parts.push(<>Ngoài danh sách {saved.monthlyApprovalNo && <DocNo no={saved.monthlyApprovalNo} docType="GTH" id={saved.monthlyApprovalId} />}</>)
        if (saved?.isOverCeiling && saved.ceilingPrice !== null) parts.push(`Vượt giá trần ${formatMoney(saved.ceilingPrice)}`)
        if (saved?.soleSourceNo) parts.push(<>Độc quyền <DocNo no={saved.soleSourceNo} docType="NDQ" id={saved.soleSourceApprovalId} /></>)
        return (
          <span className="line-grid-readonly">
            {parts.map((p, i) => (
              <Fragment key={i}>
                {i > 0 && ' · '}
                {p}
              </Fragment>
            ))}
          </span>
        )
      },
    },
    {
      key: 'deviationReason',
      title: 'Lý do lệch giá',
      width: 180,
      render: (field) =>
        isDeviation(field.name) || lines[field.name]?.priceDeviationReason ? (
          <Form.Item name={[field.name, 'priceDeviationReason']} noStyle rules={[{ max: 500, message: 'Tối đa 500 ký tự.' }]}>
            <Input variant="borderless" placeholder="Bắt buộc khi gửi duyệt" aria-label={`Lý do lệch giá dòng ${field.name + 1}`} />
          </Form.Item>
        ) : (
          <Form.Item name={[field.name, 'priceDeviationReason']} noStyle>
            <Input variant="borderless" disabled aria-label={`Lý do lệch giá dòng ${field.name + 1}`} />
          </Form.Item>
        ),
    },
    {
      key: 'delivery',
      title: 'Ngày giao',
      width: 115,
      render: (field) => (
        <Form.Item name={[field.name, 'deliveryDate']} noStyle>
          <DatePicker variant="borderless" format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label={`Ngày giao dòng ${field.name + 1}`} />
        </Form.Item>
      ),
    },
    {
      key: 'pr', title: 'PR nguồn', width: 130,
      render: (_, index) => {
        const saved = savedLine(index)
        return saved?.sourcePrNo
          ? <DocNo no={saved.sourcePrNo} docType="PR" id={saved.sourcePrId} />
          : readonlyCell(lines[index]?.sourcePrLineId ? '(từ PR)' : '')
      },
    },
  ]

  /** Trạng thái hồ sơ khẩn cấp hiện ở đầu phiếu (design.md mục 18.11). */
  const emergencyNotice =
    doc?.purchaseType === 'Emergency' && doc.status !== 'Draft' && doc.status !== 'Cancelled' ? (
      doc.docsCompletedAt ? (
        <Alert type="success" showIcon
          title={`Mua khẩn cấp – đã bổ sung hồ sơ ${formatDateTime(doc.docsCompletedAt)} (${doc.docsCompletedByName ?? ''}): ${doc.docsCompletionNote ?? ''}`} />
      ) : (
        <Alert type={doc.isDocsOverdue ? 'error' : 'warning'} showIcon
          title={`Mua khẩn cấp: ${doc.emergencyReason ?? ''}. Hạn bổ sung hồ sơ ${formatDate(doc.docsDueDate)}${doc.isDocsOverdue ? ' – ĐÃ QUÁ HẠN' : ''}.`} />
      )
    ) : null

  const status = isNew
    ? { label: 'Nháp (chưa lưu)', tone: 'warning' as const }
    : { label: doc ? withPendingApprovers(purchaseStatusText(doc.status, !!doc.closedReason), doc.approvals) : '', tone: doc ? purchaseStatusTone[doc.status] : ('neutral' as const) }

  return (
    <>
      <DocumentForm
        approval={doc ? { steps: doc.approvals, createdByName: doc.createdByName, createdAt: doc.createdAt, stageLabels: PURCHASE_STAGE_LABELS } : undefined}
        title="Đơn mua hàng"
        number={isNew ? null : (doc?.no ?? '')}
        audit={{ entityType: 'PurchaseOrder', id: doc?.id }}
        printPath={doc ? `/api/purchase-orders/${doc.id}/pdf` : undefined}
        status={status}
        meta={doc && `Người lập: ${doc.createdByName ?? ''} · Lập lúc ${formatDateTime(doc.createdAt)}`}
        loading={order.isLoading}
        error={error ?? (order.error ? order.error.message : null)}
        notice={
          <>
            {emergencyNotice ??
              (noEligibleSupplier ? (
                <Alert type="warning" showIcon title={supplierHint} />
              ) : doc?.rejectReason && doc.status === 'Draft' ? (
                <Alert type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />
              ) : doc?.contractScopeNote && doc.status === 'Draft' ? (
                <Alert type="info" showIcon title={doc.contractScopeNote} />
              ) : (!readOnly && hasDeviation) || (readOnly && doc?.hasPriceException) ? (
                <Alert type="warning" showIcon title="Có dòng lệch giá đã duyệt (hợp đồng/bảng giá/kết quả chọn thầu): ghi lý do ở từng dòng; PO phải qua thêm chặng duyệt ngoại lệ giá." />
              ) : (
                doc?.actions.readOnlyReason && <Alert type="info" showIcon title={doc.actions.readOnlyReason} />
              ))}
            <BudgetNotice budget={doc?.budget} draft={doc?.status === 'Draft'} />
            <ClosedRemainingNotice doc={doc} />
            {/* Mua ngoài danh sách hoặc ngoài giá bắt buộc duyệt (người dùng chốt 06/11/2026 – design.md mục 18.25). */}
            {doc?.isOffApprovedList && (
              <Alert
                type="warning"
                showIcon
                title={'Có dòng mua ngoài danh sách NCC/giá đã duyệt (NCC không phải NCC chính/dự phòng của phiếu duyệt tháng, hoặc chưa có giá đã duyệt): PO phải qua thêm chặng duyệt "Ngoài danh sách/giá".'}
              />
            )}
            {/* Vượt giá trần của phiếu duyệt tháng không chặn gửi nhưng phải lên Chủ tịch/TGĐ duyệt (Q3, lát H3 – mục 18.25). */}
            {doc?.isOverCeiling && (
              <Alert
                type="warning"
                showIcon
                title={'Có dòng giá ròng (sau chiết khấu) vượt giá trần của phiếu duyệt NCC và giá tháng – xem cột Nguồn giá. PO phải qua thêm chặng duyệt "Vượt giá trần" (Chủ tịch/TGĐ).'}

              />
            )}
          </>
        }
        form={form}
        initialValues={{ date: dayjs(), receiveTolerancePct: 0, purchaseType: 'Normal', lines: [] }}
        readOnly={readOnly}
        onSave={save}
        onAddLine={() => addRef.current?.()}
        actions={
          <>
            {doc?.canCompleteDocs && (
              <Button icon={<FileDoneOutlined />} onClick={confirmCompleteDocs} loading={completeDocs.isPending}>
                Đã bổ sung hồ sơ
              </Button>
            )}
            {/* Nhận hàng theo PO bằng phiếu XNH (lát K2, design.md mục 19.4) – PO đã duyệt, chưa nhận đủ. */}
            {doc?.status === 'Approved' && hasPermission(Permission.ReceiptManage) && (
              <Button icon={<InboxOutlined />} onClick={() => navigate(`/mua-hang/xac-nhan-nhan-hang/moi?po=${doc.id}`)}>
                Lập phiếu nhận hàng
              </Button>
            )}
            {actions}
          </>
        }
        header={
          <>
            <Form.Item label="Ngày đặt hàng" name="date" rules={[{ required: true, message: 'Chọn ngày.' }]}>
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Nhà cung cấp" name="supplierId" rules={[{ required: true, message: 'Chọn nhà cung cấp.' }]} extra="Chỉ NCC đã duyệt và đang áp dụng tại công ty.">
              <Select
                showSearch={{ optionFilterProp: 'label' }}
                notFoundContent="Không có NCC đã duyệt và đang áp dụng tại công ty này."
                options={(suppliers.data ?? []).map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))}
              />
            </Form.Item>
            <Form.Item label="Loại mua" name="purchaseType" extra="Khẩn cấp: duyệt theo cấp riêng (POK), bổ sung hồ sơ sau trong hạn.">
              <Select
                options={Object.entries(purchaseTypeLabels).map(([value, label]) => ({ value, label }))}
                onChange={(v: PurchaseType) => {
                  if (v === 'Emergency' && !form.getFieldValue('docsDueDate'))
                    form.setFieldValue('docsDueDate', (form.getFieldValue('date') as Dayjs | undefined ?? dayjs()).add(7, 'day'))
                }}
              />
            </Form.Item>
            {purchaseType === 'Emergency' && (
              <>
                <Form.Item label="Lý do khẩn cấp" name="emergencyReason" rules={[{ required: true, whitespace: true, message: 'Ghi lý do khẩn cấp.' }, { max: 500 }]}>
                  <Input />
                </Form.Item>
                <Form.Item label="Hạn bổ sung hồ sơ" name="docsDueDate" rules={[{ required: true, message: 'Chọn hạn bổ sung hồ sơ.' }]}
                  extra="Tối đa 30 ngày sau ngày đặt hàng.">
                  <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
                </Form.Item>
              </>
            )}
            <Form.Item label="Bộ phận đặt hàng" name="departmentId" extra="Trống mà lấy dòng từ PR thì theo bộ phận của PR. Hợp đồng giới hạn bộ phận chỉ áp giá cho bộ phận được phép.">
              <Select allowClear showSearch={{ optionFilterProp: 'label' }} options={departmentOptions} loading={departments.isLoading} placeholder="Chọn bộ phận" />
            </Form.Item>
            <Form.Item label="Kho nhận" name="warehouseId" rules={[{ required: true, message: 'Chọn kho nhận.' }]}>
              <Select showSearch={{ optionFilterProp: 'label' }}
                options={(warehouses.data ?? []).filter((w) => w.isActive || w.id === doc?.warehouseId).map((w) => ({ value: w.id, label: `${w.code} – ${w.name}` }))} />
            </Form.Item>
            <Form.Item label="Ngày giao dự kiến" name="expectedDate">
              <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
            </Form.Item>
            <Form.Item label="Điều khoản thanh toán" name="paymentTerms" rules={[{ max: 200 }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Điều khoản giao hàng" name="deliveryTerms" rules={[{ max: 200 }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Địa chỉ giao" name="deliveryAddress" rules={[{ max: 500 }]}>
              <Input />
            </Form.Item>
            <Form.Item label="Dung sai nhận hàng (%)" name="receiveTolerancePct" extra="Phiếu nhập được nhận tới SL đặt × (1 + dung sai).">
              <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} max={20} />
            </Form.Item>
            <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
              <Input />
            </Form.Item>
          </>
        }
        lines={
          <>
            {!readOnly && (
              <Space style={{ marginBottom: 6 }}>
                <Button icon={<FileAddOutlined />} onClick={() => setPicking(true)}>
                  Lấy dòng từ PR đã duyệt
                </Button>
                <Button icon={<DollarOutlined />} onClick={applyPriceList} disabled={!supplierId}>
                  Áp giá đã duyệt
                </Button>
              </Space>
            )}
            {/* scrollX: lưới PO 15–17 cột – giữ độ rộng cột mặc định và cuộn ngang thay vì ép cột hẹp (docs/ui-style.md mục 5). */}
            <LineGrid name="lines" auditEntity="PurchaseOrderLine" columns={columns} readOnly={readOnly} addRef={addRef} scrollX
              newLine={() => ({ id: null, qty: 1, unitPrice: 0, discountPct: 0, sourcePrLineId: null })} />
          </>
        }
        footer={
          <>
            <DocumentTotal label="Tiền hàng" value={formatMoney(totals.gross)} />
            <DocumentTotal label="Chiết khấu" value={formatMoney(totals.discount)} />
            <DocumentTotal label="Tiền VAT" value={formatMoney(totals.vat)} />
            <DocumentTotal label="Tổng thanh toán" value={formatMoney(totals.amount + totals.vat)} strong />
          </>
        }
        extraTabs={[
          ...(doc ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} stageLabels={PURCHASE_STAGE_LABELS} createdByName={doc.createdByName} createdAt={doc.createdAt} /> }] : []),
          attachmentTab,
        ]}
      />
      {picking && <RequestLinePicker onCancel={() => setPicking(false)} onPick={addFromRequests} />}
    </>
  )
}

/**
 * Hộp chọn dòng PR đã duyệt còn chưa đặt hàng – chỉ dòng đã qua bảng tổng hợp đề xuất mua đã duyệt (bắt buộc mọi công ty, design.md mục 19.3).
 * @param onPick Thêm các dòng đã chọn.
 * @param onCancel Đóng.
 */
export function RequestLinePicker({ onPick, onCancel }: { onPick: (lines: OpenRequestLine[]) => void; onCancel: () => void }) {
  const open = useOpenRequestLines(true)
  const [selected, setSelected] = useState<string[]>([])
  const rows = open.data ?? []
  return (
    <Modal
      open
      title="Lấy dòng từ đề nghị mua hàng đã duyệt"
      width={900}
      okText={`Thêm ${selected.length} dòng`}
      okButtonProps={{ disabled: selected.length === 0 }}
      cancelText="Đóng"
      onCancel={onCancel}
      onOk={() => onPick(rows.filter((r) => selected.includes(r.prLineId)))}
    >
      <Table<OpenRequestLine>
        size="small"
        rowKey="prLineId"
        loading={open.isLoading}
        pagination={false}
        scroll={{ y: 360 }}
        dataSource={rows}
        rowSelection={{ selectedRowKeys: selected, onChange: (keys) => setSelected(keys as string[]) }}
        locale={{ emptyText: 'Không còn dòng đề nghị nào đã qua bảng tổng hợp đã duyệt mà chưa đặt hàng.' }}
        columns={[
          { title: 'Số PR', dataIndex: 'requestNo', width: 150 },
          { title: 'Bộ phận', dataIndex: 'department', width: 120 },
          { title: 'Mặt hàng', key: 'item', render: (_, r) => `${r.itemCode} – ${r.itemName}` },
          { title: 'ĐVT', dataIndex: 'uomCode', width: 60 },
          { title: 'Còn chưa đặt', dataIndex: 'remainingQty', width: 100, align: 'right', render: (v: number) => formatQuantity(v) },
          { title: 'Ngày cần', dataIndex: 'neededDate', width: 100, render: (v: string | null) => formatDate(v) },
        ]}
      />
    </Modal>
  )
}
