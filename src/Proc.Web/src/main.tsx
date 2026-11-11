import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from './app/queryClient'
import { App as AntApp, ConfigProvider } from 'antd'
import viVN from 'antd/locale/vi_VN'
import dayjs from 'dayjs'
import 'dayjs/locale/vi'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router'
import { AuthProvider } from './auth/AuthProvider'
import { renderRequiredMark } from './components/RequiredMark/requiredMark'
import './index.css'
import { router } from './router'
// Phong cách ERP desktop cổ điển cho toàn ứng dụng – nạp sau CSS của các component dùng chung (đi theo router) để ghi đè được.
import './classic/classic-app.css'
import { appTheme } from './theme'

// Ngày giờ hiển thị tiếng Việt (dd/MM/yyyy) – CLAUDE.md mục 5.
dayjs.locale('vi')

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ConfigProvider locale={viVN} theme={appTheme} form={{ requiredMark: renderRequiredMark }}>
      {/* AntApp gắn lớp chứa biến CSS của theme, nên CSS của AppShell/ListPage/DataGrid đọc được var(--ant-*). */}
      <AntApp>
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <RouterProvider router={router} />
          </AuthProvider>
        </QueryClientProvider>
      </AntApp>
    </ConfigProvider>
  </StrictMode>,
)
