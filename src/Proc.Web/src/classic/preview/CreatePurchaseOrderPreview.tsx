import {
  ArrowLeftOutlined,
  BorderOutlined,
  CheckSquareOutlined,
  CloseCircleOutlined,
  DeleteOutlined,
  EyeOutlined,
  FileSearchOutlined,
  LogoutOutlined,
  MessageOutlined,
  PauseCircleOutlined,
  PlusOutlined,
  PrinterOutlined,
  SaveOutlined,
  SearchOutlined,
} from '@ant-design/icons'
import { App } from 'antd'
import dayjs from 'dayjs'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router'
import { screens } from '../../app/screens'
import { demoItems, demoPoTypes, demoSuppliers, demoWarehouses } from '../../mocks/classicPurchaseOrder'
import { formatMoney } from '../../utils/format'
import {
  ClassicBody,
  ClassicScreen,
  CommandBar,
  CommandButton,
  MenuBar,
  ScreenTitle,
  StatusBar,
  StatusLight,
  Toolbar,
  type StatusLevel,
} from '../ClassicScreen'
import { DateInput } from '../DateInput'
import { EntryGrid, GridButtons, SummaryRow, type EntryColumn } from '../EntryGrid'
import { Field, HeaderFields, Section } from '../HeaderFields'
import { TabStrip } from '../TabStrip'

/** Một dòng hàng của đơn mua (chỉ trong bộ nhớ – màn hình mẫu). */
interface PoLine {
  id: string
  itemCode: string | null
  name: string
  qty: number | null
  uom: string
  deliveryDate: string | null
  price: number | null
  group: string
  warehouse: string | null
}

/** Một yếu tố giá trong tab "Điều kiện giá": phí/chiết khấu theo % hoặc số tiền, hoặc dòng tạm tính. */
interface PriceCondition {
  id: string
  code: string
  name: string
  /** '%' tính trên tổng tiền hàng; 'VND' là số tiền cố định; 'sum' là dòng tạm tính (chỉ đọc, chữ xanh). */
  unit: '%' | 'VND' | 'sum'
  rate: number | null
  /** Chiết khấu thì trừ, phí thì cộng. */
  sign: 1 | -1
}

let nextId = 100
const newLine = (): PoLine => ({
  id: `l${nextId++}`,
  itemCode: null,
  name: '',
  qty: null,
  uom: '',
  deliveryDate: null,
  price: null,
  group: '',
  warehouse: null,
})

/** Dòng mẫu ban đầu: hai dòng đủ thông tin, một dòng thiếu đơn giá để thấy tam giác cảnh báo, ba dòng trống để gõ tiếp. */
function initialLines(): PoLine[] {
  const due = dayjs().add(14, 'day').format('YYYY-MM-DD')
  const fromItem = (code: string, qty: number, price: number | null): PoLine => {
    const it = demoItems.find((i) => i.code === code)!
    return { ...newLine(), itemCode: code, name: it.name, uom: it.uom, group: it.group, qty, price, deliveryDate: due, warehouse: 'KC' }
  }
  return [fromItem('VT-0001', 1_500, 18_500), fromItem('VT-0002', 120, 412_000), fromItem('PT-0004', 250, null), newLine(), newLine(), newLine()]
}

const initialConditions: PriceCondition[] = [
  { id: 'c1', code: 'CKTM', name: 'Chiết khấu thanh toán', unit: '%', rate: 0, sign: -1 },
  { id: 'c2', code: 'VC01', name: 'Phí vận chuyển', unit: 'VND', rate: 1_500_000, sign: 1 },
  { id: 's1', code: '', name: 'Giá thực tế', unit: 'sum', rate: null, sign: 1 },
  { id: 'c3', code: 'DG01', name: 'Phí đóng gói %', unit: '%', rate: 2, sign: 1 },
  { id: 'c4', code: 'BH01', name: 'Bảo hiểm hàng hóa %', unit: '%', rate: 1, sign: 1 },
  { id: 's2', code: '', name: 'Tạm tính', unit: 'sum', rate: null, sign: 1 },
  { id: 's3', code: '', name: 'Tổng giá trị', unit: 'sum', rate: null, sign: 1 },
]

