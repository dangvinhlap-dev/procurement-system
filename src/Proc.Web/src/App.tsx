import { useQuery } from '@tanstack/react-query'
import { Alert, Descriptions, Layout, Spin, Typography } from 'antd'
import { fetchHealth } from './api/health'

/**
 * Trang tạm của bản khởi tạo: chỉ hiển thị trạng thái kết nối API và CSDL.
 * Sẽ được thay bằng AppShell + ListPage ở lát giao diện khung (design.md mục 13.3, bước 2).
 */
export default function App() {
  const health = useQuery({ queryKey: ['health'], queryFn: fetchHealth, retry: false })

  return (
    <Layout style={{ minHeight: '100vh', padding: 16 }}>
      <Typography.Title level={4}>Quản lý mua hàng và kho</Typography.Title>
      {health.isPending && <Spin />}
      {health.isError && (
        <Alert type="error" showIcon title="Không kết nối được API" description={health.error.message} />
      )}
      {health.data && (
        <Descriptions bordered size="small" column={1} style={{ maxWidth: 480 }}>
          <Descriptions.Item label="Trạng thái hệ thống">
            {health.data.status === 'Healthy' ? 'Hoạt động bình thường' : 'Có lỗi'}
          </Descriptions.Item>
          {health.data.checks.map((c) => (
            <Descriptions.Item key={c.name} label={c.name === 'database' ? 'Cơ sở dữ liệu' : c.name}>
              {c.status === 'Healthy' ? 'Kết nối được' : (c.description ?? 'Lỗi')}
            </Descriptions.Item>
          ))}
          <Descriptions.Item label="Phiên bản">{health.data.version}</Descriptions.Item>
        </Descriptions>
      )}
    </Layout>
  )
}
