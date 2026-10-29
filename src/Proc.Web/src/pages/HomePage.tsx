import { useQuery } from '@tanstack/react-query'
import { Alert, Descriptions, Spin, Typography } from 'antd'
import { fetchHealth } from '../api/health'

/**
 * Trang chủ: hiện trạng thái kết nối API và CSDL. Dashboard thật làm ở giai đoạn 7 (design.md mục 13.2).
 */
export function HomePage() {
  const health = useQuery({ queryKey: ['health'], queryFn: fetchHealth, retry: false })

  return (
    <div className="simple-page">
      <Typography.Title level={4}>Trang chủ</Typography.Title>
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
          <Descriptions.Item label="Phiên bản API">{health.data.version}</Descriptions.Item>
        </Descriptions>
      )}
    </div>
  )
}
