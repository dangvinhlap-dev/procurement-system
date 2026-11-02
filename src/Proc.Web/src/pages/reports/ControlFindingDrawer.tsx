import { CheckCircleOutlined, ExportOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Flex, Form, Input, Select } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import { useState } from 'react'
import { useNavigate } from 'react-router'
import { useConflictSupplierOptions } from '../../api/conflicts'
import {
  anomalyLabels,
  findingSeverityLabels,
  useControlFinding,
  useControlFindingMutations,
  type ControlFinding,
  type FindingPrefillState,
  type FindingSeverity,
} from '../../api/control'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { renderRequiredMark } from '../../components/RequiredMark/requiredMark'
import { DATE_INPUT_FORMATS, formatDateTime } from '../../utils/format'
import { matchesKeyword } from '../../utils/text'
import { controlRefPath } from './controlPaths'

interface FindingFormValues {
  date: Dayjs | null
  severity: FindingSeverity | null
  title: string
  description: string
  supplierId: string | null
}

/** Lọc ô chọn theo nhãn, gõ có dấu hoặc không dấu đều tìm được (CLAUDE.md mục 5). */
const filterByLabel = (input: string, option?: { label?: unknown }) => matchesKeyword(String(option?.label ?? ''), input)

const severityOptions = Object.entries(findingSeverityLabels).map(([value, label]) => ({ value, label }))

/**
 * Ngăn kéo vi phạm KSNB (design.md mục 18.19). Lập mới (có thể điền sẵn từ một dấu hiệu bất thường), sửa khi còn mở, "Kết luận" thì khóa
 * phiếu. Không có nút xóa: ghi nhận nhầm thì kết luận "không vi phạm" và hạ mức Thường. Lãnh đạo (quyền nhận báo cáo) chỉ xem.
 * @param id Vi phạm đang mở; null = ghi nhận mới.
 * @param prefill Nội dung điền sẵn khi ghi nhận từ dấu hiệu bất thường.
 * @param onClose Đóng ngăn kéo.
 * @param onOpen Mở vi phạm vừa ghi nhận.
 */
export function ControlFindingDrawer({
  id,
  prefill,
  onClose,
  onOpen,
}: {
  id: string | null
  prefill?: FindingPrefillState['findingPrefill']
  onClose: () => void
  onOpen: (id: string) => void
}) {
  const finding = useControlFinding(id)
  if (id && !finding.data) {
    return <DetailDrawer open loading title="Vi phạm KSNB" tabs={[]} onClose={onClose} error={finding.error?.message ?? null} />
  }
  return <FindingForm key={finding.data?.id ?? 'new'} finding={finding.data ?? null} prefill={prefill} onClose={onClose} onOpen={onOpen} />
}

