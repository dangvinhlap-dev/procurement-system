import {
  DownloadOutlined,
  DownOutlined,
  FileExcelOutlined,
  PlusOutlined,
  SettingOutlined,
  UpOutlined,
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
  /** Tên file khi xuất CSV/Excel (không gồm đuôi; chữ thường không dấu, số, gạch ngang). */
  exportFileName: string
  /** Dòng điều kiện lọc ghi trên file Excel xuất ra (kỳ, kho, số ngày…). */
  exportSubtitle?: string
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
 * Khuôn chung của màn hình danh sách theo Dynamic Page của Fiori (ui-guide.md mục 4, 5; design.md mục 17.2):
 * phần đầu trang (tiêu đề + mẫu hiển thị, nút lệnh bên phải, tổng bản ghi, khu bộ lọc – Tìm kiếm có/không dấu, ô lọc nghiệp vụ,
 * Nhóm theo, Cột, Xóa bộ lọc – và thanh tab) thu gọn được; phần nội dung là thông báo, vùng nhóm và DataGrid trên khối trắng.
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
  exportSubtitle,
  excel,
}: ListPageProps<T>) {
  const { message } = App.useApp()
  const [importing, setImporting] = useState(false)
  const [exporting, setExporting] = useState(false)

  const [keyword, setKeyword] = useState('')
  const [groupBy, setGroupBy] = useState<string | null>(null)
  const [hidden, setHidden] = useState<Set<string>>(new Set())
  const [tab, setTab] = useState('list')
  // Phần đầu trang thu gọn: ẩn khu bộ lọc, bảng được nhiều chỗ hơn (Dynamic Page của Fiori).
  const [headerCollapsed, setHeaderCollapsed] = useState(false)

  const visibleColumns = useMemo(() => columns.filter((c) => !hidden.has(c.key)), [columns, hidden])

  // Tìm trên chuỗi hiển thị của mọi cột (kể cả cột đang ẩn), bỏ qua dấu – CLAUDE.md mục 5.
  const filteredRows = useMemo(
    () => rows.filter((row) => matchesKeyword(columns.map((c) => cellText(c, row)).join(' | '), keyword)),
    [rows, columns, keyword],
  )

  /**
   * Danh mục: xuất cả danh mục (trong phạm vi người dùng) ở server – cùng định dạng file nhập để sửa rồi nhập lại.
   * Danh sách, báo cáo khác: gửi đúng các cột đang hiện và các dòng đã lọc lên server dựng file .xlsx (design.md mục 11.2) –
   * số, ngày gửi giá trị thô để Excel giữ kiểu số, kiểu ngày.
   */
  const exportExcel = async () => {
    setExporting(true)
    try {
      if (excel) {
        await apiDownload(`/api/excel/${excel.catalog}`, `${exportFileName}.xlsx`)
      } else {
        await apiDownload('/api/excel/grid', `${exportFileName}.xlsx`, {
          title,
          subtitle: exportSubtitle ?? null,
          fileName: exportFileName,
          columns: visibleColumns.map((c) => ({ title: c.title, type: c.type ?? 'text', width: c.width })),
          rows: filteredRows.map((row) =>
            visibleColumns.map((c) => {
              // Cột chữ, trạng thái gửi đúng chuỗi đang hiển thị; cột số, ngày gửi giá trị thô.
              if (c.type === 'money' || c.type === 'quantity' || c.type === 'date' || c.type === 'datetime') {
                const v = c.value(row)
                return v == null ? null : String(v)
              }
              return cellText(c, row) || null
            }),
          ),
        })
      }
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không xuất được Excel.')
    } finally {
      setExporting(false)
    }
  }
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
      {/* Dynamic Page (ui-guide.md mục 4, 5): phần đầu trang gồm tiêu đề, nút lệnh, khu bộ lọc – thu gọn được để dành chỗ cho bảng. */}
      <div className={`dyn-header${headerCollapsed ? ' is-collapsed' : ''}`}>
        <div className="dyn-title-row">
          <div className="dyn-title">
            <Typography.Title level={4} className="list-page-title">
              {title}
            </Typography.Title>
            <Dropdown menu={{ items: [{ key: 'standard', label: 'Chuẩn' }], selectable: true, defaultSelectedKeys: ['standard'] }}>
              <Button type="text" className="list-page-template">
                Chuẩn <DownOutlined />
              </Button>
            </Dropdown>
          </div>
          <div className="list-page-actions">
            {canCreate && (
              <Button type="primary" icon={<PlusOutlined />} onClick={onCreate ?? notYet}>
                Tạo mới
              </Button>
            )}
            <Button type="text" icon={<DownloadOutlined />} onClick={exportCsv}>
              Xuất CSV
            </Button>
            <Button type="text" icon={<FileExcelOutlined />} onClick={exportExcel} loading={exporting}>
              Xuất Excel
            </Button>
            {(!excel || excel.canImport) && (
              <Button type="text" icon={<UploadOutlined />} onClick={excel ? () => setImporting(true) : notYet}>
                Nhập Excel
              </Button>
            )}
          </div>
        </div>
        <Typography.Text type="secondary" className="list-page-total">
          Tổng {filteredRows.length.toLocaleString('vi-VN')} bản ghi
        </Typography.Text>
        {!headerCollapsed && tab === 'list' && (
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
        )}
        <Tabs
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
        <Button
          size="small"
          shape="circle"
          className="dyn-header-toggle"
          icon={headerCollapsed ? <DownOutlined /> : <UpOutlined />}
          aria-label={headerCollapsed ? 'Mở rộng phần đầu trang' : 'Thu gọn phần đầu trang'}
          title={headerCollapsed ? 'Mở rộng phần đầu trang (hiện bộ lọc)' : 'Thu gọn phần đầu trang (ẩn bộ lọc)'}
          onClick={() => setHeaderCollapsed((v) => !v)}
        />
      </div>

      <div className="list-page-panel">
        {tab === 'list' ? (
          <>
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
