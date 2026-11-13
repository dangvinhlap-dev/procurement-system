import { CloseOutlined } from '@ant-design/icons'
import { ConfigProvider } from 'antd'
import { memo, useCallback, useState, type ReactNode } from 'react'
import { UNSAFE_RouteContext, useRoutes, type Location, type RouteObject } from 'react-router'
import { findScreenByPath } from '../../app/screens'
import { MAX_SCREEN_TABS, TabActiveContext, TabLabelContext } from '../../app/screenTabs'
import type { ScreenTab } from './useScreenTabsState'

/**
 * Tên hiện trên thẻ: tên màn hình; trang con (phiếu) thêm nhãn phụ do trang đặt (số phiếu), chưa có thì "Lập mới"/"Phiếu".
 * @param tab Thẻ cần đặt tên.
 * @returns Tên ngắn (trên thẻ) và tên đầy đủ (chú thích khi rê chuột).
 */
function screenTabTitle(tab: ScreenTab): { short: string; full: string; icon?: ReactNode } {
  const { pathname } = tab.location
  const screen = findScreenByPath(pathname)
  if (!screen) return { short: 'Không tìm thấy màn hình', full: pathname }
  if (screen.path === pathname) return { short: screen.navLabel, full: `${screen.title} (${screen.code})`, icon: screen.icon }
  const sub = tab.label ?? (pathname.endsWith('/moi') ? 'Lập mới' : 'Phiếu')
  return { short: `${screen.navLabel}: ${sub}`, full: `${screen.title} – ${sub}`, icon: screen.icon }
}

interface ScreenTabBarProps {
  tabs: ScreenTab[]
  activeId: string
  onSelect: (id: string) => void
  onClose: (id: string) => void
  /** Mã công ty đang làm việc – các thẻ đều thuộc công ty này. */
  companyCode?: string
}

/**
 * Thanh thẻ màn hình trên vùng nội dung (tối đa 4 thẻ – docs/ui-style.md mục 8a): thẻ ô vuông nền `--erp-tab-bg`, thẻ đang xem nền
 * `--erp-tab-active` chữ trắng; nút × (icon đỏ) hoặc bấm nút giữa chuột để đóng; Alt+1…4 chuyển thẻ. Bên phải ghi số thẻ đang mở và
 * mã công ty đang làm việc (mọi thẻ thuộc một công ty).
 */
export function ScreenTabBar({ tabs, activeId, onSelect, onClose, companyCode }: ScreenTabBarProps) {
  return (
    <div className="app-screen-tabs" data-testid="screen-tabs">
      {/* Không dùng role="tab"/"tabpanel": trùng vai trò với tab trong phiếu (Thông tin, Đính kèm…) – trình đọc màn hình và test lẫn hai loại. */}
      <nav className="app-screen-tabs-list" aria-label="Màn hình đang mở">
        {tabs.map((tab, i) => {
          const active = tab.id === activeId
          const title = screenTabTitle(tab)
          return (
            <div
              key={tab.id}
              className={`app-screen-tab${active ? ' is-active' : ''}`}
              title={`${title.full} – Alt+${i + 1}`}
              onAuxClick={(e) => {
                if (e.button === 1) onClose(tab.id)
              }}
            >
              <button
                type="button"
                className="app-screen-tab-main"
                aria-current={active ? 'page' : undefined}
                onClick={() => onSelect(tab.id)}
              >
                <span className="app-screen-tab-no">{i + 1}</span>
                {title.icon && <span className="app-screen-tab-icon">{title.icon}</span>}
                <span className="app-screen-tab-label">{title.short}</span>
              </button>
              <button
                type="button"
                className="app-screen-tab-close"
                aria-label={`Đóng màn hình ${title.short}`}
                title="Đóng màn hình"
                onClick={() => onClose(tab.id)}
              >
                <CloseOutlined />
              </button>
            </div>
          )
        })}
      </nav>
      <span className="app-screen-tabs-count" title="Số màn hình đang mở / tối đa – mọi màn hình thuộc công ty đang làm việc">
        {tabs.length}/{MAX_SCREEN_TABS} màn hình{companyCode ? ` · ${companyCode}` : ''}
      </span>
    </div>
  )
}

/** Vẽ trang của một thẻ theo vị trí riêng của thẻ – không theo thanh địa chỉ, nên thẻ ẩn giữ nguyên trang đang mở. */
function TabRoutes({ routes, location }: { routes: RouteObject[]; location: Location }) {
  return useRoutes(routes, location)
}

/** Ngữ cảnh tuyến gốc rỗng: các trang của thẻ khớp đường dẫn tuyệt đối từ "/" như một bộ tuyến riêng (không phải con của tuyến khung). */
const ROOT_ROUTE_CONTEXT = { outlet: null, matches: [], isDataRoute: false }

interface ScreenTabPanelProps {
  tab: ScreenTab
  active: boolean
  routes: RouteObject[]
  onLabel: (id: string, label: string | undefined) => void
}

/**
 * Vùng chứa trang của một thẻ. Thẻ ẩn: `visibility: hidden` + `inert` (không bấm, không Tab tới được) nhưng vẫn giữ kích thước, nên lưới
 * không phải đo lại độ rộng cột và vị trí cuộn còn nguyên khi quay lại.
 * Ngăn kéo và hộp thoại của trang vẽ vào vùng của thẻ (getPopupContainer) – chuyển thẻ thì ẩn theo thẻ, không đè lên thẻ khác; danh sách
 * thả xuống, chú thích vẫn vẽ ở cuối trang như cũ (tự đóng khi rời ô).
 * memo: thẻ ẩn không vẽ lại mỗi lần thẻ đang xem điều hướng.
 */
export const ScreenTabPanel = memo(function ScreenTabPanel({ tab, active, routes, onLabel }: ScreenTabPanelProps) {
  const [el, setEl] = useState<HTMLDivElement | null>(null)
  const getPopupContainer = useCallback(
    (node?: HTMLElement) => ((!node || node === document.body) && el ? el : document.body),
    [el],
  )
  const setLabel = useCallback((label: string | undefined) => onLabel(tab.id, label), [onLabel, tab.id])
  return (
    <div
      ref={setEl}
      className={`app-tab-panel${active ? ' is-active' : ''}`}
      inert={!active}
      aria-hidden={active ? undefined : true}
      data-tab-id={tab.id}
    >
      <TabActiveContext value={active}>
        <TabLabelContext value={setLabel}>
          <ConfigProvider getPopupContainer={getPopupContainer}>
            <UNSAFE_RouteContext value={ROOT_ROUTE_CONTEXT}>
              <TabRoutes routes={routes} location={tab.location} />
            </UNSAFE_RouteContext>
          </ConfigProvider>
        </TabLabelContext>
      </TabActiveContext>
    </div>
  )
})
