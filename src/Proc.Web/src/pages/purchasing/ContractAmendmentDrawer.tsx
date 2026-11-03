import { CheckOutlined, CloseCircleOutlined, RollbackOutlined, SendOutlined, StopOutlined } from '@ant-design/icons'
import { Alert, App, Button, DatePicker, Descriptions, Form, Input, InputNumber, Spin } from 'antd'
import { Select } from '../../classic/Select'
import dayjs, { type Dayjs } from 'dayjs'
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import {
  amendmentStatusLabels,
  useContract,
  useContractAmendment,
  useContractAmendmentMutations,
  type Contract,
  type ContractAmendment,
  type ContractTerms,
  type SaveContractAmendmentInput,
} from '../../api/contracts'
import { usePriceLists } from '../../api/pricing'
import { useAttachmentTab } from '../../components/AttachmentPanel/useAttachmentTab'
import { Permission } from '../../auth/permissions'
import { useAuth } from '../../auth/useAuth'
import { DetailDrawer } from '../../components/DetailDrawer/DetailDrawer'
import { formatDate, formatMoney, vnNumberInput, DATE_INPUT_FORMATS } from '../../utils/format'
import { ApprovalHistory } from './ApprovalHistory'
import { contractTermFields } from './contractTerms'
import { confirmReject } from './useApprovalCommands'

/** Đường dẫn trang đầy đủ một phụ lục (`AMENDMENT_PATH/:id`) – mở từ thông báo / Việc cần làm / Tra cứu chứng từ (chỉ biết Id phụ lục). */
export const AMENDMENT_PATH = '/mua-hang/hop-dong-khung/phu-luc'

interface FormValues extends Partial<Record<keyof ContractTerms, string | number | null>> {
  paperNo?: string
  reason?: string
  signDate?: Dayjs | null
  newValidTo?: Dayjs
  newPriceListId?: string
  creditLimit?: number | null
  note?: string
}

const orNull = (value: unknown) => (typeof value === 'string' && value.trim() ? value.trim() : null)

/**
 * Ngăn kéo phụ lục hợp đồng nguyên tắc (lát B4b – design.md mục 18.7): gia hạn (ngày hết hiệu lực mới), đổi bảng giá dùng chung, đổi điều
 * khoản/hạn mức công nợ. Lập mới thì điền sẵn giá trị hiện hành của hợp đồng – chỉ sửa phần cần đổi. Mỗi phụ lục phải duyệt; duyệt xong
 * mới áp vào hợp đồng (giá dòng lấy lại theo bảng giá mới).
 * @param contract Hợp đồng đang mở.
 * @param amendmentId Id phụ lục, 'moi' khi lập mới, null khi đóng.
 * @param onClose Đóng ngăn kéo.
 * @param onCreated Lập xong phụ lục mới (để đổi đường dẫn sang Id vừa tạo).
 * @param asPage Vẽ thành trang đầy đủ thay cho ngăn kéo.
 */
