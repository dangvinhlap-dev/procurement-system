import { Alert, Descriptions, Modal, Spin, Table, Tabs } from 'antd'
import { useFieldHistory, type FieldChange, type FieldInfo } from '../../api/changeHistory'
import { changeColumns } from './historyColumns'
import { RecordStampLine, ValueCell } from './HistoryParts'
import type { FieldTarget } from './auditDom'
import './FieldHelp.css'

const kindLabels: Record<FieldInfo['kind'], string> = {
  text: 'Chuỗi ký tự',
  number: 'Số',
  date: 'Ngày',
  datetime: 'Ngày giờ',
  bool: 'Có/Không',
  enum: 'Giá trị liệt kê',
  reference: 'Tham chiếu',
}

/**
 * Hộp thoại "Trợ giúp ô" mở bằng F1 (design.md mục 9.3a, như trợ giúp ô của ERP): tab LỊCH SỬ THAY ĐỔI (mở sẵn) liệt kê ai tạo, sửa,
 * xóa giá trị của ô, lúc nào, giá trị cũ → mới; tab THÔNG TIN Ô ghi bảng, cột, kiểu dữ liệu như "Thông tin kỹ thuật".
 * @param props.target Ô đang xem; null là đóng.
 * @param props.onClose Đóng hộp thoại (Esc, nút Đóng).
 */
export function FieldHelpDialog({ target, onClose }: { target: FieldTarget | null; onClose: () => void }) {
  const ref = target?.entityId ? { entityType: target.entityType, entityId: target.entityId, field: target.field } : null
  const history = useFieldHistory(ref)
  const data = history.data
  const info = data?.info

  const historyTab = !target?.entityId ? (
    <Alert type="info" showIcon title="Bản ghi chưa lưu nên chưa có lịch sử thay đổi." />
  ) : history.error ? (
    <Alert type="error" showIcon title={history.error.message} />
  ) : (
    <Spin spinning={history.isLoading}>
      {data && <RecordStampLine record={data.record} />}
      {info && info.field === null && (
        <Alert
          type="info"
          showIcon
          className="field-help-alert"
          title="Ô này không lưu trực tiếp vào bản ghi (hệ thống tính hoặc ghép từ ô khác) – xem lịch sử ở ô gốc hoặc nút Lịch sử thay đổi của phiếu."
        />
      )}
      {info?.redacted && <Alert type="warning" showIcon className="field-help-alert" title="Ô bảo mật: nhật ký chỉ ghi là đã đổi, không ghi giá trị." />}
      {data && info?.field && data.changes.length === 0 && (
        <Alert
          type="info"
          showIcon
          className="field-help-alert"
          title="Chưa có lần thay đổi nào được ghi cho ô này (dữ liệu tạo trước khi có nhật ký hoặc ô chưa từng có giá trị)."
        />
      )}
      {data && data.changes.length > 0 && (
        <Table<FieldChange>
          size="small"
          bordered
          pagination={false}
          rowKey={(c) => c.logId}
          dataSource={data.changes}
          columns={changeColumns}
          scroll={{ y: 320 }}
          aria-label="Lịch sử thay đổi của ô"
        />
      )}
      {data?.truncated && <Alert type="warning" showIcon className="field-help-alert" title="Chỉ hiện các lần thay đổi gần nhất." />}
    </Spin>
  )

  const infoTab = (
    <Descriptions size="small" bordered column={1} className="field-help-info">
      <Descriptions.Item label="Nhãn ô">{target?.label}</Descriptions.Item>
      <Descriptions.Item label="Giá trị hiện tại">
        <ValueCell text={target?.currentText ?? ''} />
      </Descriptions.Item>
      <Descriptions.Item label="Đối tượng">
        {info?.entityName ?? target?.entityType} {data?.record.label ? `– ${data.record.label}` : ''}
      </Descriptions.Item>
      <Descriptions.Item label="Tên kỹ thuật">{info ? `${info.entityType}.${info.field ?? target?.field}` : target?.field}</Descriptions.Item>
      <Descriptions.Item label="Bảng / cột CSDL">{info?.table && info.column ? `${info.table}.${info.column}` : '—'}</Descriptions.Item>
      <Descriptions.Item label="Kiểu dữ liệu">
        {info?.field ? `${kindLabels[info.kind]}${info.dataType ? ` (${info.dataType})` : ''}` : '—'}
      </Descriptions.Item>
      {info?.maxLength != null && <Descriptions.Item label="Độ dài tối đa">{info.maxLength} ký tự</Descriptions.Item>}
      {info?.field && <Descriptions.Item label="Bắt buộc">{info.required ? 'Có' : 'Không'}</Descriptions.Item>}
      {info?.referenceName && <Descriptions.Item label="Tham chiếu tới">{info.referenceName}</Descriptions.Item>}
    </Descriptions>
  )

  return (
    <Modal
      open={target !== null}
      title={`Trợ giúp ô: ${target?.label ?? ''}`}
      onCancel={onClose}
      footer={null}
      width={900}
      destroyOnHidden
      className="field-help-dialog"
    >
      <Tabs
        size="small"
        defaultActiveKey="history"
        items={[
          { key: 'history', label: 'Lịch sử thay đổi', children: historyTab },
          { key: 'info', label: 'Thông tin ô', children: infoTab },
        ]}
      />
    </Modal>
  )
}
