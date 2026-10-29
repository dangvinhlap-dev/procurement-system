import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

/** Địa chỉ API khi chạy dev (cổng cố định – CLAUDE.md mục 2). */
const apiUrl = 'http://localhost:5000'

/**
 * Cấu hình Vite: cổng cố định 5173; chuyển tiếp /api và /health sang API
 * để trình duyệt gọi cùng nguồn, không cần cấu hình CORS khi dev.
 */
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': apiUrl,
      '/health': apiUrl,
    },
  },
})
