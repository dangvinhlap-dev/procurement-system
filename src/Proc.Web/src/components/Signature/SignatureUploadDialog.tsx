import { CheckOutlined, CloseOutlined, FolderOpenOutlined, RedoOutlined, UndoOutlined } from '@ant-design/icons'
import { Alert, Button, Input, Modal, Radio, Slider } from 'antd'
import { useMemo, useRef, useState } from 'react'
import { canvasToPng, loadImage, processSignature, type InkColor, type SignatureOptions } from '../../utils/signatureImage'

/** Thuộc tính hộp thoại tải chữ ký. */
interface SignatureUploadDialogProps {
  open: boolean
  /** Họ tên (đã gồm danh xưng) in dưới chữ ký ở ô xem thử. */
  displayName: string
  /** Tải thay người khác: bắt buộc lý do. */
  requireReason: boolean
  /** Đang gửi lên máy chủ. */
  saving: boolean
  /** Lỗi máy chủ – hiện trong hộp thoại, không đóng. */
  error: string | null
  onCancel: () => void
  onSave: (file: File, reason: string | undefined) => void
}

/**
 * Hộp thoại "Tải chữ ký" (lát IN1 – ui-style.md mục 5s): chọn ảnh chụp / scan chữ ký → phần mềm tách nền, cắt sát, đổi màu mực (xử lý trên trình
 * duyệt – utils/signatureImage.ts) → xem ảnh gốc, ảnh sau xử lý trên nền ô vuông (trong suốt), ô ký thử đúng cỡ in → Lưu.
 */
