# Image chạy server: một image chứa API .NET 10 + giao diện React đã build (design.md mục 14.1).
# Build: docker build -t proc-app:<phiên-bản> .   (chạy tại thư mục gốc repo)

# --- Bước 1: build giao diện React ---
FROM node:22-alpine AS web
WORKDIR /src
# vite.config.ts đọc số phiên bản từ Directory.Build.props ở gốc repo để hiện lên thanh trạng thái.
COPY Directory.Build.props ./
COPY src/Proc.Web/package.json src/Proc.Web/package-lock.json src/Proc.Web/
RUN cd src/Proc.Web && npm ci --no-audit --no-fund
COPY src/Proc.Web/ src/Proc.Web/
RUN cd src/Proc.Web && npm run build

# --- Bước 2: build API ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Proc.Domain/Proc.Domain.csproj src/Proc.Domain/
COPY src/Proc.Application/Proc.Application.csproj src/Proc.Application/
COPY src/Proc.Infrastructure/Proc.Infrastructure.csproj src/Proc.Infrastructure/
COPY src/Proc.Api/Proc.Api.csproj src/Proc.Api/
# Restore riêng trước khi chép mã nguồn: đổi code không phải tải lại gói NuGet.
RUN dotnet restore src/Proc.Api/Proc.Api.csproj
COPY src/Proc.Domain/ src/Proc.Domain/
COPY src/Proc.Application/ src/Proc.Application/
COPY src/Proc.Infrastructure/ src/Proc.Infrastructure/
COPY src/Proc.Api/ src/Proc.Api/
RUN dotnet publish src/Proc.Api/Proc.Api.csproj -c Release -o /app --no-restore

# --- Bước 3: image chạy ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Npgsql nạp thư viện GSSAPI khi kết nối; image .NET không có sẵn nên log báo lỗi "libgssapi_krb5.so.2" mỗi lần khởi động.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=api /app ./
# API tự phục vụ giao diện khi có wwwroot/index.html (Program.cs).
COPY --from=web /src/src/Proc.Web/dist ./wwwroot
# Image .NET mặc định nghe cổng 8080 (ASPNETCORE_HTTP_PORTS); Caddy chuyển tiếp vào cổng này.
ENV ASPNETCORE_ENVIRONMENT=Production \
    TZ=Asia/Ho_Chi_Minh
EXPOSE 8080
# Tạo sẵn thư mục khóa Data Protection thuộc user app: volume mới gắn vào đây nhận quyền sở hữu này, app ghi được.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
# Chạy bằng user không phải root có sẵn trong image .NET.
USER app
ENTRYPOINT ["dotnet", "Proc.Api.dll"]
