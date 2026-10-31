#!/usr/bin/env bash
# Đo thời gian các API nặng trên CSDL procurement_perf (API ở cổng 5001). Mỗi API gọi 1 lần làm nóng + 3 lần đo, in thời gian (ms).
BASE=http://localhost:5001
for i in $(seq 1 60); do curl -s "$BASE/health" >/dev/null && break; sleep 2; done
LOGIN=$(curl -s -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' -d '{"userName":"admin","password":"Admin@2026"}')
TOKEN=$(echo "$LOGIN" | python -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
BHA=$(echo "$LOGIN" | python -c "import sys,json; print([c['id'] for c in json.load(sys.stdin)['user']['companies'] if c['code']=='BHA'][0])")
H=(-H "Authorization: Bearer $TOKEN" -H "X-Company-Id: $BHA")
ITEM=$(PGPASSWORD=proc_dev_only psql -h localhost -U proc -d procurement_perf -tAc "select \"Id\" from \"Item\" where \"Code\"='PERF-00042'")
PERIOD=$(PGPASSWORD=proc_dev_only psql -h localhost -U proc -d procurement_perf -tAc "select p.\"Id\" from \"InventoryPeriod\" p join \"Company\" c on c.\"Id\"=p.\"CompanyId\" where c.\"Code\"='BHA' and p.\"Month\"=10")
run() {
  local name=$1 url=$2
  curl -s -o /dev/null "${H[@]}" "$BASE$url"
  local times=()
  for k in 1 2 3; do
    times+=("$(curl -s -o /dev/null -w '%{time_total}' "${H[@]}" "$BASE$url" | python -c "import sys; print(round(float(sys.stdin.read())*1000))")")
  done
  local code=$(curl -s -o /dev/null -w '%{http_code}' "${H[@]}" "$BASE$url")
  printf '%-28s %s  %s ms\n' "$name" "$code" "${times[*]}"
}
run "Tồn kho hiện tại"       "/api/stock-on-hand"
run "Thẻ kho 1 mặt hàng"     "/api/stock-card?itemId=$ITEM&from=2026-10-01&to=2026-10-31"
run "NXT kỳ 10/2026"         "/api/reports/stock-movement?periodId=$PERIOD"
run "Chậm luân chuyển"       "/api/reports/slow-moving?days=1"
run "Sắp hết hạn"            "/api/reports/expiring?days=30"
run "Danh sách phiếu nhập"   "/api/inventory-documents?docType=GRN&from=2026-10-01&to=2026-10-31"
run "So sánh giá 1 mặt hàng" "/api/price-comparison?itemId=$ITEM"
run "Mua hàng theo NCC"      "/api/reports/purchase-spend?from=2026-10-01&to=2026-10-31&groupBy=supplier"
run "Đánh giá NCC"           "/api/reports/supplier-evaluation?from=2026-10-01&to=2026-10-31"
run "Trang chủ"              "/api/dashboard"
run "Xuất kế toán – danh sách" "/api/accounting-export/documents?from=2026-10-01&to=2026-10-07&docTypes=GRN"
run "Xuất kế toán – file"    "/api/accounting-export/file?from=2026-10-01&to=2026-10-07"
