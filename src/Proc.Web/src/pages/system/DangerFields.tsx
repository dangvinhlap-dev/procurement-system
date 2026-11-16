import { Form, Input } from 'antd'

/**
 * So chữ xác nhận người gõ với chữ yêu cầu: bỏ khoảng trắng hai đầu, chuẩn hóa Unicode, không phân biệt hoa thường (máy chủ so cùng cách).
 * @param typed Chữ người gõ.
 * @param expected Chữ yêu cầu (in hoa).
 * @returns Khớp.
 */
const sameConfirmation = (typed: string | undefined, expected: string) =>
  typed?.trim().normalize().toUpperCase() === expected.normalize()

/**
 * Ô nhập lý do + mật khẩu + chữ xác nhận của việc nguy hiểm Q6 (ui-style.md mục 5m) – dùng chung cho REL (cài, quay về) và TST (làm mới TEST).
 * Đặt trong `Form` có các trường `reason`, `password`, `confirmation`.
 * @param confirmation Chữ phải gõ.
 */
export function DangerFields({ confirmation }: { confirmation: string }) {
  return (
    <>
      <Form.Item label="Lý do" name="reason" rules={[{ required: true, message: 'Nhập lý do.' }, { min: 5, message: 'Lý do ít nhất 5 ký tự.' }]}>
        <Input.TextArea autoFocus maxLength={500} autoSize={{ minRows: 2, maxRows: 4 }} />
      </Form.Item>
      <Form.Item label="Mật khẩu của bạn" name="password" rules={[{ required: true, message: 'Nhập lại mật khẩu để xác nhận.' }]}>
        <Input.Password autoComplete="current-password" />
      </Form.Item>
      <Form.Item label={`Gõ "${confirmation}" để xác nhận`} name="confirmation"
        rules={[{ validator: (_, v?: string) => (sameConfirmation(v, confirmation) ? Promise.resolve() : Promise.reject(new Error(`Gõ đúng "${confirmation}".`))) }]}>
        <Input autoComplete="off" />
      </Form.Item>
    </>
  )
}
