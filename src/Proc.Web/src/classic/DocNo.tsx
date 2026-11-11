import { App } from 'antd'
import type { KeyboardEvent, MouseEvent } from 'react'
import { useNavigate } from 'react-router'
import { documentPath } from '../api/documentRegistry'

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
 * Chép chữ vào bộ nhớ tạm. Trình duyệt chỉ cho `navigator.clipboard` trên https/localhost; máy chủ nội bộ chạy http thì dùng cách cũ
 * (ô textarea tạm + `execCommand('copy')`) để vẫn sao chép được.
 * @param text Chữ cần chép.
 * @returns Có chép được không.
 */
async function copyText(text: string): Promise<boolean> {
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text)
      return true
    }
  } catch {
    // Bị chặn quyền – thử cách cũ bên dưới.
  }
  const area = document.createElement('textarea')
  area.value = text
  area.setAttribute('readonly', '')
  area.style.position = 'fixed'
  area.style.opacity = '0'
  document.body.appendChild(area)
  area.select()
  try {
    return document.execCommand('copy')
  } catch {
    return false
  } finally {
    area.remove()
  }
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
    navigate(path)
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
