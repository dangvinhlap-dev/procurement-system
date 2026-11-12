import { CheckOutlined, LoginOutlined, LogoutOutlined, MailOutlined, SaveOutlined } from '@ant-design/icons'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, App, Button, Form, Input, Spin } from 'antd'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, Navigate, NavLink, Outlet, useLocation, useNavigate, useSearchParams } from 'react-router'
import { supplierStatusLabels } from '../api/masterData'
import { ClassicScreen } from '../classic/ClassicScreen'
import { PortalUserPanel } from '../components/PortalUserPanel/PortalUserPanel'
import { formatDateTime } from '../utils/format'
import {
  loadPortalSession,
  portalApi,
  savePortalSession,
  setPortalUnauthorizedHandler,
  type PortalInviteInfo,
  type PortalLoginChallenge,
  type PortalSessionData,
} from './portalApi'
import { PortalSessionContext, usePortalSession } from './portalSession'
import './portal.css'

/** Đường dẫn trang đăng nhập Portal. */
const PORTAL_LOGIN = '/ncc/dang-nhap'

/** Quy tắc mật khẩu hiện cho NCC – cùng chính sách máy chủ (SupplierPortalUser.ValidatePassword). */
const PASSWORD_RULE = 'Tối thiểu 8 ký tự, có chữ số và chữ thường.'

/**
 * Khung mọi trang Portal nhà cung cấp `/ncc` (design.md 19.5, lát P1; docs/ui-style.md mục 5f): giữ phiên Portal riêng (không dùng phiên
 * người dùng nội bộ), thanh trên dải màu khi đã đăng nhập. Đứng ngoài AppShell – NCC không thấy menu, cây điều hướng nội bộ.
 */
export function PortalLayout() {
  const [session, setSession] = useState<PortalSessionData | null>(() => loadPortalSession())
  const navigate = useNavigate()

  const signIn = useCallback((s: PortalSessionData) => {
    savePortalSession(s)
    setSession(s)
  }, [])
  const signOut = useCallback(() => {
    savePortalSession(null)
    setSession(null)
  }, [])

  // Token bị khóa / đổi mật khẩu ở nơi khác / hết hạn: về trang đăng nhập.
  useEffect(() => {
    setPortalUnauthorizedHandler(() => {
      signOut()
      navigate(PORTAL_LOGIN, { replace: true, state: { reason: 'Phiên đăng nhập Portal đã hết hiệu lực. Hãy đăng nhập lại.' } })
    })
    return () => setPortalUnauthorizedHandler(() => {})
  }, [navigate, signOut])

  const value = useMemo(() => ({ session, signIn, signOut }), [session, signIn, signOut])
  return (
    <PortalSessionContext.Provider value={value}>
      <ClassicScreen className="erp-portal">
        {session && (
          <header className="erp-portal-bar">
            <span className="erp-portal-brand">Portal nhà cung cấp</span>
            <nav className="erp-portal-nav" aria-label="Portal">
              <NavLink to="/ncc" end>
                Trang chủ
              </NavLink>
              {session.me.isSupplierAdmin && <NavLink to="/ncc/nguoi-dung">Người dùng</NavLink>}
              <NavLink to="/ncc/doi-mat-khau">Đổi mật khẩu</NavLink>
            </nav>
            <span className="erp-portal-user" title={`${session.me.fullName} – ${session.me.supplier.name}`}>
              {session.me.fullName} – {session.me.supplier.name}
            </span>
            <Button
              size="small"
              icon={<LogoutOutlined />}
              onClick={() => {
                signOut()
                navigate(PORTAL_LOGIN, { replace: true })
              }}
            >
              Đăng xuất
            </Button>
          </header>
        )}
        <Outlet />
      </ClassicScreen>
    </PortalSessionContext.Provider>
  )
}

/**
 * Trang Portal cần đăng nhập: chưa có phiên thì chuyển sang trang đăng nhập Portal.
 * @param children Nội dung trang.
 * @param adminOnly Chỉ quản trị NCC.
 */
export function RequirePortal({ children, adminOnly = false }: { children: ReactNode; adminOnly?: boolean }) {
  const { session } = usePortalSession()
  if (!session) return <Navigate to={PORTAL_LOGIN} replace />
  if (adminOnly && !session.me.isSupplierAdmin) return <Navigate to="/ncc" replace />
  return <main className="erp-portal-body">{children}</main>
}

/** Hộp thoại giữa màn hình cho trang đăng nhập / kích hoạt / quên mật khẩu. */
function PortalDialog({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="erp-portal-center">
      <section className="erp-portal-dialog" aria-label={title}>
        <h1>{title}</h1>
        <div className="erp-portal-dialog-body">{children}</div>
        <div className="erp-portal-footer">Portal nhà cung cấp – phiên bản {__APP_VERSION__}</div>
      </section>
    </div>
  )
}

