import { useCallback, useEffect, useLayoutEffect, useRef } from 'react'
import type { FormInstance } from 'antd'
import { markFormClean } from '../../utils/formDirty'

/** Kết quả của {@link useDocumentFormSync}. */
export interface DocumentFormSync<TDoc> {
  /** Nạp phiếu vừa lưu vào form – gọi ngay sau `create`/`update` thành công. */
  loadSaved: (saved: TDoc) => void
  /**
   * Id phiếu cần lưu: Id phiếu đã lưu trên trang này (kể cả vừa tạo xong mà trang chưa kịp dựng lại theo Id mới), không có thì
   * Id trên đường dẫn; null = phiếu mới, gọi tạo.
   */
  targetId: () => string | null
}

/**
 * Đồng bộ form chứng từ với dữ liệu phiếu tải từ server mà không ghi đè chữ người dùng đang gõ.
 *
 * Tại sao: sau khi lưu/gửi duyệt, TanStack Query tải lại phiếu (invalidate, không chờ). Trước đây mỗi lần `doc` đổi tham chiếu
 * là `setFieldsValue` lại toàn bộ – nếu người dùng gõ trong lúc chờ tải xong (ví dụ gõ lý do lệch giá ngay sau khi gửi duyệt bị
 * chặn) thì chữ vừa gõ bị mất. Cách làm:
 * - Chỉ nạp form khi giá trị form tính từ phiếu (`toValues`) khác lần nạp trước (so chuỗi JSON; dayjs ra chuỗi ISO), hoặc đổi phiếu.
 * - Lưu thành công thì gọi `loadSaved` với phiếu vừa lưu: nạp ngay (lấy Id dòng mới, số đã làm tròn) trước khi người dùng gõ tiếp,
 *   nên lần tải lại sau đó trùng nội dung và bị bỏ qua.
 * Phiếu đổi thật trên server (người khác sửa, đổi trạng thái làm đổi giá trị form) vẫn nạp lại như cũ.
 *
 * `targetId`: sau khi tạo phiếu, trang chuyển sang `/…/{id}` trong chuyển trang nền nên lúc máy bận, nút lệnh vẫn giữ hàm cũ
 * (`isNew` = true) – bấm Gửi duyệt/Lưu lúc đó sẽ tạo phiếu lần nữa (trùng phiếu, hoặc lỗi "không tìm thấy dòng cần sửa" vì form đã
 * có Id dòng). Id phiếu vừa lưu giữ trong ref nên hàm cũ vẫn đọc đúng.
 *
 * @param form Form antd của trang chứng từ.
 * @param routeId Id trên đường dẫn (`'moi'` hoặc rỗng khi phiếu mới) – đổi sang phiếu khác thì quên Id phiếu đã lưu.
 * @param doc Phiếu tải từ server (undefined khi phiếu mới hoặc đang tải).
 * @param toValues Đổi phiếu sang giá trị form; có thể là hàm viết tại chỗ (luôn dùng bản mới nhất).
 */
export function useDocumentFormSync<TDoc extends { id: string }, TValues extends object>(
  form: FormInstance,
  routeId: string,
  doc: TDoc | undefined,
  toValues: (doc: TDoc) => TValues,
): DocumentFormSync<TDoc> {
  const toValuesRef = useRef(toValues)
  // Cập nhật trong layout effect (chạy trước effect nạp form bên dưới) – không gán ref trong lúc render.
  useLayoutEffect(() => {
    toValuesRef.current = toValues
  })
  /** Khóa lần nạp gần nhất: Id phiếu + giá trị form dạng JSON. */
  const loadedRef = useRef<string | null>(null)
  /** Id phiếu đã lưu trên trang này; trùng Id đường dẫn hoặc chưa có thì dùng Id đường dẫn. */
  const savedIdRef = useRef<string | null>(null)
  const routeIdRef = useRef(routeId)

  // Sang phiếu khác (hoặc "Tạo mới") thì quên Id đã lưu; chuyển từ /moi sang đúng Id vừa tạo thì giữ.
  useLayoutEffect(() => {
    routeIdRef.current = routeId
    if (savedIdRef.current && savedIdRef.current !== routeId) savedIdRef.current = null
  }, [routeId])

  const apply = useCallback(
    (d: TDoc) => {
      const values = toValuesRef.current(d)
      const key = `${d.id}|${JSON.stringify(values)}`
      if (key === loadedRef.current) return
      loadedRef.current = key
      form.setFieldsValue(values)
      // Vừa nạp đúng dữ liệu máy chủ: không còn gì chưa lưu (hỏi lại khi rời trang – formDirty.ts).
      markFormClean(form)
    },
    [form],
  )

  const loadSaved = useCallback(
    (saved: TDoc) => {
      savedIdRef.current = saved.id
      apply(saved)
      // Lưu thành công: sạch kể cả khi nội dung trùng lần nạp trước (apply bỏ qua).
      markFormClean(form)
    },
    [apply, form],
  )

  const targetId = useCallback(() => {
    if (savedIdRef.current) return savedIdRef.current
    const route = routeIdRef.current
    return route && route !== 'moi' ? route : null
  }, [])

  useEffect(() => {
    if (doc) apply(doc)
  }, [doc, apply])

  return { loadSaved, targetId }
}
