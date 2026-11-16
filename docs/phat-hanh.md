# Phát hành, phiên bản và quay lại bản cũ

Tách từ CLAUDE.md (mục 8 cũ) ngày 31/10/2026; viết lại 15/11/2026 khi có trung tâm phát hành REL (lát QT8 – người dùng đồng ý). Đọc khi người dùng
yêu cầu phát hành hoặc "quay về phiên bản X". Quy định vẫn bắt buộc. Thiết kế: design.md mục 21.6, 21.6a, 21.8, 21.17.

## Số phiên bản
- MAJOR.MINOR.PATCH (Semantic Versioning): PATCH = sửa lỗi; MINOR = thêm chức năng; MAJOR = thay đổi lớn, 1.0.0 = bản chạy thật đầu tiên.
- Ghi trong `Directory.Build.props` (thẻ `<Version>`); hiện trong menu người dùng trên Shellbar và trên màn hình đăng nhập.

## Mỗi lần phát hành (chỉ khi người dùng yêu cầu) – phần trên máy lập trình (DEV) làm đủ
1. Cập nhật `CHANGELOG.md` (tiếng Việt), mẫu `## [x.y.z] - YYYY-MM-DD` với các mục:
   Thêm mới / Thay đổi / Sửa lỗi / Thay đổi CSDL / Lưu ý khi nâng cấp / Cách quay lại bản trước.
   Mỗi gạch đầu dòng của Thêm mới / Thay đổi / Sửa lỗi thành một **mục kiểm thử UAT** trên phiếu phát hành – viết sao cho người dùng thử được.
   Mục "Cách quay lại bản trước" ghi **mức quay về** (1 / 2 / 3 – design.md 21.6a) cùng mức trong `rollback.json`.
2. Cập nhật `<Version>` trong `Directory.Build.props`.
3. Bản có thay đổi CSDL (xem mục dưới): script nâng cấp, script gỡ (nếu mức 2), file khai mức quay về `db/scripts/rollback/<phiên-bản>.json`.
4. Commit `Phát hành phiên bản x.y.z`.
5. `git tag -a vx.y.z -m "Phiên bản x.y.z: <tóm tắt>"`.
6. `git push` và `git push --tags`.
7. Đóng gói bản Windows: `.\deploy\windows\publish.ps1` → `artifacts\procurement-x.y.z.zip` + `artifacts\procurement-x.y.z.zip.sha256` (không
   commit). Gói có sẵn `rollback.json`, `CHANGELOG.md`, bộ thực thi `tools\qt-agent*.ps1`; bản có script CSDL mà chưa khai
   `db/scripts/rollback/<phiên-bản>.json` thì script dừng. Tạo trang GitHub Release kèm **cả hai file**, nội dung lấy từ mục CHANGELOG của bản:
   `gh release create vx.y.z artifacts\procurement-x.y.z.zip artifacts\procurement-x.y.z.zip.sha256 --verify-tag --title "Phiên bản x.y.z" --notes-file <file>`
   (`gh` ở `C:\Program Files\GitHub CLI\gh.exe`, đã đăng nhập). Bản Docker build trên server từ tag (README mục "Triển khai server").
8. Báo người dùng: số phiên bản, link Release, tóm tắt thay đổi, có thay đổi CSDL hay không, mức quay về, có cần sao lưu trước khi nâng cấp không.

## Đưa bản lên TEST và PROD
- **Từ bản 0.36.1 (GĐ3 – design.md 21.8)**: cài qua **trung tâm phát hành REL trên máy PROD** – Nhận gói (từ GitHub Release hoặc tải lên) → Cài
  TEST → người dùng kiểm thử các mục, ghi lỗi → Xác nhận UAT đạt → Cài PROD → theo dõi màn hình ERR → Đóng phiếu. Bộ thực thi trên từng máy sao lưu,
  cài, chạy script CSDL, kiểm tra; cài lỗi thì tự trả bản cũ (design.md 21.17, docs/deploy-windows.md mục 4g).
- **Bản 0.36.0 và trước đó** (module QT chưa đủ để tự chuyển bản – GĐ1, GĐ2): cài tay theo docs/deploy-windows.md mục 5. 0.36.0 là **lần cài tay
  cuối**.
- **Cài tay chỉ còn là đường dự phòng** khi trung tâm phát hành / bộ thực thi hỏng (docs/deploy-windows.md mục 5, 6); cài tay xong ghi lại vào
  phiếu phát hành (ghi chú) để lịch sử đủ.

## Thay đổi CSDL
- Chỉ qua EF Core migration, tên rõ nghĩa (ví dụ `V0_4_0_ThemBangGiaNoiBo`), luôn có hàm `Down()` gỡ được; ghi chú nếu `Down()` làm mất dữ liệu.
- Không xóa cột/bảng đang có dữ liệu trong cùng một bản: đánh dấu ngừng dùng trước, xóa ở bản sau. Ưu tiên kiểu "thêm trước – bỏ sau" (cột mới cho
  phép trống / có mặc định; đổi tên = thêm cột mới, chép dữ liệu, bản sau mới bỏ cột cũ) để bản nào cũng quay về được **mức 1**.
- Mỗi bản có thay đổi CSDL:
  - Script nâng cấp `dotnet ef migrations script <migration-cuối-bản-trước> <migration-cuối-bản-mới> --idempotent …` vào
    `db/scripts/<phiên-bản>.sql` (cách làm, bỏ BOM: `db/scripts/README.md`).
  - Khai mức quay về `db/scripts/rollback/<phiên-bản>.json`: `{"version":"x.y.z","minLevel":1,"migrations":["V…"]}` – **1** khi chỉ thêm (bản
    trước chạy được trên CSDL mới); **2** khi không tương thích và gỡ được bằng script; **3** khi chỉ khôi phục bản sao lưu mới quay về được.
  - Mức 2: script gỡ `dotnet ef migrations script <migration-cuối-bản-mới> <migration-cuối-bản-trước>` vào `db/scripts/down/<phiên-bản>.sql`
    (chạy lùi các `Down()`); liệt kê dữ liệu bị mất trong mục "Cách quay lại bản trước" của CHANGELOG.

## Khi người dùng yêu cầu "quay về phiên bản X"
1. Không xóa lịch sử. Liệt kê thay đổi giữa bản hiện tại và bản X (code và migration), nêu rủi ro mất dữ liệu theo **mức quay về** (design.md
   21.6a: mức 1 giữ dữ liệu, mức 2 mất dữ liệu ở phần bị gỡ, mức 3 mất dữ liệu nhập sau lần cài), chờ người dùng đồng ý.
2. Bản đang chạy trên TEST / PROD: quay về trên **REL** (phiếu phát hành của bản hiện tại → "Quay về bản cũ…", chọn mức ≥ mức tối thiểu của gói); quay
   về nhiều bản thì đi lần lượt từng bản theo thứ tự ngược, mức của cả chuỗi là mức cao nhất.
3. Trong mã nguồn: tạo nhánh `rollback/vX` từ tag X hoặc dùng `git revert` cho các commit cần gỡ; chạy build và test.
4. Chỉ gỡ migration (`dotnet ef database update <migration-của-bản-X>`) hoặc chạy script gỡ khi người dùng xác nhận đã sao lưu CSDL.
