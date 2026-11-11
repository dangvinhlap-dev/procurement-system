# 2. Danh mục

Danh mục chia hai loại:

- **Dùng chung toàn tập đoàn** (thêm mới chỉ ở **tổng công ty**): đơn vị tính, nhóm hàng, hàng hóa, nhóm NCC, nhà cung cấp. Dùng chung để
  so sánh giá giữa các công ty.
- **Theo từng công ty**: kho, bộ phận, NCC áp dụng tại công ty (mã NCC tại công ty), bảng giá nội bộ, tham số.

Quy tắc sửa danh mục dùng chung: ở tổng công ty với quyền quản lý danh mục; hoặc ở bất kỳ công ty nào với quyền **"Sửa thông tin danh mục
dùng chung (trừ mã)"**. **Mã không bao giờ sửa được.** Mọi lần sửa ghi nhật ký, xem bằng F1. Màn hình báo rõ "chỉ xem" hay "được sửa
thông tin".

Danh mục mở dạng **danh sách + ngăn kéo chi tiết** bên phải: bấm đúp dòng để mở, sửa rồi **Lưu** (Ctrl+S), **Esc** để đóng.

## 2.1 Mã tự cấp (đánh số danh mục)

Mã **NCC, nhóm NCC, nhóm hàng, hàng hóa** do hệ thống tự cấp khi lưu (ô mã ghi "(tự cấp khi lưu)"). Dải mã cấu hình ở **Hệ thống → Đánh số
danh mục (DSDM)** – xem 08. Chưa cấu hình dải thì không thêm mới được. Mã ĐVT, công ty, kho do người dùng tự đặt và khóa sau khi tạo.

## 2.2 Công ty (COMP) – Hệ thống → Công ty

- Thêm công ty: chỉ ở tổng công ty, quyền `System.Company.Manage`. Thêm công ty là **tạo luôn kho tổng** của công ty đó; người thêm được gán
  các vai trò mình đang có ở tổng công ty tại công ty mới.
- Mã công ty nằm trong số chứng từ (ví dụ PO-**BHA**-2611-0001) nên chỉ gồm chữ và số, không đổi được.
- Mã số thuế: 10 số hoặc 10 số + "-" + 3 số; không trùng giữa các công ty.
- Ngừng dùng công ty thì không ai chọn làm việc được nữa. Tổng công ty không ngừng dùng được.
- Ở công ty thành viên chỉ thấy (và chỉ sửa được) công ty mình.

## 2.3 Kho (WH) – Hệ thống → Kho

- Mỗi công ty có **đúng một kho tổng (Main)**; kho phụ (Sub) thuộc một kho cha cùng công ty.
- Thông tin: mã, tên, loại, kho cha, địa chỉ, thủ kho, **cho phép tồn âm** (mặc định không).
- Thêm kho phụ hoặc đổi kho cha cần phạm vi **mọi kho** của công ty. Kho cha không được là chính nó hay kho con cháu của nó.
- Kho tổng không ngừng dùng được; kho còn kho con đang dùng thì ngừng kho con trước.

## 2.4 Bộ phận (DEPT) – Hệ thống → Bộ phận

Danh mục bộ phận của từng công ty (mã, tên, trưởng bộ phận). PR **bắt buộc** chọn bộ phận; ngân sách khai theo bộ phận. Khai báo bộ phận
trước khi người dùng lập PR. Quyền `Organization.Department.View/Manage`.

Từ bản 0.22.0 (lát O1): mỗi bộ phận gán **Khối** và **Trưởng phòng ban (tài khoản)** – người duyệt cấp "Trưởng phòng ban của phiếu" ở Quy trình
duyệt; ô "Trưởng bộ phận (tên in)" chỉ để in. **Khối (DIV – Hệ thống → Khối)**: mã, tên, **Giám đốc khối** (tài khoản) – người duyệt cấp
"GĐ khối của phiếu". Chỉ chọn được tài khoản có vai trò tại công ty. Người dùng gán vào bộ phận ở Người dùng → tab Phân quyền (ô "Bộ phận");
khi người đó lập PR, bộ phận được điền sẵn.

**Đơn vị mua (DVM – Mua hàng → Đơn vị mua, bản 0.23.0)**: danh mục của từng công ty các đơn vị được giao đi mua (ví dụ VP NDD, Phòng CUVT) –
Phòng CUVT gán cho từng dòng trên bảng tổng hợp đề xuất mua 2A/2B. Mã 2–20 chữ không dấu hoặc số, không đổi sau khi tạo; không xóa, đơn vị
không dùng nữa thì bỏ "Còn sử dụng" (dòng đã gán giữ nguyên). Quyền riêng `Purchase.Unit.View/Manage` – giao Phòng CUVT tự quản lý mà không
sửa được bộ phận.

