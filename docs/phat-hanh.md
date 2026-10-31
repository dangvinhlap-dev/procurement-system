# Phát hành, phiên bản và quay lại bản cũ

Tách từ CLAUDE.md (mục 8 cũ) ngày 31/10/2026. Đọc khi người dùng yêu cầu phát hành hoặc "quay về phiên bản X". Quy định vẫn bắt buộc.

## Số phiên bản
- MAJOR.MINOR.PATCH (Semantic Versioning): PATCH = sửa lỗi; MINOR = thêm chức năng; MAJOR = thay đổi lớn, 1.0.0 = bản chạy thật đầu tiên.
- Ghi trong `Directory.Build.props` (thẻ `<Version>`); hiện trong menu người dùng trên Shellbar và trên màn hình đăng nhập.

## Mỗi lần phát hành (chỉ khi người dùng yêu cầu) làm đủ
1. Cập nhật `CHANGELOG.md` (tiếng Việt), mẫu `## [x.y.z] - YYYY-MM-DD` với các mục:
   Thêm mới / Thay đổi / Sửa lỗi / Thay đổi CSDL / Lưu ý khi nâng cấp / Cách quay lại bản trước.
2. Cập nhật `<Version>` trong `Directory.Build.props`.
3. Commit `Phát hành phiên bản x.y.z`.
4. `git tag -a vx.y.z -m "Phiên bản x.y.z: <tóm tắt>"`.
5. `git push` và `git push --tags`.
6. Báo người dùng: số phiên bản, tóm tắt thay đổi, có thay đổi CSDL hay không, có cần sao lưu trước khi nâng cấp không.

## Thay đổi CSDL
- Chỉ qua EF Core migration, tên rõ nghĩa (ví dụ `V0_4_0_ThemBangGiaNoiBo`), luôn có hàm `Down()` gỡ được; ghi chú nếu `Down()` làm mất dữ liệu.
- Không xóa cột/bảng đang có dữ liệu trong cùng một bản: đánh dấu ngừng dùng trước, xóa ở bản sau.
- Mỗi bản có thay đổi CSDL: sinh script `dotnet ef migrations script <bản-trước> <bản-mới>` vào `db/scripts/<phiên-bản>.sql`.

## Khi người dùng yêu cầu "quay về phiên bản X"
1. Không xóa lịch sử. Liệt kê thay đổi giữa bản hiện tại và bản X (code và migration), nêu rủi ro mất dữ liệu, chờ người dùng đồng ý.
2. Sau khi đồng ý: tạo nhánh `rollback/vX` từ tag X hoặc dùng `git revert` cho các commit cần gỡ; chạy build và test.
3. Chỉ gỡ migration (`dotnet ef database update <migration-của-bản-X>`) khi người dùng xác nhận đã sao lưu CSDL.
