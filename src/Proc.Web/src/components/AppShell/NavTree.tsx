import { DoubleRightOutlined, FolderOpenOutlined, FolderOutlined, PushpinOutlined } from '@ant-design/icons'
import { useState, type MouseEvent as ReactMouseEvent } from 'react'
import { useNavigate } from 'react-router'
import { moduleGroups, screens, type Screen } from '../../app/screens'
import { layoutTokens } from '../../theme'

/** Thuộc tính của cây điều hướng. */
export interface NavTreeProps {
  activePath: string
  /** Mã màn hình đã ghim (nhóm "Ghim" ở đầu cây). */
  pinnedCodes: string[]
  /** Số đếm theo mã màn hình; attention = true thì badge đỏ. */
  counts: Record<string, { count: number; attention?: boolean }>
}

/** Khóa lưu độ rộng/trạng thái thu gọn trong trình duyệt – chỉ là tiện ích riêng từng máy. */
const STORAGE_KEY = 'proc.navTree'

/**
 * Đọc độ rộng và trạng thái ẩn đã lưu; trả mặc định khi trình duyệt chặn localStorage.
 */
function loadState(): { width: number; hidden: boolean } {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? 'null') as { width?: number; hidden?: boolean } | null
    return { width: saved?.width ?? layoutTokens.navWidth, hidden: saved?.hidden ?? false }
  } catch {
    return { width: layoutTokens.navWidth, hidden: false }
  }
}

function saveState(state: { width: number; hidden: boolean }) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
  } catch {
    // Không lưu được thì thôi; lần sau dùng mặc định.
  }
}

/**
 * Cây điều hướng bên trái (ui-guide.md mục AppShell, ý 3): rộng 240px, kéo mép phải để đổi rộng, ẩn/hiện được;
 * "Thu gọn tất cả" và nút ghim ở đầu; nhóm "Ghim" rồi các nhóm theo module; mỗi mục có biểu tượng, tên,
 * số đếm (xám, đỏ nếu cần chú ý); mục đang mở nền xanh đậm chữ trắng.
 */
export function NavTree({ activePath, pinnedCodes, counts }: NavTreeProps) {
  const navigate = useNavigate()
  const [{ width, hidden }, setState] = useState(loadState)
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set())

  const update = (next: { width: number; hidden: boolean }) => {
    setState(next)
    saveState(next)
  }

  const groups: { key: string; label: string; items: Screen[] }[] = [
    { key: 'pinned', label: 'Ghim', items: pinnedCodes.map((c) => screens.find((s) => s.code === c)).filter((s): s is Screen => !!s) },
    ...moduleGroups.map((g) => ({ key: g.key, label: g.label, items: screens.filter((s) => s.group === g.key) })),
  ]

  const startResize = (e: ReactMouseEvent) => {
    e.preventDefault()
    const startX = e.clientX
    const startWidth = width
    let latest = width
    const onMove = (ev: MouseEvent) => {
      latest = Math.min(layoutTokens.navMaxWidth, Math.max(layoutTokens.navMinWidth, startWidth + ev.clientX - startX))
      setState({ width: latest, hidden: false })
    }
    const onUp = () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
      saveState({ width: latest, hidden: false })
    }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
  }

  if (hidden) {
    return (
      <nav className="app-nav is-hidden" data-testid="nav-tree" aria-label="Điều hướng">
        <button type="button" className="app-nav-show" aria-label="Hiện cây điều hướng" title="Hiện cây điều hướng" onClick={() => update({ width, hidden: false })}>
          <DoubleRightOutlined />
        </button>
      </nav>
    )
  }

  return (
    <nav className="app-nav" data-testid="nav-tree" aria-label="Điều hướng" style={{ width }}>
      <div className="app-nav-head">
        <button type="button" className="app-nav-link" onClick={() => setCollapsed(new Set(groups.map((g) => g.key)))}>
          Thu gọn tất cả
        </button>
        <button type="button" className="app-nav-icon" aria-label="Ẩn cây điều hướng" title="Ẩn cây điều hướng" onClick={() => update({ width, hidden: true })}>
          <PushpinOutlined />
        </button>
      </div>
      <div className="app-nav-scroll">
        {groups.map((g) => {
          const isCollapsed = collapsed.has(g.key)
          return (
            <div key={g.key} className="app-nav-group">
              <button
                type="button"
                className="app-nav-group-head"
                aria-expanded={!isCollapsed}
                onClick={() =>
                  setCollapsed((prev) => {
                    const next = new Set(prev)
                    if (isCollapsed) next.delete(g.key)
                    else next.add(g.key)
                    return next
                  })
                }
              >
                {isCollapsed ? <FolderOutlined /> : <FolderOpenOutlined />}
                {g.label}
              </button>
              {!isCollapsed &&
                g.items.map((s) => {
                  const active = s.path === activePath
                  const count = counts[s.code]
                  return (
                    <button
                      key={`${g.key}-${s.code}`}
                      type="button"
                      className={`app-nav-item${active ? ' is-active' : ''}`}
                      aria-current={active ? 'page' : undefined}
                      title={s.title}
                      onClick={() => navigate(s.path)}
                    >
                      <span className="app-nav-item-icon">{s.icon}</span>
                      <span className="app-nav-item-label">{s.navLabel}</span>
                      {count && (
                        <span className={`app-nav-count${count.attention ? ' is-attention' : ''}`}>
                          {count.count.toLocaleString('vi-VN')}
                        </span>
                      )}
                    </button>
                  )
                })}
            </div>
          )
        })}
      </div>
      <div className="app-nav-resize" aria-hidden onMouseDown={startResize} />
    </nav>
  )
}
