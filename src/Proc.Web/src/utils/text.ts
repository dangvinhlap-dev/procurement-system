/**
 * Chuẩn hóa chuỗi tiếng Việt để so khớp tìm kiếm: bỏ dấu, đổi đ/Đ thành d, chữ thường, gộp khoảng trắng.
 * Nhờ vậy gõ "cho duyet" vẫn tìm được "Chờ duyệt" (CLAUDE.md mục 5).
 * Ví dụ: "Đơn  Mua Hàng" → "don mua hang".
 * @param value Chuỗi cần chuẩn hóa.
 * @returns Chuỗi không dấu, chữ thường.
 */
export function normalizeVi(value: string): string {
  return value
    .normalize('NFD')
    // Khối U+0300–U+036F là các dấu thanh/dấu mũ tách ra sau khi NFD.
    .replace(/[̀-ͯ]/g, '')
    .replace(/[đĐ]/g, 'd')
    .toLowerCase()
    .replace(/\s+/g, ' ')
    .trim()
}

/**
 * Kiểm tra một chuỗi có chứa từ khóa không, bỏ qua dấu và hoa/thường.
 * Từ khóa nhiều từ thì mọi từ đều phải có mặt (không cần đúng thứ tự).
 * @param haystack Chuỗi nguồn (ví dụ nội dung các ô của một dòng).
 * @param keyword Từ khóa người dùng gõ.
 * @returns true khi khớp hoặc từ khóa rỗng.
 */
export function matchesKeyword(haystack: string, keyword: string): boolean {
  const words = normalizeVi(keyword).split(' ').filter(Boolean)
  if (words.length === 0) return true
  const source = normalizeVi(haystack)
  return words.every((w) => source.includes(w))
}
