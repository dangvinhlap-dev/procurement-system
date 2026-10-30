import {
  DownloadOutlined,
  DownOutlined,
  FileExcelOutlined,
  PlusOutlined,
  SettingOutlined,
  UploadOutlined,
} from '@ant-design/icons'
import { App, Button, Checkbox, Dropdown, Empty, Input, Popover, Select, Tabs, Typography } from 'antd'
import { useId, useMemo, useState, type ReactNode } from 'react'
import { matchesKeyword } from '../../utils/text'
import { DataGrid } from '../DataGrid/DataGrid'
import { cellText, type GridColumn } from '../DataGrid/types'
import { apiDownload } from '../../api/client'
import { ExcelImportDialog } from './ExcelImportDialog'
import './ListPage.css'

/** Thuộc tính của ListPage. */
export interface ListPageProps<T> {
  /** Tiêu đề màn hình, ví dụ "Danh sách đơn mua hàng (PO)". */
  title: string
  /** Dòng đã qua bộ lọc nghiệp vụ của trang; ListPage lọc tiếp theo ô Tìm kiếm. */
  rows: T[]
  /** Tất cả các cột; người dùng ẩn/hiện bằng nút "Cột". */
  columns: GridColumn<T>[]
  rowKey: (row: T) => string
  /** Các ô lọc nghiệp vụ (dùng FilterField), nằm giữa ô Tìm kiếm và ô Nhóm theo. */
  filters?: ReactNode
  /** Xóa giá trị các ô lọc nghiệp vụ khi bấm "Xóa bộ lọc". */
  onClearFilters?: () => void
  /** Bấm "+ Tạo mới". */
  onCreate?: () => void
  /** false thì ẩn nút "Tạo mới" (người dùng không có quyền thêm). Mặc định true. */
  canCreate?: boolean
  /** Đang tải dữ liệu từ server. */
  loading?: boolean
  /** Thông báo hiện ngay trên lưới, ví dụ "kết quả bị cắt, hãy thu hẹp bộ lọc". */
  notice?: ReactNode
  /** Mở chi tiết dòng (bấm đúp hoặc Enter). */
  onOpenRow?: (row: T) => void
  isMarked?: (row: T) => boolean
  /** Tên file khi xuất CSV (không gồm đuôi). */
  exportFileName: string
  /**
   * Danh mục hỗ trợ nhập/xuất Excel ở server (design.md mục 5.10): catalog là mã trên API /api/excel/{catalog},
   * canImport = người dùng được nhập (có quyền quản lý, đúng công ty). Không truyền thì hai nút Excel chưa dùng được.
   */
  excel?: { catalog: string; title: string; canImport: boolean }
}

/**
 * Ô lọc có nhãn phía trên, dùng trong khung bộ lọc của ListPage.
 * @param label Nhãn hiển thị; gắn với ô nhập qua htmlFor/id để đọc màn hình và test tìm được.
 * @param width Độ rộng ô (px).
 * @param children Hàm nhận id để gắn vào ô nhập.
 */
export function FilterField({ label, width, children }: { label: string; width: number; children: (id: string) => ReactNode }) {
  const id = useId()
  return (
    <div className="list-filter-field" style={{ width }}>
      <label htmlFor={id}>{label}</label>
      {children(id)}
    </div>
  )
}

/**
 * Khuôn chung của màn hình danh sách (ui-guide.md mục "Mẫu màn hình danh sách", design.md mục 17.2):
 * tiêu đề + mẫu hiển thị + tổng bản ghi; hàng nút lệnh; tab kiểu Windows; khung bộ lọc
 * (Tìm kiếm có/không dấu, ô lọc nghiệp vụ, Nhóm theo, Cột, Xóa bộ lọc); vùng nhóm và DataGrid.
 */