/** Lý do dòng chưa hợp lệ (hiện ở tam giác vàng); null nếu dòng trống hoặc đủ thông tin. */
function lineIssue(l: PoLine): string | null {
  const empty = !l.itemCode && l.qty == null && l.price == null && !l.deliveryDate && !l.warehouse
  if (empty) return null
  if (!l.itemCode) return 'Chưa chọn mã hàng'
  if (l.qty == null || l.qty <= 0) return 'Chưa nhập số lượng'
  if (l.price == null) return 'Chưa có đơn giá'
  if (!l.deliveryDate) return 'Chưa nhập ngày giao'
  if (!l.warehouse) return 'Chưa chọn kho'
  return null
}

const isEmptyLine = (l: PoLine) => !l.itemCode && lineIssue(l) === null

/** Thành tiền dòng = SL × đơn giá, làm tròn đồng (VND không số lẻ – CLAUDE.md mục 5). Ví dụ 1.500 × 18.500 = 27.750.000. */
const lineAmount = (l: PoLine) => (l.qty != null && l.price != null ? Math.round(l.qty * l.price) : null)

/**
 * Tính giá trị từng yếu tố giá theo thứ tự trên lưới.
 * - '%': tiền hàng × mức / 100, làm tròn đồng; 'VND': lấy đúng số tiền; có dấu trừ nếu là chiết khấu.
 * - 'sum': tiền hàng + tổng các yếu tố đứng trước nó.
 * Ví dụ tiền hàng 77.190.000: VC01 1.500.000 → Giá thực tế 78.690.000; DG01 2% = 1.543.800; BH01 1% = 771.900
 * → Tạm tính 81.005.700.
 */
function conditionValues(conditions: PriceCondition[], net: number): Map<string, number> {
  const values = new Map<string, number>()
  let running = net
  for (const c of conditions) {
    if (c.unit === 'sum') {
      values.set(c.id, running)
      continue
    }
    const raw = c.unit === '%' ? Math.round((net * (c.rate ?? 0)) / 100) : (c.rate ?? 0)
    // "|| 0" bỏ số âm 0 (-0) khi chiết khấu bằng 0, tránh hiện "-0".
    const v = c.sign * raw || 0
    values.set(c.id, v)
    running += v
  }
  return values
}

/**
 * Màn hình MẪU "Tạo đơn mua hàng" theo phong cách ERP desktop cổ điển (docs/ui-style.md, ảnh docs/imageSAP.png).
 * Dữ liệu mẫu trong bộ nhớ, không gọi API; Lưu chỉ kiểm tra hợp lệ và báo ở thanh trạng thái.
 */