export function SignatureUploadDialog({ open, displayName, requireReason, saving, error, onCancel, onSave }: SignatureUploadDialogProps) {
  const fileRef = useRef<HTMLInputElement>(null)
  const [source, setSource] = useState<HTMLImageElement | null>(null)
  const [sourceUrl, setSourceUrl] = useState<string | null>(null)
  const [options, setOptions] = useState<SignatureOptions>({ ink: 'blue', sensitivity: 6, rotate: 0 })
  const [readError, setReadError] = useState<string | null>(null)
  const [reason, setReason] = useState('')

  // Xử lý lại mỗi khi đổi ảnh hoặc tùy chọn (hộp thoại chỉ được vẽ khi mở nên mỗi lần mở bắt đầu từ đầu).
  const result = useMemo(() => {
    if (!source) return null
    const canvas = processSignature(source, options)
    return canvas ? { canvas, url: canvas.toDataURL('image/png') } : null
  }, [source, options])
  const noStroke = source !== null && result === null

  const choose = async (file: File | undefined) => {
    if (!file) return
    try {
      const image = await loadImage(file)
      setReadError(null)
      setSource(image)
      // URL riêng để xem ảnh gốc (loadImage đã bỏ URL dùng để đọc).
      setSourceUrl((old) => {
        if (old) URL.revokeObjectURL(old)
        return URL.createObjectURL(file)
      })
    } catch (e) {
      setReadError(e instanceof Error ? e.message : 'Không đọc được ảnh.')
    }
  }

  const save = async () => {
    if (!result) return
    onSave(await canvasToPng(result.canvas, 'chu-ky.png'), requireReason ? reason : undefined)
  }

  const rotate = (by: 90 | -90) => setOptions((o) => ({ ...o, rotate: (((o.rotate + by + 360) % 360) as SignatureOptions['rotate']) }))

  return (
    <Modal
      open={open}
      width={860}
      title="Tải chữ ký"
      onCancel={onCancel}
      mask={{ closable: false }}
      destroyOnHidden
      footer={
        <div className="erp-signature-footer">
          <Button icon={<FolderOpenOutlined />} onClick={() => fileRef.current?.click()}>
            Chọn ảnh…
          </Button>
          <span className="erp-signature-spacer" />
          <Button danger icon={<CloseOutlined />} onClick={onCancel}>
            Bỏ qua
          </Button>
          <Button
            type="primary"
            icon={<CheckOutlined />}
            loading={saving}
            disabled={!result || (requireReason && !reason.trim())}
            onClick={() => void save()}
            title={!result ? 'Chọn ảnh chữ ký trước' : requireReason && !reason.trim() ? 'Nhập lý do tải thay' : undefined}
          >
            Lưu chữ ký
          </Button>
        </div>
      }
    >
      <input
        ref={fileRef}
        type="file"
        accept="image/png,image/jpeg"
        hidden
        data-testid="signature-file"
        onChange={(e) => {
          void choose(e.target.files?.[0])
          e.target.value = ''
        }}
      />
      {error && <Alert type="error" showIcon title={error} className="erp-signature-alert" />}
      {readError && <Alert type="warning" showIcon title={readError} className="erp-signature-alert" />}
      {noStroke && (
        <Alert
          type="warning"
          showIcon
          title='Không thấy nét chữ ký – tăng mức "Tách nền" hoặc chụp lại rõ hơn trên giấy trắng.'
          className="erp-signature-alert"
        />
      )}
      <div className="erp-signature-panes">
        <div className="erp-signature-pane">
          <div className="erp-signature-pane-head">Ảnh gốc (chụp / scan chữ ký trên giấy trắng)</div>
          <div className="erp-signature-pane-body">
            {sourceUrl ? <img src={sourceUrl} alt="Ảnh gốc" /> : <span className="erp-signature-hint">Bấm "Chọn ảnh…" (PNG / JPG)</span>}
          </div>
        </div>
        <div className="erp-signature-pane">
          <div className="erp-signature-pane-head">Sau khi phần mềm xử lý (nền trong suốt)</div>
          <div className="erp-signature-pane-body erp-signature-check">
            {result && <img src={result.url} alt="Chữ ký sau xử lý" data-testid="signature-processed" />}
          </div>
        </div>
        <div className="erp-signature-pane">
          <div className="erp-signature-pane-head">Xem thử trên ô ký (đúng cỡ in)</div>
          <div className="erp-signature-pane-body">
            <SignatureSample url={result?.url ?? null} name={displayName} />
          </div>
        </div>
      </div>
      <div className="erp-signature-options">
        <span>Màu mực:</span>
        <Radio.Group
          value={options.ink}
          onChange={(e) => setOptions((o) => ({ ...o, ink: e.target.value as InkColor }))}
          options={[
            { value: 'blue', label: 'Xanh' },
            { value: 'black', label: 'Đen' },
            { value: 'original', label: 'Giữ màu gốc' },
          ]}
        />
        <span>Tách nền:</span>
        <Slider
          className="erp-signature-slider"
          min={1}
          max={10}
          value={options.sensitivity}
          onChange={(v) => setOptions((o) => ({ ...o, sensitivity: v }))}
          tooltip={{ formatter: (v) => `Mức ${v} – cao hơn giữ cả nét mờ` }}
        />
        <span>Xoay:</span>
        <Button icon={<UndoOutlined />} onClick={() => rotate(-90)} title="Xoay trái 90°" disabled={!source} />
        <Button icon={<RedoOutlined />} onClick={() => rotate(90)} title="Xoay phải 90°" disabled={!source} />
      </div>
      {requireReason && (
        <div className="erp-signature-reason">
          <label htmlFor="signature-reason">
            Lý do tải thay <span className="erp-required">*</span>
          </label>
          <Input
            id="signature-reason"
            value={reason}
            maxLength={500}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Ví dụ: theo bản ký mẫu nộp phòng HC ngày …"
          />
        </div>
      )}
      <div className="erp-signature-hint">
        PNG / JPG; nên chụp thẳng, đủ sáng, ký bút mực trên giấy trắng. Phần mềm tự tách nền trắng thành trong suốt, cắt sát nét chữ, làm đậm nét mờ,
        đổi màu mực và thu về cỡ chuẩn. Chữ ký mới chỉ dùng cho bước duyệt từ lúc lưu; phiếu đã duyệt trước đó vẫn in chữ ký cũ.
      </div>
    </Modal>
  )
}

/**
 * Ô ký thử đúng cỡ in (rộng ≈ 46 mm như ô "Phòng CUVT" của mẫu 1A, ảnh cao tối đa 15 mm): chức danh, chữ ký, họ tên.
 * @param url Ảnh chữ ký (data / blob URL); null = ô trống.
 * @param name Họ tên in dưới chữ ký.
 */
export function SignatureSample({ url, name }: { url: string | null; name: string }) {
  return (
    <div className="erp-signature-sample">
      <div className="erp-signature-sample-title">Người đề xuất</div>
      <div className="erp-signature-sample-caption">(Trình ký đề xuất)</div>
      <div className="erp-signature-sample-image">{url && <img src={url} alt="Chữ ký" />}</div>
      <div className="erp-signature-sample-name">{name}</div>
    </div>
  )
}
