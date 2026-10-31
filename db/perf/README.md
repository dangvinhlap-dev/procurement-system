# Đo hiệu năng (design.md mục 14.6)

Chỉ chạy trên CSDL riêng `procurement_perf` – **không** chạy trên CSDL dev, test hay chạy thật.

1. Tạo CSDL đo từ bản sao lưu dev rồi sinh dữ liệu (BHA: 3.000 mặt hàng, 200 NCC, 20.000 phiếu nhập × 5 dòng,
   10.000 phiếu chuyển kho × 5 dòng → 200.000 dòng sổ kho, kỳ 10/2026):

   ```powershell
   $env:PGPASSWORD='proc_dev_only'; $env:PGCLIENTENCODING='UTF8'
   pg_dump -h localhost -U proc -F c -f procurement_perf_nguon.dump procurement
   createdb -h localhost -U proc procurement_perf
   pg_restore -h localhost -U proc -d procurement_perf --no-owner procurement_perf_nguon.dump
   psql -h localhost -U proc -d procurement_perf -f db\perf\seed.sql
   createdb -h localhost -U proc -T procurement_perf procurement_perf_mau   # bản mẫu để đặt lại sau khi đo đóng kỳ
   ```

2. Chạy API nối CSDL đo ở cổng 5001 (chép thư mục `bin` ra chỗ khác để không khóa file khi build):

   ```powershell
   $env:ASPNETCORE_ENVIRONMENT='Development'
   $env:ConnectionStrings__Procurement='Host=localhost;Port=5432;Database=procurement_perf;Username=proc;Password=proc_dev_only'
   dotnet <thư-mục-bản-chép>\Proc.Api.dll --urls http://localhost:5001
   ```

3. Đo (Git Bash): `bash db/perf/bench.sh` – mỗi API 1 lần làm nóng + 3 lần đo (ms).
   Đo đóng kỳ: `bash db/perf/close.sh` (làm thay đổi dữ liệu – đặt lại bằng
   `dropdb procurement_perf` rồi `createdb -T procurement_perf_mau procurement_perf`).
