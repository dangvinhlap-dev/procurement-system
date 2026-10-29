import { BellOutlined } from '@ant-design/icons'
import { Avatar, Badge, Dropdown, Select } from 'antd'
import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { moduleGroups, screens, type ModuleGroup, type ModuleKey } from '../../app/screens'
import { normalizeVi } from '../../utils/text'

/** Thuộc tính của thanh menu. */
export interface MenuBarProps {
  /** Nhóm của màn hình đang mở – hiện chữ đậm. */
  activeGroup?: ModuleKey
  companies: { value: string; label: string }[]
  company: string
  onCompanyChange: (value: string) => void
  unreadCount: number
  userFullName: string
  userInitials: string
}

/**
 * Nhãn menu có chữ gạch chân; Alt + chữ đó mở menu (ui-guide.md mục Bàn phím).
 */
function HotkeyLabel({ group }: { group: ModuleGroup }) {
  const { label, hotkeyIndex: i } = group
  return (
    <span>
      {label.slice(0, i)}
      <u>{label[i]}</u>
      {label.slice(i + 1)}
    </span>
  )
}

/**
 * Thanh menu trên cùng, cao 28px (ui-guide.md mục AppShell, ý 1): nhóm chức năng theo module,
 * bấm mở menu thả xuống; bên phải là công ty đang làm việc, thông báo, ngôn ngữ, người dùng.
 */
export function MenuBar({ activeGroup, companies, company, onCompanyChange, unreadCount, userFullName, userInitials }: MenuBarProps) {
  const navigate = useNavigate()
  const [openKey, setOpenKey] = useState<ModuleKey | null>(null)
  const buttons = useRef(new Map<ModuleKey, HTMLButtonElement>())

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (!e.altKey || e.ctrlKey || e.metaKey || !e.code.startsWith('Key')) return
      const letter = e.code.slice(3).toLowerCase()
      const group = moduleGroups.find((g) => normalizeVi(g.label[g.hotkeyIndex]) === letter)
      if (!group) return
      // Chặn trình duyệt dùng Alt + chữ cho menu riêng của nó.
      e.preventDefault()
      buttons.current.get(group.key)?.focus()
      setOpenKey(group.key)
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  return (
    <div className="app-menu-bar" data-testid="menu-bar" role="menubar">
      {moduleGroups.map((g) => (
        <Dropdown
          key={g.key}
          trigger={['click']}
          open={openKey === g.key}
          onOpenChange={(open) => setOpenKey(open ? g.key : null)}
          menu={{
            items: screens
              .filter((s) => s.group === g.key)
              .map((s) => ({ key: s.path, icon: s.icon, label: `${s.navLabel}  (${s.code})` })),
            onClick: ({ key }) => {
              setOpenKey(null)
              navigate(key)
            },
          }}
        >
          <button
            type="button"
            ref={(el) => {
              if (el) buttons.current.set(g.key, el)
            }}
            className={`app-menu-item${activeGroup === g.key ? ' is-active' : ''}`}
          >
            <HotkeyLabel group={g} />
          </button>
        </Dropdown>
      ))}

      <div className="app-menu-right">
        <Select
          size="small"
          variant="borderless"
          className="app-company-select"
          aria-label="Công ty đang làm việc"
          value={company}
          onChange={onCompanyChange}
          options={companies}
          popupMatchSelectWidth={false}
        />
        <Badge count={unreadCount} size="small" offset={[-2, 2]}>
          <button type="button" className="app-menu-icon" aria-label={`Thông báo (${unreadCount} chưa đọc)`}>
            <BellOutlined />
          </button>
        </Badge>
        <span className="app-menu-lang" title="Ngôn ngữ: Tiếng Việt">
          VI
        </span>
        <span className="app-menu-user">
          <Avatar size={18} className="app-menu-avatar">
            {userInitials}
          </Avatar>
          {userFullName}
        </span>
      </div>
    </div>
  )
}
