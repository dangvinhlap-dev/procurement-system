import { CloseCircleOutlined, PaperClipOutlined, PlusCircleOutlined } from '@ant-design/icons'
import { Alert, App, Button, ConfigProvider, Flex, Form, Input, Modal, Radio, Table, Typography } from 'antd'
import { useState } from 'react'
import {
  appraisalAspectLabels,
  appraisalResultLabels,
  useAppraisalMutations,
  useAppraisals,
  type AppraisalAspect,
  type AppraisalOpinion,
  type AppraisalOpinionList,
  type AppraisalOwnerType,
  type AppraisalResult,
} from '../../api/appraisals'
import { Select } from '../../classic/Select'
import { StatusLight } from '../../classic/ClassicScreen'
import { formatDateTime } from '../../utils/format'
import { AttachmentPanel } from '../AttachmentPanel/AttachmentPanel'
import { useAttachmentTab } from '../AttachmentPanel/useAttachmentTab'
import { renderRequiredMark } from '../RequiredMark/requiredMark'

/** Độ dài nội dung tối đa – khớp AppraisalOpinion.ContentMaxLength. */
const CONTENT_MAX = 2000

const aspectOptions = (Object.keys(appraisalAspectLabels) as AppraisalAspect[]).map((value) => ({ value, label: appraisalAspectLabels[value] }))

/** Đèn + chữ kết luận (màu không là thông tin duy nhất – docs/ui-style.md mục 2). */
function ResultLight({ result }: { result: AppraisalResult }) {
  const label = appraisalResultLabels[result]
  return (
    <Flex align="center" gap={4}>
      <StatusLight level={result === 'Pass' ? 'success' : 'error'} label={label} />
      {label}
    </Flex>
  )
}

interface GiveValues {
  aspect: AppraisalAspect
  result: AppraisalResult
  content: string
}

/**
 * Hộp thoại ghi ý kiến: khía cạnh, kết luận Đạt/Không đạt, nội dung (bắt buộc – đóng vai Ghi chú) và Đính kèm (file chờ, ghi xong tự tải lên theo
 * Id ý kiến – CLAUDE.md mục 4). Chỉ dựng khi mở để hàng chờ đính kèm làm mới mỗi lần.
 */
function GiveDialog({ ownerType, ownerId, onClose }: { ownerType: AppraisalOwnerType; ownerId: string; onClose: () => void }) {
  const { message } = App.useApp()
  const { give } = useAppraisalMutations()
  const attachments = useAttachmentTab('AppraisalOpinion', undefined, true)
  const [form] = Form.useForm<GiveValues>()

  const save = async () => {
    const values = await form.validateFields()
    try {
      const opinion = await give.mutateAsync({ ownerType, ownerId, aspect: values.aspect, result: values.result, content: values.content.trim() })
      await attachments.uploadTo(opinion.id)
      message.success('Đã ghi ý kiến thẩm định.')
      onClose()
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Không ghi được ý kiến thẩm định.')
    }
  }

  return (
    <Modal open width={640} title="Ghi ý kiến thẩm định" okText="Ghi ý kiến" cancelText="Đóng" onOk={save}
      confirmLoading={give.isPending} onCancel={onClose}>
      {/* Form riêng, disabled={false}: mở từ phiếu chỉ đọc (Chờ duyệt) – DisabledContext truyền qua portal; name riêng để id ô không trùng
          ô của phiếu phía sau. */}
      <Form form={form} name="appraisal-give" layout="vertical" disabled={false} initialValues={{ aspect: 'Quality' }}>
        <Typography.Paragraph type="secondary">
          Ý kiến để Hội đồng tham khảo trước khi duyệt – không phải cấp duyệt, không chặn phiếu. Ghi nhầm thì rút lại (có lý do) rồi ghi ý kiến mới.
        </Typography.Paragraph>
        <Form.Item label="Khía cạnh" name="aspect" rules={[{ required: true, message: 'Chọn khía cạnh.' }]}>
          <Select options={aspectOptions} aria-label="Khía cạnh thẩm định" />
        </Form.Item>
        <Form.Item label="Kết luận" name="result" rules={[{ required: true, message: 'Chọn Đạt hoặc Không đạt.' }]}>
          <Radio.Group options={[{ value: 'Pass', label: 'Đạt' }, { value: 'Fail', label: 'Không đạt' }]} />
        </Form.Item>
        <Form.Item label="Nội dung ý kiến" name="content"
          rules={[{ required: true, whitespace: true, message: 'Nhập nội dung ý kiến.' }, { max: CONTENT_MAX }]}>
          <Input.TextArea rows={4} maxLength={CONTENT_MAX} showCount placeholder="Căn cứ, điều kiện, đề nghị bổ sung…" />
        </Form.Item>
      </Form>
      <Typography.Text strong>{attachments.label}</Typography.Text>
      {attachments.children}
    </Modal>
  )
}

