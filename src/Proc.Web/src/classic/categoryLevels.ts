/** Tên 4 cấp nhóm hàng (người dùng chốt 14/11/2026 – design.md mục 5.7, N4-1). Chỉ số = cấp. */
export const CATEGORY_LEVEL_NAMES = ['', 'Ngành hàng', 'Nhóm hàng', 'Loại hàng', 'Chủng loại'] as const

/** Số cấp tối đa của cây nhóm hàng (design.md mục 5.7, N4-1) – trùng `ItemCategory.MaxLevel` ở máy chủ. */
export const CATEGORY_MAX_LEVEL = 4
