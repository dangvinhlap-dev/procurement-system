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
  checkAttachmentFile,
  downloadAttachment,
  formatFileSize,
  useAttachmentMutations,
  useAttachmentMaxMb,
  useAttachments,
  type AttachmentItem,
  type AttachmentOwnerType,
} from "../../api/attachments";
import { formatDateTime } from "../../utils/format";
import {
  usePendingAttachments,
  type PendingAttachment,
  type PendingAttachments,
} from "./usePendingAttachments";

/** Thuộc tính của AttachmentPanel. */
export interface AttachmentPanelProps {
  /** Loại đối tượng chủ. */
  ownerType: AttachmentOwnerType;
  /** Id đối tượng chủ; chưa có (phiếu chưa lưu) thì file chọn vào hàng chờ, lưu phiếu xong mới tải lên. */
  ownerId: string | undefined;
  /** Người dùng có quyền sửa đối tượng chủ: được tải lên, xóa; không thì chỉ xem và tải xuống. */
  canManage: boolean;
  /**
   * Hàng chờ do trang giữ (useAttachmentTab) – để file chọn khi tạo phiếu không mất khi đổi tab. Không truyền thì khối tự giữ
   * (dùng trong ngăn kéo không đóng khi lưu).
   */
  pending?: PendingAttachments;
}

/**
 * Khối file đính kèm dùng chung (design.md mục 18.4): bảng file (bấm tên để tải), tải lên nhiều file kèm ghi chú, xóa mềm.
 * Phiếu đang tạo (chưa có Id) vẫn chọn được file – giữ trong hàng chờ, lưu phiếu xong tự tải lên.
 * Kiểm tra đuôi và dung lượng tối đa (cấu hình máy chủ, mặc định 2 MB) ngay trên trình duyệt cho nhanh; máy chủ vẫn kiểm tra lại.
 * @param props Xem {@link AttachmentPanelProps}.
 */
export function AttachmentPanel({
  ownerType,
  ownerId,
  canManage,
  pending,
}: AttachmentPanelProps) {
  const { message } = App.useApp();
  const maxMb = useAttachmentMaxMb();
  const { data = [], isLoading } = useAttachments(ownerType, ownerId);
  const { upload, remove } = useAttachmentMutations(ownerType, ownerId ?? "");
  const ownPending = usePendingAttachments(ownerType, ownerId);
  const queue = pending ?? ownPending;
  const [note, setNote] = useState("");

  if (!ownerId && !canManage)
    return (
      <Typography.Text type="secondary">Chưa có file đính kèm.</Typography.Text>
    );

  /** Kiểm tra rồi gửi từng file (phiếu chưa lưu thì vào hàng chờ); trả false để Upload của Ant Design không tự gửi (API cần header đăng nhập). */
  const send = (file: File) => {
    const error = checkAttachmentFile(file, maxMb);
    if (error) {
      message.error(error);
    } else if (!ownerId) {
      queue.add(file, note.trim() || undefined);
      setNote("");
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
              Tối đa {maxMb} MB/file: pdf, Word, Excel, PowerPoint, ảnh,
              zip/rar/7z, thư.
              {!ownerId && " File sẽ tải lên khi lưu phiếu."}
            </Typography.Text>
          </Flex>
        )}
        {!ownerId ? (
          <Table<PendingAttachment>
            size="small"
            pagination={false}
            rowKey="key"
            dataSource={queue.items}
            locale={{ emptyText: "Chưa chọn file nào." }}
            columns={[
              { title: "Tên file (chờ lưu phiếu)", ellipsis: true, render: (_, r) => r.file.name },
              {
                title: "Dung lượng",
                width: 100,
                align: "right",
                render: (_, r) => formatFileSize(r.file.size),
              },
              { title: "Ghi chú", dataIndex: "note", ellipsis: true },
              {
                title: "",
                key: "actions",
                width: 70,
                render: (_, r) => (
                  <Button type="text" size="small" danger onClick={() => queue.remove(r.key)}>
                    Bỏ
                  </Button>
                ),
              },
            ]}
          />
        ) : (
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
        )}
      </Flex>
    </ConfigProvider>
  );
}