/** Thuộc tính của AppraisalPanel. */
export interface AppraisalPanelProps {
  /** Loại đối tượng. */
  ownerType: AppraisalOwnerType
  /** Id đối tượng đã lưu. */
  ownerId: string
}

/**
 * Tab THẨM ĐỊNH (design.md mục 18.25 – lát H1): ý kiến Đạt/Không đạt của thành viên chuyên môn trên GTH, RFQ, hợp đồng, hồ sơ NCC. Tổng hợp
 * số Đạt/Không đạt còn hiệu lực; người có quyền ghi thêm ý kiến khi đối tượng chưa có quyết định cuối cùng; người ghi rút lại (bắt buộc lý do)
 * và đính kèm bằng chứng cho ý kiến của mình. Máy chủ kiểm tra lại mọi quy tắc.
 * @param props Xem {@link AppraisalPanelProps}.
 */
export function AppraisalPanel({ ownerType, ownerId }: AppraisalPanelProps) {
  const { message, modal } = App.useApp()
  const { data, isLoading, error } = useAppraisals(ownerType, ownerId)
  const { withdraw } = useAppraisalMutations()
  const [giving, setGiving] = useState(false)
  const [filesOf, setFilesOf] = useState<AppraisalOpinion | null>(null)

  const confirmWithdraw = (opinion: AppraisalOpinion) => {
    let reason = ''
    modal.confirm({
      title: 'Rút ý kiến thẩm định?',
      content: (
        <Flex vertical gap={6}>
          <Typography.Text type="secondary">Ý kiến đã rút vẫn hiện để tra cứu nhưng không tính vào tổng hợp.</Typography.Text>
          <label htmlFor="appraisal-withdraw-reason">{renderRequiredMark('Lý do rút', { required: true })}</label>
          <Input.TextArea id="appraisal-withdraw-reason" rows={3} maxLength={500} onChange={(e) => (reason = e.target.value)} />
        </Flex>
      ),
      okText: 'Rút ý kiến',
      okButtonProps: { danger: true },
      cancelText: 'Không',
      onOk: async () => {
        if (!reason.trim()) {
          message.error('Hãy nhập lý do rút.')
          throw new Error('missing reason')
        }
        try {
          await withdraw.mutateAsync({ id: opinion.id, reason: reason.trim() })
          message.success('Đã rút ý kiến thẩm định.')
        } catch (e) {
          message.error(e instanceof Error ? e.message : 'Không rút được ý kiến.')
        }
      },
    })
  }

  return (
    // Phiếu chỉ xem (Chờ duyệt) khóa cả Form của DocumentForm; ghi ý kiến vẫn được phép nên bỏ khóa kế thừa ở khối này.
    <ConfigProvider componentDisabled={false}>
      <Flex vertical gap={8}>
        <Flex gap={8} align="center" wrap>
          <AppraisalCounts passCount={data?.passCount ?? 0} failCount={data?.failCount ?? 0} />
          <Button icon={<PlusCircleOutlined />} disabled={!data?.canGive} onClick={() => setGiving(true)}
            title={data?.giveBlockedReason ?? 'Ghi ý kiến thẩm định'}>
            Ghi ý kiến thẩm định
          </Button>
          {data && !data.canGive && data.giveBlockedReason && <Typography.Text type="secondary">{data.giveBlockedReason}</Typography.Text>}
        </Flex>
        {error && <Alert type="error" showIcon title={error.message} />}
        <Table<AppraisalOpinion>
          size="small"
          pagination={false}
          rowKey="id"
          loading={isLoading}
          dataSource={data?.opinions ?? []}
          tableLayout="fixed"
          scroll={{ x: 1000 }}
          aria-label="Ý kiến thẩm định"
          locale={{ emptyText: 'Chưa có ý kiến thẩm định.' }}
          rowClassName={(r) => (r.isWithdrawn ? 'is-cancelled' : '')}
          columns={[
            { title: 'Thời điểm', dataIndex: 'createdAt', width: 115, render: (v: string) => formatDateTime(v) },
            { title: 'Người ghi', dataIndex: 'authorName', width: 150, ellipsis: { showTitle: true } },
            { title: 'Vai trò', dataIndex: 'authorRoles', width: 150, ellipsis: { showTitle: true } },
            { title: 'Khía cạnh', dataIndex: 'aspect', width: 80, render: (v: AppraisalAspect) => appraisalAspectLabels[v] },
            { title: 'Kết luận', dataIndex: 'result', width: 95, render: (v: AppraisalResult) => <ResultLight result={v} /> },
            // Nội dung không đặt độ rộng: chiếm phần còn lại để cột lệnh luôn nằm trong khung.
            { title: 'Nội dung', dataIndex: 'content', ellipsis: { showTitle: true } },
            {
              title: 'Tình trạng', key: 'state', width: 160, ellipsis: { showTitle: true },
              render: (_, r) => (r.isWithdrawn ? `Đã rút ${r.withdrawnAt ? formatDateTime(r.withdrawnAt) : ''}: ${r.withdrawReason ?? ''}` : 'Hiệu lực'),
            },
            {
              title: '', key: 'actions', width: 150,
              render: (_: unknown, r: AppraisalOpinion) => (
                <Flex gap={4}>
                  <Button size="small" icon={<PaperClipOutlined />} onClick={() => setFilesOf(r)} aria-label="Đính kèm của ý kiến">
                    Đính kèm
                  </Button>
                  {r.canWithdraw && (
                    <Button size="small" danger icon={<CloseCircleOutlined />} onClick={() => confirmWithdraw(r)}>
                      Rút
                    </Button>
                  )}
                </Flex>
              ),
            },
          ]}
        />
        {giving && <GiveDialog ownerType={ownerType} ownerId={ownerId} onClose={() => setGiving(false)} />}
        <Modal open={!!filesOf} width={720} title={filesOf ? `Đính kèm – ý kiến của ${filesOf.authorName ?? ''}` : ''} footer={null}
          onCancel={() => setFilesOf(null)} destroyOnHidden>
          {filesOf && <AttachmentPanel ownerType="AppraisalOpinion" ownerId={filesOf.id} canManage={filesOf.canWithdraw} />}
        </Modal>
      </Flex>
    </ConfigProvider>
  )
}

