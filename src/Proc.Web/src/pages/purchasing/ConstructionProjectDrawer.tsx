import { EditOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Form, Input, InputNumber, Modal, Table, Typography } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useState } from 'react'
import {
  constructionStatusLabels,
  useConstructionProject,
  useConstructionProjectMutations,
  type ConstructionProject,
  type ConstructionProjectRevision,
  type ConstructionProjectStatus,
} from '../../api/constructionProjects'
import { useDepartments } from '../../api/organization'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Select } from '../../classic/Select'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { DATE_INPUT_FORMATS, formatDate, formatDateTime, formatMoney, vnNumberInput } from '../../utils/format'

interface ProjectFormValues {
  code: string
  name: string
  departmentId: string | null
  location: string
  startDate: Dayjs | null
  expectedEndDate: Dayjs | null
  estimateAmount: number | null
  basis: string
  status: ConstructionProjectStatus
  note: string
}

interface ReviseFormValues {
  newAmount: number | null
  effectiveDate: Dayjs
  basis: string
  reason: string
}

/** Số tiền phông đơn cách, canh phải (docs/ui-style.md mục 3). */
const money = (v: number) => <span style={{ fontFamily: 'var(--erp-font-mono)' }}>{formatMoney(v)}</span>

/** Chênh lệch có dấu: +6.000.000 / −2.000.000. */
const signedMoney = (v: number) => (
  <span style={{ fontFamily: 'var(--erp-font-mono)' }}>
    {v > 0 ? '+' : v < 0 ? '−' : ''}
    {formatMoney(Math.abs(v))}
  </span>
)

const toDate = (d: Dayjs | null) => (d ? d.format('YYYY-MM-DD') : null)

/**
 * Hộp thoại điều chỉnh tổng dự toán (lát Y2): dự toán mới, ngày hiệu lực, căn cứ, lý do (bắt buộc). Mỗi lần thêm một dòng lịch sử, không sửa
 * dòng cũ.
 * @param project Công trình đang mở.
 * @param onClose Đóng hộp thoại.
 */
