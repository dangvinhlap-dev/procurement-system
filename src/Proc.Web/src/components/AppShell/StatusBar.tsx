import { InfoCircleOutlined } from '@ant-design/icons'

/** Thuộc tính của thanh trạng thái. */
export interface StatusBarProps {
  message: string
  screenTitle: string
  userFullName: string
  /** Ngày làm việc dd/MM/yyyy. */
  workingDate: string
  version: string
  /** true = chế độ chèn (INS), false = ghi đè (OVR); đổi bằng phím Insert. */
  insertMode: boolean
}

/**
 * Thanh trạng thái dưới cùng, cao 22px (ui-guide.md mục AppShell, ý 5): trái là thông báo hệ thống;
 * phải là các ô có viền: tên màn hình | người dùng | ngày làm việc | phiên bản | INS.
 */
export function StatusBar({ message, screenTitle, userFullName, workingDate, version, insertMode }: StatusBarProps) {
  return (
    <footer className="app-status-bar" data-testid="status-bar">
      <span className="app-status-message">
        <InfoCircleOutlined /> {message}
      </span>
      <span className="app-status-cell" title="Màn hình">{screenTitle}</span>
      <span className="app-status-cell" title="Người dùng">{userFullName}</span>
      <span className="app-status-cell" title="Ngày làm việc">{workingDate}</span>
      <span className="app-status-cell" title="Phiên bản phần mềm">{version}</span>
      <span className="app-status-cell" title="Chế độ nhập (phím Insert)">{insertMode ? 'INS' : 'OVR'}</span>
    </footer>
  )
}
