import type { ReactNode } from 'react'
import { formatDateTime } from '../utils/format'

/** Một mục hỏi đáp hiển thị: câu hỏi (kèm trả lời nếu có) hoặc thông báo làm rõ. */
export interface QaThreadItem {
  id: string
  /** Thông báo làm rõ (không có câu hỏi). */
  isNotice: boolean
  /** Nhãn người hỏi, ví dụ "NCC001 – Công ty A (ban@a.vn)" hoặc "Câu hỏi của bạn (ban@a.vn)". */
  askedBy?: string | null
  askedAt?: string | null
  question?: string | null
  /** Câu trả lời / nội dung thông báo; null = chưa trả lời. */
  answer?: string | null
  answeredAt?: string | null
  /** Người trả lời / đăng (chỉ hiện phía nội bộ). */
  answeredBy?: string | null
}

/**
 * Luồng hỏi đáp kiểu ERP cổ điển (lát P3b-2 – docs/ui-style.md mục 5g): mỗi mục là khung viền 1px, dải đầu ghi loại – người – giờ;
 * câu hỏi chưa trả lời có đèn vàng kèm chữ "Chưa trả lời", thông báo làm rõ có dải màu tab. Dùng chung cho Portal NCC và tab Hỏi đáp RFQ.
 * @param items Các mục, cũ trước.
 * @param emptyText Chữ khi chưa có mục nào.
 * @param action Nút thêm cho từng mục (ví dụ "Trả lời…" phía nội bộ); null = không có.
 */
export function QaThread({ items, emptyText, action }: {
  items: QaThreadItem[]
  emptyText: string
  action?: (item: QaThreadItem) => ReactNode
}) {
  if (items.length === 0) return <p className="erp-qa-empty">{emptyText}</p>
  return (
    <ol className="erp-qa" aria-label="Hỏi đáp">
      {items.map((item, index) => (
        <li key={item.id} className={item.isNotice ? 'erp-qa-item is-notice' : 'erp-qa-item'} data-testid="qa-item">
          <div className="erp-qa-head">
            <span className="erp-qa-kind">{item.isNotice ? 'Thông báo làm rõ' : `Câu hỏi ${index + 1}`}</span>
            {!item.isNotice && item.askedBy && <span>{item.askedBy}</span>}
            {!item.isNotice && <span className="erp-qa-time">{formatDateTime(item.askedAt)}</span>}
            {item.isNotice && <span className="erp-qa-time">{formatDateTime(item.answeredAt)}</span>}
            {item.isNotice && item.answeredBy && <span>{item.answeredBy}</span>}
            {!item.isNotice && !item.answer && (
              <span className="erp-qa-pending" title="Chưa trả lời">
                <i aria-hidden /> Chưa trả lời
              </span>
            )}
            {action && <span className="erp-qa-action">{action(item)}</span>}
          </div>
          {item.isNotice ? (
            <div className="erp-qa-text">{item.answer}</div>
          ) : (
            <>
              <div className="erp-qa-text">{item.question}</div>
              {item.answer && (
                <div className="erp-qa-answer">
                  <div className="erp-qa-answer-head">
                    Trả lời{item.answeredBy ? ` – ${item.answeredBy}` : ''} – {formatDateTime(item.answeredAt)}
                  </div>
                  <div className="erp-qa-text">{item.answer}</div>
                </div>
              )}
            </>
          )}
        </li>
      ))}
    </ol>
  )
}
