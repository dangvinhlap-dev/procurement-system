# Dữ liệu thử cho môi trường test

Chỉ dùng cho CSDL test – **không chạy trên CSDL thật**. Kế hoạch dữ liệu, tài khoản và kịch bản test: `docs/DuLieuThu_KichBanTest_0.14.0.docx`.

| File | Nội dung |
| --- | --- |
| `du-lieu.json` | Toàn bộ dữ liệu thử: công ty, kho, bộ phận, ĐVT, nhóm hàng, NCC, vật tư, tài khoản, quy trình duyệt PO |
| `tao-du-lieu-thu.mjs` | Script Node.js 22 (Windows/Linux) tạo dữ liệu qua API – đi qua mọi quy tắc nghiệp vụ; chạy lại không tạo trùng |

## Bước 1 – CSDL mới và bộ khởi tạo

Tạo CSDL trống, chạy các script `db-scripts` (docs/deploy-windows.md mục 3.2). Trong `app\appsettings.Production.json` đặt mục `Seed`
dưới đây (công ty, kho, ĐVT, nhóm hàng, nhóm NCC, đánh số danh mục, mở kỳ kho tháng hiện tại – do ứng dụng tạo lúc khởi động):

```json
{
  "Seed": {
    "Enabled": true,
    "AdminUserName": "admin",
    "AdminFullName": "Quản trị hệ thống",
    "AdminPassword": "<mật khẩu admin>",
    "HeadOffice": {
      "Code": "TCT",
      "Name": "Tổng công ty Cung ứng Thử nghiệm",
      "Warehouses": [
        {
          "Code": "KT",
          "Name": "Kho tổng Tổng công ty",
          "Type": "Main"
        }
      ]
    },
    "DemoCompanies": [
      {
        "Code": "APH",
        "Name": "Công ty TNHH An Phú (thành viên)",
        "Warehouses": [
          {
            "Code": "KT",
            "Name": "Kho tổng An Phú",
            "Type": "Main"
          },
          {
            "Code": "KP1",
            "Name": "Kho phụ xưởng cơ điện",
            "Type": "Sub"
          },
          {
            "Code": "KP2",
            "Name": "Kho phụ xưởng lắp ráp",
            "Type": "Sub"
          }
        ]
      },
      {
        "Code": "BMI",
        "Name": "Công ty CP Bình Minh (thành viên)",
        "Warehouses": [
          {
            "Code": "KT",
            "Name": "Kho tổng Bình Minh",
            "Type": "Main"
          },
          {
            "Code": "KP1",
            "Name": "Kho phụ xưởng cán",
            "Type": "Sub"
          },
          {
            "Code": "KP2",
            "Name": "Kho phụ xưởng sơn",
            "Type": "Sub"
          }
        ]
      }
    ],
    "DemoOpenCurrentPeriod": true,
    "DemoSupplierGroups": {
      "TN": "Trong nước",
      "NK": "Nhập khẩu",
      "NB": "Nội bộ"
    },
    "DemoCatalogNumberings": [
      {
        "Catalog": "supplier",
        "Prefix": "NCC",
        "Digits": 6,
        "From": 1,
        "To": 999999
      },
      {
        "Catalog": "supplier-group",
        "Prefix": "GN",
        "Digits": 4,
        "From": 1,
        "To": 9999
      },
      {
        "Catalog": "item-category",
        "Prefix": "NH",
        "Digits": 6,
        "From": 1,
        "To": 999999
      },
      {
        "Catalog": "item",
        "Prefix": "{NHOM}-",
        "Digits": 5,
        "From": 1,
        "To": 99999
      }
    ],
    "DemoUoms": {
      "CAI": "Cái",
      "BO": "Bộ",
      "DOI": "Đôi",
      "HOP": "Hộp",
      "THUNG": "Thùng",
      "KG": "Kg",
      "M": "Mét",
      "CUON": "Cuộn",
      "LIT": "Lít",
      "TAM": "Tấm",
      "RAM": "Ram",
      "CHAI": "Chai"
    },
    "DemoItemCategories": [
      {
        "Code": "VT",
        "Name": "Vật tư"
      },
      {
        "Code": "VTD",
        "Name": "Vật tư điện",
        "Parent": "VT"
      },
      {
        "Code": "VTC",
        "Name": "Vật tư cơ khí",
        "Parent": "VT"
      },
      {
        "Code": "BHLD",
        "Name": "Bảo hộ lao động"
      },
      {
        "Code": "VPP",
        "Name": "Văn phòng phẩm"
      },
      {
        "Code": "NVL",
        "Name": "Nguyên vật liệu"
      },
      {
        "Code": "HCH",
        "Name": "Hóa chất"
      }
    ]
  }
}
```

## Bước 2 – chạy ứng dụng nghe trong mạng nội bộ

Không viết cứng địa chỉ trong code: truyền lúc chạy, ví dụ nghe mọi địa chỉ ở cổng 5006 rồi mở tường lửa (PowerShell Administrator):

```powershell
.\app\Proc.Api.exe --urls http://0.0.0.0:5006
New-NetFirewallRule -DisplayName "Procurement test 5006" -Direction Inbound -Protocol TCP -LocalPort 5006 -Action Allow -Profile Private
```

Máy khác trong mạng mở `http://<IP máy chủ>:5006` (ví dụ http://192.168.1.199:5006).

## Bước 3 – tạo dữ liệu thử

```powershell
node .\tools\du-lieu-thu\tao-du-lieu-thu.mjs --url http://192.168.1.199:5006 --user admin --password <mật khẩu admin> --out ket-qua.json
```

`ket-qua.json` ghi mã NCC, mã vật tư đã sinh. Mật khẩu mọi tài khoản test: `Test@2026`.
