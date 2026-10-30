/** Lớp phủ có thể nằm phía trên trang/ngăn kéo: danh sách thả xuống, lịch, hộp thoại. */
const POPUP_SELECTOR = [
  '.ant-select-dropdown:not(.ant-select-dropdown-hidden)',
  '.ant-picker-dropdown:not(.ant-picker-dropdown-hidden)',
  '.ant-modal-wrap:not([style*="display: none"])',
].join(', ')

/**
 * Có lớp phủ nào đang mở không – để phím Esc/Enter dành cho lớp phủ đó trước. Bỏ qua lớp phủ đang chạy hiệu ứng ẩn (lớp *-leave):
 * vừa chọn xong một mục, danh sách thả xuống còn ẩn dần vài trăm mili giây.
 * @returns true nếu đang có lớp phủ mở.
 */
export function hasOpenPopup(): boolean {
  return [...document.querySelectorAll(POPUP_SELECTOR)].some((e) => !/-leave(\s|$)/.test(e.className))
}
