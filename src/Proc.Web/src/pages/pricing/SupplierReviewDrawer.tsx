import { CheckCircleOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Form, Input } from 'antd'
import { Select } from '../../classic/Select'
import dayjs, { type Dayjs } from 'dayjs'
import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useConflictSupplierOptions } from '../../api/conflicts'
import {
  reviewCriteria,
  reviewResultLabels,
  reviewScoreOptions,
  reviewStatusLabels,
  useSupplierReview,
  useSupplierReviewMutations,
  type ReviewScoreKey,
  type SupplierReview,
  type SupplierReviewInput,
  type SupplierReviewResult,
} from '../../api/supplierReviews'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { DATE_INPUT_FORMATS, formatDateTime, formatMoney, formatQuantity } from '../../utils/format'
import { matchesKeyword } from '../../utils/text'

/** NCC × quý cần lập phiếu mới, kèm số liệu hệ thống đang có trên bảng đánh giá. */
export interface NewSupplierReview {
  year: number
  quarter: number
  supplierId?: string
  supplierCode?: string
  supplierName?: string
  receiptAmount?: number
  onTimePct?: number | null
  returnPct?: number | null
  qualityPct?: number | null
  systemRating?: string | null
}

type ReviewFormValues = Record<ReviewScoreKey, number | null> & {
  supplierId: string | null
  date: Dayjs | null
  result: SupplierReviewResult | null
  comment: string
}

/** Lọc ô chọn theo nhãn, gõ có dấu hoặc không dấu đều tìm được (CLAUDE.md mục 5). */
const filterByLabel = (input: string, option?: { label?: unknown }) => matchesKeyword(String(option?.label ?? ''), input)

const resultOptions = Object.entries(reviewResultLabels).map(([value, label]) => ({ value, label }))

/** Điểm từ mức này trở xuống là "kém" – bắt buộc nhận xét (khớp SupplierReview.LowScore). */
const lowScore = 2

/**
 * Điểm bình quân tạm tính khi đủ 6 tiêu chí, 2 số lẻ – ví dụ 4, 5, 4, 3, 4, 2 → 3,67 (cùng công thức máy chủ).
 * @param values Điểm đang nhập.
 */
function averageScore(values: Partial<Record<ReviewScoreKey, number | null>>): number | null {
  const scores = reviewCriteria.map((c) => values[c.key])
  if (scores.some((s) => s == null)) return null
  return Math.round((scores.reduce<number>((sum, s) => sum + (s ?? 0), 0) / scores.length) * 100) / 100
}

/**
 * Ngăn kéo phiếu đánh giá NCC định kỳ (design.md mục 18.20): chấm 6 tiêu chí 1–5, kết luận, nhận xét; số liệu hệ thống của quý để tham
 * khảo. "Lưu" giữ Nháp; "Hoàn thành" lưu rồi khóa phiếu (tính vào KPI); "Hủy phiếu" chỉ khi Nháp, bắt buộc lý do.
 * @param id Phiếu đang mở; null = lập mới.
 * @param fresh NCC × quý khi lập mới.
 * @param onClose Đóng ngăn kéo.
 * @param onOpen Mở phiếu vừa lập.
 * @param asPage Vẽ thành trang đầy đủ thay cho ngăn kéo.
 */
export function SupplierReviewDrawer({
  id,
  fresh,
  onClose,
  onOpen,
  asPage = false,
}: {
  id: string | null
  fresh?: NewSupplierReview
  onClose: () => void
  onOpen: (id: string) => void
  asPage?: boolean
}) {
  const review = useSupplierReview(id)
  if (id && !review.data) {
    return <DetailDrawer open asPage={asPage} loading title="Đánh giá NCC" tabs={[]} onClose={onClose} error={review.error?.message ?? null} />
  }
  return (
    <ReviewForm key={review.data?.id ?? 'new'} review={review.data ?? null} fresh={fresh} onClose={onClose} onOpen={onOpen} asPage={asPage} />
  )
}