## 2.5 Đơn vị tính (UOM)

Mã 1–10 chữ/số (ví dụ CAI, HOP, KG), tên. Chỉ ngừng dùng được khi không còn mặt hàng đang dùng ĐVT đó.

## 2.6 Nhóm hàng (CAT)

- Cây nhiều cấp; mỗi nhóm có **tài khoản kho mặc định** (dùng khi xuất chứng từ cho kế toán).
- **Ai cũng xem đủ cây** nhóm hàng; nhóm ngoài phạm vi của mình chỉ xem, không sửa, không làm nhóm cha, không gán cho mặt hàng.
- **Không trùng tên** trong cùng nhóm cha (so không dấu, không phân biệt hoa thường) – màn hình cảnh báo ngay khi gõ.
- Thêm nhóm gốc, đổi nhóm cha cần phạm vi **mọi nhóm hàng**. Ngừng nhóm thì nhóm con và hàng trong nhóm phải ngừng/chuyển trước.

## 2.7 Hàng hóa (ITEM)

Thông tin chính:

| Nhóm | Trường |
| --- | --- |
| Chung | Mã (tự cấp), tên, tên tiếng Anh, **loại hàng** (hàng tồn kho, dịch vụ…), nhóm hàng, **ĐVT cơ sở**, quy cách, thương hiệu, xuất xứ, mã vạch, thuế suất mặc định (KCT, 0, 5, 8, 10%) |
| Tồn kho (chỉ hàng tồn kho) | Quản lý **lô / hạn dùng**, tồn tối thiểu/tối đa, **tồn an toàn**, **điểm đặt hàng lại**, **vật tư trọng yếu** |
| Quy đổi ĐVT | 1 ĐVT quy đổi = hệ số × ĐVT cơ sở (ví dụ 1 thùng = 24 chai); tồn kho luôn tính theo ĐVT cơ sở |
| Thông số kỹ thuật | Bảng tên – giá trị |
| Tab Nhà cung cấp | Những NCC bán mặt hàng này, NCC ưu tiên |

- Danh sách hàng hóa chỉ hiện mặt hàng trong **phạm vi nhóm hàng** của người xem. Tìm theo mã, tên, tên tiếng Anh, mã vạch; lọc nhóm (gồm
  nhóm con) và loại hàng.
- Mặt hàng bật lô/hạn dùng thì phiếu kho bắt buộc nhập lô, hạn dùng.
- Điểm đặt hàng lại và tồn an toàn dùng cho báo cáo **Gợi ý đặt hàng (REORD)**.

## 2.8 Nhóm NCC (SGRP)

Phân loại NCC (ví dụ Trong nước, Nhập khẩu, Nội bộ). Chỉ ngừng dùng được khi không còn NCC đang giao dịch trong nhóm.

## 2.9 Nhà cung cấp (SUP)

### Thông tin

| Tab | Nội dung |
| --- | --- |
| Chung | Mã (tự cấp), tên, tên viết tắt, nhóm NCC, trong nước/nước ngoài, **mã số thuế** (không trùng; trong nước 10 hoặc 10-3 số), địa chỉ, điện thoại, email, website, điều khoản thanh toán, **ngành hàng cung cấp** (chọn nhiều nhóm hàng; thiếu nhóm thì bấm "Thêm nhóm hàng"), xếp hạng 1–5 sao, **trạng thái** |
| Người liên hệ | Nhiều người, tối đa một người chính |
| Ngân hàng | Nhiều tài khoản, tối đa một tài khoản mặc định |
| Sản phẩm/dịch vụ | Mặt hàng NCC bán: **mã hàng theo NCC**, **ĐVT mua**, MOQ, thời gian giao (ngày), NCC ưu tiên |
| Mở rộng cho công ty | Bảng mọi công ty: Đang giao dịch / Ngừng giao dịch / Chưa mở rộng, mã NCC tại công ty, ngày bắt đầu |
| Hồ sơ đính kèm | Giấy phép, hợp đồng, chứng chỉ… |
| Thẩm định | Ý kiến thẩm định của thành viên chuyên môn |

### Trạng thái NCC

**Tiềm năng** (mặc định) → **Đã duyệt** / **Tạm ngưng** / **Danh sách đen**. Tạm ngưng và Danh sách đen bắt buộc ghi lý do.
Chỉ NCC **Đã duyệt** và **đang giao dịch tại công ty** mới lập được PO, phiếu nhập mua.