/**
 * Tổng hợp số ý kiến Đạt/Không đạt còn hiệu lực (đèn + chữ).
 * @param passCount Số Đạt.
 * @param failCount Số Không đạt.
 */
export function AppraisalCounts({ passCount, failCount }: { passCount: number; failCount: number }) {
  return (
    <Flex gap={12} align="center" aria-label="Tổng hợp ý kiến thẩm định">
      <Flex gap={4} align="center"><StatusLight level="success" label="Đạt" />Đạt: {passCount}</Flex>
      <Flex gap={4} align="center"><StatusLight level="error" label="Không đạt" />Không đạt: {failCount}</Flex>
    </Flex>
  )
}

/**
 * Tóm tắt ý kiến thẩm định đặt trong hộp thoại duyệt – người duyệt thấy trước khi quyết (lát H1). Có ý kiến Không đạt thì cảnh báo; chưa có
 * ý kiến nào thì chỉ ghi chú, vì thẩm định không bắt buộc và không chặn duyệt. Nhận dữ liệu trang đã tải (useAppraisals) chứ không tự gọi
 * query: hộp thoại modal.confirm dựng ở ngoài QueryClientProvider (main.tsx).
 * @param data Ý kiến của đối tượng; chưa tải xong thì không hiện gì.
 */
export function AppraisalSummary({ data }: { data: AppraisalOpinionList | undefined }) {
  if (!data) return null
  const total = data.passCount + data.failCount
  if (total === 0) return <Typography.Paragraph type="secondary">Chưa có ý kiến thẩm định của thành viên chuyên môn.</Typography.Paragraph>
  return (
    <Alert
      type={data.failCount > 0 ? 'warning' : 'info'}
      showIcon
      title={`Ý kiến thẩm định: ${data.passCount} Đạt, ${data.failCount} Không đạt`}
      description={data.opinions
        .filter((o) => !o.isWithdrawn)
        .map((o) => `${appraisalResultLabels[o.result]} (${appraisalAspectLabels[o.aspect]}) – ${o.authorName ?? ''}: ${o.content}`)
        .join(' · ')}
      style={{ marginBottom: 8 }}
    />
  )
}
