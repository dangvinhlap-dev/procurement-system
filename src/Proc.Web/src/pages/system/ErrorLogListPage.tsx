import { BugOutlined, CodeOutlined } from '@ant-design/icons'
import { Alert, App, Button, Form, Input } from 'antd'
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import {
  useErrorGroup,
  useErrorGroupMutations,
  useErrorGroups,
  type ErrorGroupFilter,
  type ErrorGroupItem,
  type ErrorGroupStatus,
  type ErrorOccurrence,
  type ErrorSource,
} from '../../api/errorLog'
import { defaultLogRange } from '../../api/logs'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { Select } from '../../classic/Select'
import { DataGrid } from '../../components/DataGrid/DataGrid'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatDateTime } from '../../utils/format'
import { LogDateRangeFilter, TruncatedNotice } from './logFilters'

/** Đường dẫn màn hình ERR. */
const ERROR_LOG_PATH = '/he-thong/nhat-ky-loi'

/** Chữ và đèn của trạng thái – màu không là thông tin duy nhất (ui-style.md mục 2). */
const errorStatusLabels: Record<ErrorGroupStatus, { label: string; tone: StatusTone }> = {
  New: { label: 'Mới', tone: 'error' },
  InProgress: { label: 'Đang xử lý', tone: 'warning' },
  Resolved: { label: 'Đã xử lý', tone: 'success' },
}

const sourceLabels: Record<ErrorSource, string> = { Server: 'Máy chủ', Client: 'Giao diện' }

const columns: GridColumn<ErrorGroupItem>[] = [
  {
    key: 'status',
    title: 'Trạng thái',
    width: 95,
    type: 'status',
    value: (r) => errorStatusLabels[r.status]?.label ?? r.status,
    tone: (r) => errorStatusLabels[r.status]?.tone ?? 'neutral',
    groupable: true,
  },
  { key: 'lastSeenAt', title: 'Lần cuối', width: 120, type: 'datetime', value: (r) => r.lastSeenAt },
  { key: 'occurrenceCount', title: 'Số lần', width: 60, type: 'quantity', value: (r) => r.occurrenceCount },
  { key: 'source', title: 'Nguồn', width: 80, value: (r) => sourceLabels[r.source] ?? r.source, groupable: true },
  { key: 'errorType', title: 'Loại lỗi', width: 190, value: (r) => r.errorType, groupable: true },
  { key: 'message', title: 'Nội dung', width: 320, value: (r) => r.message },
  { key: 'path', title: 'Đường dẫn', width: 200, value: (r) => r.path, groupable: true },
  { key: 'lastTrackingCode', title: 'Mã theo dõi gần nhất', width: 125, value: (r) => r.lastTrackingCode },
  { key: 'lastVersion', title: 'Phiên bản', width: 70, value: (r) => r.lastVersion, groupable: true },
  { key: 'firstSeenAt', title: 'Lần đầu', width: 120, type: 'datetime', value: (r) => r.firstSeenAt },
  { key: 'reopenCount', title: 'Mở lại', width: 55, type: 'quantity', value: (r) => r.reopenCount },
  { key: 'handledByName', title: 'Người xử lý', width: 110, value: (r) => r.handledByName, groupable: true },
  { key: 'note', title: 'Ghi chú xử lý', width: 220, value: (r) => r.note },
]

const occurrenceColumns: GridColumn<ErrorOccurrence>[] = [
  { key: 'occurredAt', title: 'Thời điểm', width: 120, type: 'datetime', value: (r) => r.occurredAt },
  { key: 'trackingCode', title: 'Mã theo dõi', width: 110, value: (r) => r.trackingCode },
  { key: 'userName', title: 'Người dùng', width: 100, value: (r) => r.userName, groupable: true },
  { key: 'companyCode', title: 'Công ty', width: 65, value: (r) => r.companyCode, groupable: true },
  { key: 'method', title: 'PT', width: 45, value: (r) => r.method },
  { key: 'path', title: 'Đường dẫn', width: 220, value: (r) => r.path },
  { key: 'message', title: 'Nội dung', width: 240, value: (r) => r.message },
  { key: 'version', title: 'Phiên bản', width: 70, value: (r) => r.version, groupable: true },
  { key: 'environment', title: 'Môi trường', width: 75, value: (r) => r.environment },
  { key: 'userAgent', title: 'Trình duyệt', width: 260, value: (r) => r.userAgent },
]

/**
 * Màn hình ERR – Nhật ký lỗi (design.md mục 21.4, lát QT2): lỗi máy chủ (500) và lỗi giao diện gộp theo nhóm, kèm số lần, lần đầu / lần cuối,
 * trạng thái Mới / Đang xử lý / Đã xử lý (đã xử lý mà lặp lại thì tự về Mới). Ô "Mã theo dõi" tìm đúng lỗi người dùng báo (mã trên câu báo
 * lỗi). Chỉ ở tổng công ty; quyền xem System.OpsErrorLog.View, đổi trạng thái / gây lỗi thử System.OpsErrorLog.Manage. Đường dẫn
 * `/he-thong/nhat-ky-loi/:id` (từ thông báo) mở sẵn nhóm lỗi đó.
 */
