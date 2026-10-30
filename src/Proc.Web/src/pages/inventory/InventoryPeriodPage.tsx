import { UnlockOutlined } from '@ant-design/icons'
import { Alert, App, Button, Descriptions, Select } from 'antd'
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
  { key: 'canOpen', title: 'Mở được', width: 90, value: (r) => (r.canOpen ? 'Kỳ tiếp theo' : '') },
]

/**
 * Màn hình Kỳ kho (design.md mục 7, 7.6): lịch kỳ năm và 12 kỳ tháng của công ty đang làm việc.
 * "Tạo mới" tạo lịch năm tiếp theo; bấm đúp một kỳ tháng để mở kỳ (mở tuần tự theo tháng).
 * Đóng kỳ, mở lại kỳ làm ở giai đoạn 3 cùng tính giá vốn.
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
 * Ngăn kéo chi tiết kỳ tháng, có nút "Mở kỳ" khi kỳ mở được.
 * @param period Kỳ đang xem.
 * @param canManage Có quyền mở kỳ.
 * @param onClose Đóng.
 */
function PeriodDrawer({ period, canManage, onClose }: { period: InventoryPeriod; canManage: boolean; onClose: () => void }) {
  const { message } = App.useApp()
  const mutations = useInventoryPeriodMutations()
  const [error, setError] = useState<string | null>(null)

  const openPeriod = async () => {
    setError(null)
    try {
      await mutations.open.mutateAsync(period.id)
      message.success(`Đã mở kỳ ${period.label}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không mở được kỳ.')
    }
  }

  return (
    <DetailDrawer
      open
      title={`Kỳ kho ${period.label}`}
      onClose={onClose}
      error={error}
      extraActions={
        canManage &&
        period.status === 'NotOpened' && (
          <Button type="primary" icon={<UnlockOutlined />} onClick={openPeriod} loading={mutations.open.isPending} disabled={!period.canOpen}>
            Mở kỳ
          </Button>
        )
      }
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
              <Descriptions size="small" column={1} bordered>
                <Descriptions.Item label="Kỳ">{period.label}</Descriptions.Item>
                <Descriptions.Item label="Từ ngày">{formatDate(period.startDate)}</Descriptions.Item>
                <Descriptions.Item label="Đến ngày">{formatDate(period.endDate)}</Descriptions.Item>
                <Descriptions.Item label="Trạng thái">{periodStatusLabels[period.status]}</Descriptions.Item>
                <Descriptions.Item label="Ngày đóng">{formatDateTime(period.closedAt)}</Descriptions.Item>
              </Descriptions>
              <div className="detail-hint" style={{ marginTop: 8 }}>
                Chứng từ kho chỉ ghi sổ được khi ngày chứng từ thuộc kỳ tháng đang mở. Đóng kỳ, mở lại kỳ làm cùng tính giá vốn.
              </div>
            </>
          ),
        },
      ]}
    />
  )
}
