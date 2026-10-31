import {
  ArrowLeftOutlined,
  BellOutlined,
  CalendarOutlined,
  InfoCircleOutlined,
  KeyOutlined,
  LogoutOutlined,
  SearchOutlined,
} from '@ant-design/icons'
import { App, AutoComplete, Avatar, Badge, Button, Dropdown, Input, Select, Tooltip, type MenuProps } from 'antd'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { searchScreens } from '../../app/commands'
import { moduleGroups, type ModuleGroup, type ModuleKey } from '../../app/screens'
import { useVisibleScreens } from '../../app/useVisibleScreens'
import { normalizeVi } from '../../utils/text'

type MenuItems = NonNullable<MenuProps['items']>

/** Thuộc tính của Shellbar. */
export interface ShellBarProps {
  /** Tên ứng dụng hiện cạnh logo. */
  appName: string
  /** Nhóm của màn hình đang mở – nút module tương ứng được tô nhấn. */
  activeGroup?: ModuleKey
  /** Các công ty người dùng được làm việc. */
  companies: { value: string; label: string }[]
  /** Id công ty đang làm việc. */
  company: string
  onCompanyChange: (value: string) => void
  /** Số thông báo chưa đọc; bấm chuông mở màn hình Thông báo. */
  unreadCount: number
  userFullName: string
  userName: string
  /** Ngày làm việc dd/MM/yyyy – hiện trong menu người dùng (ui-guide.md mục 4, ý 4). */
  workingDate: string
  /** Số phiên bản phần mềm – hiện trong menu người dùng. */
  version: string
  onLogout: () => void
  onChangePassword: () => void
}

/**
 * Chữ viết tắt từ họ tên cho ô avatar: chữ đầu của từ đầu và từ cuối, ví dụ "Nguyễn Văn An" → "NA".
 */
function initialsOf(fullName: string): string {
  const words = fullName.trim().split(/\s+/).filter(Boolean)
  if (words.length === 0) return '?'
  const first = words[0][0]
  const last = words.length > 1 ? words[words.length - 1][0] : ''
  return (first + last).toUpperCase()
}

/**
 * Nhãn menu có chữ gạch chân; Alt + chữ đó mở menu (ui-guide.md mục 8).
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
 * Shellbar trên cùng (ui-guide.md mục 4, ý 1), cao 48px, thay cho thanh menu + thanh công cụ + thanh trạng thái kiểu Windows trước đây:
 * trái là logo + tên ứng dụng (về trang chủ; tên ẩn khi màn hình hẹp hơn 1440px), nút Quay lại, menu module (Alt + chữ gạch chân mở); giữa là ô lệnh (gõ mã màn hình như PO, PR,
 * NXT rồi Enter); phải là công ty đang làm việc, thông báo, avatar người dùng (tài khoản, ngày làm việc, phiên bản, đổi mật khẩu, đăng xuất).
 */