export function ContractAmendmentDrawer({
  contract,
  amendmentId,
  onClose,
  onCreated,
  asPage = false,
}: {
  contract: Contract
  amendmentId: string | null
  onClose: () => void
  onCreated: (id: string) => void
  asPage?: boolean
}) {
  const isNew = amendmentId === 'moi'
  const { hasPermission } = useAuth()
  const { message, modal } = App.useApp()
  const [form] = Form.useForm<FormValues>()
  const amendment = useContractAmendment(amendmentId && !isNew ? amendmentId : null)
  const mutations = useContractAmendmentMutations()
  const priceLists = usePriceLists()
  const [error, setError] = useState<string | null>(null)
  const doc = amendment.data
  const canEdit = isNew ? contract.actions.canAmend : !!doc?.actions.canEdit
  // Bản scan phụ lục đã ký – đính kèm được cả sau khi duyệt, quyền theo quyền sửa hợp đồng (design.md mục 18.4).
  const attachmentTab = useAttachmentTab('ContractAmendment', doc?.id, hasPermission(Permission.ContractManage))

  // Bảng giá chọn được: cùng NCC; bảng của công ty (chưa ngừng) hoặc bảng toàn tập đoàn đã hiệu lực – server kiểm tra lại.
  const priceListOptions = (priceLists.data ?? [])
    .filter((p) => p.supplierCode === contract.supplierCode && p.status !== 'Inactive' && (p.isOwn || p.status === 'Active'))
    .map((p) => ({ value: p.id, label: `${p.no} – ${p.title}${p.isGroupWide ? ' (toàn tập đoàn)' : ''}` }))
  for (const [id, label] of [
    [contract.priceListId, `${contract.priceListNo} – ${contract.priceListTitle}`],
    [doc?.newPriceListId, doc ? `${doc.newPriceListNo} – ${doc.newPriceListTitle}` : ''],
  ] as const)
    if (id && !priceListOptions.some((o) => o.value === id)) priceListOptions.push({ value: id, label })

  useEffect(() => {
    if (!amendmentId) return
    form.resetFields()
    const terms: ContractTerms = doc ? doc.terms : contract
    form.setFieldsValue({
      paperNo: doc?.paperNo ?? '',
      reason: doc?.reason ?? '',
      signDate: doc?.signDate ? dayjs(doc.signDate) : null,
      newValidTo: dayjs(doc?.newValidTo ?? contract.validTo),
      newPriceListId: doc?.newPriceListId ?? contract.priceListId,
      creditLimit: terms.creditLimit,
      note: doc?.note ?? '',
      ...Object.fromEntries(contractTermFields.map((t) => [t.name, terms[t.name] ?? ''])),
    })
  }, [amendmentId, doc, contract, form])

  /** Giá trị hiện hành của hợp đồng – hiện dưới ô khi giá trị phụ lục khác, để người lập/duyệt thấy trước – sau. */
  const current = doc?.status === 'Approved' ? null : contract
  const watched = Form.useWatch([], form) as FormValues | undefined
  const before = (name: keyof ContractTerms, format: (v: string | number) => string = String) => {
    if (!current || !watched) return undefined
    const now = watched[name]
    const old = current[name]
    const same = (orNull(now) ?? (typeof now === 'number' ? now : null)) === (orNull(old) ?? (typeof old === 'number' ? old : null))
    return same ? undefined : `Hiện hành: ${old === null || old === '' ? '(trống)' : format(old)}`
  }

  const persist = async (): Promise<ContractAmendment | null> => {
    let values: FormValues
    try {
      values = await form.validateFields()
    } catch {
      setError('Còn ô chưa hợp lệ – xem các ô báo đỏ ở tab Phụ lục và Điều khoản.')
      return null
    }
    setError(null)
    const input: SaveContractAmendmentInput = {
      paperNo: orNull(values.paperNo),
      reason: values.reason?.trim() ?? '',
      signDate: values.signDate ? values.signDate.format('YYYY-MM-DD') : null,
      newValidTo: values.newValidTo!.format('YYYY-MM-DD'),
      newPriceListId: values.newPriceListId!,
      creditLimit: values.creditLimit ?? null,
      note: orNull(values.note),
      priceAdjustmentTerms: orNull(values.priceAdjustmentTerms),
      paymentTerms: orNull(values.paymentTerms),
      deliveryTerms: orNull(values.deliveryTerms),
      discountTerms: orNull(values.discountTerms),
      sponsorshipTerms: orNull(values.sponsorshipTerms),
      penaltyTerms: orNull(values.penaltyTerms),
      otherTerms: orNull(values.otherTerms),
    }
    try {
      return isNew
        ? await mutations.create.mutateAsync({ contractId: contract.id, input })
        : await mutations.update.mutateAsync({ id: amendmentId!, input })
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Không lưu được phụ lục.')
      return null
    }
  }

  const save = async () => {
    if (!canEdit) return
    const saved = await persist()
    if (!saved) return
    message.success(`Đã lưu phụ lục ${saved.no}.`)
    if (isNew) {
      await attachmentTab.uploadTo(saved.id)
      onCreated(saved.id)
    }
  }

  const fail = (e: unknown, fallback: string) => setError(e instanceof Error ? e.message : fallback)

  const submit = () =>
    modal.confirm({
      title: 'Gửi duyệt phụ lục?',
      content: 'Phụ lục chuyển sang Chờ duyệt, không sửa được nữa trừ khi bị từ chối hoặc rút về nháp. Duyệt xong mới áp vào hợp đồng.',
      okText: 'Gửi duyệt',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        const saved = await persist()
        if (!saved) return
        if (isNew) {
          await attachmentTab.uploadTo(saved.id)
          onCreated(saved.id)
        }
        try {
          const sent = await mutations.submit.mutateAsync(saved.id)
          message.success(sent.status === 'Approved' ? `Phụ lục ${sent.no} đã duyệt và áp vào hợp đồng.` : `Đã gửi duyệt phụ lục ${sent.no}.`)
        } catch (e) {
          fail(e, 'Không gửi duyệt được.')
        }
      },
    })

  const approve = () =>
    doc &&
    modal.confirm({
      title: `Duyệt phụ lục ${doc.no}?`,
      content: `${doc.changeSummary}. Giá trị dự kiến sau phụ lục ${formatMoney(doc.expectedAmount)} đ. Duyệt cấp cuối thì phụ lục áp vào hợp đồng.`,
      okText: 'Duyệt',
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          const result = await mutations.approve.mutateAsync({ id: doc.id })
          message.success(result.status === 'Approved' ? `Đã duyệt xong – phụ lục ${doc.no} đã áp vào hợp đồng.` : `Đã duyệt cấp của bạn – phụ lục ${doc.no} chuyển cấp tiếp theo.`)
        } catch (e) {
          fail(e, 'Không duyệt được.')
        }
      },
    })

  const reject = () =>
    doc &&
    confirmReject(modal, message, `Từ chối phụ lục ${doc.no}?`, async (reason) => {
      setError(null)
      try {
        await mutations.reject.mutateAsync({ id: doc.id, comment: reason })
        message.success(`Đã từ chối phụ lục ${doc.no}.`)
      } catch (e) {
        fail(e, 'Không từ chối được.')
      }
    })

  const simple = (title: string, okText: string, run: () => Promise<unknown>, done: string, danger: boolean, content: string) =>
    modal.confirm({
      title,
      content,
      okText,
      okButtonProps: { danger },
      cancelText: 'Không',
      onOk: async () => {
        setError(null)
        try {
          await run()
          message.success(done)
        } catch (e) {
          fail(e, 'Không thực hiện được.')
        }
      },
    })

  const actions = (
    <>
      {(isNew ? canEdit : doc?.actions.canSubmit) && (
        <Button icon={<SendOutlined />} onClick={submit} loading={mutations.submit.isPending}>
          Gửi duyệt
        </Button>
      )}
      {doc?.actions.canApprove && (
        <>
          <Button type="primary" icon={<CheckOutlined />} onClick={approve} loading={mutations.approve.isPending}>
            Duyệt
          </Button>
          <Button danger icon={<CloseCircleOutlined />} onClick={reject} loading={mutations.reject.isPending}>
            Từ chối
          </Button>
        </>
      )}
      {doc?.actions.canRecall && (
        <Button icon={<RollbackOutlined />} loading={mutations.recall.isPending}
          onClick={() => simple(`Rút phụ lục ${doc.no} về nháp?`, 'Rút về nháp', () => mutations.recall.mutateAsync(doc.id), `Đã rút phụ lục ${doc.no} về nháp.`,
            false, 'Các cấp đang chờ duyệt bỏ qua; sửa xong thì gửi duyệt lại từ đầu.')}>
          Rút về nháp
        </Button>
      )}
      {!isNew && doc?.actions.canCancel && (
        <Button danger icon={<StopOutlined />}
          onClick={() => simple(`Hủy phụ lục ${doc.no}?`, 'Hủy phụ lục', () => mutations.cancel.mutateAsync(doc.id), `Đã hủy phụ lục ${doc.no}.`, true,
            'Phụ lục đã hủy giữ lại số và lịch sử; hợp đồng không đổi.')}>
          Hủy phụ lục
        </Button>
      )}
    </>
  )

  const statusLabel = isNew ? 'Nháp (chưa lưu)' : doc ? amendmentStatusLabels[doc.status] : ''
  const notices = doc && (
    <>
      {doc.rejectReason && doc.status === 'Draft' && <Alert type="warning" showIcon title={`Bị từ chối: ${doc.rejectReason}`} />}
      {doc.actions.readOnlyReason && <Alert type="info" showIcon title={doc.actions.readOnlyReason} />}
    </>
  )

  return (
    <DetailDrawer
      open={!!amendmentId}
      asPage={asPage}
      title={isNew ? `Lập phụ lục – hợp đồng ${contract.no}` : `Phụ lục ${doc?.no ?? ''} · ${statusLabel}`}
      audit={{ entityType: 'PurchaseContractAmendment', id: doc?.id }}
      onClose={onClose}
      onSave={canEdit ? save : undefined}
      saving={mutations.create.isPending || mutations.update.isPending}
      loading={!isNew && amendment.isLoading}
      error={error ?? (amendment.error ? amendment.error.message : null)}
      extraActions={actions}
      form={form}
      formName="amendment"
      readOnly={!canEdit}
      tabs={[
        {
          key: 'main',
          label: 'Phụ lục',
          children: (
            <>
              {notices}
              {doc && (
                <Descriptions size="small" column={1} bordered style={{ marginBottom: 8 }}
                  items={[
                    { key: 'summary', label: 'Thay đổi', children: doc.changeSummary },
                    {
                      key: 'amount',
                      label: 'Giá trị dự kiến',
                      children: doc.status === 'Approved'
                        ? `${formatMoney(doc.expectedAmount)} đ (đã áp)`
                        : `Hiện hành ${formatMoney(doc.currentExpectedAmount)} đ → sau phụ lục ${formatMoney(doc.expectedAmount)} đ`,
                    },
                    { key: 'creator', label: 'Người lập', children: doc.createdByName ?? '' },
                  ]}
                />
              )}
              <Form.Item label="Lý do lập phụ lục" name="reason" rules={[{ required: true, whitespace: true, message: 'Nhập lý do.' }, { max: 500 }]}>
                <Input.TextArea rows={2} />
              </Form.Item>
              <Form.Item label="Số phụ lục giấy" name="paperNo" rules={[{ max: 100 }]}>
                <Input />
              </Form.Item>
              <Form.Item label="Ngày ký phụ lục" name="signDate">
                <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} />
              </Form.Item>
              <Form.Item label="Hiệu lực đến (sau phụ lục)" name="newValidTo" rules={[{ required: true, message: 'Chọn ngày.' }]}
                extra={`Hiệu lực từ ${formatDate(contract.validFrom)}; hiện hành đến ${formatDate(doc?.status === 'Approved' ? doc.oldValidTo : contract.validTo)}.`}>
                <DatePicker format={DATE_INPUT_FORMATS} style={{ width: '100%' }} aria-label="Hiệu lực đến (sau phụ lục)" />
              </Form.Item>
              <Form.Item label="Bảng giá dùng chung (sau phụ lục)" name="newPriceListId" rules={[{ required: true, message: 'Chọn bảng giá.' }]}
                extra={`Hiện hành: ${doc?.status === 'Approved' ? doc.oldPriceListNo : contract.priceListNo}. Đổi bảng giá thì giá các dòng sản lượng lấy lại theo bảng mới.`}>
                <Select showSearch={{ optionFilterProp: 'label' }} options={priceListOptions} aria-label="Bảng giá sau phụ lục" />
              </Form.Item>
              <Form.Item label="Ghi chú" name="note" rules={[{ max: 1000 }]}>
                <Input />
              </Form.Item>
            </>
          ),
        },
        {
          key: 'terms',
          label: 'Điều khoản',
          children: (
            <>
              <Form.Item label="Hạn mức công nợ (VND)" name="creditLimit" rules={[{ type: 'number', min: 0, message: '≥ 0.' }]}
                extra={before('creditLimit', (v) => `${formatMoney(Number(v))} đ`)}>
                <InputNumber {...vnNumberInput} style={{ width: '100%' }} min={0} precision={0} />
              </Form.Item>
              {contractTermFields.map((t) => (
                <Form.Item key={t.name} label={t.label} name={t.name} rules={[{ max: t.max }]} extra={before(t.name)}>
                  <Input.TextArea rows={2} />
                </Form.Item>
              ))}
            </>
          ),
        },
        ...(doc && doc.approvals.length > 0 ? [{ key: 'approvals', label: 'Lịch sử duyệt', children: <ApprovalHistory steps={doc.approvals} /> }] : []),
        attachmentTab,
      ]}
    />
  )
}

/**
 * Trang đầy đủ một phụ lục (`/mua-hang/hop-dong-khung/phu-luc/:id`) – mở từ thông báo, Việc cần làm, Tra cứu chứng từ (đường dẫn chỉ có Id
 * phụ lục): tải phụ lục rồi tải hợp đồng của nó; "Quay lại" về trang hợp đồng.
 */
export function ContractAmendmentPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const amendment = useContractAmendment(id)
  const contract = useContract(amendment.data?.contractId ?? null)
  const error = amendment.error ?? contract.error
  if (error) return <Alert type="error" showIcon title={error.message} style={{ margin: 16 }} />
  if (!contract.data) return <Spin style={{ margin: 32 }} />
  const contractPath = `/mua-hang/hop-dong-khung/${contract.data.id}`
  return (
    <ContractAmendmentDrawer
      contract={contract.data}
      amendmentId={id}
      asPage
      onClose={() => navigate(contractPath)}
      onCreated={(created) => navigate(`${AMENDMENT_PATH}/${created}`)}
    />
  )
}
