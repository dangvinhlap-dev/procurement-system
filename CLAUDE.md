# Procurement & Inventory – quy tắc dự án

Tài liệu thiết kế đầy đủ: `docs/design.md`. Đọc mục liên quan trước khi làm mỗi tính năng.

## Công nghệ
- .NET 10, ASP.NET Core Web API, EF Core 10 + Npgsql, PostgreSQL 18.
- Frontend: React 19 + TypeScript + Vite + Ant Design + TanStack Query.
- Test: xUnit + Testcontainers (PostgreSQL thật).
- Môi trường dev: GitHub Codespaces; CSDL chạy bằng `docker-compose.dev.yml`
  (Host=localhost;Port=5432;Database=procurement;Username=proc;Password=proc_dev_only).

## Kiến trúc
- Modular monolith, Clean Architecture: Domain → Application → Infrastructure → Api, cộng Proc.Web.
- Cấu trúc thư mục theo mục 13.1 của docs/design.md.

## Quy tắc nghiệp vụ bất biến
- Mỗi công ty là pháp nhân độc lập. Mọi bảng giao dịch có CompanyId.
- Luôn đi qua ICurrentUserScope; không truy vấn bỏ qua global query filter (công ty, kho, nhóm hàng).
- Liên công ty chỉ đi kho Main → kho Main, qua cặp chứng từ ICS (xuất bán nội bộ) / ICR (nhập mua nội bộ).
- Kho Sub chỉ chuyển trong cùng công ty (TRF). Cấm chuyển kho Sub sang kho của công ty khác.
- Chỉ ghi sổ khi ngày chứng từ thuộc kỳ Open. Chứng từ đã ghi sổ không sửa trực tiếp.
- Giá vốn: bình quân gia quyền cuối kỳ tháng theo Company × Item (mục 7 docs/design.md).
- Số lượng numeric(18,4), đơn giá numeric(19,6), tiền numeric(19,2). Không dùng double/float cho tiền, số lượng.
- Mọi thay đổi entity [Auditable] phải vào AuditLog qua SaveChangesInterceptor.

## Cách làm việc
- Mỗi tính năng: lập kế hoạch trước, viết test trước, chạy `dotnet test` trước khi báo xong.
- Làm từng lát nhỏ, commit sau mỗi lát chạy được.
- Giao diện tiếng Việt; tên code, bảng, cột bằng tiếng Anh.
- Không commit mật khẩu thật hay file .env.
  
