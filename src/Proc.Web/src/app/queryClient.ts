import { QueryClient } from '@tanstack/react-query'

/**
 * Bộ nhớ đệm TanStack Query dùng chung cả ứng dụng. Tách khỏi main.tsx để code chạy ngoài cây React Query (hộp thoại của App.useApp() nằm
 * ngoài QueryClientProvider) vẫn làm mới dữ liệu được – ví dụ tải file của người duyệt xong thì tải lại danh sách đính kèm.
 */
export const queryClient = new QueryClient()
