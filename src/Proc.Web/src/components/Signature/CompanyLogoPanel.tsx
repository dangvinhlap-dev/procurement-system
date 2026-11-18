import { DeleteOutlined, UploadOutlined } from '@ant-design/icons'
import { App, Button } from 'antd'
import { useRef } from 'react'
import { useCompanyLogo, useCompanyLogoMutations } from '../../api/signatures'
import { formatDateTime } from '../../utils/format'
import { canvasToPng, loadImage, processLogo } from '../../utils/signatureImage'

/**
 * Tab LOGO của ngăn kéo công ty (lát IN1 – design.md mục 20.3): logo in góc trên trái phiếu theo biểu mẫu CUVT; làm chứng từ ở công ty nào thì in
 * logo công ty đó (người dùng chốt 18/11/2026). Ảnh PNG/JPG được thu về tối đa 600 điểm mỗi cạnh trên trình duyệt trước khi tải lên.
 * @param companyId Công ty của ngăn kéo.
 * @param canEdit Có quyền sửa công ty.
 */
export function CompanyLogoPanel({ companyId, canEdit }: { companyId: string; canEdit: boolean }) {
  const { message, modal } = App.useApp()
  const fileRef = useRef<HTMLInputElement>(null)
  const { info, image } = useCompanyLogo(companyId)
  const { upload, remove } = useCompanyLogoMutations(companyId)

  const choose = async (file: File | undefined) => {
    if (!file) return
    try {
      const png = await canvasToPng(processLogo(await loadImage(file)), 'logo.png')
      await upload.mutateAsync(png)
      void message.success('Đã lưu logo công ty.')
    } catch (e) {
      void message.error(e instanceof Error ? e.message : 'Không lưu được logo.')
    }
  }

  const askRemove = () =>
    modal.confirm({
      title: 'Xóa logo công ty?',
      content: 'Phiếu in sau đó để trống góc logo.',
      okText: 'Xóa logo',
      cancelText: 'Không',
      okButtonProps: { danger: true, icon: <DeleteOutlined /> },
      onOk: async () => {
        await remove.mutateAsync()
        void message.success('Đã xóa logo.')
      },
    })

  const logo = info.data
  return (
    <div className="erp-logo-panel" data-testid="company-logo">
      <div className="erp-logo-box">{image.data ? <img src={image.data} alt="Logo công ty" /> : <span>Chưa có logo</span>}</div>
      <div className="erp-signature-hint">
        {logo?.hasLogo
          ? `Ảnh ${logo.width}×${logo.height} điểm · cập nhật ${logo.updatedAt ? formatDateTime(logo.updatedAt) : ''}.`
          : 'Chưa có logo – phiếu in để trống góc logo.'}{' '}
        In góc trên trái các phiếu theo biểu mẫu CUVT (đề nghị mua 1A/1B…). PNG nền trong suốt hoặc JPG; ảnh được thu về tối đa 600 điểm.
      </div>
      {canEdit && (
        <div className="erp-signature-buttons">
          <input
            ref={fileRef}
            type="file"
            accept="image/png,image/jpeg"
            hidden
            data-testid="logo-file"
            onChange={(e) => {
              void choose(e.target.files?.[0])
              e.target.value = ''
            }}
          />
          <Button icon={<UploadOutlined />} loading={upload.isPending} onClick={() => fileRef.current?.click()}>
            {logo?.hasLogo ? 'Thay logo…' : 'Tải logo…'}
          </Button>
          <Button icon={<DeleteOutlined />} disabled={!logo?.hasLogo} onClick={askRemove}>
            Xóa logo
          </Button>
        </div>
      )}
    </div>
  )
}
