import { App } from 'antd'
import { useCallback, useEffect, useRef, useState } from 'react'
import type { RouteObject } from 'react-router'
import { findScreenByPath } from '../../app/screens'
import { useAuth } from '../../auth/useAuth'
import { useDashboard } from '../../api/dashboard'
import { demoPinnedScreens } from '../../mocks/session'
import { layoutCssVars } from '../../theme'
import { FieldHelpHost } from '../FieldHelp/FieldHelpHost'
import { ChangePasswordDialog } from './ChangePasswordDialog'
import { NavTree } from './NavTree'
import { ShellBar } from './ShellBar'
import { focusListSearch } from '../../app/commands'
import { MAX_SCREEN_TABS, ScreenTabsContext } from '../../app/screenTabs'
import { ScreenTabBar, ScreenTabPanel } from './ScreenTabs'
import { screenTabTitle, useScreenTabsState } from './useScreenTabsState'
import { useUnsavedGuard } from './useUnsavedGuard'
import './AppShell.css'

/** Tên ứng dụng ghép vào tiêu đề tab trình duyệt. */
const APP_NAME = 'Quản lý mua hàng và kho'

/**
 * Ngày làm việc theo giờ Việt Nam, dd/MM/yyyy (CLAUDE.md mục 5) – không phụ thuộc múi giờ của máy.
 */
function todayInVietnam(): string {
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(new Date())
}

/**
 * Khung ứng dụng cố định cho mọi trang theo phong cách ERP desktop cổ điển (docs/ui-style.md, design.md mục 17): thanh menu chữ + thanh
 * công cụ 54px trên cùng, cây điều hướng trái 240px, vùng nội dung (ListPage/DocumentForm). Phiên bản và ngày làm việc nằm trong menu người dùng.
 * Phím tắt chung: F1 xem lịch sử thay đổi của ô (FieldHelpHost), F3 tìm kiếm trong danh sách, Alt + chữ gạch chân mở menu module (trong ShellBar).
 * Người dùng và công ty lấy từ phiên đăng nhập; số đếm và số thông báo chưa đọc theo số liệu trang chủ; ghim còn là dữ liệu giả (src/mocks/session.ts).
 * AppShell luôn nằm trong RequireAuth nên phiên đăng nhập chắc chắn có.
 * Vùng nội dung chia **tối đa 4 thẻ màn hình** (yêu cầu người dùng 13/11/2026, docs/ui-style.md mục 8a): mỗi thẻ giữ nguyên trang đang mở
 * khi chuyển sang thẻ khác; Alt+1…4 chuyển thẻ.
 * @param routes Các trang con của khung (cùng mảng khai báo trong router) – mỗi thẻ tự vẽ trang theo đường dẫn riêng.
 */