const errorText = (e: unknown, fallback: string) => (e instanceof Error ? e.message : fallback)

/**
 * Đăng nhập Portal hai bước: email + mật khẩu, rồi mã OTP 6 số gửi qua email (design.md 19.5, lát P1).
 */
export function PortalLoginPage() {
  const { session, signIn } = usePortalSession()
  const navigate = useNavigate()
  const location = useLocation()
  const reason = (location.state as { reason?: string; notice?: string } | null) ?? null
  const [challenge, setChallenge] = useState<PortalLoginChallenge | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [code, setCode] = useState('')

  if (session) return <Navigate to="/ncc" replace />

  const submitPassword = async (values: { email: string; password: string }) => {
    setBusy(true)
    setError(null)
    try {
      setChallenge(await portalApi.login(values.email.trim(), values.password))
      setCode('')
    } catch (e) {
      setError(errorText(e, 'Không đăng nhập được.'))
    } finally {
      setBusy(false)
    }
  }

  const submitCode = async () => {
    if (!challenge) return
    setBusy(true)
    setError(null)
    try {
      signIn(await portalApi.verifyOtp(challenge.challengeId, code.trim()))
      navigate('/ncc', { replace: true })
    } catch (e) {
      setError(errorText(e, 'Mã xác nhận không đúng.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <PortalDialog title="Đăng nhập Portal nhà cung cấp">
      {reason?.notice && !error && !challenge && <Alert type="success" showIcon title={reason.notice} />}
      {reason?.reason && !error && !challenge && <Alert type="warning" showIcon title={reason.reason} />}
      {error && <Alert type="error" showIcon title={error} />}
      {!challenge ? (
        <Form layout="vertical" onFinish={(v) => void submitPassword(v)} requiredMark={false}>
          <Form.Item name="email" label="Email" rules={[{ required: true, message: 'Nhập email.' }]}>
            <Input autoComplete="username" autoFocus />
          </Form.Item>
          <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, message: 'Nhập mật khẩu.' }]}>
            <Input.Password autoComplete="current-password" />
          </Form.Item>
          <div className="erp-portal-actions">
            <Link to="/ncc/quen-mat-khau">Quên mật khẩu?</Link>
            <Button htmlType="submit" type="primary" icon={<LoginOutlined />} loading={busy}>
              Tiếp tục
            </Button>
          </div>
        </Form>
      ) : (
        <Form layout="vertical" onFinish={() => void submitCode()} requiredMark={false}>
          <Alert
            type="info"
            showIcon
            title={`Đã gửi mã xác nhận 6 số tới ${challenge.maskedEmail}. Mã có hiệu lực đến ${formatDateTime(challenge.expiresAt)}.`}
          />
          {challenge.devCode && <Alert type="warning" showIcon title={`Môi trường thử – mã xác nhận: ${challenge.devCode}`} />}
          <Form.Item label="Mã xác nhận (OTP)" required>
            <Input
              value={code}
              onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
              inputMode="numeric"
              autoComplete="one-time-code"
              maxLength={6}
              autoFocus
              aria-label="Mã xác nhận (OTP)"
            />
          </Form.Item>
          <div className="erp-portal-actions">
            <Button onClick={() => setChallenge(null)}>Quay lại</Button>
            <Button htmlType="submit" type="primary" icon={<CheckOutlined />} loading={busy} disabled={code.length !== 6}>
              Xác nhận
            </Button>
          </div>
        </Form>
      )}
    </PortalDialog>
  )
}

/** Ô mật khẩu mới + nhập lại, dùng ở kích hoạt, đặt lại, đổi mật khẩu. */
function NewPasswordFields() {
  return (
    <>
      <Form.Item name="password" label="Mật khẩu mới" extra={PASSWORD_RULE} rules={[{ required: true, message: 'Nhập mật khẩu mới.' }]}>
        <Input.Password autoComplete="new-password" />
      </Form.Item>
      <Form.Item
        name="confirm"
        label="Nhập lại mật khẩu mới"
        dependencies={['password']}
        rules={[
          { required: true, message: 'Nhập lại mật khẩu mới.' },
          ({ getFieldValue }) => ({
            validator: (_, v) => (v === getFieldValue('password') ? Promise.resolve() : Promise.reject(new Error('Hai lần nhập không khớp.'))),
          }),
        ]}
      >
        <Input.Password autoComplete="new-password" />
      </Form.Item>
    </>
  )
}

/**
 * Trang kích hoạt từ link mời `/ncc/kich-hoat?token=…`: NCC đặt mật khẩu lần đầu.
 */
