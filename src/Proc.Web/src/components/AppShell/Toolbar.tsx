import {
  ArrowLeftOutlined,
  CheckOutlined,
  CloseOutlined,
  HomeOutlined,
  PrinterOutlined,
  SaveOutlined,
  SearchOutlined,
  SettingOutlined,
} from '@ant-design/icons'
import { App, AutoComplete, Button, Input, Tooltip } from 'antd'
import { useMemo, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router'
import { focusListSearch, searchScreens } from '../../app/commands'
import { useVisibleScreens } from '../../app/useVisibleScreens'

/** Một nút biểu tượng trên thanh công cụ; nút không dùng được thì mờ đi (ui-guide.md). */
function ToolButton({ title, icon, onClick, disabled }: { title: string; icon: ReactNode; onClick?: () => void; disabled?: boolean }) {
  return (
    <Tooltip title={title} mouseEnterDelay={0.6}>
      <Button type="text" size="small" aria-label={title} icon={icon} onClick={onClick} disabled={disabled ?? !onClick} />
    </Tooltip>
  )
}

/**
 * Thanh công cụ, cao 32px (ui-guide.md mục AppShell, ý 2): ô lệnh "Ô lệnh – tên màn hình"
 * (gõ mã như PO, PR, NXT rồi Enter để mở nhanh) và các nút Lưu, Quay lại, Trang chủ, Hủy, In, Tìm kiếm.
 * Lưu và Hủy chỉ bật trên màn hình chứng từ (DocumentForm, lát sau).
 */
export function Toolbar() {
  const navigate = useNavigate()
  const { message } = App.useApp()
  const screens = useVisibleScreens()
  const [text, setText] = useState('')
  const matches = useMemo(() => searchScreens(text, screens), [text, screens])

  const open = (code: string) => {
    const screen = screens.find((s) => s.code === code)
    setText('')
    if (screen) navigate(screen.path)
  }

  return (
    <div className="app-toolbar" data-testid="toolbar">
      <CheckOutlined className="app-toolbar-ok" aria-hidden />
      <AutoComplete
        className="app-command-box"
        value={text}
        onChange={setText}
        onSelect={open}
        defaultActiveFirstOption
        popupMatchSelectWidth={320}
        options={matches.map((s) => ({ value: s.code, label: `${s.code} – ${s.title}` }))}
        onKeyDown={(e) => {
          if (e.key === 'Enter' && text.trim() && matches.length === 0) {
            message.warning(`Không có màn hình "${text.trim()}".`)
          }
        }}
      >
        <Input size="small" placeholder="Ô lệnh – tên màn hình" aria-label="Ô lệnh – tên màn hình" />
      </AutoComplete>
      <span className="app-toolbar-sep" />
      <ToolButton title="Lưu (Ctrl+S)" icon={<SaveOutlined />} disabled />
      <span className="app-toolbar-sep" />
      <ToolButton title="Quay lại" icon={<ArrowLeftOutlined />} onClick={() => navigate(-1)} />
      <ToolButton title="Trang chủ" icon={<HomeOutlined />} onClick={() => navigate('/')} />
      <ToolButton title="Hủy" icon={<CloseOutlined />} disabled />
      <span className="app-toolbar-sep" />
      <ToolButton title="In" icon={<PrinterOutlined />} onClick={() => window.print()} />
      <ToolButton title="Tìm kiếm (F3)" icon={<SearchOutlined />} onClick={focusListSearch} />
      <span className="app-toolbar-spacer" />
      <ToolButton title="Cài đặt" icon={<SettingOutlined />} disabled />
    </div>
  )
}