export function ErrorLogListPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { message } = App.useApp()
  const { hasPermission } = useAuth()
  const canManage = hasPermission(Permission.ErrorLogManage)
  const [filter, setFilter] = useState<ErrorGroupFilter>(() => ({ ...defaultLogRange(), status: null, trackingCode: '' }))
  const [codeInput, setCodeInput] = useState('')
  const groups = useErrorGroups(filter)
  const { testServerError } = useErrorGroupMutations()

  const causeServerError = async () => {
    try {
      await testServerError.mutateAsync()
    } catch (e) {
      // Câu báo lỗi 500 có mã theo dõi – đúng câu người dùng thật sẽ thấy.
      message.warning(e instanceof Error ? e.message : 'Đã gây lỗi máy chủ thử.')
    }
  }

  const causeClientError = () => {
    // Ném lỗi ngoài React (không ai bắt) – đi đúng đường window.onerror → nhật ký lỗi như lỗi giao diện thật.
    window.setTimeout(() => {
      throw new Error(`Lỗi giao diện thử từ màn hình Nhật ký lỗi lúc ${formatDateTime(new Date())}`)
    })
    message.info('Đã gây lỗi giao diện thử – dòng mới hiện trong lưới sau giây lát.')
    window.setTimeout(() => void groups.refetch(), 1500)
  }

  return (
    <>
      <ListPage
        title="Nhật ký lỗi"
        exportFileName="nhat-ky-loi"
        rows={groups.data?.items ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={groups.isLoading}
        canCreate={false}
        isMarked={(r) => r.status === 'New'}
        onOpenRow={(r) => navigate(`${ERROR_LOG_PATH}/${r.id}`)}
        actions={
          canManage && (
            <>
              <Button icon={<BugOutlined />} loading={testServerError.isPending} onClick={() => void causeServerError()}
                title="Gây một lỗi 500 ở máy chủ để kiểm tra việc ghi nhật ký lỗi (sau khi cài đặt / nâng cấp)">
                Thử lỗi máy chủ
              </Button>
              <Button icon={<CodeOutlined />} onClick={causeClientError} title="Gây một lỗi giao diện để kiểm tra việc gửi lỗi từ trình duyệt">
                Thử lỗi giao diện
              </Button>
            </>
          )
        }
        notice={
          groups.error ? <Alert type="error" showIcon title={groups.error.message} /> : <TruncatedNotice truncated={groups.data?.truncated} />
        }
        onClearFilters={() => {
          setCodeInput('')
          setFilter({ ...defaultLogRange(), status: null, trackingCode: '' })
        }}
        filters={
          <>
            <LogDateRangeFilter value={filter} onChange={(range) => setFilter((f) => ({ ...f, ...range }))} />
            <FilterField label="Trạng thái" width={120}>
              {(fieldId) => (
                <Select
                  id={fieldId}
                  value={filter.status ?? ''}
                  onChange={(v: string) => setFilter((f) => ({ ...f, status: (v || null) as ErrorGroupStatus | null }))}
                  options={[
                    { value: '', label: 'Tất cả' },
                    ...(Object.keys(errorStatusLabels) as ErrorGroupStatus[]).map((s) => ({ value: s, label: errorStatusLabels[s].label })),
                  ]}
                />
              )}
            </FilterField>
            <FilterField label="Mã theo dõi" width={150}>
              {(fieldId) => (
                <Input.Search
                  id={fieldId}
                  allowClear
                  placeholder="ERR-…"
                  title="Mã trên câu báo lỗi người dùng gửi – có mã thì tìm mọi ngày"
                  value={codeInput}
                  onChange={(e) => {
                    setCodeInput(e.target.value)
                    if (!e.target.value) setFilter((f) => ({ ...f, trackingCode: '' }))
                  }}
                  onSearch={(v) => setFilter((f) => ({ ...f, trackingCode: v.trim() }))}
                />
              )}
            </FilterField>
          </>
        }
      />
      {id && <ErrorGroupDrawer key={id} groupId={id} canManage={canManage} onClose={() => navigate(ERROR_LOG_PATH)} />}
    </>
  )
}

interface StatusFormValues {
  status: ErrorGroupStatus
  note: string
}