Đổi trạng thái sang **Đã duyệt / Danh sách đen** và **đổi tài khoản ngân hàng** không áp ngay mà thành **yêu cầu duyệt NCC** (hộp thoại
"Lưu NCC và gửi duyệt thay đổi?"), xem 2.11.

NCC tạo mới từ bản 0.21.0 phải có **hồ sơ đánh giá NCC mới (HSN)** được duyệt mới chuyển sang Đã duyệt (trừ khi tổng công ty bật tham số
cho phép bỏ qua) – xem 2.10.

### Mở rộng NCC cho công ty

Một NCC bán cho nhiều công ty thì **không tạo NCC mới** mà mở rộng:

1. Làm việc ở **tổng công ty**, mở NCC → tab **Mở rộng cho công ty**.
2. Bấm **Mở rộng cho công ty…**, chọn một, vài hoặc tất cả công ty, ngày bắt đầu → Lưu.
3. Mỗi công ty tự điền **mã NCC tại công ty** (khớp mã đối tượng trong phần mềm kế toán của công ty mình).
4. Ngừng: **Ngừng giao dịch…**. Mở lại giữ mã và ngày công ty đã khai.

Công ty thành viên cũng tự khai NCC mình mua được (quyền `MasterData.Supplier.Manage` tại công ty đó). NCC danh sách đen không mở rộng được.

### NCC nội bộ

NCC liên kết một công ty thành viên (mỗi công ty tối đa một NCC nội bộ) – dùng cho mua bán nội bộ ICS/ICR.

## 2.10 Hồ sơ đánh giá NCC mới (HSN) – Giá và NCC → Hồ sơ NCC mới

Dùng ở tổng công ty để đánh giá NCC trước khi đưa vào danh sách Đã duyệt.

1. Lập hồ sơ: chọn NCC, **nhóm hàng đề xuất NCC cung cấp**, nguồn tìm được NCC, danh mục **giấy tờ pháp lý** (đánh dấu đã cung cấp).
2. Chấm **6 tiêu chí đánh giá**, hoàn thành đánh giá.
3. Một người **khác** người đánh giá lập **đề xuất** (Đề xuất / Không đề xuất) kèm nhận xét.
4. Gửi duyệt theo luồng duyệt NCC. Hồ sơ được duyệt thì NCC mới chuyển sang Đã duyệt được.

Quyền `MasterData.SupplierOnboarding.View/Manage/Propose`. Tham số tổng công ty `SUPPLIER_ONBOARDING_OPTIONAL` cho phép bỏ qua hồ sơ.

## 2.11 Duyệt NCC (SUPAPR) – Giá và NCC → Duyệt NCC

Yêu cầu đổi trạng thái (Đã duyệt / Danh sách đen) hoặc đổi tài khoản ngân hàng đi **hai chặng**:

- **Chặng 1**: các công ty **đang mua** của NCC cùng duyệt (2 công ty thì cả 2).
- **Chặng 2**: **tổng công ty** duyệt lần cuối.

Một công ty từ chối là kết thúc yêu cầu. Người lập rút được yêu cầu đang chờ. Duyệt xong thay đổi mới áp vào NCC. Ngăn kéo NCC báo nếu có
yêu cầu đang chờ. Màn hình yêu cầu có tab **Hồ sơ NCC** và **Hồ sơ đính kèm** (chỉ xem) để người duyệt đối chiếu.

## 2.12 Nhập/xuất Excel danh mục

Áp dụng cho ĐVT, nhóm hàng, hàng hóa, nhóm NCC, NCC – nút **Xuất Excel** / **Nhập Excel** trên danh sách.

- **Nhập theo mã**: dòng có mã đã có thì cập nhật; dòng **để trống mã** thì thêm mới (hệ thống cấp mã). Không xóa bản ghi – ngừng dùng bằng
  cột "Còn dùng".
- Tối đa 5.000 dòng, 10 MB mỗi file. Một dòng lỗi thì cả file không lưu.
- Nhóm hàng: nhóm cha đứng trên nhóm con.
- Hàng hóa: tối đa 3 ĐVT quy đổi trên file; để trống cả 3 cột quy đổi thì giữ quy đổi cũ.
- NCC: người liên hệ chính và tài khoản ngân hàng **chỉ dùng khi thêm NCC mới**; cột **Ngành hàng** ghi **mã nhóm hàng**, nhiều mã cách
  nhau dấu phẩy.