/** Đường dẫn màn hình đánh giá NCC định kỳ; trang đầy đủ một phiếu là `${SUPPLIER_REVIEW_PATH}/:id`. */
export const SUPPLIER_REVIEW_PATH = '/gia-ncc/danh-gia-ncc-dinh-ky'

/** Trang đầy đủ một phiếu đánh giá NCC định kỳ (`/gia-ncc/danh-gia-ncc-dinh-ky/:id`) – mở từ Tra cứu chứng từ. */
export function SupplierReviewDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  return (
    <SupplierReviewDrawer
      key={id}
      id={id}
      asPage
      onClose={() => navigate(SUPPLIER_REVIEW_PATH)}
      onOpen={(other) => navigate(`${SUPPLIER_REVIEW_PATH}/${other}`)}
    />
  )
}

function ReviewForm({
  review,
  fresh,
  onClose,
  onOpen,
  asPage,
}: {
  review: SupplierReview | null
  fresh?: NewSupplierReview
  onClose: () => void
  onOpen: (id: string) => void
  asPage: boolean
}) {
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<ReviewFormValues>()
  const mutations = useSupplierReviewMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = review === null
  const canManage = hasPermission(Permission.SupplierReviewManage)
  const editable = canManage && (isNew || review.status === 'Draft')
  // Biên bản, bằng chứng chấm điểm – đính kèm được cả khi đã hoàn thành (design.md mục 18.4).
  const attachmentTab = useAttachmentTab('SupplierReview', review?.id, canManage)
  // Lập từ nút "Tạo mới" (chưa có NCC) thì chọn NCC; lập từ một dòng bảng đánh giá thì NCC đã cố định.
  const pickSupplier = isNew && !fresh?.supplierId
  const suppliers = useConflictSupplierOptions(pickSupplier)
  const period = review ?? { year: fresh?.year ?? dayjs().year(), quarter: fresh?.quarter ?? 1 }
  const metrics = review ?? fresh
  const watched = Form.useWatch([], form) as Partial<ReviewFormValues> | undefined
  const preview = review?.score ?? averageScore(watched ?? {})

  const initialValues: ReviewFormValues = {
    supplierId: review?.supplierId ?? fresh?.supplierId ?? null,
    date: review ? dayjs(review.date) : dayjs(),
    result: review?.result ?? null,
    comment: review?.comment ?? '',
    priceScore: review?.priceScore ?? null,
    qualityScore: review?.qualityScore ?? null,
    deliveryScore: review?.deliveryScore ?? null,
    warrantyScore: review?.warrantyScore ?? null,
    paymentScore: review?.paymentScore ?? null,
    complianceScore: review?.complianceScore ?? null,
  }

  /** Đọc form thành nội dung gửi máy chủ; null khi còn ô lỗi. */
  const readInput = async (): Promise<SupplierReviewInput | null> => {
    let values: ReviewFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ.')
      return null
    }
    setError(null)
    return {
      supplierId: values.supplierId!,
      year: period.year,
      quarter: period.quarter,
      date: values.date!.format('YYYY-MM-DD'),
      result: values.result ?? null,
      comment: values.comment?.trim() || null,
      priceScore: values.priceScore ?? null,
      qualityScore: values.qualityScore ?? null,
      deliveryScore: values.deliveryScore ?? null,
      warrantyScore: values.warrantyScore ?? null,
      paymentScore: values.paymentScore ?? null,
      complianceScore: values.complianceScore ?? null,
    }
  }

  const save = async () => {
    const input = await readInput()
    if (!input) return
    setSaving(true)
    try {
      if (isNew) {
        const saved = await mutations.create.mutateAsync(input)
        message.success(`Đã lập phiếu đánh giá ${saved.no}.`)
        // Form dựng lại theo Id mới nên tải file chọn lúc lập ngay tại đây.
        await attachmentTab.uploadTo(saved.id)
        onOpen(saved.id)
      } else {
        await mutations.update.mutateAsync({ id: review.id, input })
        message.success(`Đã lưu phiếu ${review.no}.`)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phiếu đánh giá.')
    } finally {
      setSaving(false)
    }
  }

  const complete = async () => {
    const input = await readInput()
    if (!input) return
    modal.confirm({
      title: 'Hoàn thành đánh giá?',
      content: 'Phiếu bị khóa, số liệu hệ thống được chốt và tính vào KPI "Hoàn thành đánh giá NCC".',
      okText: 'Hoàn thành',
      cancelText: 'Không',
      onOk: async () => {
        try {
          // Phiếu mới: lập nháp trước rồi hoàn thành ngay (máy chủ cấp số khi lập).
          const target = isNew ? await mutations.create.mutateAsync(input) : review
          if (isNew) await attachmentTab.uploadTo(target.id)
          const done = await mutations.complete.mutateAsync({ id: target.id, input })
          message.success(`Đã hoàn thành phiếu đánh giá ${done.no}.`)
          if (isNew) onOpen(done.id)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không hoàn thành được phiếu đánh giá.')
        }
      },
    })
  }

  const cancel = () => {
    if (!review) return
    let reason = ''
    modal.confirm({
      title: `Hủy phiếu ${review.no}?`,
      content: (
        <>
          <p>Chỉ hủy phiếu lập nhầm; hủy xong có thể lập lại phiếu cho NCC này trong quý.</p>
          <Input.TextArea aria-label="Lý do hủy" rows={3} maxLength={2000} onChange={(e) => (reason = e.target.value)} />
        </>
      ),
      okText: 'Hủy phiếu',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        if (!reason.trim()) {
          message.error('Hãy nhập lý do hủy.')
          throw new Error('missing reason')
        }
        try {
          await mutations.cancel.mutateAsync({ id: review.id, reason: reason.trim() })
          message.success(`Đã hủy phiếu ${review.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không hủy được phiếu đánh giá.')
        }
      },
    })
  }

  const supplierOptions = pickSupplier
    ? (suppliers.data ?? []).map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))
    : [
        {
          value: review?.supplierId ?? fresh?.supplierId ?? '',
          label: `${review?.supplierCode ?? fresh?.supplierCode ?? ''} – ${review?.supplierName ?? fresh?.supplierName ?? ''}`,
        },
      ]

  // Kết luận khác "Tiếp tục" hoặc có tiêu chí kém thì bắt buộc nhận xét (design.md mục 18.20) – báo ngay trên form, máy chủ kiểm lại.
  const needComment =
    (watched?.result != null && watched.result !== 'Continue') || reviewCriteria.some((c) => (watched?.[c.key] ?? 99) <= lowScore)

  const notice = isNew
    ? 'Chấm 6 tiêu chí (1 – rất kém … 5 – rất tốt). "Lưu" giữ nháp; "Hoàn thành" khóa phiếu và tính vào KPI.'
    : review.status === 'Completed'
      ? `Hoàn thành ${formatDateTime(review.completedAt)} (${review.completedByName ?? ''}) – điểm ${formatQuantity(review.score)}.`
      : review.status === 'Cancelled'
        ? `Đã hủy: ${review.cancelReason ?? ''}`
        : 'Nháp – sửa được đến khi hoàn thành. Số liệu hệ thống được tính lại mỗi lần lưu.'

  return (
    <DetailDrawer
      asPage={asPage}
      open
      title={isNew ? `Lập phiếu đánh giá NCC quý ${period.quarter}/${period.year}` : `Phiếu ${review.no} – ${reviewStatusLabels[review.status]}`}
      audit={{ entityType: 'SupplierReview', id: review?.id }}
      onClose={onClose}
      onSave={editable ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      formName="supplier-review"
      initialValues={initialValues}
      readOnly={!editable}
      extraActions={
        editable && (
          <>
            <Button icon={<CheckCircleOutlined />} onClick={() => void complete()} loading={mutations.complete.isPending}>
              Hoàn thành
            </Button>
            {!isNew && (
              <Button danger icon={<StopOutlined />} onClick={cancel} loading={mutations.cancel.isPending}>
                Hủy phiếu
              </Button>
            )}
          </>
        )
      }
      tabs={[
        {
          key: 'general',
          label: 'Đánh giá',
          children: (
            <>
              <Alert type={review?.status === 'Completed' ? 'success' : 'info'} showIcon style={{ marginBottom: 8 }} title={notice} />
              <div className="detail-grid">
                <Form.Item label="Nhà cung cấp" name="supplierId" rules={[{ required: true, message: 'Hãy chọn nhà cung cấp.' }]}>
                  <Select
                    options={supplierOptions}
                    disabled={!pickSupplier}
                    loading={suppliers.isLoading}
                    showSearch={{ filterOption: filterByLabel }}
                    placeholder="Gõ mã hoặc tên NCC"
                  />
                </Form.Item>
                <Form.Item label="Ngày đánh giá" name="date" rules={[{ required: true, message: 'Chọn ngày đánh giá.' }]}>
                  <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
                </Form.Item>
              </div>
              <div className="detail-grid">
                {reviewCriteria.map((c) => (
                  <Form.Item key={c.key} label={c.label} name={c.key}>
                    <Select allowClear options={reviewScoreOptions} placeholder="Chưa chấm" />
                  </Form.Item>
                ))}
              </div>
              <div className="detail-grid">
                <Form.Item label="Kết luận" name="result">
                  <Select allowClear options={resultOptions} placeholder="Chọn khi hoàn thành" />
                </Form.Item>
                <Form.Item label={review?.status === 'Completed' ? 'Điểm' : 'Điểm bình quân (tạm tính)'} htmlFor="review-score">
                  <Input id="review-score" value={preview == null ? '' : formatQuantity(preview)} readOnly />
                </Form.Item>
              </div>
              <Form.Item
                label="Nhận xét, yêu cầu cải thiện"
                name="comment"
                rules={[{ max: 2000 }]}
                extra={needComment ? 'Bắt buộc khi kết luận khác "Tiếp tục hợp tác" hoặc có tiêu chí từ 2 điểm trở xuống.' : undefined}
              >
                <Input.TextArea rows={3} maxLength={2000} />
              </Form.Item>
            </>
          ),
        },
        {
          key: 'metrics',
          label: 'Số liệu hệ thống',
          children: (
            <>
              <Alert
                type="info"
                showIcon
                style={{ marginBottom: 8 }}
                title="Số liệu của NCC trong quý tại công ty (báo cáo Đánh giá NCC, Kiểm tra chất lượng) – chốt lúc hoàn thành."
              />
              <div className="detail-grid">
                <Form.Item label="Giá trị nhập (trước VAT)">
                  <Input value={formatMoney(metrics?.receiptAmount ?? 0)} readOnly />
                </Form.Item>
                <Form.Item label="Xếp loại hệ thống">
                  <Input value={metrics?.systemRating ?? ''} readOnly />
                </Form.Item>
                <Form.Item label="Giao đúng hạn (%)">
                  <Input value={formatQuantity(metrics?.onTimePct)} readOnly />
                </Form.Item>
                <Form.Item label="Tỷ lệ trả hàng (%)">
                  <Input value={formatQuantity(metrics?.returnPct)} readOnly />
                </Form.Item>
                <Form.Item label="Đạt chất lượng (%)">
                  <Input value={formatQuantity(metrics?.qualityPct)} readOnly />
                </Form.Item>
              </div>
              {review && (
                <div className="detail-grid">
                  <Form.Item label="Người lập">
                    <Input value={review.createdByName ?? ''} readOnly />
                  </Form.Item>
                  <Form.Item label="Thời điểm lập">
                    <Input value={formatDateTime(review.createdAt)} readOnly />
                  </Form.Item>
                </div>
              )}
            </>
          ),
        },
        attachmentTab,
      ]}
    />
  )
}
