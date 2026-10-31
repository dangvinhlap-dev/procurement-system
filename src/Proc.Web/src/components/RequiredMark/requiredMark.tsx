import type { FormProps } from 'antd'
import type { ReactNode } from 'react'
import './requiredMark.css'

/**
 * Vẽ nhãn ô nhập kèm dấu * đỏ phía sau khi ô bắt buộc (quy chuẩn Fiori – docs/ui-guide.md mục 6). Ant Design mặc định đặt dấu *
 * trước nhãn; dùng hàm này ở ConfigProvider để mọi form cùng một kiểu, và dấu * của tiêu đề cột lưới dòng (LineGrid) cũng đặt sau.
 * Dấu * vẽ bằng CSS ::after có văn bản thay thế rỗng, nên tên ô (trình đọc màn hình, getByLabel) vẫn đúng chữ của nhãn.
 * @param label Nhãn gốc của Form.Item.
 * @param info.required Ô có luật required (rules) hoặc thuộc tính required.
 * @returns Nhãn, gắn lớp required-mark khi bắt buộc.
 */
export function renderRequiredMark(label: ReactNode, { required }: { required: boolean }): ReactNode {
  return required ? <span className="required-mark">{label}</span> : label
}

/**
 * Giá trị requiredMark cho Form: đang sửa thì hiện dấu * sau nhãn; chỉ xem thì ẩn (người dùng không nhập gì nên dấu * vô nghĩa).
 * @param editable Form đang cho sửa.
 * @returns Hàm vẽ dấu * hoặc false.
 */
export function formRequiredMark(editable: boolean): FormProps['requiredMark'] {
  return editable ? renderRequiredMark : false
}