export function CreatePurchaseOrderPreview() {
  const navigate = useNavigate()
  const { modal } = App.useApp()

  const [poType, setPoType] = useState('NB')
  const [supplierCode, setSupplierCode] = useState('NCC-0001')
  const [docDate, setDocDate] = useState(dayjs().format('YYYY-MM-DD'))
  const [tab, setTab] = useState('conditions')
  const [lines, setLines] = useState<PoLine[]>(initialLines)
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [conditions, setConditions] = useState(initialConditions)
  const [note, setNote] = useState('')
  const [held, setHeld] = useState(false)
  const [message, setMessage] = useState<{ level: StatusLevel; text: string } | null>({
    level: 'warning',
    text: 'Màn hình mẫu giao diện – dữ liệu mẫu, không ghi vào CSDL.',
  })

  const supplier = demoSuppliers.find((s) => s.code === supplierCode)
  const net = useMemo(() => lines.reduce((s, l) => s + (lineAmount(l) ?? 0), 0), [lines])
  const condValues = useMemo(() => conditionValues(conditions, net), [conditions, net])
  const total = condValues.get('s3') ?? net
  const issues = lines.map(lineIssue).filter((x) => x !== null).length
  const filledLines = lines.filter((l) => !isEmptyLine(l)).length

  const updateLine = (index: number, key: string, value: string | number | null) => {
    setLines((prev) =>
      prev.map((l, i) => {
        if (i !== index) return l
        if (key === 'itemCode') {
          // Chọn mã hàng thì điền sẵn tên, ĐVT, nhóm hàng và đơn giá gợi ý (đơn giá đã gõ thì giữ).
          const it = demoItems.find((x) => x.code === value)
          return it
            ? { ...l, itemCode: it.code, name: it.name, uom: it.uom, group: it.group, price: l.price ?? it.price }
            : { ...l, itemCode: null, name: '', uom: '', group: '' }
        }
        return { ...l, [key]: value }
      }),
    )
  }

  const addLine = () => setLines((prev) => [...prev, newLine()])
  const deleteSelected = () => {
    setLines((prev) => prev.filter((l) => !selected.has(l.id)))
    setMessage({ level: 'success', text: `Đã xóa ${selected.size} dòng.` })
    setSelected(new Set())
  }

  const save = () => {
    // Kiểm tra như khi lưu thật; màn hình mẫu không gửi đi đâu.
    if (!supplierCode) return setMessage({ level: 'error', text: 'Chưa chọn nhà cung cấp.' })
    if (filledLines === 0) return setMessage({ level: 'error', text: 'Đơn mua hàng chưa có dòng hàng nào.' })
    const firstBad = lines.findIndex((l) => lineIssue(l) !== null)
    if (firstBad >= 0) {
      return setMessage({ level: 'error', text: `Dòng ${(firstBad + 1) * 10}: ${lineIssue(lines[firstBad])}.` })
    }
    setMessage({ level: 'success', text: `Đơn mua hàng hợp lệ, tổng giá trị ${formatMoney(total)} VND (màn hình mẫu – không ghi CSDL).` })
  }

  const back = () => navigate('/mua-hang/po')
  const cancel = () =>
    modal.confirm({
      title: 'Hủy thay đổi',
      content: 'Bỏ mọi thay đổi trên đơn mua hàng đang lập?',
      okText: 'Hủy thay đổi',
      cancelText: 'Không',
      onOk: () => {
        setLines(initialLines())
        setConditions(initialConditions)
        setNote('')
        setSelected(new Set())
        setMessage({ level: 'warning', text: 'Đã hủy thay đổi.' })
      },
    })
  const printPreview = () => setMessage({ level: 'warning', text: 'Xem trước in chưa có trong màn hình mẫu.' })
  const focusCommand = () => document.querySelector<HTMLInputElement>('.erp-toolbar__command')?.focus()

  const lineColumns: EntryColumn<PoLine>[] = [
    { key: 'stt', title: 'STT', width: 44, numeric: true, display: (l) => (lines.indexOf(l) + 1) * 10 },
    {
      key: 'itemCode',
      title: 'Mã hàng',
      width: 96,
      kind: 'select',
      required: true,
      options: demoItems.map((i) => ({ value: i.code, label: i.code })),
      value: (l) => l.itemCode,
    },
    { key: 'name', title: 'Tên hàng', width: 200, value: (l) => l.name },
    { key: 'qty', title: 'Số lượng', width: 84, kind: 'number', numeric: true, required: true, value: (l) => l.qty },
    { key: 'uom', title: 'ĐVT', width: 44, value: (l) => l.uom },
    { key: 'deliveryDate', title: 'Ngày giao', width: 112, kind: 'date', required: true, value: (l) => l.deliveryDate },
    { key: 'price', title: 'Đơn giá', width: 100, kind: 'number', numeric: true, required: true, format: formatMoney, value: (l) => l.price },
    { key: 'amount', title: 'Thành tiền', width: 112, numeric: true, format: formatMoney, value: lineAmount },
    { key: 'currency', title: 'Tiền tệ', width: 52, display: (l) => (isEmptyLine(l) ? '' : 'VND') },
    { key: 'group', title: 'Nhóm hàng', width: 120, value: (l) => l.group },
    { key: 'warehouse', title: 'Kho', width: 110, kind: 'select', required: true, options: demoWarehouses, value: (l) => l.warehouse },
  ]

  const conditionColumns: EntryColumn<PriceCondition>[] = [
    { key: 'code', title: 'Mã ĐK', width: 56, value: (c) => c.code },
    { key: 'name', title: 'Tên điều kiện', width: 180, value: (c) => c.name },
    {
      key: 'rate',
      title: 'Mức',
      width: 100,
      kind: 'number',
      numeric: true,
      editable: (c) => c.unit !== 'sum',
      value: (c) => c.rate,
    },
    { key: 'unit', title: 'ĐV', width: 44, display: (c) => (c.unit === 'sum' ? '' : c.unit) },
    { key: 'value', title: 'Giá trị điều kiện', width: 130, numeric: true, format: formatMoney, value: (c) => condValues.get(c.id) ?? null },
    { key: 'currency', title: 'Tiền tệ', width: 52, display: () => 'VND' },
    {
      key: 'status',
      title: 'TT',
      width: 32,
      display: (c) => (c.unit === 'sum' ? null : <StatusLight level="success" label="Điều kiện đang áp dụng" />),
    },
  ]

  const field = (label: string, value: string) => (
    <Field label={label}>{(id) => <input id={id} className="erp-input" style={{ width: 280 }} value={value} readOnly />}</Field>
  )

  const tabs = [
    {
      key: 'delivery',
      label: 'Giao hàng/Hóa đơn',
      content: (
        <HeaderFields>
          {field('Điều kiện thanh toán', supplier?.paymentTerm ?? '')}
          {field('Điều kiện giao hàng', 'Giao tại kho người mua')}
          {field('Tiền tệ', 'VND')}
        </HeaderFields>
      ),
    },
    {
      key: 'conditions',
      label: 'Điều kiện giá',
      content: (
        <>
          <div className="erp-net">
            <span>Tiền hàng</span>
            <input className="erp-input erp-input--num" style={{ width: 140 }} value={formatMoney(net)} readOnly aria-label="Tiền hàng" />
            <span>VND</span>
          </div>
          <EntryGrid
            label="Các yếu tố giá"
            caption="Các yếu tố giá"
            columns={conditionColumns}
            rows={conditions}
            getRowKey={(c) => c.id}
            rowClassName={(c) => (c.unit === 'sum' ? 'erp-grid__subtotal' : undefined)}
            onCellChange={(i, key, v) => setConditions((prev) => prev.map((c, ci) => (ci === i ? { ...c, [key]: v } : c)))}
          />
        </>
      ),
    },
    {
      key: 'texts',
      label: 'Ghi chú',
      content: (
        <Field label="Ghi chú gửi NCC">
          {(id) => (
            <textarea id={id} className="erp-input" style={{ width: 560, height: 80 }} value={note} onChange={(e) => setNote(e.target.value)} />
          )}
        </Field>
      ),
    },
    { key: 'address', label: 'Địa chỉ', content: <HeaderFields>{field('Địa chỉ NCC', supplier?.address ?? '')}</HeaderFields> },
    { key: 'partners', label: 'Đối tác', content: <HeaderFields>{field('Người liên hệ', supplier?.contact ?? '')}</HeaderFields> },
    {
      key: 'extra',
      label: 'Thông tin thêm',
      content: (
        <HeaderFields>
          {field('Số PR tham chiếu', '')}
          {field('Số hợp đồng khung', '')}
        </HeaderFields>
      ),
    },
    {
      key: 'org',
      label: 'Tổ chức',
      content: (
        <HeaderFields>
          {field('Công ty', 'BHA – Công ty Thép Bắc Hà')}
          {field('Phòng mua hàng', 'Phòng Cung ứng')}
        </HeaderFields>
      ),
    },
    {
      key: 'status',
      label: 'Trạng thái',
      content: (
        <HeaderFields>
          <span>
            <StatusLight level="warning" label="Nháp" /> Nháp – chưa lưu
          </span>
          <span>
            <StatusLight level={issues ? 'error' : 'success'} label={issues ? 'Có dòng lỗi' : 'Dòng hàng hợp lệ'} />{' '}
            {issues ? `${issues} dòng chưa đủ thông tin` : 'Mọi dòng hàng hợp lệ'}
          </span>
          {held && (
            <span>
              <StatusLight level="warning" label="Đang giữ" /> Đang giữ
            </span>
          )}
        </HeaderFields>
      ),
    },
  ]

  return (
    <ClassicScreen shortcuts={{ save, print: printPreview, addRow: addLine, back }}>
      <MenuBar items={[{ label: '&Phiếu' }, { label: '&Sửa' }, { label: 'Đi &tới' }, { label: '&Hệ thống' }, { label: 'Trợ &giúp' }]} />
      <Toolbar
        onCommand={(code) => {
          const s = screens.find((x) => x.code === code)
          if (s) navigate(s.path)
          else setMessage({ level: 'error', text: `Không có màn hình mã "${code}".` })
        }}
        items={[
          { key: 'save', icon: <SaveOutlined />, title: 'Lưu (Ctrl+S)', tone: 'success', onClick: save },
          { key: 's1', separator: true },
          { key: 'back', icon: <ArrowLeftOutlined />, title: 'Quay lại (F3)', tone: 'warning', onClick: back },
          { key: 'exit', icon: <LogoutOutlined />, title: 'Thoát', tone: 'danger', onClick: () => navigate('/') },
          { key: 'cancel', icon: <CloseCircleOutlined />, title: 'Hủy', tone: 'danger', onClick: cancel },
          { key: 's2', separator: true },
          { key: 'print', icon: <PrinterOutlined />, title: 'In (Ctrl+P)', tone: 'warning', onClick: printPreview },
          { key: 'find', icon: <SearchOutlined />, title: 'Tìm', tone: 'warning', onClick: focusCommand },
        ]}
      />
      <ScreenTitle>Tạo đơn mua hàng</ScreenTitle>
      <CommandBar>
        <CommandButton icon={<FileSearchOutlined />} tone="warning" onClick={() => navigate('/mua-hang/po')}>
          Tổng quan chứng từ
        </CommandButton>
        <CommandButton icon={<PauseCircleOutlined />} tone="warning" onClick={() => setHeld(!held)}>
          {held ? 'Bỏ giữ' : 'Giữ'}
        </CommandButton>
        <CommandButton icon={<EyeOutlined />} tone="warning" onClick={printPreview}>
          Xem trước in
        </CommandButton>
        <CommandButton icon={<MessageOutlined />} tone="warning" onClick={() => setMessage({ level: 'success', text: 'Không có thông báo mới.' })}>
          Thông báo
        </CommandButton>
        <span className="erp-preview-note">Màn hình mẫu – dữ liệu mẫu, không ghi CSDL</span>
      </CommandBar>
      <ClassicBody>
        <Section title="Thông tin chung">
          <HeaderFields>
            <Field label="Loại đơn" required>
              {(id) => (
                <select id={id} className="erp-input" style={{ width: 180 }} value={poType} onChange={(e) => setPoType(e.target.value)}>
                  {demoPoTypes.map((t) => (
                    <option key={t.value} value={t.value}>
                      {t.label}
                    </option>
                  ))}
                </select>
              )}
            </Field>
            <Field label="Nhà cung cấp" required>
              {(id) => (
                <select id={id} className="erp-input" style={{ width: 300 }} value={supplierCode} onChange={(e) => setSupplierCode(e.target.value)}>
                  <option value="" />
                  {demoSuppliers.map((s) => (
                    <option key={s.code} value={s.code}>
                      {s.code} {s.name}
                    </option>
                  ))}
                </select>
              )}
            </Field>
            <Field label="Ngày chứng từ" required>
              {(id) => (
                <DateInput id={id} style={{ width: 120 }} value={docDate} onChange={(v) => v && setDocDate(v)} />
              )}
            </Field>
          </HeaderFields>
        </Section>
        <Section title="Thông tin đầu phiếu">
          <TabStrip label="Thông tin đầu phiếu" items={tabs} activeKey={tab} onChange={setTab} />
        </Section>
        <Section title="Dòng hàng">
          <EntryGrid
            label="Dòng hàng"
            columns={lineColumns}
            rows={lines}
            getRowKey={(l) => l.id}
            onCellChange={updateLine}
            selectedKeys={selected}
            onSelectionChange={setSelected}
            onAppendRow={addLine}
            maxHeight={260}
            rowMarker={(l) => {
              const issue = lineIssue(l)
              if (issue)
                return (
                  <span className="erp-warning" role="img" aria-label={issue} title={issue}>
                    ▲
                  </span>
                )
              return isEmptyLine(l) ? null : <StatusLight level="success" label="Dòng hợp lệ" />
            }}
            summary={
              <SummaryRow
                cells={[
                  { colSpan: 2 },
                  { content: 'Tổng tiền hàng', colSpan: 7 },
                  { content: formatMoney(net), numeric: true },
                  { content: 'VND' },
                  { colSpan: 2 },
                ]}
              />
            }
          />
          <GridButtons
            hasSelection={selected.size > 0}
            onAdd={addLine}
            onDelete={deleteSelected}
            onSelectAll={() => setSelected(new Set(lines.map((l) => l.id)))}
            onClearSelection={() => setSelected(new Set())}
            icons={{ add: <PlusOutlined />, delete: <DeleteOutlined />, selectAll: <CheckSquareOutlined />, clear: <BorderOutlined /> }}
          />
        </Section>
      </ClassicBody>
      <StatusBar message={message} right={`${filledLines} dòng · Tổng giá trị ${formatMoney(total)} VND`} />
    </ClassicScreen>
  )
}
