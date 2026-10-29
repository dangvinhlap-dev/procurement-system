import { App, Button, Descriptions, Form, Input } from 'antd'
import dayjs from 'dayjs'
import { useState } from 'react'
import { useDocumentNumbering, useUpdateDocumentNumbering, type DocumentNumbering } from '../../api/documentNumbering'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import type { GridColumn } from '../../components/DataGrid/types'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { ListPage } from '../../components/ListPage/ListPage'

const columns: GridColumn<DocumentNumbering>[] = [
  { key: 'docType', title: 'Loại', width: 70, value: (r) => r.docType },
  { key: 'name', title: 'Chứng từ', width: 200, value: (r) => r.name },
  { key: 'format', title: 'Mẫu số', width: 210, value: (r) => r.format },
  {
    key: 'kind',
    title: 'Cấu hình',
    width: 100,
    type: 'status',
    value: (r) => (r.isDefault ? 'Mặc định' : 'Riêng'),
    tone: (r) => (r.isDefault ? 'neutral' : 'success'),
    groupable: true,
  },
  { key: 'last', title: 'Số gần nhất kỳ này', width: 170, value: (r) => r.lastNumber },
  { key: 'next', title: 'Số kế tiếp', width: 170, value: (r) => r.nextNumber },
]

/**
 * Xem trước số theo mẫu ngay khi gõ (số thứ tự = 1, ngày hôm nay). Chỉ để người dùng hình dung;
 * kiểm tra mẫu hợp lệ và sinh số thật do server làm (DocumentNumberFormat – design.md mục 5.5).
 */
function previewNumber(pattern: string, companyCode: string): string {
  const today = dayjs()
  return pattern.trim().toUpperCase().replace(/\{([^{}]*)\}/g, (token, name: string) => {
    if (/^0{3,10}$/.test(name)) return '1'.padStart(name.length, '0')
    const values: Record<string, string> = {
      CTY: companyCode,
      YYYY: today.format('YYYY'),
      YY: today.format('YY'),
      MM: today.format('MM'),
      YYMM: today.format('YYMM'),
    }
    return values[name] ?? token
  })
}

/**
 * Màn hình Đánh số chứng từ (design.md mục 5.4, 5.5): mẫu số từng loại chứng từ tại công ty đang làm việc,
 * số gần nhất và số kế tiếp của kỳ hiện tại. Bấm đúp để sửa mẫu.
 */
export function DocumentNumberingPage() {
  const { workingCompany } = useAuth()
  const numbering = useDocumentNumbering()
  const [openType, setOpenType] = useState<string | null>(null)
  const open = numbering.data?.find((r) => r.docType === openType)

  return (
    <>
      <ListPage
        title={`Đánh số chứng từ – ${workingCompany?.code ?? ''}`}
        exportFileName="danh-so-chung-tu"
        rows={numbering.data ?? []}
        columns={columns}
        rowKey={(r) => r.docType}
        loading={numbering.isLoading}
        canCreate={false}
        onOpenRow={(r) => setOpenType(r.docType)}
      />
      {open && <NumberingDrawer row={open} onClose={() => setOpenType(null)} />}
    </>
  )
}

/** Ngăn kéo sửa mẫu số của một loại chứng từ. */
function NumberingDrawer({ row, onClose }: { row: DocumentNumbering; onClose: () => void }) {
  const { message } = App.useApp()
  const { workingCompany, hasPermission } = useAuth()
  const [form] = Form.useForm<{ format: string }>()
  const format = Form.useWatch('format', form) ?? row.format
  const update = useUpdateDocumentNumbering()
  const [error, setError] = useState<string | null>(null)
  const canEdit = hasPermission(Permission.ParameterManage)
  const defaultFormat = `${row.docType}-{CTY}-{YYMM}-{0000}`

  const save = async (value: string | null) => {
    setError(null)
    try {
      const saved = await update.mutateAsync({ docType: row.docType, format: value })
      message.success(`Đã lưu mẫu số ${row.docType}: ${saved.format}.`)
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được mẫu số.')
    }
  }

  return (
    <DetailDrawer
      open
      title={`Đánh số: ${row.docType} – ${row.name}`}
      onClose={onClose}
      onSave={canEdit ? () => void form.validateFields().then((v) => save(v.format)) : undefined}
      saving={update.isPending}
      error={error}
      form={form}
      initialValues={{ format: row.format }}
      readOnly={!canEdit}
      extraActions={
        canEdit &&
        !row.isDefault && (
          <Button type="text" onClick={() => void save(null)}>
            Về mẫu mặc định
          </Button>
        )
      }
      tabs={[
        {
          key: 'format',
          label: 'Mẫu số',
          children: (
            <>
              <Form.Item
                label="Mẫu số"
                name="format"
                rules={[{ required: true, whitespace: true, message: 'Hãy nhập mẫu số, hoặc bấm "Về mẫu mặc định".' }]}
                extra={`Mẫu mặc định: ${defaultFormat}`}
              >
                <Input autoFocus maxLength={40} style={{ fontFamily: 'var(--ant-font-family-code)' }} />
              </Form.Item>
              <Descriptions
                size="small"
                bordered
                column={1}
                items={[
                  { key: 'preview', label: 'Xem trước (số 1, hôm nay)', children: <strong>{previewNumber(format, workingCompany?.code ?? '')}</strong> },
                  { key: 'last', label: 'Số gần nhất kỳ này', children: row.lastNumber ?? '(chưa cấp)' },
                  { key: 'next', label: 'Số kế tiếp (mẫu đang dùng)', children: row.nextNumber },
                ]}
              />
              <fieldset className="detail-section" style={{ marginTop: 10 }}>
                <legend>Các mã dùng trong mẫu</legend>
                <div className="detail-hint">
                  {'{CTY}'} mã công ty · {'{YYYY}'} năm 4 số · {'{YY}'} năm 2 số · {'{MM}'} tháng · {'{YYMM}'} năm + tháng ·{' '}
                  {'{0000}'} số thứ tự (3–10 chữ số 0). Chữ cố định chỉ dùng A–Z, 0–9 và - / . _
                  <br />
                  Mẫu có tháng thì mỗi tháng đếm lại từ 1, chỉ có năm thì mỗi năm, không có ngày thì đếm liên tục. Năm, tháng lấy
                  theo ngày chứng từ. Đổi mẫu không đổi các số đã cấp.
                </div>
              </fieldset>
            </>
          ),
        },
      ]}
    />
  )
}
