import { Alert, App, Button, Empty, Flex, Input, Radio, Spin } from 'antd'
import { Select } from '../../classic/Select'
import { useDeferredValue, useState } from 'react'
import {
  usePurchaseHistoryCandidates,
  usePurchaseHistoryMapping,
  usePurchaseHistorySources,
  type PurchaseHistoryMappingKind,
  type PurchaseHistorySource,
} from '../../api/purchaseHistory'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn, StatusTone } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { FilterField, ListPage } from '../../components/ListPage/ListPage'
import { formatMoney } from '../../utils/format'

/** Trạng thái gán của một mã cũ / NCC cũ. */
function mappingStatus(s: PurchaseHistorySource): { text: string; tone: StatusTone } {
  if (s.matchedCount === 0) return { text: 'Chưa gán', tone: 'neutral' }
  if (s.matchedCount < s.recordCount) return { text: 'Gán một phần', tone: 'warning' }
  return { text: s.hasMapping ? 'Đã ánh xạ' : 'Tự khớp', tone: 'success' }
}

/** Nhãn theo loại ánh xạ. */
const labels = {
  item: { code: 'Mã vật tư cũ', name: 'Tên vật tư', target: 'mặt hàng chung', targetCode: 'Mã hàng chung' },
  supplier: { code: 'MST', name: 'Tên NCC', target: 'NCC trong danh mục', targetCode: 'Mã NCC' },
} as const

/** Cột lưới cho màn hình ánh xạ theo loại (mặt hàng hoặc NCC). */
function columnsFor(kind: PurchaseHistoryMappingKind): GridColumn<PurchaseHistorySource>[] {
  const l = labels[kind]
  return [
    { key: 'code', title: l.code, width: 120, value: (s) => s.code },
    { key: 'name', title: l.name, width: 240, value: (s) => s.name },
    ...(kind === 'item' ? [{ key: 'detail', title: 'Quy cách · ĐVT', width: 150, value: (s: PurchaseHistorySource) => s.detail }] : []),
    { key: 'count', title: 'Số dòng', width: 80, type: 'quantity', value: (s) => s.recordCount },
    { key: 'matched', title: 'Đã gán', width: 80, type: 'quantity', value: (s) => s.matchedCount },
    // Cộng thẳng thành tiền các dòng (dòng ngoại tệ chưa quy đổi) – chỉ để xếp ưu tiên mã nhiều tiền làm trước.
    { key: 'amount', title: 'Thành tiền', width: 130, type: 'money', value: (s) => s.amount },
    { key: 'last', title: 'Mua gần nhất', width: 100, type: 'date', value: (s) => s.lastPurchaseDate },
    {
      key: 'status',
      title: 'Trạng thái',
      width: 120,
      type: 'status',
      value: (s) => mappingStatus(s).text,
      tone: (s) => mappingStatus(s).tone,
      groupable: true,
    },
    { key: 'targetCode', title: l.targetCode, width: 120, value: (s) => s.targetCode },
    { key: 'targetName', title: kind === 'item' ? 'Tên hàng chung' : 'Tên NCC trong danh mục', width: 240, value: (s) => s.targetName },
  ]
}

/**
 * Báo cáo → Ánh xạ dữ liệu mua (mã PHMAP, design.md mục 18.2, lát E2): gán mã vật tư cũ của pháp nhân về mã hàng chung, NCC cũ về NCC
 * trong danh mục, để báo cáo phân tích (E3) cộng gộp được giữa các pháp nhân. Mở một dòng → chọn gợi ý (tên giống nhất) hoặc tìm mã khác.
 */
