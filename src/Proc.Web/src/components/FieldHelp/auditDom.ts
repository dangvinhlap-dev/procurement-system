/*
 * Nhận ra "ô dữ liệu" trên màn hình để bấm F1 xem lịch sử (design.md mục 9.3a), không phải khai báo từng ô:
 * - Vùng bản ghi gắn data-audit-entity (mã loại đối tượng của AuditLog, ví dụ PurchaseOrder) và data-audit-id (khóa bản ghi):
 *   DocumentForm/DetailDrawer gắn cho đầu phiếu, LineGrid gắn cho từng dòng, DataGrid gắn cho từng dòng danh sách.
 * - Tên ô lấy từ id của ô nhập antd (tên Form.Item, ví dụ supplierId, lines_0_qty → qty, receiptInvoice_invoiceNo → invoiceNo),
 *   hoặc từ data-audit-field khi ô không phải ô nhập (ô của lưới danh sách) hoặc tên ô khác tên trường.
 * - data-audit-fields trên vùng bản ghi (JSON {tên ô: tên trường}) đổi tên ô sang tên trường khi khác nhau.
 */

/** Ô được chọn để xem lịch sử. */
export interface FieldTarget {
  entityType: string
  /** null = bản ghi chưa lưu (chưa có lịch sử). */
  entityId: string | null
  /** Tên ô/tên trường gửi server (server tự thêm hậu tố Id khi cần). */
  field: string
  /** Nhãn ô trên màn hình. */
  label: string
  /** Giá trị đang hiển thị. */
  currentText: string
}

const CONTROL_SELECTOR = 'input[id], textarea[id], button[id], [role="switch"][id]'
const WRAPPER_SELECTOR =
  '.ant-select, .ant-picker, .ant-input-number, .ant-checkbox-wrapper, .ant-radio-group, .ant-input-affix-wrapper, .ant-switch, .ant-input-textarea'

/**
 * Tên ô từ id của ô nhập antd: phần sau dấu "_" cuối cùng (tên form và chỉ số dòng đứng trước).
 * @param id Id ô nhập, ví dụ "lines_0_qty".
 * @returns Tên ô, ví dụ "qty".
 */
export function fieldNameOfId(id: string): string {
  const parts = id.split('_').filter(Boolean)
  return parts[parts.length - 1] ?? id
}

/** Ô nhập (có id) gần nhất của một phần tử: chính nó, ô nhập trong khung ô antd, trong Form.Item hoặc trong ô lưới. */
function controlOf(el: Element): HTMLElement | null {
  if (el.matches(CONTROL_SELECTOR)) return el as HTMLElement
  const wrapper = el.closest(WRAPPER_SELECTOR)
  const inWrapper = wrapper?.querySelector<HTMLElement>(CONTROL_SELECTOR)
  if (inWrapper) return inWrapper
  const item = el.closest('.ant-form-item')
  if (item) {
    // Nhãn trỏ tới ô qua for=… – đúng cả khi ô đang khóa (phiếu chỉ xem).
    const forId = item.querySelector('label[for]')?.getAttribute('for')
    const byLabel = forId ? document.getElementById(forId) : null
    return byLabel ?? item.querySelector<HTMLElement>(CONTROL_SELECTOR)
  }
  const cell = el.closest('td')
  return cell?.querySelector<HTMLElement>(CONTROL_SELECTOR) ?? null
}

/** Nhãn ô: nhãn Form.Item, tiêu đề cột của ô lưới, hoặc data-audit-label. */
function labelOf(anchor: Element, fallback: string): string {
  const explicit = anchor.closest('[data-audit-label]')?.getAttribute('data-audit-label')
  if (explicit) return explicit
  const label = anchor.closest('.ant-form-item')?.querySelector('.ant-form-item-label label')
  if (label?.textContent) return label.textContent.trim()
  const cell = anchor.closest('td, th')
  const row = cell?.parentElement
  const table = cell?.closest('table')
  if (cell && row && table) {
    const index = [...row.children].indexOf(cell)
    const head = table.querySelector('thead tr')?.children[index]
    if (head?.textContent) return head.textContent.trim()
  }
  return fallback
}

