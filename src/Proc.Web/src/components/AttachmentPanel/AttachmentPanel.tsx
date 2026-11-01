import {
  App,
  Button,
  ConfigProvider,
  Flex,
  Input,
  Popconfirm,
  Table,
  Typography,
  Upload,
} from "antd";
import { useState } from "react";
import {
  attachmentExtensions,
  attachmentMaxBytes,
  downloadAttachment,
  formatFileSize,
  useAttachmentMutations,
  useAttachments,
  type AttachmentItem,
  type AttachmentOwnerType,
} from "../../api/attachments";
import { formatDateTime } from "../../utils/format";

/** Thuộc tính của AttachmentPanel. */
export interface AttachmentPanelProps {
  /** Loại đối tượng chủ. */
  ownerType: AttachmentOwnerType;
  /** Id đối tượng chủ; chưa có (phiếu chưa lưu) thì chỉ hiện lời nhắc. */
  ownerId: string | undefined;
  /** Người dùng có quyền sửa đối tượng chủ: được tải lên, xóa; không thì chỉ xem và tải xuống. */
  canManage: boolean;
}

/**
 * Khối file đính kèm dùng chung (design.md mục 18.4): bảng file (bấm tên để tải), tải lên nhiều file kèm ghi chú, xóa mềm.
 * Kiểm tra đuôi và 20 MB ngay trên trình duyệt cho nhanh; máy chủ vẫn kiểm tra lại.
 * @param props Xem {@link AttachmentPanelProps}.
 */
export function AttachmentPanel({
  ownerType,
  ownerId,
  canManage,
}: AttachmentPanelProps) {
  const { message } = App.useApp();
  const { data = [], isLoading } = useAttachments(ownerType, ownerId);
  const { upload, remove } = useAttachmentMutations(ownerType, ownerId ?? "");
  const [note, setNote] = useState("");

  if (!ownerId)
    return (
      <Typography.Text type="secondary">
        Lưu phiếu trước khi đính kèm file.
      </Typography.Text>
    );

  /** Kiểm tra rồi gửi từng file; trả false để Upload của Ant Design không tự gửi (API cần header đăng nhập). */
  const send = (file: File) => {
    const extension = file.name.includes(".")
      ? file.name.slice(file.name.lastIndexOf(".")).toLowerCase()
      : "";
    if (!attachmentExtensions.includes(extension)) {
      message.error(
        `Không nhận file "${file.name}". Chỉ nhận tài liệu, ảnh, file nén thông dụng.`,
      );
    } else if (file.size > attachmentMaxBytes) {
      message.error(
        `File "${file.name}" quá 20 MB. Hãy nén hoặc chia nhỏ file.`,
      );
    } else {
      upload.mutate(
        { file, note: note.trim() || undefined },
        {
          onSuccess: () => {
            message.success(`Đã tải lên ${file.name}.`);
            setNote("");
          },
          onError: (e) =>
            message.error(
              e instanceof Error ? e.message : "Không tải lên được file.",
            ),
        },
      );
    }
    return false;
  };

  const download = (item: AttachmentItem) =>
    void downloadAttachment(item).catch((e: unknown) =>
      message.error(e instanceof Error ? e.message : "Không tải được file."),
    );

  return (
    // Phiếu chỉ xem (đã duyệt, đã ghi sổ) khóa cả Form của DocumentForm; đính kèm vẫn được phép nên bỏ khóa kế thừa ở khối này.
    <ConfigProvider componentDisabled={false}>
      <Flex vertical gap={8}>
        {canManage && (
          <Flex gap={8} wrap>
            <Input
              aria-label="Ghi chú cho file tải lên"
              placeholder="Ghi chú cho file tải lên (không bắt buộc)"
              value={note}
              maxLength={500}
              onChange={(e) => setNote(e.target.value)}
              style={{ maxWidth: 360 }}
            />
            <Upload
              accept={attachmentExtensions.join(",")}
              multiple
              showUploadList={false}
              beforeUpload={send}
            >
              <Button loading={upload.isPending}>Tải lên file…</Button>
            </Upload>
            <Typography.Text type="secondary">
              Tối đa 20 MB/file: pdf, Word, Excel, PowerPoint, ảnh, zip/rar/7z,
              thư.
            </Typography.Text>
          </Flex>
        )}
        <Table<AttachmentItem>
          size="small"
          pagination={false}
          rowKey="id"
          loading={isLoading}
          dataSource={data}
          locale={{ emptyText: "Chưa có file đính kèm." }}
          columns={[
            {
              title: "Tên file",
              dataIndex: "fileName",
              ellipsis: true,
              render: (v: string, r) => (
                <Typography.Link onClick={() => download(r)} title={`Tải ${v}`}>
                  {v}
                </Typography.Link>
              ),
            },
            {
              title: "Dung lượng",
              dataIndex: "sizeBytes",
              width: 100,
              align: "right",
              render: (v: number) => formatFileSize(v),
            },
            { title: "Ghi chú", dataIndex: "note", ellipsis: true },
            { title: "Người tải lên", dataIndex: "uploadedByName", width: 180 },
            {
              title: "Thời điểm",
              dataIndex: "uploadedAt",
              width: 140,
              render: (v: string) => formatDateTime(v),
            },
            ...(canManage
              ? [
                  {
                    title: "",
                    key: "actions",
                    width: 70,
                    render: (_: unknown, r: AttachmentItem) => (
                      <Popconfirm
                        title={`Xóa file ${r.fileName}?`}
                        okText="Xóa"
                        cancelText="Không"
                        onConfirm={() =>
                          remove
                            .mutateAsync(r.id)
                            .catch((e: unknown) =>
                              message.error(
                                e instanceof Error
                                  ? e.message
                                  : "Không xóa được file.",
                              ),
                            )
                        }
                      >
                        <Button type="text" size="small" danger>
                          Xóa
                        </Button>
                      </Popconfirm>
                    ),
                  },
                ]
              : []),
          ]}
        />
      </Flex>
    </ConfigProvider>
  );
}
