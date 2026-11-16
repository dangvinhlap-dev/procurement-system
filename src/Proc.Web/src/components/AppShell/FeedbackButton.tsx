import { BugOutlined, CloseCircleOutlined, PictureOutlined, SendOutlined } from '@ant-design/icons'
import { Alert, App, Button, Form, Input, Modal } from 'antd'
import { useRef, useState, type ClipboardEvent } from 'react'
import { useLocation } from 'react-router'
import { useEnvironmentStatus } from '../../api/operations'
import { useSendFeedback } from '../../api/testRefresh'

/** Ảnh tối đa (byte) – bằng giới hạn đính kèm mặc định của máy chủ (Attachments:MaxFileSizeMb = 2). */
const MAX_IMAGE_BYTES = 2 * 1024 * 1024

/** Ảnh đã dán / chọn: tên file, base64 (không phần "data:…,"), địa chỉ xem trước. */
interface PickedImage {
  fileName: string
  base64: string
  preview: string
}

/**
 * Đọc file ảnh thành base64; chặn đuôi khác png / jpg và ảnh quá 2 MB.
 * @param file File ảnh (dán từ clipboard hoặc chọn).
 * @returns Ảnh đã đọc.
 */
function readImage(file: File): Promise<PickedImage> {
  return new Promise((resolve, reject) => {
    if (!/^image\/(png|jpeg)$/.test(file.type)) {
      reject(new Error('Chỉ nhận ảnh PNG hoặc JPG.'))
      return
    }
    if (file.size > MAX_IMAGE_BYTES) {
      reject(new Error('Ảnh quá 2 MB – chụp vùng nhỏ hơn.'))
      return
    }
    const reader = new FileReader()
    reader.onload = () => {
      const url = String(reader.result)
      const ext = file.type === 'image/png' ? 'png' : 'jpg'
      const name = file.name && file.name !== 'image.png' ? file.name : `anh-man-hinh-${Date.now()}.${ext}`
      resolve({ fileName: name, base64: url.slice(url.indexOf(',') + 1), preview: url })
    }
    reader.onerror = () => reject(new Error('Không đọc được ảnh.'))
    reader.readAsDataURL(file)
  })
}

/**
 * Nút "Báo lỗi / góp ý" trên thanh trên của AppShell – chỉ hiện ở máy TEST đã khai Operations:Upstream (lát QT9, design.md 21.6; ui-style.md mục
 * 5r). Hộp thoại: tiêu đề, mô tả, màn hình đang mở (điền sẵn), ảnh màn hình (Win+Shift+S rồi Ctrl+V vào ô, hoặc chọn file). Máy chủ TEST chuyển về
 * phiếu phát hành cùng phiên bản trên PROD – thành dòng "Lỗi phát hiện", ảnh vào tab Đính kèm.
 */
export function FeedbackButton() {
  const enabled = useEnvironmentStatus().data?.feedbackEnabled === true
  const location = useLocation()
  const { message } = App.useApp()
  const [open, setOpen] = useState(false)
  const [image, setImage] = useState<PickedImage | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [form] = Form.useForm<{ title: string; description: string; screen: string }>()
  const fileInput = useRef<HTMLInputElement>(null)
  const send = useSendFeedback()
  if (!enabled) return null

  const close = () => {
    setOpen(false)
    setImage(null)
    setError(null)
  }
  const pick = async (file: File | null | undefined) => {
    if (!file) return
    try {
      setImage(await readImage(file))
      setError(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không đọc được ảnh.')
    }
  }
  const onPaste = (e: ClipboardEvent<HTMLDivElement>) => {
    const file = Array.from(e.clipboardData.items).find((i) => i.kind === 'file' && i.type.startsWith('image/'))?.getAsFile()
    if (file) {
      e.preventDefault()
      void pick(file)
    }
  }

  return (
    <>
      <Button type="text" icon={<BugOutlined />} title="Báo lỗi / góp ý cho bản đang thử – gửi về trung tâm phát hành" onClick={() => setOpen(true)}>
        Báo lỗi / góp ý
      </Button>
      <Modal
        title="Báo lỗi / góp ý"
        open={open}
        onCancel={close}
        onOk={() => form.submit()}
        okText="Gửi"
        okButtonProps={{ icon: <SendOutlined /> }}
        cancelText="Bỏ qua"
        confirmLoading={send.isPending}
        destroyOnHidden
        width={560}
      >
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 8 }}
          title="Lỗi gửi về phiếu phát hành của bản đang thử (trung tâm phát hành trên PROD)."
          description="Ghi rõ đã làm gì, thấy gì, mong đợi gì. Chụp màn hình bằng Win+Shift+S rồi bấm vào ô ảnh, Ctrl+V."
        />
        {error && <Alert type="error" showIcon title={error} style={{ marginBottom: 8 }} />}
        <Form
          form={form}
          layout="vertical"
          preserve={false}
          initialValues={{ title: '', description: '', screen: `${location.pathname}${location.search}` }}
          onFinish={async (v) => {
            setError(null)
            try {
              const result = await send.mutateAsync({
                title: v.title.trim(),
                description: v.description?.trim() || null,
                screen: v.screen?.trim() || null,
                imageFileName: image?.fileName ?? null,
                imageBase64: image?.base64 ?? null,
              })
              message.success(result.message)
              close()
            } catch (e) {
              setError(e instanceof Error ? e.message : 'Không gửi được báo lỗi.')
            }
          }}
        >
          <Form.Item label="Tiêu đề" name="title" rules={[{ required: true, whitespace: true, message: 'Nhập tiêu đề lỗi.' }]}>
            <Input maxLength={300} autoFocus placeholder="Ví dụ: Nút Lưu trên đơn mua hàng không chạy" />
          </Form.Item>
          <Form.Item label="Mô tả" name="description">
            <Input.TextArea maxLength={4000} autoSize={{ minRows: 3, maxRows: 8 }} />
          </Form.Item>
          <Form.Item label="Màn hình" name="screen">
            <Input maxLength={300} />
          </Form.Item>
          <Form.Item label="Ảnh màn hình">
            <div
              className="erp-feedback-paste"
              tabIndex={0}
              role="textbox"
              aria-label="Ô dán ảnh màn hình"
              data-testid="feedback-paste"
              onPaste={onPaste}
            >
              {image ? (
                <>
                  <img src={image.preview} alt="Ảnh màn hình gửi kèm" />
                  {image.fileName}
                </>
              ) : (
                'Bấm vào đây rồi Ctrl+V để dán ảnh (PNG / JPG, tối đa 2 MB).'
              )}
            </div>
            <input
              ref={fileInput}
              type="file"
              accept="image/png,image/jpeg"
              // CSS chung đặt display cho input – thuộc tính hidden không đủ.
              style={{ display: 'none' }}
              onChange={(e) => {
                void pick(e.target.files?.[0])
                e.target.value = ''
              }}
            />
            <div style={{ marginTop: 4, display: 'flex', gap: 4 }}>
              <Button icon={<PictureOutlined />} onClick={() => fileInput.current?.click()}>
                Chọn ảnh…
              </Button>
              {image && (
                <Button icon={<CloseCircleOutlined />} onClick={() => setImage(null)}>
                  Bỏ ảnh
                </Button>
              )}
            </div>
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
