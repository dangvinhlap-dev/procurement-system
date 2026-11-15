import { ReloadOutlined, WarningFilled } from '@ant-design/icons'
import { Button } from 'antd'
import { Component, useEffect, useState, type ErrorInfo, type ReactNode } from 'react'
import { useRouteError } from 'react-router'
import { reportClientError } from '../app/clientErrorReporter'

/**
 * Khối báo lỗi giao diện (lát QT2 – ui-style.md mục 5k): đèn đỏ + câu báo lỗi, mã theo dõi để báo quản trị (trùng mã trên màn hình Nhật ký lỗi
 * ERR), nút Thử lại / Tải lại trang.
 * @param code Mã theo dõi; undefined = đang gửi; null = không gửi được (chưa đăng nhập, mất mạng).
 * @param onRetry Vẽ lại phần bị lỗi (không có thì ẩn nút).
 */
export function ErrorFallback({ code, onRetry }: { code: string | null | undefined; onRetry?: () => void }) {
  return (
    <div className="erp-error-fallback" role="alert" data-testid="error-fallback">
      <div className="erp-error-fallback-title">
        <WarningFilled /> Màn hình gặp lỗi giao diện
      </div>
      <div>
        {code === undefined
          ? 'Đang ghi lỗi vào nhật ký…'
          : code
            ? <>Mã theo dõi <span className="erp-error-fallback-code">{code}</span> – hãy thử lại; lỗi còn thì báo quản trị hệ thống kèm mã này.</>
            : 'Không ghi được lỗi vào nhật ký – hãy thử lại hoặc báo quản trị hệ thống.'}
      </div>
      <div className="erp-error-fallback-actions">
        {onRetry && <Button onClick={onRetry}>Thử lại</Button>}
        <Button icon={<ReloadOutlined />} onClick={() => window.location.reload()}>
          Tải lại trang
        </Button>
      </div>
    </div>
  )
}

interface ErrorBoundaryProps {
  children: ReactNode
  /** Đổi giá trị này (ví dụ đường dẫn trang) thì tự bỏ trạng thái lỗi, vẽ lại. */
  resetKey?: string
}

interface ErrorBoundaryState {
  failed: boolean
  code: string | null | undefined
  resetKey?: string
}

/**
 * Bắt lỗi khi vẽ component con (lát QT2): gửi về nhật ký lỗi (kèm cây component), thay phần hỏng bằng {@link ErrorFallback} – các phần khác
 * của trang (thanh menu, thẻ màn hình khác) vẫn dùng được.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { failed: false, code: undefined, resetKey: this.props.resetKey }

  /** Có lỗi thì chuyển sang khối báo lỗi; đổi resetKey thì bỏ lỗi. */
  static getDerivedStateFromError(): Partial<ErrorBoundaryState> {
    return { failed: true, code: undefined }
  }

  /** Đổi trang (resetKey) thì thử vẽ lại. */
  static getDerivedStateFromProps(props: ErrorBoundaryProps, state: ErrorBoundaryState): Partial<ErrorBoundaryState> | null {
    return props.resetKey !== state.resetKey ? { failed: false, code: undefined, resetKey: props.resetKey } : null
  }

  /** Gửi lỗi về máy chủ, nhận mã theo dõi. */
  componentDidCatch(error: Error, info: ErrorInfo) {
    void reportClientError(error, 'render', info.componentStack).then((code) => this.setState({ code }))
  }

  render() {
    if (!this.state.failed) return this.props.children
    return <ErrorFallback code={this.state.code} onRetry={() => this.setState({ failed: false, code: undefined })} />
  }
}

/**
 * Trang lỗi của bộ định tuyến (`errorElement`): lỗi khi vẽ trang ngoài thẻ màn hình (đăng nhập, khung AppShell, Portal, trang điện thoại) –
 * gửi về nhật ký lỗi như {@link ErrorBoundary}.
 */
export function RouteErrorFallback() {
  const error = useRouteError()
  const [code, setCode] = useState<string | null | undefined>(undefined)
  useEffect(() => {
    let alive = true
    void reportClientError(error, 'render').then((c) => alive && setCode(c))
    return () => {
      alive = false
    }
  }, [error])
  return (
    <div className="erp-error-page">
      <ErrorFallback code={code} />
    </div>
  )
}