export function ListPage<T>({
  title,
  rows,
  columns,
  rowKey,
  filters,
  onClearFilters,
  onCreate,
  canCreate = true,
  loading = false,
  notice,
  onOpenRow,
  isMarked,
  exportFileName,
  excel,
}: ListPageProps<T>) {
  const { message } = App.useApp()
  const [importing, setImporting] = useState(false)
  const [exporting, setExporting] = useState(false)

  /** Xuất cả danh mục (trong phạm vi người dùng) ra Excel ở server – cùng định dạng file nhập để sửa rồi nhập lại. */
  const exportExcel = async () => {
    if (!excel) return
    setExporting(true)
    try {
      await apiDownload(`/api/excel/${excel.catalog}`, `${exportFileName}.xlsx`)
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không xuất được Excel.')
    } finally {
      setExporting(false)
    }
  }
  const [keyword, setKeyword] = useState('')
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const [hidden, setHidden] = useState<Set<string>>(new Set())
  const [tab, setTab] = useState('list')

  const visibleColumns = useMemo(() => columns.filter((c) => !hidden.has(c.key)), [columns, hidden])

  // Tìm trên chuỗi hiển thị của mọi cột (kể cả cột đang ẩn), bỏ qua dấu – CLAUDE.md mục 5.
  const filteredRows = useMemo(
    () => rows.filter((row) => matchesKeyword(columns.map((c) => cellText(c, row)).join(' | '), keyword)),
    [rows, columns, keyword],
  )

  const exportCsv = () => {
    const escape = (v: string) => (/[",\n]/.test(v) ? `"${v.replaceAll('"', '""')}"` : v)
    const lines = [
      visibleColumns.map((c) => escape(c.title)).join(','),
      ...filteredRows.map((row) =>
        visibleColumns
          // Cột số xuất số thô để Excel tính toán được; cột khác xuất đúng chuỗi đang hiển thị.
          .map((c) => escape(c.type === 'money' || c.type === 'quantity' ? String(c.value(row) ?? '') : cellText(c, row)))
          .join(','),
      ),
    ]
    // BOM UTF-8 để Excel đọc đúng tiếng Việt có dấu.
    const blob = new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = Object.assign(document.createElement('a'), { href: url, download: `${exportFileName}.csv` })
    a.click()
    URL.revokeObjectURL(url)
  }

  const notYet = () => message.info('Chức năng này sẽ làm ở giai đoạn sau.')
  const groupableColumns = columns.filter((c) => c.groupable)
  const hiddenCount = hidden.size

  const columnChooser = (
    <div className="list-column-chooser">
      {columns.map((c) => (
        <Checkbox
          key={c.key}
          checked={!hidden.has(c.key)}
          // Giữ lại ít nhất một cột để lưới không trống.
          disabled={!hidden.has(c.key) && visibleColumns.length === 1}
          onChange={(e) =>
            setHidden((prev) => {
              const next = new Set(prev)
              if (e.target.checked) next.delete(c.key)
              else next.add(c.key)
              return next
            })
          }
        >
          {c.title}
        </Checkbox>
      ))}
    </div>
  )

  return (
    <div className="list-page">
      <div className="list-page-header">
        <Typography.Title level={4} className="list-page-title">
          {title}
        </Typography.Title>
        <Dropdown menu={{ items: [{ key: 'standard', label: 'Chuẩn' }], selectable: true, defaultSelectedKeys: ['standard'] }}>
          <Button type="text" className="list-page-template">
            Chuẩn <DownOutlined />
          </Button>
        </Dropdown>
      </div>
      <Typography.Text type="secondary" className="list-page-total">
        Tổng {filteredRows.length.toLocaleString('vi-VN')} bản ghi
      </Typography.Text>

      <div className="list-page-actions">
        {canCreate && (
          <Button type="primary" icon={<PlusOutlined />} onClick={onCreate ?? notYet}>
            Tạo mới
          </Button>
        )}
        <Button type="text" icon={<DownloadOutlined />} onClick={exportCsv}>
          Xuất CSV
        </Button>
        <Button type="text" icon={<FileExcelOutlined />} onClick={excel ? exportExcel : notYet} loading={exporting}>
          Xuất Excel
        </Button>
        {(!excel || excel.canImport) && (
          <Button type="text" icon={<UploadOutlined />} onClick={excel ? () => setImporting(true) : notYet}>
            Nhập Excel
          </Button>
        )}
      </div>

      <Tabs
        type="card"
        size="small"
        className="list-page-tabs"
        activeKey={tab}
        onChange={setTab}
        items={[
          { key: 'list', label: 'Danh sách' },
          { key: 'summary', label: 'Tổng hợp' },
          { key: 'analysis', label: 'Phân tích' },
        ]}
      />

      <div className="list-page-panel">
        {tab === 'list' ? (
          <>
            <div className="list-filter-bar">
              <FilterField label="Tìm kiếm" width={290}>
                {(id) => (
                  <Input
                    id={id}
                    data-list-search
                    allowClear
                    placeholder="Gõ từ khóa — có dấu hoặc không dấu đều được"
                    value={keyword}
                    onChange={(e) => setKeyword(e.target.value)}
                  />
                )}
              </FilterField>
              {filters}
              <FilterField label="Nhóm theo" width={140}>
                {(id) => (
                  <Select
                    id={id}
                    value={groupBy ?? ''}
                    onChange={(v) => setGroupBy(v || null)}
                    options={[
                      { value: '', label: 'Không nhóm' },
                      ...groupableColumns.map((c) => ({ value: c.key, label: c.title })),
                    ]}
                  />
                )}
              </FilterField>
              <Popover trigger="click" placement="bottomRight" content={columnChooser} title="Chọn cột hiển thị">
                <Button icon={<SettingOutlined />}>
                  Cột
                  {hiddenCount > 0 && (
                    <span className="list-hidden-count" data-testid="hidden-column-count">
                      {hiddenCount}
                    </span>
                  )}
                </Button>
              </Popover>
              <Button
                onClick={() => {
                  setKeyword('')
                  onClearFilters?.()
                }}
              >
                Xóa bộ lọc
              </Button>
            </div>
            {notice}
            <DataGrid
              rows={filteredRows}
              columns={visibleColumns}
              allColumns={columns}
              rowKey={rowKey}
              groupBy={groupBy}
              onGroupByChange={setGroupBy}
              onOpenRow={onOpenRow}
              isMarked={isMarked}
              loading={loading}
            />
          </>
        ) : (
          <Empty className="list-page-empty" description="Chưa có nội dung – sẽ làm ở giai đoạn báo cáo." />
        )}
      </div>
      {importing && excel && <ExcelImportDialog catalog={excel.catalog} title={excel.title} onClose={() => setImporting(false)} />}
    </div>
  )
}
