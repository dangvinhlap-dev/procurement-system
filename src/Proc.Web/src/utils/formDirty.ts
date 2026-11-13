import type { FormInstance } from 'antd'

/**
 * Dấu "có thay đổi chưa lưu" của từng form antd (yêu cầu người dùng 13/11/2026: hỏi lại khi rời trang còn phiếu chưa lưu).
 *
 * Tại sao đánh dấu thay vì so giá trị: form phiếu có dayjs, dòng hàng, ô ẩn… – so cả form tốn kém và dễ lệch (máy chủ làm tròn số).
 * `onValuesChange` của antd chỉ chạy khi **người dùng** sửa ô (không chạy khi trang gọi `setFieldsValue`), nên:
 * - người dùng gõ → `markFormDirty`;
 * - nạp phiếu từ máy chủ, lưu thành công (useDocumentFormSync), mở ngăn kéo → `markFormClean`.
 * WeakMap: form bỏ đi thì dấu tự mất theo.
 */
const dirtyForms = new WeakMap<FormInstance, boolean>()

/** Đánh dấu form có thay đổi chưa lưu. @param form Form antd. */
export function markFormDirty(form: FormInstance): void {
  dirtyForms.set(form, true)
}

/** Bỏ dấu thay đổi chưa lưu (vừa nạp hoặc vừa lưu). @param form Form antd. */
export function markFormClean(form: FormInstance): void {
  dirtyForms.delete(form)
}

/**
 * Form có thay đổi chưa lưu không.
 * @param form Form antd.
 * @returns true nếu người dùng đã sửa từ lần nạp/lưu gần nhất.
 */
export function isFormDirty(form: FormInstance): boolean {
  return dirtyForms.get(form) === true
}
