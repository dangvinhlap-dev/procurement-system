import { CheckOutlined, CloseCircleOutlined, FileAddOutlined, RollbackOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { withPendingApprovers } from '../../api/purchasing'
import { approveContent, confirmSubmitWithPreview } from '../purchasing/useApprovalCommands'
import { Alert, App, Button, DatePicker, Form, Input, Modal, Spin } from 'antd'
import { Select } from '../../classic/Select'
import dayjs, { type Dayjs } from 'dayjs'
import { useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { useItemSearch, useSuppliers } from '../../api/masterData'
import {
  soleSourceMaxReviewMonths,
  soleSourceStateLabel,
  useSoleSource,
  useSoleSourceMutations,
  type SoleSource,
} from '../../api/soleSources'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { DATE_INPUT_FORMATS, formatDate, formatDateTime } from '../../utils/format'
import { hasWildcard, matchesKeyword } from '../../utils/text'
import { ApprovalHistory } from '../purchasing/ApprovalHistory'

interface SoleSourceFormValues {
  supplierId: string | null
  itemId: string | null
  reason: string
  validFrom: Dayjs | null
  reviewDate: Dayjs | null
}

/** Lọc ô chọn theo nhãn, gõ có dấu hoặc không dấu đều tìm được (CLAUDE.md mục 5). */
const filterByLabel = (input: string, option?: { label?: unknown }) => matchesKeyword(String(option?.label ?? ''), input)

/**
 * Ngăn kéo phiếu NCC độc quyền (design.md mục 18.9). Tải chi tiết trước rồi mới vẽ form để giá trị ban đầu đúng phiếu đang mở.
 * @param id Id phiếu; null = lập mới.
 * @param onClose Đóng ngăn kéo.
 * @param onOpen Mở phiếu khác (phiếu đánh giá lại vừa lập, phiếu trước/sau).
 * @param asPage Vẽ thành trang đầy đủ thay cho ngăn kéo.
 */
export function SoleSourceDrawer({
  id,
  onClose,
  onOpen,
  asPage = false,
}: {
  id: string | null
  onClose: () => void
  onOpen: (id: string) => void
  asPage?: boolean
}) {
  const detail = useSoleSource(id)
  if (id !== null && !detail.data) {
    return (
      <DetailDrawer open asPage={asPage} title="Phiếu NCC độc quyền" onClose={onClose} loading error={detail.error?.message}
        tabs={[{ key: 'general', label: 'Thông tin chung', children: <Spin /> }]} />
    )
  }
  return <SoleSourceForm key={detail.data?.id ?? 'new'} data={id === null ? null : detail.data!} onClose={onClose} onOpen={onOpen} asPage={asPage} />
}

/** Đường dẫn màn hình NCC độc quyền; trang đầy đủ một phiếu là `${SOLE_SOURCE_PATH}/:id`. */
export const SOLE_SOURCE_PATH = '/gia-ncc/ncc-doc-quyen'

/** Trang đầy đủ một phiếu NCC độc quyền (`/gia-ncc/ncc-doc-quyen/:id`) – mở từ thông báo, Việc cần làm, Tra cứu chứng từ. */
export function SoleSourceDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  return (
    <SoleSourceDrawer
      key={id}
      id={id}
      asPage
      onClose={() => navigate(SOLE_SOURCE_PATH)}
      onOpen={(other) => navigate(`${SOLE_SOURCE_PATH}/${other}`)}
    />
  )
}

/**
 * Nội dung phiếu: nháp thì sửa được (người có quyền lập); chờ duyệt thì người đến lượt Duyệt/Từ chối, người lập rút về; đã duyệt thì kết
 * thúc sớm (bắt buộc lý do) hoặc lập phiếu đánh giá lại.
 */
function SoleSourceForm({
  data,
  onClose,
  onOpen,
  asPage,
}: {
  data: SoleSource | null
  onClose: () => void
  onOpen: (id: string) => void
  asPage: boolean
}) {
  const { message, modal } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<SoleSourceFormValues>()
  const mutations = useSoleSourceMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [rejecting, setRejecting] = useState(false)
  const [rejectReason, setRejectReason] = useState('')
  const [itemSearch, setItemSearch] = useState('')

  const isNew = data === null
  const canManage = hasPermission(Permission.SoleSourceManage)
  const editable = isNew ? canManage : data.actions.canEdit
  const suppliers = useSuppliers({})
  const foundItems = useItemSearch(itemSearch)
  const attachmentTab = useAttachmentTab('SoleSourceApproval', data?.id, canManage && (!data || data.status !== 'Ended'))
  const validFrom = Form.useWatch('validFrom', form)

  const today = dayjs()
  const initialValues: SoleSourceFormValues = isNew
    ? { supplierId: null, itemId: null, reason: '', validFrom: today, reviewDate: today.add(soleSourceMaxReviewMonths, 'month') }
    : {
        supplierId: data.supplierId,
        itemId: data.itemId,
        reason: data.reason,
        validFrom: dayjs(data.validFrom),
        reviewDate: dayjs(data.reviewDate),
      }

  // NCC danh sách đen không được lập phiếu (máy chủ cũng chặn); phiếu đang mở luôn có NCC của nó trong ô chọn.
  const supplierOptions = (suppliers.data?.items ?? [])
    .filter((s) => s.status !== 'Blacklisted')
    .map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))
  if (data && !supplierOptions.some((o) => o.value === data.supplierId))
    supplierOptions.unshift({ value: data.supplierId, label: `${data.supplierCode} – ${data.supplierName}` })
  const itemOptions = (foundItems.data?.items ?? []).map((i) => ({ value: i.id, label: `${i.code} – ${i.name}` }))
  if (data && !itemOptions.some((o) => o.value === data.itemId))
    itemOptions.unshift({ value: data.itemId, label: `${data.itemCode} – ${data.itemName}` })

  const run = async (action: () => Promise<unknown>, done: string) => {
    setError(null)
    try {
      await action()
      message.success(done)
      return true
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không thực hiện được.')
      return false
    }
  }

  const save = async () => {
    let values: SoleSourceFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ.')
      return
    }
    const input = {
      supplierId: values.supplierId!,
      itemId: values.itemId!,
      reason: values.reason.trim(),
      validFrom: values.validFrom!.format('YYYY-MM-DD'),
      reviewDate: values.reviewDate!.format('YYYY-MM-DD'),
    }
    setSaving(true)
    await run(async () => {
      if (isNew) {
        const created = await mutations.create.mutateAsync(input)
        // Form dựng lại theo Id mới nên tải file chọn lúc lập ngay tại đây (design.md mục 18.4).
        await attachmentTab.uploadTo(created.id)
        onOpen(created.id)
      } else {
        await mutations.update.mutateAsync({ id: data.id, input })
      }
    }, isNew ? 'Đã lập phiếu NCC độc quyền.' : 'Đã lưu phiếu.')
    setSaving(false)
  }

  const end = () => {
    if (!data) return
    let reason = ''
    modal.confirm({
      title: `Kết thúc phiếu ${data.no}?`,
      content: (
        <>
          <p>Từ hôm nay phiếu không còn tác dụng; mời thầu mặt hàng này lại phải đủ số NCC hoặc ghi giải trình.</p>
          <Input.TextArea aria-label="Lý do kết thúc" rows={3} maxLength={1000} onChange={(e) => (reason = e.target.value)} />
        </>
      ),
      okText: 'Kết thúc',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        if (!reason.trim()) {
          message.error('Hãy nhập lý do kết thúc.')
          throw new Error('missing reason')
        }
        await run(() => mutations.end.mutateAsync({ id: data.id, reason: reason.trim() }), 'Đã kết thúc phiếu.')
      },
    })
  }

  const renew = () =>
    data &&
    run(async () => {
      const created = await mutations.renew.mutateAsync(data.id)
      onOpen(created.id)
    }, 'Đã lập phiếu đánh giá lại (nháp) – kiểm tra giải trình rồi gửi duyệt.')

  // Gửi duyệt: xem trước người duyệt (popup); Duyệt: nêu người lập và người duyệt sau mình – yêu cầu người dùng 03/11/2026.
  const submit = () =>
    data &&
    void confirmSubmitWithPreview(modal, {
      title: `Gửi duyệt phiếu ${data.no}?`,
      docType: 'NDQ',
      prepare: async () => ({ id: data.id }),
      onSubmit: async (id) => {
        await run(() => mutations.submit.mutateAsync(id), 'Đã gửi duyệt.')
      },
      onError: (e) => setError(e instanceof Error ? e.message : 'Không gửi duyệt được.'),
    })

  const approve = () =>
    data &&
    modal.confirm({
      title: `Duyệt phiếu ${data.no}?`,
      content: approveContent(data),
      okText: 'Duyệt',
      cancelText: 'Không',
      onOk: async () => {
        await run(() => mutations.approve.mutateAsync({ id: data.id }), 'Đã duyệt.')
      },
    })

  const a = data?.actions
  const buttons = data && a && (
    <>
      {a.canEdit && (
        <Button icon={<SendOutlined />} loading={mutations.submit.isPending}
          onClick={submit}>
          Gửi duyệt
        </Button>
      )}
      {a.canApprove && (
        <Button icon={<CheckOutlined />} loading={mutations.approve.isPending}
          onClick={approve}>
          Duyệt
        </Button>
      )}
      {a.canApprove && <Button icon={<CloseCircleOutlined />} onClick={() => setRejecting(true)}>Từ chối</Button>}
      {a.canRecall && (
        <Button icon={<RollbackOutlined />} loading={mutations.recall.isPending}
          onClick={() => run(() => mutations.recall.mutateAsync(data.id), 'Đã rút về nháp.')}>
          Rút về
        </Button>
      )}
      {a.canRenew && <Button icon={<FileAddOutlined />} loading={mutations.renew.isPending} onClick={renew}>Đánh giá lại</Button>}
      {a.canEnd && <Button danger icon={<StopOutlined />} onClick={end}>Kết thúc</Button>}
    </>
  )

  const notice = data && (
    <Alert
      type={data.isEffective ? 'success' : data.isOverdue || data.rejectReason ? 'warning' : 'info'}
      showIcon
      style={{ marginBottom: 8 }}
      title={
        data.status === 'Ended'
          ? `Đã kết thúc ngày ${formatDate(data.endedOn)}: ${data.endReason ?? ''}`
          : data.isOverdue
            ? `Quá hạn đánh giá lại từ ${formatDate(data.reviewDate)} – phiếu hết tác dụng, bấm "Đánh giá lại" để lập phiếu mới.`
            : data.isEffective
              ? `Đang hiệu lực đến ${formatDate(data.reviewDate)} – gửi RFQ mặt hàng này cho NCC ${data.supplierCode} không cần giải trình mời thiếu NCC.`
              : data.rejectReason
                ? `Bị từ chối: ${data.rejectReason}`
                : data.status === 'Draft'
                  ? 'Nháp – kiểm tra giải trình, đính kèm văn bản xác nhận độc quyền (nếu có) rồi bấm "Gửi duyệt".'
                  : `Trạng thái: ${soleSourceStateLabel(data)}.`
      }
    />
  )

  return (
    <>
      <DetailDrawer
        open
        asPage={asPage}
        title={isNew ? 'Lập phiếu NCC độc quyền' : `Phiếu ${data.no} – ${withPendingApprovers(soleSourceStateLabel(data), data.approvals)}`}
        audit={{ entityType: 'SoleSourceApproval', id: data?.id }}
        onClose={onClose}
        onSave={editable ? save : undefined}
        saving={saving}
        error={error}
        form={form}
        formName="sole-source"
        initialValues={initialValues}
        readOnly={!editable}
        extraActions={buttons}
        tabs={[
          {
            key: 'general',
            label: 'Thông tin chung',
            children: (
              <>
                {isNew ? (
                  <Alert type="info" showIcon style={{ marginBottom: 8 }}
                    title="Giải trình vì sao mặt hàng chỉ mua được của một NCC. Phiếu phải được duyệt và đánh giá lại tối thiểu hằng năm." />
                ) : notice}
                <Form.Item label="Nhà cung cấp" name="supplierId" rules={[{ required: true, message: 'Hãy chọn nhà cung cấp.' }]}>
                  <Select options={supplierOptions} loading={suppliers.isLoading} showSearch={{ filterOption: filterByLabel }}
                    placeholder="Gõ mã hoặc tên NCC" autoFocus={isNew} />
                </Form.Item>
                <Form.Item label="Mặt hàng" name="itemId" rules={[{ required: true, message: 'Hãy chọn mặt hàng.' }]}>
                  <Select
                    showSearch={{ filterOption: false, onSearch: setItemSearch }}
                    options={itemOptions}
                    placeholder="Gõ ít nhất 2 ký tự mã, tên hoặc mã vạch"
                    notFoundContent={foundItems.isFetching ? <Spin size="small" /> : itemSearch.trim().length < 2 && !hasWildcard(itemSearch) ? 'Gõ ít nhất 2 ký tự, hoặc * để xem tất cả' : 'Không có'}
                  />
                </Form.Item>
                <Form.Item
                  label="Giải trình"
                  name="reason"
                  rules={[{ required: true, whitespace: true, message: 'Hãy ghi giải trình.' }, { max: 1000 }]}
                  extra="Ví dụ: Hãng là nhà sản xuất duy nhất phụ tùng thay thế cho dây chuyền hiện có; có văn bản xác nhận độc quyền (đính kèm)."
                >
                  <Input.TextArea rows={4} maxLength={1000} />
                </Form.Item>
                <div className="detail-grid">
                  <Form.Item label="Hiệu lực từ ngày" name="validFrom" rules={[{ required: true, message: 'Chọn ngày hiệu lực.' }]}>
                    <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
                  </Form.Item>
                  <Form.Item
                    label="Hạn đánh giá lại"
                    name="reviewDate"
                    dependencies={['validFrom']}
                    rules={[
                      { required: true, message: 'Chọn hạn đánh giá lại.' },
                      ({ getFieldValue }) => ({
                        validator: (_, value: Dayjs | null) => {
                          const from = getFieldValue('validFrom') as Dayjs | null
                          if (!value || !from) return Promise.resolve()
                          if (!value.isAfter(from, 'day')) return Promise.reject(new Error('Phải sau ngày hiệu lực.'))
                          if (value.isAfter(from.add(soleSourceMaxReviewMonths, 'month'), 'day'))
                            return Promise.reject(new Error(`Tối đa ${soleSourceMaxReviewMonths} tháng kể từ ngày hiệu lực.`))
                          return Promise.resolve()
                        },
                      }),
                    ]}
                    extra={validFrom ? `Muộn nhất ${formatDate(validFrom.add(soleSourceMaxReviewMonths, 'month').format('YYYY-MM-DD'))}.` : undefined}
                  >
                    <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
                  </Form.Item>
                </div>
                {data && (
                  <div className="detail-grid">
                    <Form.Item label="Người lập">
                      <Input value={`${data.createdByName ?? ''} – ${formatDateTime(data.createdAt)}`} readOnly />
                    </Form.Item>
                    <Form.Item label="Duyệt xong lúc">
                      <Input value={formatDateTime(data.approvedAt)} readOnly />
                    </Form.Item>
                    {data.renewedFromId && (
                      <Form.Item label="Đánh giá lại từ phiếu">
                        <Button type="link" onClick={() => onOpen(data.renewedFromId!)}>{data.renewedFromNo}</Button>
                      </Form.Item>
                    )}
                    {data.renewedById && (
                      <Form.Item label="Phiếu đánh giá lại">
                        <Button type="link" onClick={() => onOpen(data.renewedById!)}>{data.renewedByNo}</Button>
                      </Form.Item>
                    )}
                  </div>
                )}
              </>
            ),
          },
          ...(data ? [{ key: 'history', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={data.approvals} createdByName={data.createdByName} createdAt={data.createdAt} /> }] : []),
          attachmentTab,
        ]}
      />
      <Modal
        title={data ? `Từ chối phiếu ${data.no}` : 'Từ chối'}
        open={rejecting}
        okText="Từ chối"
        okButtonProps={{ danger: true, disabled: rejectReason.trim() === '' }}
        cancelText="Hủy"
        confirmLoading={mutations.reject.isPending}
        onCancel={() => setRejecting(false)}
        onOk={async () => {
          if (data) await run(() => mutations.reject.mutateAsync({ id: data.id, comment: rejectReason }), 'Đã từ chối – phiếu về nháp.')
          setRejecting(false)
          setRejectReason('')
        }}
        destroyOnHidden
      >
        <Input.TextArea aria-label="Lý do từ chối" rows={3} maxLength={500} value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} />
      </Modal>
    </>
  )
}