export function AppShell({ routes }: { routes: RouteObject[] }) {
  const { session, workingCompany, setWorkingCompany, logout } = useAuth()
  const { message, modal } = App.useApp()
  const companyId = workingCompany?.id ?? session?.user.defaultCompanyId ?? ''
  // Số đếm cây điều hướng lấy từ số liệu trang chủ (design.md mục 11.4), theo quyền và công ty đang làm việc.
  const navCounts = useDashboard().data?.navCounts ?? {}
  const [changingPassword, setChangingPassword] = useState(false)
  const onFull = useCallback(() => {
    void message.warning({
      key: 'screen-tabs-full',
      content: `Đã mở đủ ${MAX_SCREEN_TABS} màn hình – đóng bớt một màn hình (nút × trên thẻ) hoặc Shift+bấm để mở thay ở màn hình đang xem.`,
    })
  }, [message])
  const { tabs, activeTab, api, switchTo, close, keepOnly, setLabel, isTabNavigating } = useScreenTabsState(onFull, companyId)
  const tabsRef = useRef(tabs)
  useEffect(() => {
    tabsRef.current = tabs
  }, [tabs])
  // Hỏi lại khi rời trang còn phiếu chưa lưu (yêu cầu người dùng 13/11/2026).
  const tabName = useCallback((id: string) => {
    const tab = tabsRef.current.find((t) => t.id === id)
    return tab ? screenTabTitle(tab).short : 'màn hình đã đóng'
  }, [])
  const { register, confirmDiscard } = useUnsavedGuard({ activeId: activeTab.id, isTabNavigating, tabName })
  const closeTab = useCallback((id: string) => confirmDiscard([id], () => close(id)), [confirmDiscard, close])
  // Màn hình theo thẻ đang xem (không theo địa chỉ – địa chỉ có thể chậm một nhịp khi bấm chuyển thẻ liên tiếp).
  const { pathname } = activeTab.location
  const screen = findScreenByPath(pathname)
  const screenTitle = screen?.title ?? 'Không tìm thấy màn hình'

  useEffect(() => {
    document.title = `${screenTitle} – ${APP_NAME}`
  }, [screenTitle])

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'F3') {
        // F3 mặc định của trình duyệt là "tìm trong trang"; ở đây dùng để tìm trong danh sách.
        if (focusListSearch()) e.preventDefault()
      }
      // Alt+1…4: chuyển thẻ màn hình (Alt + chữ đã dành cho menu module).
      if (e.altKey && !e.ctrlKey && !e.metaKey && /^Digit[1-9]$/.test(e.code)) {
        const tab = tabsRef.current[Number(e.code.slice(5)) - 1]
        if (tab) {
          e.preventDefault()
          switchTo(tab.id)
        }
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [switchTo])

  if (!session) return null
  const user = session.user

  return (
    <ScreenTabsContext value={api}>
    <div className="app-shell" style={layoutCssVars()}>
      <ShellBar
        appName={APP_NAME}
        activeGroup={screen?.group}
        companies={user.companies.map((c) => ({ value: c.id, label: `${c.code} – ${c.shortName ?? c.name}` }))}
        company={workingCompany?.id ?? user.defaultCompanyId}
        onCompanyChange={(id) => {
          const company = user.companies.find((c) => c.id === id)
          const apply = () => {
            // Đang mở phiếu thì về danh sách của phiếu – phiếu thuộc công ty cũ, không mở được ở công ty mới.
            if (tabs.length > 1) keepOnly(screen?.path ?? '/')
            setWorkingCompany(id)
            if (company) message.success(`Đang làm việc tại ${company.code} – ${company.name}.`)
          }
          // Mọi thẻ thuộc công ty đang làm việc (người dùng chốt 13/11/2026): đổi công ty khi đang mở nhiều màn hình thì hỏi lại rồi đóng bớt.
          // Một thẻ: trang dựng lại dưới công ty mới – còn phiếu chưa lưu thì hỏi bỏ thay đổi.
          if (tabs.length <= 1) {
            confirmDiscard([activeTab.id], apply)
            return
          }
          modal.confirm({
            title: 'Đổi công ty đang làm việc',
            content: `Đang mở ${tabs.length} màn hình của ${workingCompany?.code ?? 'công ty hiện tại'}. Đổi sang ${company?.code ?? 'công ty khác'} sẽ đóng các màn hình này (dữ liệu chưa lưu sẽ mất), chỉ mở lại ${screen?.navLabel ?? 'Trang chủ'}.`,
            okText: 'Đổi công ty',
            cancelText: 'Không đổi',
            // Hỏi tiếp nếu còn phiếu chưa lưu ở các thẻ sẽ đóng.
            onOk: () => confirmDiscard(tabs.map((t) => t.id), apply),
          })
        }}
        // Số thông báo chưa đọc đi cùng số liệu trang chủ (design.md mục 11.6).
        unreadCount={navCounts.NOTI?.count ?? 0}
        userFullName={user.fullName}
        userName={user.userName}
        workingDate={todayInVietnam()}
        version={__APP_VERSION__}
        onLogout={() => confirmDiscard(tabs.map((t) => t.id), () => logout(), true)}
        onChangePassword={() => setChangingPassword(true)}
      />
      <ChangePasswordDialog open={changingPassword} onClose={() => setChangingPassword(false)} />
      <FieldHelpHost />
      <div className="app-body">
        {/* Trang con (phiếu /kho/grn/123) đánh dấu màn hình cha trên cây điều hướng. */}
        <NavTree activePath={screen?.path ?? pathname} pinnedCodes={demoPinnedScreens} counts={navCounts} />
        <main className="app-content app-content-tabs">
          <ScreenTabBar tabs={tabs} activeId={activeTab.id} onSelect={switchTo} onClose={closeTab} companyCode={workingCompany?.code} />
          <div className="app-tab-panels">
            {tabs.map((t) => (
              <ScreenTabPanel key={t.id} tab={t} active={t.id === activeTab.id} routes={routes} onLabel={setLabel} onRegister={register} />
            ))}
          </div>
        </main>
      </div>
    </div>
    </ScreenTabsContext>
  )
}