export function PortalActivatePage() {
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const navigate = useNavigate()
  const [info, setInfo] = useState<PortalInviteInfo | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let alive = true
    portalApi
      .invite(token)
      .then((i) => alive && setInfo(i))
      .catch((e: unknown) => alive && setError(errorText(e, 'Link kích hoạt không hợp lệ.')))
      .finally(() => alive && setLoading(false))
    return () => {
      alive = false
    }
  }, [token])

  const submit = async (values: { password: string }) => {
    setBusy(true)
    setError(null)
    try {
      await portalApi.activate(token, values.password)
      navigate(PORTAL_LOGIN, { replace: true, state: { notice: 'Đã kích hoạt tài khoản. Hãy đăng nhập.' } })
    } catch (e) {
      setError(errorText(e, 'Không kích hoạt được.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <PortalDialog title="Kích hoạt tài khoản Portal">
      <Spin spinning={loading}>
        {error && <Alert type="error" showIcon title={error} />}
        {info && (
          <Form layout="vertical" onFinish={(v) => void submit(v)} requiredMark={false}>
            <dl className="erp-portal-fields" style={{ marginBottom: 8 }}>
              <dt>Nhà cung cấp</dt>
              <dd>{info.supplierName}</dd>
              <dt>Họ tên</dt>
              <dd>{info.fullName}</dd>
              <dt>Email đăng nhập</dt>
              <dd>{info.email}</dd>
            </dl>
            <NewPasswordFields />
            <div className="erp-portal-actions">
              <Link to={PORTAL_LOGIN}>Đăng nhập</Link>
              <Button htmlType="submit" type="primary" icon={<CheckOutlined />} loading={busy}>
                Kích hoạt
              </Button>
            </div>
          </Form>
        )}
      </Spin>
    </PortalDialog>
  )
}

/**
 * Quên mật khẩu: nhập email, máy chủ gửi link đặt lại (không báo email có tài khoản hay không).
 */
export function PortalForgotPasswordPage() {
  const [sent, setSent] = useState(false)
  const [devToken, setDevToken] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (values: { email: string }) => {
    setBusy(true)
    setError(null)
    try {
      const r = await portalApi.forgotPassword(values.email.trim())
      setDevToken(r.devToken)
      setSent(true)
    } catch (e) {
      setError(errorText(e, 'Không gửi được yêu cầu.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <PortalDialog title="Quên mật khẩu Portal">
      {error && <Alert type="error" showIcon title={error} />}
      {sent ? (
        <>
          <Alert type="success" showIcon title="Nếu email có tài khoản Portal đang dùng, link đặt lại mật khẩu đã được gửi tới email đó." />
          {devToken && (
            <Alert
              type="warning"
              showIcon
              title={<Link to={`/ncc/dat-lai-mat-khau?token=${encodeURIComponent(devToken)}`}>Môi trường thử – mở link đặt lại mật khẩu</Link>}
            />
          )}
          <Link to={PORTAL_LOGIN}>Về trang đăng nhập</Link>
        </>
      ) : (
        <Form layout="vertical" onFinish={(v) => void submit(v)} requiredMark={false}>
          <Form.Item name="email" label="Email đăng nhập" rules={[{ required: true, message: 'Nhập email.' }]}>
            <Input autoComplete="username" autoFocus />
          </Form.Item>
          <div className="erp-portal-actions">
            <Link to={PORTAL_LOGIN}>Đăng nhập</Link>
            <Button htmlType="submit" type="primary" icon={<MailOutlined />} loading={busy}>
              Gửi link đặt lại
            </Button>
          </div>
        </Form>
      )}
    </PortalDialog>
  )
}

/**
 * Đặt lại mật khẩu từ link `/ncc/dat-lai-mat-khau?token=…`.
 */
export function PortalResetPasswordPage() {
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const navigate = useNavigate()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (values: { password: string }) => {
    setBusy(true)
    setError(null)
    try {
      await portalApi.resetPassword(token, values.password)
      navigate(PORTAL_LOGIN, { replace: true, state: { notice: 'Đã đặt lại mật khẩu. Hãy đăng nhập bằng mật khẩu mới.' } })
    } catch (e) {
      setError(errorText(e, 'Không đặt lại được mật khẩu.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <PortalDialog title="Đặt lại mật khẩu Portal">
      {error && <Alert type="error" showIcon title={error} />}
      <Form layout="vertical" onFinish={(v) => void submit(v)} requiredMark={false}>
        <NewPasswordFields />
        <div className="erp-portal-actions">
          <Link to={PORTAL_LOGIN}>Đăng nhập</Link>
          <Button htmlType="submit" type="primary" icon={<SaveOutlined />} loading={busy}>
            Lưu mật khẩu
          </Button>
        </div>
      </Form>
    </PortalDialog>
  )
}

/** Khối có dải tiêu đề. */
function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="erp-portal-section">
      <h2>{title}</h2>
      <div className="erp-portal-section-body">{children}</div>
    </section>
  )
}

/**
 * Trang chủ Portal: thông tin NCC và tài khoản đang dùng. Báo giá, đơn hàng, giao hàng, hóa đơn có ở các lát sau (P3–P6).
 */
export function PortalHomePage() {
  const { session } = usePortalSession()
  if (!session) return null
  const { me } = session
  const s = me.supplier
  return (
    <>
      <h1 className="erp-portal-title">Trang chủ</h1>
      <Section title="Thông tin nhà cung cấp">
        <dl className="erp-portal-fields" data-testid="portal-supplier">
          <dt>Mã NCC</dt>
          <dd>{s.code}</dd>
          <dt>Tên NCC</dt>
          <dd>{s.name}</dd>
          <dt>Mã số thuế</dt>
          <dd>{s.taxCode ?? '–'}</dd>
          <dt>Địa chỉ</dt>
          <dd>{s.address ?? '–'}</dd>
          <dt>Điện thoại</dt>
          <dd>{s.phone ?? '–'}</dd>
          <dt>Email</dt>
          <dd>{s.email ?? '–'}</dd>
          <dt>Tình trạng</dt>
          <dd>{supplierStatusLabels[s.status]}</dd>
        </dl>
      </Section>
      <Section title="Tài khoản của tôi">
        <dl className="erp-portal-fields">
          <dt>Họ tên</dt>
          <dd>{me.fullName}</dd>
          <dt>Email đăng nhập</dt>
          <dd>{me.email}</dd>
          <dt>Chức vụ</dt>
          <dd>{me.position ?? '–'}</dd>
          <dt>Vai trò</dt>
          <dd>{me.isSupplierAdmin ? 'Quản trị NCC – mời, khóa người dùng của NCC' : 'Người dùng'}</dd>
        </dl>
      </Section>
      <Alert
        type="info"
        showIcon
        title="Thư mời báo giá, xác nhận đơn hàng, báo giao hàng và hóa đơn sẽ có trên Portal ở các bản cập nhật tới. Thông tin NCC cần sửa, xin báo Phòng Cung ứng vật tư."
      />
    </>
  )
}

/**
 * Trang "Người dùng" của quản trị NCC: mời người dùng con, gửi lại lời mời, khóa / mở khóa (Q11).
 */
export function PortalUsersPage() {
  const { session } = usePortalSession()
  const queryClient = useQueryClient()
  const users = useQuery({ queryKey: ['portal', 'users', session?.me.userId ?? ''], queryFn: portalApi.users })
  const rows = users.data ?? []
  const loading = users.isFetching

  const after = async <T,>(p: Promise<T>) => {
    const r = await p
    await queryClient.invalidateQueries({ queryKey: ['portal', 'users'] })
    return r
  }

  return (
    <>
      <h1 className="erp-portal-title">Người dùng của nhà cung cấp</h1>
      <PortalUserPanel
        rows={rows}
        loading={loading}
        canManage
        portalSelfId={session?.me.userId}
        inviteLabel="Mời người dùng…"
        inviteHint="Người được mời nhận link kích hoạt qua email, tự đặt mật khẩu. Người dùng xem và thao tác dữ liệu của NCC mình, không quản lý người dùng."
        onInvite={(input) => after(portalApi.inviteUser(input))}
        onResend={(id) => after(portalApi.resendInvite(id))}
        onDisable={(id) => after(portalApi.disableUser(id))}
        onEnable={(id) => after(portalApi.enableUser(id))}
      />
    </>
  )
}

/**
 * Đổi mật khẩu khi đã đăng nhập; máy chủ trả token mới (token cũ hết hiệu lực).
 */
export function PortalChangePasswordPage() {
  const { signIn } = usePortalSession()
  const { message } = App.useApp()
  const [form] = Form.useForm()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (values: { current: string; password: string }) => {
    setBusy(true)
    setError(null)
    try {
      signIn(await portalApi.changePassword(values.current, values.password))
      form.resetFields()
      void message.success('Đã đổi mật khẩu.')
    } catch (e) {
      setError(errorText(e, 'Không đổi được mật khẩu.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <h1 className="erp-portal-title">Đổi mật khẩu</h1>
      <Section title="Mật khẩu">
        <div style={{ maxWidth: 360 }}>
          {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
          <Form form={form} layout="vertical" onFinish={(v) => void submit(v)} requiredMark={false}>
            <Form.Item name="current" label="Mật khẩu hiện tại" rules={[{ required: true, message: 'Nhập mật khẩu hiện tại.' }]}>
              <Input.Password autoComplete="current-password" />
            </Form.Item>
            <NewPasswordFields />
            <Button htmlType="submit" type="primary" icon={<SaveOutlined />} loading={busy}>
              Lưu
            </Button>
          </Form>
        </div>
      </Section>
    </>
  )
}