export function PurchaseHistoryMappingPage() {
  const { hasPermission } = useAuth()
  const canMap = hasPermission(Permission.PurchaseHistoryImport)
  const [kind, setKind] = useState<PurchaseHistoryMappingKind>('item')
  const [status, setStatus] = useState('')
  const [opened, setOpened] = useState<PurchaseHistorySource | null>(null)
  const sources = usePurchaseHistorySources(kind)
  const all = sources.data ?? []
  const rows = all.filter((s) => {
    if (status === 'unmapped') return s.matchedCount < s.recordCount
    if (status === 'mapped') return s.matchedCount === s.recordCount
    return true
  })
  const pending = all.filter((s) => s.matchedCount < s.recordCount)

  return (
    <>
      <ListPage
        title="Ánh xạ dữ liệu mua"
        exportFileName={kind === 'item' ? 'anh-xa-ma-vat-tu' : 'anh-xa-ncc'}
        exportSubtitle={kind === 'item' ? 'Mã vật tư cũ' : 'NCC cũ'}
        canCreate={false}
        rows={rows}
        columns={columnsFor(kind)}
        rowKey={(s) => s.key}
        loading={sources.isLoading}
        onOpenRow={setOpened}
        notice={
          sources.error ? (
            <Alert type="error" showIcon title={sources.error.message} />
          ) : (
            <Alert
              type="info"
              showIcon
              style={{ marginBottom: 6 }}
              title={`${pending.length.toLocaleString('vi-VN')} / ${all.length.toLocaleString('vi-VN')} ${kind === 'item' ? 'mã vật tư cũ' : 'NCC cũ'} còn dòng chưa gán ${labels[kind].target} (thành tiền ${formatMoney(pending.reduce((sum, s) => sum + s.amount, 0))} đ). Mở một dòng để chọn – ánh xạ áp cho mọi dòng cùng mã và cho các lần nhập sau.`}
            />
          )
        }
        onClearFilters={() => setStatus('')}
        filters={
          <>
            <FilterField label="Loại" width={150}>
              {(id) => (
                <Select
                  id={id}
                  value={kind}
                  onChange={setKind}
                  options={[
                    { value: 'item', label: 'Mã vật tư' },
                    { value: 'supplier', label: 'Nhà cung cấp' },
                  ]}
                />
              )}
            </FilterField>
            <FilterField label="Trạng thái" width={150}>
              {(id) => (
                <Select
                  id={id}
                  value={status}
                  onChange={setStatus}
                  options={[
                    { value: '', label: 'Tất cả' },
                    { value: 'unmapped', label: 'Còn dòng chưa gán' },
                    { value: 'mapped', label: 'Đã gán hết' },
                  ]}
                />
              )}
            </FilterField>
          </>
        }
      />
      {opened && <MappingDrawer kind={kind} source={opened} canMap={canMap} onClose={() => setOpened(null)} />}
    </>
  )
}

/**
 * Ngăn kéo ánh xạ một mã cũ / NCC cũ: gợi ý tên giống nhất, ô tìm mã khác, Lưu (gán mọi dòng cùng khóa), Bỏ ánh xạ (quay về tự khớp).
 * @param kind Loại ánh xạ.
 * @param source Mã cũ / NCC cũ đang mở.
 * @param canMap Có quyền lưu ánh xạ (không có thì chỉ xem).
 * @param onClose Đóng ngăn kéo.
 */
