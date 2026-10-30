import { LockOutlined, RollbackOutlined, UnlockOutlined } from '@ant-design/icons'
import { Alert, App, Button, Descriptions, Input, Select } from 'antd'
import { useState } from 'react'
import {
  periodStatusLabels,
  useInventoryCalendar,
  useInventoryPeriodMutations,
  type InventoryPeriod,
  type PeriodStatus,
} from '../../api/inventory'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatDate, formatDateTime } from '../../utils/format'

const statusTone: Record<PeriodStatus, StatusTone> = {
  NotOpened: 'neutral',
  Open: 'success',
  Costing: 'warning',
  Closed: 'error',
  Locked: 'error',
}

const columns: GridColumn<InventoryPeriod>[] = [
  { key: 'label', title: 'Kỳ', width: 110, value: (r) => r.label },
  { key: 'start', title: 'Từ ngày', width: 100, type: 'date', value: (r) => r.startDate },
  { key: 'end', title: 'Đến ngày', width: 100, type: 'date', value: (r) => r.endDate },
  {
    key: 'status',
    title: 'Trạng thái',
    width: 130,
    type: 'status',
    value: (r) => periodStatusLabels[r.status],
    tone: (r) => statusTone[r.status],
    groupable: true,
  },
  { key: 'closedAt', title: 'Ngày đóng', width: 130, type: 'datetime', value: (r) => r.closedAt },
  {
    key: 'next',
    title: 'Làm được',
    width: 110,
    value: (r) => (r.canOpen ? 'Mở kỳ' : r.canClose ? 'Đóng kỳ' : r.canReopen ? 'Mở lại kỳ' : ''),
  },
  { key: 'reopenReason', title: 'Lý do mở lại gần nhất', width: 220, value: (r) => r.reopenReason },
]

/**
 * Màn hình Kỳ kho (design.md mục 7, 7.6): lịch kỳ năm và 12 kỳ tháng của công ty đang làm việc.
 * "Tạo mới" tạo lịch năm tiếp theo; bấm đúp một kỳ tháng để mở kỳ, đóng kỳ (tính giá vốn) hoặc mở lại kỳ (design.md mục 7.2–7.4a).
 */
export function InventoryPeriodPage() {
  const { message, modal } = App.useApp()
  const { workingCompany, hasPermission } = useAuth()
  const [year, setYear] = useState<number | undefined>()
  const calendar = useInventoryCalendar(year)
  const mutations = useInventoryPeriodMutations()
  const [open, setOpen] = useState<InventoryPeriod | undefined>()
  const canManage = hasPermission(Permission.PeriodOpen)
  const data = calendar.data

  const createYear = () => {
    if (!data) return
    modal.confirm({
      title: `Tạo lịch kỳ kho năm ${data.nextYear} cho ${workingCompany?.code ?? ''}?`,
      content: 'Tạo kỳ năm và 12 kỳ tháng ở trạng thái Chưa mở. Lịch các năm phải liền nhau.',
      okText: 'Tạo lịch',
      cancelText: 'Không',
      onOk: async () => {
        try {
          const result = await mutations.createYear.mutateAsync(data.nextYear)
          setYear(result.year ?? undefined)
          message.success(`Đã tạo lịch kỳ kho năm ${data.nextYear}.`)
        } catch (e) {
          message.error(e instanceof Error ? e.message : 'Không tạo được lịch kỳ.')
        }
      },
    })
  }

  return (
    <>
      <ListPage
        title={`Kỳ kho – ${workingCompany?.code ?? ''}`}
        exportFileName="ky-kho"
        rows={data?.periods ?? []}
        columns={columns}
        rowKey={(r) => r.id}
        loading={calendar.isLoading}
        canCreate={canManage && data !== undefined}
        onCreate={createYear}
        onOpenRow={(r) => (r.month > 0 ? setOpen(r) : undefined)}
        notice={
          data && data.years.length === 0 ? (
            <Alert type="info" showIcon title={`Công ty chưa có lịch kỳ kho. Bấm "Tạo mới" để tạo lịch năm ${data.nextYear}.`} />
          ) : undefined
        }
        filters={
          <FilterField label="Năm" width={120}>
            {(id) => (
              <Select
                id={id}
                value={data?.year ?? undefined}
                onChange={setYear}
                options={(data?.years ?? []).map((y) => ({ value: y, label: String(y) }))}
              />
            )}
          </FilterField>
        }
        onClearFilters={() => setYear(undefined)}
      />
      {open && <PeriodDrawer period={open} canManage={canManage} onClose={() => setOpen(undefined)} />}
    </>
  )
}

