import { App } from 'antd'
import type { KeyboardEvent, MouseEvent } from 'react'
import { useNavigate } from 'react-router'
import { documentPath } from '../api/documentRegistry'
import { copyText } from '../utils/clipboard'

/** Thuộc tính ô số chứng từ. */
interface DocNoProps {
  /** Số chứng từ; trống thì không vẽ gì. */
  no: string | null | undefined
  /** Mã loại phiếu (PR, PO, SRC…) – cùng với `id` để bấm đúp mở phiếu gốc (`documentPath`). */
  docType?: string
  /** Id phiếu gốc; thiếu thì ô chỉ sao chép được. */
  id?: string | null
  /** Lớp CSS thêm (ví dụ `document-form-number` – số phiếu trên đầu form). */
  className?: string
  /** Chữ đi kèm sau số trong ô, ví dụ trạng thái "(Đã duyệt)" – không bị sao chép. */
  suffix?: string
}

/**
 * Ô số chứng từ dùng chung (design.md mục 20.1 R5, docs/ui-style.md mục 5e): số phiếu nằm trong một ô chữ chỉ đọc – **bấm** để sao chép số,
 * **bấm đúp** (hoặc Enter khi ô đang có con trỏ) để mở form chứng từ gốc. Không có loại/Id phiếu gốc (ví dụ chính số phiếu đang mở) thì ô
 * chỉ sao chép.
 */
export function DocNo({ no, docType, id, className, suffix }: DocNoProps) {
  const { message } = App.useApp()
  const navigate = useNavigate()
  if (!no) return null
  const path = docType && id ? documentPath(docType, id) : null

  const copy = async () => {
    // Cùng key: bấm đúp (hai lần bấm) chỉ hiện một thông báo.
    if (await copyText(no)) void message.success({ key: 'doc-no-copy', content: `Đã sao chép số ${no}.` })
    else void message.warning({ key: 'doc-no-copy', content: 'Trình duyệt không cho sao chép – bôi đen số rồi bấm Ctrl+C.' })
  }
  const open = (e?: MouseEvent<HTMLSpanElement>) => {
    if (!path) return
    // Ô nằm trong dòng lưới có bấm đúp mở dòng: chỉ mở phiếu gốc của ô, không để sự kiện lan lên dòng.
    e?.stopPropagation()
    void navigate(path)
  }
  const onKeyDown = (e: KeyboardEvent<HTMLSpanElement>) => {
    if (e.key === 'Enter' && path) {
      e.preventDefault()
      e.stopPropagation()
      open()
    }
  }

  return (
    <span
      className={['erp-doc-no', path ? 'is-link' : null, className].filter(Boolean).join(' ')}
      role="textbox"
      aria-readonly="true"
      aria-label={`Số chứng từ ${no}`}
      tabIndex={0}
      title={path ? 'Bấm để sao chép số; bấm đúp để mở phiếu gốc' : 'Bấm để sao chép số'}
      data-doc-type={docType}
      onClick={() => void copy()}
      onDoubleClick={open}
      onKeyDown={onKeyDown}
    >
      {no}
      {suffix ? <span className="erp-doc-no-suffix"> {suffix}</span> : null}
    </span>
  )
}

/** Một số chứng từ trong danh sách `DocNoList`. */
export interface DocNoRef {
  /** Id phiếu gốc (bấm đúp mở). */
  id?: string | null
  /** Số chứng từ. */
  no: string
}

/**
 * Nhiều ô số chứng từ cùng loại trên một hàng (tự xuống dòng), ví dụ các PO đã lấy một PR, các PR nguồn của một PO (người dùng yêu cầu 19/11/2026
 * – docs/ui-style.md mục 5e). Mỗi số là một `DocNo` (bấm sao chép, bấm đúp mở). Không có số nào thì hiện chữ mờ `empty`.
 */
export function DocNoList({ items, docType, empty }: { items: DocNoRef[]; docType: string; empty?: string }) {
  if (items.length === 0) return empty ? <span className="erp-doc-no-empty">{empty}</span> : null
  return (
    <span className="erp-doc-no-list">
      {items.map((d) => <DocNo key={d.id ?? d.no} no={d.no} docType={docType} id={d.id} />)}
    </span>
  )
}
