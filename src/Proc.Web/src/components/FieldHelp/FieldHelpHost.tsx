import { HistoryOutlined } from '@ant-design/icons'
import { App, Button } from 'antd'
import { useEffect, useRef, useState, type RefObject } from 'react'
import { collectFieldLabels, findFieldTarget, type FieldTarget } from './auditDom'
import { FieldHelpDialog } from './FieldHelpDialog'
import { RecordHistoryDialog } from './RecordHistoryDialog'

/**
 * Phím F1 cho mọi màn hình (design.md mục 9.3a, docs/ui-style.md mục 6): mở "Trợ giúp ô" của ô đang có con trỏ; con trỏ không ở ô dữ liệu
 * thì lấy ô dưới chuột – ô khóa (phiếu chỉ xem) không nhận con trỏ được nhưng vẫn xem được lịch sử. F1 mặc định của trình duyệt
 * (mở trang trợ giúp) bị chặn. Đặt một lần trong AppShell.
 */
export function FieldHelpHost() {
  const { message } = App.useApp()
  const [target, setTarget] = useState<FieldTarget | null>(null)
  const pointer = useRef<{ x: number; y: number } | null>(null)

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      pointer.current = { x: e.clientX, y: e.clientY }
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== 'F1') return
      e.preventDefault()
      const focused = findFieldTarget(document.activeElement)
      const hovered = pointer.current ? findFieldTarget(document.elementFromPoint(pointer.current.x, pointer.current.y)) : null
      const found = focused ?? hovered
      if (found) setTarget(found)
      else message.info('Đặt con trỏ vào một ô dữ liệu (hoặc rê chuột lên ô) rồi bấm F1 để xem lịch sử thay đổi của ô.')
    }
    // Trình duyệt cũ mở trợ giúp qua sự kiện "help" thay vì phím F1.
    const onHelp = (e: Event) => e.preventDefault()
    window.addEventListener('mousemove', onMove, { passive: true })
    window.addEventListener('keydown', onKey)
    window.addEventListener('help', onHelp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('keydown', onKey)
      window.removeEventListener('help', onHelp)
    }
  }, [message])

  return <FieldHelpDialog target={target} onClose={() => setTarget(null)} />
}

/**
 * Nút "Lịch sử thay đổi" của phiếu/ngăn kéo (như "Hiển thị thay đổi" của ERP – design.md mục 9.3a). Nhãn ô lấy từ màn hình lúc bấm.
 * @param props.entityType Mã loại đối tượng, ví dụ PurchaseOrder.
 * @param props.entityId Khóa bản ghi (đã lưu).
 * @param props.rootRef Phần tử bao phiếu/ngăn kéo để đọc nhãn ô.
 */
export function RecordHistoryButton({
  entityType,
  entityId,
  rootRef,
}: {
  entityType: string
  entityId: string
  rootRef: RefObject<HTMLElement | null>
}) {
  const [labels, setLabels] = useState<Record<string, string> | null>(null)
  return (
    <>
      {/* Xem lịch sử không đổi dữ liệu: bấm được cả khi phiếu chỉ xem (form khóa). */}
      <Button icon={<HistoryOutlined />} disabled={false} onClick={() => setLabels(collectFieldLabels(rootRef.current))} title="Lịch sử thay đổi (F1 trên một ô: lịch sử của ô đó)">
        Lịch sử thay đổi
      </Button>
      <RecordHistoryDialog
        open={labels !== null}
        onClose={() => setLabels(null)}
        entityType={entityType}
        entityId={entityId}
        screenLabels={labels ?? {}}
      />
    </>
  )
}
