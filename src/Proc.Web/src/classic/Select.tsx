import { Select as AntSelect, type SelectProps } from 'antd'
import type { BaseOptionType, DefaultOptionType } from 'antd/es/select'
import { useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { hasWildcard, matchesKeyword } from '../utils/text'

/** Chữ để so khớp của một lựa chọn: thuộc tính `optionFilterProp` (mặc định nhãn), không có thì ghép mã giá trị. */
function optionText(option: BaseOptionType | undefined, prop: string | undefined): string {
  if (!option) return ''
  const raw: unknown = option[prop ?? 'label'] ?? option.label ?? option.children ?? option.value
  return typeof raw === 'string' || typeof raw === 'number' ? String(raw) : textOf(raw as ReactNode)
}

/** Lấy chữ thuần từ nhãn dạng phần tử React (nhãn có định dạng). */
function textOf(node: ReactNode): string {
  if (node === null || node === undefined || typeof node === 'boolean') return ''
  if (typeof node === 'string' || typeof node === 'number') return String(node)
  if (Array.isArray(node)) return node.map(textOf).join(' ')
  if (typeof node === 'object' && 'props' in node) return textOf((node.props as { children?: ReactNode }).children)
  return ''
}

/**
 * Ô chọn danh mục dùng chung (thay `Select` của antd ở mọi màn hình – docs/ui-style.md mục 5b), thêm cách tìm kiểu SAP ECC
 * (yêu cầu người dùng 02/11/2026):
 * - Gõ `*` rồi Enter: liệt kê **toàn bộ** danh mục để chọn (Enter không tự chọn dòng đầu; dùng mũi tên rồi Enter hoặc bấm chuột).
 *   `*` giữa từ khóa là ký tự đại diện: `b*long` khớp "Bu lông".
 * - Tìm không phân biệt có dấu/không dấu, nhiều từ không cần đúng thứ tự (CLAUDE.md mục 5).
 * - Ô tìm trên máy chủ (`showSearch.filterOption = false` + `onSearch`) nhận nguyên chuỗi có `*`; API tìm hiểu `*` là đại diện
 *   (`LikePatterns.Contains`) và trả tới 500 dòng.
 * Không bật `showSearch` thì giống hệt `Select` của antd.
 * @param props Như `SelectProps` của antd.
 */
export function Select<V = unknown, O extends BaseOptionType | DefaultOptionType = DefaultOptionType>(props: SelectProps<V, O>) {
  const { showSearch, defaultActiveFirstOption, onKeyDown, ...rest } = props
  const [term, setTerm] = useState('')
  // Người dùng đã dùng mũi tên chọn dòng sau lần gõ cuối – khi đó Enter là chọn dòng đó, không chặn.
  const navigated = useRef(false)

  if (!showSearch) return <AntSelect<V, O> {...props} />

  const config = typeof showSearch === 'object' ? showSearch : {}
  const wildcard = hasWildcard(term)
  const search: SelectProps<V, O>['showSearch'] = {
    ...config,
    onSearch: (value: string) => {
      setTerm(value)
      navigated.current = false
      config.onSearch?.(value)
    },
    filterOption:
      config.filterOption === false
        ? false
        : (config.filterOption ?? ((input: string, option?: O) => matchesKeyword(optionText(option, config.optionFilterProp as string | undefined), input))),
  }

  // Bắt Enter ở pha capture (trước khi danh sách của antd chọn dòng đầu và đóng lại): đang gõ mẫu có `*` mà chưa dùng mũi tên thì
  // Enter chỉ để liệt kê – giữ danh sách mở như F4 của SAP.
  const onKeyDownCapture = (e: KeyboardEvent<HTMLElement>) => {
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') navigated.current = true
    if (e.key === 'Enter' && wildcard && !navigated.current) {
      e.preventDefault()
      e.stopPropagation()
    }
  }

  return (
    <span style={{ display: 'contents' }} onKeyDownCapture={onKeyDownCapture}>
      <AntSelect<V, O>
        {...rest}
        onKeyDown={onKeyDown}
        showSearch={search}
        defaultActiveFirstOption={wildcard ? false : defaultActiveFirstOption}
        onOpenChange={(open) => {
          if (!open) setTerm('')
          rest.onOpenChange?.(open)
        }}
      />
    </span>
  )
}