function MappingDrawer({
  kind,
  source,
  canMap,
  onClose,
}: {
  kind: PurchaseHistoryMappingKind
  source: PurchaseHistorySource
  canMap: boolean
  onClose: () => void
}) {
  const { message, modal } = App.useApp()
  const [search, setSearch] = useState('')
  const deferredSearch = useDeferredValue(search.trim())
  const [targetId, setTargetId] = useState(source.targetId)
  const [error, setError] = useState<string | null>(null)
  const candidates = usePurchaseHistoryCandidates(kind, source.key, deferredSearch)
  const mapping = usePurchaseHistoryMapping()
  const l = labels[kind]
  const options = candidates.data ?? []

  const save = async () => {
    if (!targetId) {
      setError(`Chọn ${l.target} trước khi lưu.`)
      return
    }
    try {
      const r = await mapping.save.mutateAsync({ kind, key: source.key, targetId })
      message.success(`Đã ánh xạ, gán ${r.updatedRecords.toLocaleString('vi-VN')} dòng mua lịch sử.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được ánh xạ.')
    }
  }

  const unmap = () =>
    modal.confirm({
      title: 'Bỏ ánh xạ?',
      content: 'Các dòng của mã này quay về tự khớp như lúc nhập (mã trùng mã chung, MST trùng NCC); không khớp thì để trống. Thao tác ghi nhật ký.',
      okText: 'Bỏ ánh xạ',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        try {
          const r = await mapping.remove.mutateAsync({ kind, key: source.key })
          message.success(`Đã bỏ ánh xạ, gán lại ${r.updatedRecords.toLocaleString('vi-VN')} dòng.`)
          onClose()
        } catch (e) {
          setError(e instanceof Error ? e.message : 'Không bỏ được ánh xạ.')
        }
      },
    })

  return (
    <DetailDrawer
      open
      title={`Ánh xạ ${kind === 'item' ? 'mã vật tư' : 'NCC'}: ${source.code ?? source.name}`}
      onClose={onClose}
      onSave={canMap ? save : undefined}
      saving={mapping.save.isPending}
      readOnly={!canMap}
      error={error}
      extraActions={
        canMap && source.hasMapping ? (
          <Button danger onClick={unmap} loading={mapping.remove.isPending}>
            Bỏ ánh xạ
          </Button>
        ) : undefined
      }
      tabs={[
        {
          key: 'map',
          label: 'Ánh xạ',
          children: (
            <>
              <fieldset className="detail-section">
                <legend>{kind === 'item' ? 'Mã vật tư cũ của pháp nhân' : 'NCC cũ của pháp nhân'}</legend>
                <div>
                  <strong>{source.code ?? '(không có MST)'}</strong> – {source.name}
                </div>
                {source.detail && <div className="detail-hint">{source.detail}</div>}
                <div className="detail-hint">
                  {source.recordCount.toLocaleString('vi-VN')} dòng, đã gán {source.matchedCount.toLocaleString('vi-VN')}; thành tiền{' '}
                  {formatMoney(source.amount)} đ.{' '}
                  {source.targetCode && `Đang gán: ${source.targetCode} – ${source.targetName ?? ''} (${source.hasMapping ? 'ánh xạ' : 'tự khớp'}).`}
                </div>
              </fieldset>
              <fieldset className="detail-section">
                <legend>Chọn {l.target}</legend>
                <Input
                  allowClear
                  aria-label={`Tìm ${l.target}`}
                  placeholder="Gõ mã hoặc tên khác — có dấu hoặc không dấu đều được"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  disabled={!canMap}
                  style={{ marginBottom: 8 }}
                />
                <div className="detail-hint" style={{ marginBottom: 8 }}>
                  {deferredSearch
                    ? 'Kết quả khớp từ khóa, tên giống nhất trước.'
                    : 'Gợi ý: tên (và quy cách) giống nhất, so không dấu. Điểm chỉ để xếp hạng – kiểm tra lại trước khi lưu.'}
                </div>
                <Spin spinning={candidates.isFetching}>
                  {candidates.error ? (
                    <Alert type="error" showIcon title={candidates.error.message} />
                  ) : options.length === 0 ? (
                    <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Không có gợi ý – gõ từ khóa để tìm." />
                  ) : (
                    <Radio.Group value={targetId} onChange={(e) => setTargetId(e.target.value)} disabled={!canMap}>
                      <Flex vertical gap={4}>
                        {options.map((c) => (
                          <Radio key={c.id} value={c.id}>
                            <strong>{c.code}</strong> – {c.name}
                            {c.detail && <span className="detail-hint"> · {c.detail}</span>}
                            <span className="detail-hint"> · giống {c.score}%</span>
                          </Radio>
                        ))}
                      </Flex>
                    </Radio.Group>
                  )}
                </Spin>
              </fieldset>
            </>
          ),
        },
      ]}
    />
  )
}