export function ShellBar({
  appName,
  activeGroup,
  companies,
  company,
  onCompanyChange,
  unreadCount,
  userFullName,
  userName,
  workingDate,
  version,
  onLogout,
  onChangePassword,
}: ShellBarProps) {
  const navigate = useNavigate()
  const { message } = App.useApp()
  const screens = useVisibleScreens()
  const [command, setCommand] = useState('')
  const matches = useMemo(() => searchScreens(command, screens), [command, screens])

  // Nhóm vẫn hiện để giữ bố cục cố định; không có màn hình được phép thì báo rõ trong menu.
  const menuItemsOf = (group: ModuleKey): MenuItems => {
    const items: MenuItems = screens
      .filter((s) => s.group === group)
      .map((s) => ({ key: s.path, icon: s.icon, label: `${s.navLabel}  (${s.code})` }))
    return items.length > 0 ? items : [{ key: `none-${group}`, label: 'Không có màn hình được phép', disabled: true }]
  }
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

  const openScreen = (code: string) => {
    const screen = screens.find((s) => s.code === code)
    setCommand('')
    if (screen) navigate(screen.path)
  }

  return (
    <header className="app-shell-bar" data-testid="shell-bar">
      <div className="app-shell-start">
        <button type="button" className="app-shell-brand" onClick={() => navigate('/')} title="Trang chủ">
          <span className="app-shell-logo" aria-hidden>
            QM
          </span>
          <span className="app-shell-title">{appName}</span>
        </button>
        <Tooltip title="Quay lại" mouseEnterDelay={0.6}>
          <Button type="text" aria-label="Quay lại" icon={<ArrowLeftOutlined />} onClick={() => navigate(-1)} />
        </Tooltip>
        <nav className="app-shell-menu" role="menubar" aria-label="Menu module">
          {moduleGroups.map((g) => (
            <Dropdown
              key={g.key}
              trigger={['click']}
              open={openKey === g.key}
              onOpenChange={(open) => setOpenKey(open ? g.key : null)}
              menu={{
                items: menuItemsOf(g.key),
                onClick: ({ key }) => {
                  setOpenKey(null)
                  navigate(key)
                },
              }}
            >
              <button
                type="button"
                role="menuitem"
                ref={(el) => {
                  if (el) buttons.current.set(g.key, el)
                }}
                className={`app-shell-menu-item${activeGroup === g.key ? ' is-active' : ''}`}
              >
                <HotkeyLabel group={g} />
              </button>
            </Dropdown>
          ))}
        </nav>
      </div>

      <AutoComplete
        className="app-command-box"
        value={command}
        onChange={setCommand}
        onSelect={openScreen}
        defaultActiveFirstOption
        popupMatchSelectWidth={360}
        options={matches.map((s) => ({ value: s.code, label: `${s.code} – ${s.title}` }))}
        onKeyDown={(e) => {
          if (e.key === 'Enter' && command.trim() && matches.length === 0) {
            message.warning(`Không có màn hình "${command.trim()}".`)
          }
        }}
      >
        <Input prefix={<SearchOutlined />} placeholder="Ô lệnh – tên màn hình" aria-label="Ô lệnh – tên màn hình" allowClear />
      </AutoComplete>

      <div className="app-shell-end">
        <Select
          variant="borderless"
          className="app-company-select"
          aria-label="Công ty đang làm việc"
          value={company}
          onChange={onCompanyChange}
          options={companies}
          popupMatchSelectWidth={false}
        />
        <Badge count={unreadCount} size="small" offset={[-4, 4]}>
          <Button
            type="text"
            aria-label={`Thông báo (${unreadCount} chưa đọc)`}
            title="Thông báo"
            icon={<BellOutlined />}
            onClick={() => navigate('/hang-ngay/thong-bao')}
          />
        </Badge>
        <Dropdown
          trigger={['click']}
          menu={{
            items: [
              { key: 'account', label: `${userFullName} (${userName})`, disabled: true },
              { key: 'date', icon: <CalendarOutlined />, label: `Ngày làm việc: ${workingDate}`, disabled: true },
              { key: 'version', icon: <InfoCircleOutlined />, label: `Phiên bản ${version}`, disabled: true },
              { type: 'divider' },
              { key: 'password', icon: <KeyOutlined />, label: 'Đổi mật khẩu' },
              { key: 'logout', icon: <LogoutOutlined />, label: 'Đăng xuất' },
            ],
            onClick: ({ key }) => {
              if (key === 'logout') onLogout()
              if (key === 'password') onChangePassword()
            },
          }}
        >
          {/* Fiori chỉ hiện avatar; họ tên ở nhãn, chú thích và dòng đầu menu. */}
          <button type="button" className="app-shell-user" aria-label={`Người dùng: ${userFullName}`} title={userFullName}>
            <Avatar size={32} className="app-shell-avatar">
              {initialsOf(userFullName)}
            </Avatar>
          </button>
        </Dropdown>
      </div>
    </header>
  )
}
