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
 * Từ khóa có ký tự đại diện `*` kiểu SAP ECC (yêu cầu người dùng 02/11/2026): `*` một mình = liệt kê tất cả.
 * @param keyword Từ khóa người dùng gõ.
 * @returns true khi có dấu *.
 */
export function hasWildcard(keyword: string): boolean {
  return keyword.includes('*')
}

/** Một từ khóa có `*` thành biểu thức chính quy "chứa": `b*long` → /b.*long/ (ký tự khác được thoát). */
function wildcardRegex(word: string): RegExp {
  const escaped = word.split('*').map((part) => part.replace(/[.+?^${}()|[\]\\]/g, '\\$&'))
  return new RegExp(escaped.join('.*'))
}

/**
 * Kiểm tra một chuỗi có chứa từ khóa không, bỏ qua dấu và hoa/thường.
 * Từ khóa nhiều từ thì mọi từ đều phải có mặt (không cần đúng thứ tự). Dấu `*` là ký tự đại diện kiểu SAP ECC:
 * `*` khớp mọi dòng, `b*long` khớp "Bu lông".
 * @param haystack Chuỗi nguồn (ví dụ nội dung các ô của một dòng).
 * @param keyword Từ khóa người dùng gõ.
 * @returns true khi khớp hoặc từ khóa rỗng.
 */
export function matchesKeyword(haystack: string, keyword: string): boolean {
  const words = normalizeVi(keyword).split(' ').filter(Boolean)
  if (words.length === 0) return true
  const source = normalizeVi(haystack)
  return words.every((w) => (hasWildcard(w) ? wildcardRegex(w).test(source) : source.includes(w)))
}