/**
 * Ngăn kéo chi tiết kỳ tháng: "Mở kỳ" (kỳ chưa mở), "Đóng kỳ" (tính giá vốn, chốt số dư), "Mở lại kỳ" (bắt buộc lý do).
 * Nút chỉ hiện khi server báo làm được (đúng thứ tự, có quyền) – server vẫn kiểm tra lại khi bấm.
 * @param period Kỳ đang xem.
 * @param canManage Có quyền mở kỳ.
 * @param onClose Đóng.
 */
function PeriodDrawer({ period, canManage, onClose }: { period: InventoryPeriod; canManage: boolean; onClose: () => void }) {
  const { message, modal } = App.useApp()
  const mutations = useInventoryPeriodMutations()
  const [error, setError] = useState<string | null>(null)
  const [reason, setReason] = useState('')

  const run = async (action: () => Promise<unknown>, done: string, fallback: string) => {
    setError(null)
    try {
      await action()
      message.success(done)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : fallback)
    }
  }

  const closePeriod = () =>
    modal.confirm({
      title: `Đóng kỳ ${period.label}?`,
      content:
        'Hệ thống tính giá vốn bình quân cuối kỳ, gán giá cho phiếu chuyển kho, chốt số dư từng kho. Sau khi đóng không ghi sổ, ' +
        'bỏ ghi sổ chứng từ trong kỳ được nữa; muốn sửa phải mở lại kỳ.',
      okText: 'Đóng kỳ',
      cancelText: 'Không',
      onOk: () => run(() => mutations.close.mutateAsync(period.id), `Đã đóng kỳ ${period.label}.`, 'Không đóng được kỳ.'),
    })

  const actions = (
    <>
      {canManage && period.status === 'NotOpened' && (
        <Button type="primary" icon={<UnlockOutlined />} disabled={!period.canOpen} loading={mutations.open.isPending}
          onClick={() => run(() => mutations.open.mutateAsync(period.id), `Đã mở kỳ ${period.label}.`, 'Không mở được kỳ.')}>
          Mở kỳ
        </Button>
      )}
      {period.canClose && (
        <Button type="primary" icon={<LockOutlined />} onClick={closePeriod} loading={mutations.close.isPending}>
          Đóng kỳ
        </Button>
      )}
      {period.canReopen && (
        <Button danger icon={<RollbackOutlined />} disabled={!reason.trim()} loading={mutations.reopen.isPending}
          onClick={() => run(() => mutations.reopen.mutateAsync({ id: period.id, reason: reason.trim() }), `Đã mở lại kỳ ${period.label}.`, 'Không mở lại được kỳ.')}>
          Mở lại kỳ
        </Button>
      )}
    </>
  )

  return (
    <DetailDrawer
      open
      title={`Kỳ kho ${period.label}`}
      onClose={onClose}
      error={error}
      extraActions={actions}
      tabs={[
        {
          key: 'general',
          label: 'Thông tin chung',
          children: (
            <>
              {period.status === 'NotOpened' && !period.canOpen && (
                <Alert
                  type="info"
                  showIcon
                  className="detail-section"
                  title="Mở kỳ tuần tự theo tháng: chỉ mở được kỳ liền sau kỳ đã mở gần nhất."
                />
              )}
              {period.status === 'Open' && !period.canClose && (
                <Alert type="info" showIcon className="detail-section" title="Đóng kỳ tuần tự: kỳ tháng trước phải đã đóng (và cần quyền đóng kỳ)." />
              )}
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label="Kỳ">{period.label}</Descriptions.Item>
                <Descriptions.Item label="Từ ngày">{formatDate(period.startDate)}</Descriptions.Item>
                <Descriptions.Item label="Đến ngày">{formatDate(period.endDate)}</Descriptions.Item>
                <Descriptions.Item label="Trạng thái">{periodStatusLabels[period.status]}</Descriptions.Item>
                <Descriptions.Item label="Ngày đóng">{formatDateTime(period.closedAt)}</Descriptions.Item>
                <Descriptions.Item label="Lý do mở lại gần nhất">{period.reopenReason}</Descriptions.Item>
              </Descriptions>
              {period.canReopen && (
                <div className="detail-section" style={{ marginTop: 8 }}>
                  <label htmlFor="reopen-reason">Lý do mở lại kỳ (bắt buộc)</label>
                  <Input.TextArea id="reopen-reason" rows={2} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
                </div>
              )}
              <div className="detail-hint" style={{ marginTop: 8 }}>
                Chứng từ kho chỉ ghi sổ được khi ngày chứng từ thuộc kỳ tháng đang mở. Đóng kỳ cần kỳ trước đã đóng và không còn
                phiếu nháp trong kỳ; chỉ mở lại được kỳ đã đóng gần nhất.
              </div>
            </>
          ),
        },
      ]}
    />
  )
}