/**
 * Ngăn kéo một nhóm lỗi: tab Thông tin (nội dung, vị trí, số lần + Trạng thái, Ghi chú xử lý sửa được khi có quyền), Lần xảy ra (lưới), Chi tiết
 * kỹ thuật (ngăn xếp của lần đang chọn trong lưới, mặc định lần gần nhất).
 * @param groupId Id nhóm lỗi.
 * @param canManage Có quyền xử lý.
 * @param onClose Đóng ngăn kéo.
 */
function ErrorGroupDrawer({ groupId, canManage, onClose }: { groupId: string; canManage: boolean; onClose: () => void }) {
  const { message } = App.useApp()
  const detail = useErrorGroup(groupId)
  const { changeStatus } = useErrorGroupMutations()
  const [form] = Form.useForm<StatusFormValues>()
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<ErrorOccurrence | null>(null)
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const group = detail.data?.group
  const occurrences = detail.data?.occurrences ?? []
  const shown = selected ?? occurrences[0] ?? null
  // Dữ liệu tải sau khi ngăn kéo mở: đặt lại ô Trạng thái / Ghi chú theo nhóm vừa tải.
  useEffect(() => {
    if (group) form.setFieldsValue({ status: group.status, note: group.note ?? '' })
  }, [group, form])

  const save = async () => {
    if (!group) return
    const values = form.getFieldsValue()
    setError(null)
    try {
      await changeStatus.mutateAsync({ id: group.id, status: values.status, note: values.note?.trim() || null })
      message.success(`Đã lưu trạng thái nhóm lỗi: ${errorStatusLabels[values.status].label}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được trạng thái.')
    }
  }

  const info = (label: string, value: string | number | null | undefined) => (
    <Form.Item label={label}>
      <Input readOnly value={value ?? ''} title={value?.toString() ?? ''} />
    </Form.Item>
  )

  return (
    <DetailDrawer
      open
      title={group ? `Nhóm lỗi: ${group.errorType}` : 'Nhóm lỗi'}
      onClose={onClose}
      loading={detail.isLoading}
      error={error ?? detail.error?.message ?? null}
      onSave={canManage && group ? () => void save() : undefined}
      saving={changeStatus.isPending}
      form={form}
      initialValues={group ? { status: group.status, note: group.note ?? '' } : undefined}
      readOnly={!canManage}
      tabs={[
        {
          key: 'info',
          label: 'Thông tin',
          children: group && (
            <>
              {info('Nguồn', sourceLabels[group.source] ?? group.source)}
              {info('Loại lỗi', group.errorType)}
              <Form.Item label="Nội dung">
                <Input.TextArea readOnly value={group.message} autoSize={{ minRows: 2, maxRows: 5 }} />
              </Form.Item>
              {info('Vị trí', group.location)}
              {info('Đường dẫn', group.path)}
              {info('Số lần / mở lại', `${group.occurrenceCount.toLocaleString('vi-VN')} lần · mở lại ${group.reopenCount} lần`)}
              {info('Lần đầu', formatDateTime(group.firstSeenAt))}
              {info('Lần cuối', formatDateTime(group.lastSeenAt))}
              {info('Người xử lý', group.handledByName ? `${group.handledByName} · ${formatDateTime(group.handledAt)}` : '')}
              <Form.Item label="Trạng thái" name="status" extra="Đã xử lý mà lỗi lặp lại thì nhóm tự về Mới và báo người xử lý.">
                <Select
                  showSearch={false}
                  options={(Object.keys(errorStatusLabels) as ErrorGroupStatus[]).map((s) => ({ value: s, label: errorStatusLabels[s].label }))}
                />
              </Form.Item>
              <Form.Item label="Ghi chú xử lý" name="note" extra="Bỏ trống thì giữ ghi chú cũ.">
                <Input.TextArea autoSize={{ minRows: 2, maxRows: 6 }} maxLength={2000} />
              </Form.Item>
            </>
          ),
        },
        {
          key: 'occurrences',
          label: `Lần xảy ra (${occurrences.length})`,
          lazy: true,
          children: (
            <div style={{ height: 360, display: 'flex', flexDirection: 'column' }} data-testid="error-occurrences">
              <DataGrid
                rows={occurrences}
                columns={occurrenceColumns}
                rowKey={(r) => r.id}
                groupBy={groupBy}
                onGroupByChange={setGroupBy}
                onSelectRow={setSelected}
              />
            </div>
          ),
        },
        {
          key: 'detail',
          label: 'Chi tiết kỹ thuật',
          lazy: true,
          children: (
            <div>
              <div style={{ marginBottom: 4 }}>
                {shown ? `Lần ${shown.trackingCode} lúc ${formatDateTime(shown.occurredAt)} – chọn lần khác ở tab Lần xảy ra.` : 'Không còn lần xảy ra nào.'}
              </div>
              <pre className="erp-error-detail" data-testid="error-detail">
                {shown?.detail ?? shown?.message ?? ''}
              </pre>
            </div>
          ),
        },
      ]}
    />
  )
}