function FindingForm({
  finding,
  prefill,
  onClose,
  onOpen,
}: {
  finding: ControlFinding | null
  prefill?: FindingPrefillState['findingPrefill']
  onClose: () => void
  onOpen: (id: string) => void
}) {
  const { message, modal } = App.useApp()
  const navigate = useNavigate()
  const { hasPermission } = useAuth()
  const [form] = Form.useForm<FindingFormValues>()
  const mutations = useControlFindingMutations()
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const isNew = finding === null
  const canManage = hasPermission(Permission.ControlFindingManage)
  const editable = canManage && (isNew || finding.status === 'Open')
  const suppliers = useConflictSupplierOptions(editable)
  const source = finding ?? prefill
  const refPath = controlRefPath(source?.refScreen ?? null, source?.refId ?? null)

  const initialValues: FindingFormValues = {
    date: source?.date ? dayjs(source.date) : dayjs(),
    severity: source?.severity ?? null,
    title: source?.title ?? '',
    description: source?.description ?? '',
    supplierId: source?.supplierId ?? null,
  }

  const save = async () => {
    let values: FindingFormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn trường chưa hợp lệ – xem các ô báo đỏ.')
      return
    }
    setError(null)
    setSaving(true)
    const input = {
      date: values.date!.format('YYYY-MM-DD'),
      severity: values.severity!,
      title: values.title.trim(),
      description: values.description.trim(),
      supplierId: values.supplierId ?? null,
    }
    try {
      if (isNew) {
        const saved = await mutations.create.mutateAsync({
          ...input,
          anomalyCode: prefill?.anomalyCode ?? null,
          refScreen: prefill?.refScreen ?? null,
          refId: prefill?.refId ?? null,
          refNo: prefill?.refNo ?? null,
        })
        message.success(`Đã ghi nhận vi phạm ${saved.no} và thông báo lãnh đạo.`)
        onOpen(saved.id)
      } else {
        await mutations.update.mutateAsync({ id: finding.id, input })
        message.success(`Đã lưu vi phạm ${finding.no}.`)
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được vi phạm.')
    } finally {
      setSaving(false)
    }
  }

  const conclude = () => {
    if (!finding) return
    let conclusion = ''
    let severity: FindingSeverity = finding.severity
    modal.confirm({
      title: `Kết luận vi phạm ${finding.no}?`,
      content: (
        <Flex vertical gap={6}>
          <label htmlFor="finding-conclusion">{renderRequiredMark('Kết luận, hướng xử lý', { required: true })}</label>
          <Input.TextArea id="finding-conclusion" rows={4} maxLength={2000} onChange={(e) => (conclusion = e.target.value)} />
          <label htmlFor="finding-final-severity">Mức sau xác minh</label>
          <Select
            id="finding-final-severity"
            defaultValue={severity}
            options={severityOptions}
            onChange={(v: FindingSeverity) => (severity = v)}
          />
          <span>Kết luận xong phiếu bị khóa; lãnh đạo nhận thông báo. Ghi nhận nhầm thì kết luận "không vi phạm", mức Thường.</span>
        </Flex>
      ),
      okText: 'Kết luận',
      cancelText: 'Không',
      onOk: async () => {
        if (!conclusion.trim()) {
          message.error('Hãy nhập kết luận.')
          throw new Error('missing conclusion')
        }
        try {
          await mutations.conclude.mutateAsync({ id: finding.id, conclusion: conclusion.trim(), severity })
          message.success(`Đã kết luận vi phạm ${finding.no}.`)
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không kết luận được vi phạm.')
        }
      },
    })
  }

  const supplierOptions = editable
    ? (suppliers.data ?? []).map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` }))
    : source?.supplierId
      ? [{ value: source.supplierId, label: `${source.supplierCode ?? ''} – ${source.supplierName ?? ''}` }]
      : []

  return (
    <DetailDrawer
      open
      title={isNew ? 'Ghi nhận vi phạm' : `Vi phạm ${finding.no}`}
      audit={{ entityType: 'ControlFinding', id: finding?.id }}
      onClose={onClose}
      onSave={editable ? save : undefined}
      saving={saving}
      error={error}
      form={form}
      formName="control-finding"
      initialValues={initialValues}
      readOnly={!editable}
      extraActions={
        <>
          {refPath && (
            <Button icon={<ExportOutlined />} onClick={() => navigate(refPath)}>
              Mở chứng từ
            </Button>
          )}
          {!isNew && canManage && finding.status === 'Open' && (
            <Button icon={<CheckCircleOutlined />} onClick={conclude} loading={mutations.conclude.isPending}>
              Kết luận
            </Button>
          )}
        </>
      }
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              <Alert
                type={finding?.status === 'Concluded' ? 'info' : 'warning'}
                showIcon
                style={{ marginBottom: 8 }}
                title={
                  isNew
                    ? 'Lưu xong, người có quyền nhận báo cáo vi phạm (lãnh đạo) được thông báo ngay. Vi phạm không xóa được.'
                    : finding.status === 'Concluded'
                      ? `Đã kết luận ${formatDateTime(finding.concludedAt)} (${finding.concludedByName ?? ''}): ${finding.conclusion ?? ''}`
                      : 'Đang xác minh – sửa được đến khi kết luận.'
                }
              />
              {source?.anomalyCode && (
                <div className="detail-grid">
                  <Form.Item label="Từ dấu hiệu" htmlFor="finding-anomaly">
                    <Input id="finding-anomaly" value={anomalyLabels[source.anomalyCode]} readOnly />
                  </Form.Item>
                  <Form.Item label="Chứng từ liên quan" htmlFor="finding-ref">
                    <Input id="finding-ref" value={source.refNo ?? ''} readOnly />
                  </Form.Item>
                </div>
              )}
              <div className="detail-grid">
                <Form.Item label="Ngày phát hiện" name="date" rules={[{ required: true, message: 'Chọn ngày phát hiện.' }]}>
                  <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
                </Form.Item>
                <Form.Item label="Mức" name="severity" rules={[{ required: true, message: 'Hãy chọn mức vi phạm.' }]}>
                  <Select options={severityOptions} />
                </Form.Item>
              </div>
              <Form.Item
                label="Tiêu đề"
                name="title"
                rules={[{ required: true, whitespace: true, message: 'Hãy nhập tiêu đề vi phạm.' }, { max: 200 }]}
              >
                <Input maxLength={200} autoFocus={isNew} />
              </Form.Item>
              <Form.Item
                label="Mô tả, bằng chứng"
                name="description"
                rules={[{ required: true, whitespace: true, message: 'Hãy mô tả vi phạm.' }, { max: 2000 }]}
              >
                <Input.TextArea rows={5} maxLength={2000} />
              </Form.Item>
              <Form.Item label="Nhà cung cấp" name="supplierId">
                <Select
                  allowClear
                  options={supplierOptions}
                  loading={suppliers.isLoading}
                  showSearch={{ filterOption: filterByLabel }}
                  placeholder="Gõ mã hoặc tên NCC"
                />
              </Form.Item>
              {!isNew && (
                <div className="detail-grid">
                  <Form.Item label="Người ghi nhận">
                    <Input value={finding.createdByName ?? ''} readOnly />
                  </Form.Item>
                  <Form.Item label="Thời điểm ghi nhận">
                    <Input value={formatDateTime(finding.createdAt)} readOnly />
                  </Form.Item>
                </div>
              )}
            </>
          ),
        },
      ]}
    />
  )
}
