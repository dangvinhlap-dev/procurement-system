import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

/** Địa chỉ API khi chạy dev (cổng cố định – CLAUDE.md mục 2). */
const apiUrl = 'http://localhost:5000'

/**
 * Đọc số phiên bản từ Directory.Build.props ở gốc repo – nơi duy nhất ghi phiên bản (CLAUDE.md mục 8),
 * để thanh trạng thái luôn hiện đúng phiên bản mà không phải sửa ở hai nơi.
 * @returns Chuỗi phiên bản, ví dụ "0.1.0".
 * @throws Error khi không tìm thấy thẻ Version.
 */
function readAppVersion(): string {
  const propsPath = fileURLToPath(new URL('../../Directory.Build.props', import.meta.url))
  const match = /<Version>\s*([^<\s]+)\s*<\/Version>/.exec(readFileSync(propsPath, 'utf8'))
  if (!match) throw new Error('Không tìm thấy <Version> trong Directory.Build.props')
  return match[1]
}

/**
 * Cấu hình Vite: cổng cố định 5173; chuyển tiếp /api và /health sang API
 * để trình duyệt gọi cùng nguồn, không cần cấu hình CORS khi dev.
 */
export default defineConfig({
  plugins: [react()],
  define: {
    __APP_VERSION__: JSON.stringify(readAppVersion()),
  },
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': apiUrl,
      '/health': apiUrl,
    },
  },
})
