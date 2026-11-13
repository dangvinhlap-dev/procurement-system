import { CloseOutlined } from '@ant-design/icons'
import { ConfigProvider } from 'antd'
import { memo, useCallback, useState } from 'react'
import { UNSAFE_RouteContext, useRoutes, type Location, type RouteObject } from 'react-router'
import { MAX_SCREEN_TABS, TabActiveContext, TabLabelContext, UnsavedContext } from '../../app/screenTabs'
import { screenTabTitle, type ScreenTab } from './useScreenTabsState'

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
  /** Đăng ký kiểm tra "còn thay đổi chưa lưu" của trang trong thẻ (useUnsavedGuard). */
  onRegister: (id: string, check: () => boolean) => () => void
}

/**
 * Vùng chứa trang của một thẻ. Thẻ ẩn: `visibility: hidden` + `inert` (không bấm, không Tab tới được) nhưng vẫn giữ kích thước, nên lưới
 * không phải đo lại độ rộng cột và vị trí cuộn còn nguyên khi quay lại.
 * Ngăn kéo và hộp thoại của trang vẽ vào vùng của thẻ (getPopupContainer) – chuyển thẻ thì ẩn theo thẻ, không đè lên thẻ khác; danh sách
 * thả xuống, chú thích vẫn vẽ ở cuối trang như cũ (tự đóng khi rời ô).
 * memo: thẻ ẩn không vẽ lại mỗi lần thẻ đang xem điều hướng.
 */
export const ScreenTabPanel = memo(function ScreenTabPanel({ tab, active, routes, onLabel, onRegister }: ScreenTabPanelProps) {
  const [el, setEl] = useState<HTMLDivElement | null>(null)
  const getPopupContainer = useCallback(
    (node?: HTMLElement) => ((!node || node === document.body) && el ? el : document.body),
    [el],
  )
  const setLabel = useCallback((label: string | undefined) => onLabel(tab.id, label), [onLabel, tab.id])
  const register = useCallback((check: () => boolean) => onRegister(tab.id, check), [onRegister, tab.id])
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
        <UnsavedContext value={register}>
          <ConfigProvider getPopupContainer={getPopupContainer}>
            <UNSAFE_RouteContext value={ROOT_ROUTE_CONTEXT}>
              <TabRoutes routes={routes} location={tab.location} />
            </UNSAFE_RouteContext>
          </ConfigProvider>
        </UnsavedContext>
        </TabLabelContext>
      </TabActiveContext>
    </div>
  )
})