/** Giá trị đang hiển thị của ô: chữ đã chọn của ô chọn, giá trị ô nhập, chữ của ô lưới. */
function currentTextOf(control: Element | null, anchor: Element): string {
  const wrapper = (control ?? anchor).closest(WRAPPER_SELECTOR) ?? control
  const selected = wrapper?.querySelector('.ant-select-selection-item')
  if (selected) return (selected.getAttribute('title') || selected.textContent || '').trim()
  if (control instanceof HTMLInputElement) {
    if (control.type === 'checkbox' || control.type === 'radio') return control.checked ? 'Có' : 'Không'
    return control.value
  }
  if (control instanceof HTMLTextAreaElement) return control.value
  if (control?.getAttribute('role') === 'switch') return control.getAttribute('aria-checked') === 'true' ? 'Có' : 'Không'
  return (anchor.textContent ?? '').trim()
}

/**
 * Tìm ô dữ liệu tại một phần tử (ô đang có con trỏ hoặc phần tử dưới chuột).
 * @param el Phần tử.
 * @returns Ô, hoặc null nếu phần tử không thuộc ô dữ liệu nào của một bản ghi có nhật ký.
 */
export function findFieldTarget(el: Element | null): FieldTarget | null {
  if (!el) return null
  const explicit = el.closest('[data-audit-field]')
  const control = explicit ? null : controlOf(el)
  const anchor = explicit ?? control
  if (!anchor) return null
  const scope = anchor.closest('[data-audit-entity]')
  if (!scope) return null

  let field = explicit ? explicit.getAttribute('data-audit-field')! : fieldNameOfId(control!.id)
  // Ô ẩn/kỹ thuật (id dòng) không phải dữ liệu người dùng nhập.
  if (!field || field === 'id') return null
  const overrides = scope.getAttribute('data-audit-fields')
  if (overrides) {
    try {
      const map = JSON.parse(overrides) as Record<string, string | null>
      if (field in map) {
        // null = ô không gắn với trường nào của bản ghi này (ô lọc, ô tạm).
        if (map[field] === null) return null
        field = map[field]!
      }
    } catch {
      // Khai báo hỏng thì dùng tên ô.
    }
  }

  return {
    entityType: scope.getAttribute('data-audit-entity')!,
    entityId: scope.getAttribute('data-audit-id') || null,
    field,
    label: labelOf(anchor, field),
    currentText: currentTextOf(control, anchor),
  }
}

/**
 * Nhãn các ô đang hiển thị trong một vùng (đầu phiếu và lưới dòng), để hộp thoại "Lịch sử thay đổi" ghi nhãn tiếng Việt như trên màn hình.
 * @param root Phần tử bao (phiếu, ngăn kéo).
 * @returns Nhãn theo tên ô viết thường, ví dụ { supplierid: "Nhà cung cấp", qty: "SL" }.
 */
export function collectFieldLabels(root: Element | null): Record<string, string> {
  const labels: Record<string, string> = {}
  if (!root) return labels
  root.querySelectorAll('.ant-form-item').forEach((item) => {
    const control = controlOf(item)
    const text = item.querySelector('.ant-form-item-label label')?.textContent?.trim()
    if (control?.id && text) labels[fieldNameOfId(control.id).toLowerCase()] ??= text
  })
  root.querySelectorAll('table').forEach((table) => {
    const heads = [...(table.querySelector('thead tr')?.children ?? [])]
    table.querySelectorAll('tbody tr').forEach((row) => {
      ;[...row.children].forEach((cell, index) => {
        const text = heads[index]?.textContent?.trim()
        if (!text) return
        const explicit = cell.getAttribute('data-audit-field')
        const control = explicit ? null : cell.querySelector<HTMLElement>(CONTROL_SELECTOR)
        const name = explicit ?? (control?.id ? fieldNameOfId(control.id) : null)
        if (name && name !== 'id') labels[name.toLowerCase()] ??= text
      })
    })
  })
  return labels
}