function ReviseDialog({ project, onClose }: { project: ConstructionProject; onClose: () => void }) {
  const { message } = App.useApp()
  const mutations = useConstructionProjectMutations()
  const [form] = Form.useForm<ReviseFormValues>()

  const save = async () => {
    const values = await form.validateFields()
    try {
      const saved = await mutations.revise.mutateAsync({
        id: project.id,
        input: {
          newAmount: values.newAmount ?? 0,
          effectiveDate: values.effectiveDate.format('YYYY-MM-DD'),
          basis: values.basis?.trim() || null,
          reason: values.reason.trim(),
        },
      })
      message.success(`Đã điều chỉnh dự toán ${saved.code} thành ${formatMoney(saved.estimateAmount)} đ.`)
      onClose()
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không điều chỉnh được dự toán.')
    }
  }

  return (
    <Modal
      open
      width={560}
      title={`Điều chỉnh dự toán – ${project.code}`}
      okText="Ghi điều chỉnh"
      cancelText="Đóng"
      onOk={save}
      confirmLoading={mutations.revise.isPending}
      onCancel={onClose}
    >
      {/* Form riêng, disabled={false}: ngăn kéo phía sau có thể chỉ đọc (DisabledContext truyền qua portal); name riêng để id ô không trùng ô
          Ghi chú của ngăn kéo. */}
      <Form form={form} name="ctdt-revise" layout="vertical" disabled={false} initialValues={{ newAmount: null, effectiveDate: dayjs(), basis: '', reason: '' }}>
        <Typography.Paragraph type="secondary">
          Dự toán hiện hành {formatMoney(project.estimateAmount)} đ (chưa VAT). Điều chỉnh được ghi thành một dòng lịch sử, không sửa hay xóa được.
        </Typography.Paragraph>
        <Form.Item
          label="Tổng dự toán mới (VND, chưa VAT)"
          name="newAmount"
          rules={[
            { required: true, type: 'number', min: 1, message: 'Tổng dự toán phải lớn hơn 0.' },
            { validator: (_, v: number | null) => (v === project.estimateAmount ? Promise.reject(new Error('Dự toán mới phải khác dự toán hiện hành.')) : Promise.resolve()) },
          ]}
        >
          <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} precision={0} autoFocus />
        </Form.Item>
        <Form.Item label="Ngày hiệu lực" name="effectiveDate" rules={[{ required: true, message: 'Chọn ngày hiệu lực.' }]}>
          <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
        </Form.Item>
        <Form.Item label="Căn cứ" name="basis" rules={[{ max: 500 }]} extra="Ví dụ: QĐ 15/QĐ-CT ngày 10/11/2026.">
          <Input maxLength={500} />
        </Form.Item>
        <Form.Item label="Lý do" name="reason" rules={[{ required: true, whitespace: true, message: 'Hãy nhập lý do điều chỉnh dự toán.' }, { max: 500 }]}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

/**
 * Ngăn kéo công trình (lát Y2, design.md mục 19.2). Tạo mới: mã (không đổi về sau), tên, phòng ban đề xuất, tổng dự toán ban đầu, căn cứ.
 * Sửa: thông tin chung và trạng thái; dự toán chỉ đổi bằng "Điều chỉnh dự toán" khi đang thực hiện. Tab Dự toán liệt kê lần 0 (ban đầu) và
 * các lần điều chỉnh; tab Đính kèm giữ quyết định phê duyệt / điều chỉnh dự toán, bản vẽ.
 * @param project Công trình mở từ danh sách; null = tạo mới.
 * @param onClose Đóng ngăn kéo.
 */
export function ConstructionProjectDrawer({ project, onClose }: { project: ConstructionProject | null; onClose: () => void }) {
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<ProjectFormValues>()
  const mutations = useConstructionProjectMutations()
  const departments = useDepartments()
  const isNew = project === null
  // Chi tiết kèm lịch sử dự toán; trong lúc tải dùng dòng của danh sách.
  const detail = useConstructionProject(project?.id)
  const current = detail.data ?? project
  const canManage = hasPermission(Permission.ProjectManage)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [revising, setRevising] = useState(false)
  const attachmentTab = useAttachmentTab('ConstructionProject', project?.id, canManage)

  // Form dựng ngay với dòng của danh sách (đủ thông tin chung); dự toán hiện hành đọc từ chi tiết vì điều chỉnh không đóng ngăn kéo.
  const initialValues: ProjectFormValues = project
    ? {
        code: project.code,
        name: project.name,
        departmentId: project.departmentId,
        location: project.location ?? '',
        startDate: project.startDate ? dayjs(project.startDate) : null,
        expectedEndDate: project.expectedEndDate ? dayjs(project.expectedEndDate) : null,
        estimateAmount: null,
        basis: '',
        status: project.status,
        note: project.note ?? '',
      }
    : {
        code: '', name: '', departmentId: null, location: '', startDate: null, expectedEndDate: null, estimateAmount: null, basis: '',
        status: 'InProgress', note: '',
      }

  const save = async () => {
    let values: ProjectFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ.')
      return
    }
    setError(null)
    setSaving(true)
    try {
      const common = {
        name: values.name.trim(),
        departmentId: values.departmentId!,
        location: values.location.trim() || null,
        startDate: toDate(values.startDate),
        expectedEndDate: toDate(values.expectedEndDate),
        note: values.note.trim() || null,
      }
      if (isNew) {
        const saved = await mutations.create.mutateAsync({
          ...common,
          code: values.code.trim(),
          estimateAmount: values.estimateAmount ?? 0,
          basis: values.basis.trim() || null,
        })
        // Ngăn kéo đóng sau khi lưu nên tải file chọn lúc tạo ngay tại đây.
        await attachmentTab.uploadTo(saved.id)
        message.success(`Đã tạo công trình ${saved.code}.`)
      } else {
        await mutations.update.mutateAsync({ id: project.id, input: { ...common, status: values.status } })
        message.success(`Đã lưu công trình ${project.code}.`)
      }
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được công trình.')
    } finally {
      setSaving(false)
    }
  }

  // Phòng ban: chỉ phòng ban đang dùng, giữ phòng ban cũ của công trình nếu đã ngừng dùng.
  const departmentOptions = (departments.data ?? [])
    .filter((d) => d.isActive || d.id === current?.departmentId)
    .map((d) => ({ value: d.id, label: `${d.code} – ${d.name}` }))
  const revisions = detail.data?.revisions ?? []

  return (
    <>
      <DetailDrawer
        open
        title={isNew ? 'Tạo công trình' : `Công trình: ${project.code} – ${project.name}`}
        audit={{ entityType: 'ConstructionProject', id: project?.id }}
        onClose={onClose}
        onSave={canManage ? save : undefined}
        saving={saving}
        loading={!isNew && detail.isLoading}
        error={error}
        form={form}
        formName="construction-project"
        initialValues={initialValues}
        readOnly={!canManage}
        extraActions={
          !isNew && canManage && current?.status === 'InProgress' ? (
            <Button icon={<EditOutlined />} onClick={() => setRevising(true)}>
              Điều chỉnh dự toán
            </Button>
          ) : undefined
        }
        tabs={[
          {
            key: 'general',
            label: 'Thông tin chung',
            children: (
              <>
                {isNew && (
                  <Alert
                    type="info"
                    showIcon
                    style={{ marginBottom: 8 }}
                    title="Tổng dự toán tính chưa VAT. Sau khi tạo, mã không đổi được và dự toán chỉ đổi bằng nút “Điều chỉnh dự toán” (có lý do, lưu lịch sử)."
                  />
                )}
                <div className="detail-grid">
                  <Form.Item
                    label="Mã công trình"
                    name="code"
                    rules={[
                      { required: true, whitespace: true, message: 'Hãy nhập mã công trình.' },
                      { pattern: /^[A-Za-z0-9][A-Za-z0-9._/-]{1,29}$/, message: 'Mã 2–30 ký tự chữ không dấu, số hoặc . _ / -, không khoảng trắng.' },
                    ]}
                  >
                    <Input maxLength={30} readOnly={!isNew} autoFocus={isNew} placeholder="Ví dụ CT-2026-01" />
                  </Form.Item>
                  <Form.Item label="Trạng thái" name="status">
                    <Select
                      disabled={isNew}
                      showSearch={false}
                      options={Object.entries(constructionStatusLabels).map(([value, label]) => ({ value, label }))}
                    />
                  </Form.Item>
                </div>
                <Form.Item label="Tên công trình" name="name" rules={[{ required: true, whitespace: true, message: 'Hãy nhập tên công trình.' }, { max: 300 }]}>
                  <Input maxLength={300} />
                </Form.Item>
                <div className="detail-grid">
                  <Form.Item label="Phòng ban đề xuất" name="departmentId" rules={[{ required: true, message: 'Hãy chọn phòng ban đề xuất.' }]}>
                    <Select options={departmentOptions} loading={departments.isLoading} placeholder="Gõ mã hoặc tên phòng ban" />
                  </Form.Item>
                  <Form.Item label="Địa điểm" name="location" rules={[{ max: 300 }]}>
                    <Input maxLength={300} />
                  </Form.Item>
                </div>
                <div className="detail-grid">
                  <Form.Item label="Ngày khởi công" name="startDate">
                    <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
                  </Form.Item>
                  <Form.Item
                    label="Dự kiến hoàn thành"
                    name="expectedEndDate"
                    dependencies={['startDate']}
                    rules={[
                      ({ getFieldValue }) => ({
                        validator: (_, value: Dayjs | null) => {
                          const start = getFieldValue('startDate') as Dayjs | null
                          return value && start && value.isBefore(start, 'day')
                            ? Promise.reject(new Error('Ngày dự kiến hoàn thành không được trước ngày khởi công.'))
                            : Promise.resolve()
                        },
                      }),
                    ]}
                  >
                    <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
                  </Form.Item>
                </div>
                <div className="detail-grid">
                  {isNew ? (
                    <Form.Item
                      label="Tổng dự toán (VND, chưa VAT)"
                      name="estimateAmount"
                      rules={[{ required: true, type: 'number', min: 1, message: 'Tổng dự toán phải lớn hơn 0.' }]}
                    >
                      <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} precision={0} />
                    </Form.Item>
                  ) : (
                    <Form.Item
                      label="Tổng dự toán hiện hành (VND, chưa VAT)"
                      extra={current ? `Ban đầu ${formatMoney(current.initialEstimateAmount)} đ, điều chỉnh ${current.revisionCount} lần.` : undefined}
                    >
                      <Input value={current ? formatMoney(current.estimateAmount) : ''} readOnly style={{ textAlign: 'right', fontFamily: 'var(--erp-font-mono)' }} aria-label="Tổng dự toán hiện hành" />
                    </Form.Item>
                  )}
                  {!isNew && current && (
                    <>
                      {/* Lũy kế theo PO đã duyệt lấy dòng từ đề nghị loại A (lát Y3, Q4) – chỉ xem. */}
                      <Form.Item
                        label="Lũy kế đã mua (chưa VAT)"
                        extra={`Gồm VAT ${formatMoney(current.spentAmountWithVat)} đ. Tính theo PO đã duyệt lấy từ đề nghị mua loại A.`}
                      >
                        <Input value={formatMoney(current.spentAmount)} readOnly style={{ textAlign: 'right', fontFamily: 'var(--erp-font-mono)' }} aria-label="Lũy kế đã mua" />
                      </Form.Item>
                      <Form.Item label="Còn lại (chưa VAT)">
                        <Input value={formatMoney(current.remainingAmount)} readOnly style={{ textAlign: 'right', fontFamily: 'var(--erp-font-mono)' }} aria-label="Còn lại" />
                      </Form.Item>
                    </>
                  )}
                  {isNew && (
                    <Form.Item label="Căn cứ dự toán" name="basis" rules={[{ max: 500 }]} extra="Ví dụ: QĐ 12/QĐ-CT phê duyệt dự toán.">
                      <Input maxLength={500} />
                    </Form.Item>
                  )}
                </div>
                <Form.Item label="Ghi chú" name="note" rules={[{ max: 500 }]}>
                  <Input.TextArea rows={2} maxLength={500} />
                </Form.Item>
                {!isNew && current && (
                  <div className="detail-grid">
                    <Form.Item label="Người tạo">
                      <Input value={current.createdByName ?? ''} readOnly />
                    </Form.Item>
                    <Form.Item label="Thời điểm tạo">
                      <Input value={formatDateTime(current.createdAt)} readOnly />
                    </Form.Item>
                  </div>
                )}
              </>
            ),
          },
          ...(!isNew
            ? [
                {
                  key: 'estimate',
                  label: 'Dự toán',
                  children: (
                    <Table<ConstructionProjectRevision>
                      size="small"
                      pagination={false}
                      rowKey={(r) => r.id}
                      dataSource={revisions}
                      loading={detail.isLoading}
                      tableLayout="fixed"
                      scroll={{ x: 960 }}
                      aria-label="Lịch sử dự toán công trình"
                      columns={[
                        { title: 'Lần', dataIndex: 'revisionNo', width: 90, render: (v: number) => (v === 0 ? 'Ban đầu' : `Điều chỉnh ${v}`) },
                        { title: 'Ngày hiệu lực', dataIndex: 'effectiveDate', width: 100, render: (v: string) => formatDate(v) },
                        // Lần 0 là dự toán ban đầu – không có "trước" và chênh lệch để trống cho khỏi hiểu nhầm là tăng.
                        { title: 'Dự toán trước', key: 'previous', width: 120, align: 'right', render: (_, r) => (r.revisionNo === 0 ? '' : money(r.previousAmount)) },
                        { title: 'Dự toán sau', dataIndex: 'newAmount', width: 120, align: 'right', render: money },
                        { title: 'Chênh lệch', key: 'difference', width: 115, align: 'right', render: (_, r) => (r.revisionNo === 0 ? '' : signedMoney(r.difference)) },
                        { title: 'Tiền tệ', key: 'currency', width: 70, render: () => 'VND' },
                        { title: 'Căn cứ', dataIndex: 'basis', width: 160, ellipsis: { showTitle: true } },
                        { title: 'Lý do', dataIndex: 'reason', width: 180, ellipsis: { showTitle: true } },
                        {
                          title: 'Người ghi', key: 'by', width: 200, ellipsis: { showTitle: true },
                          render: (_, r) => `${r.createdByName ?? ''} ${formatDateTime(r.createdAt)}`,
                        },
                      ]}
                    />
                  ),
                },
              ]
            : []),
          attachmentTab,
        ]}
      />
      {revising && current && <ReviseDialog project={current} onClose={() => setRevising(false)} />}
    </>
  )
}
