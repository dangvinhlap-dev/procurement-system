#!/usr/bin/env bash
# Đo thời gian đóng kỳ 10/2026 của BHA trên CSDL procurement_perf (API cổng 5001). Đóng kỳ làm thay đổi dữ liệu – đặt lại CSDL từ
# procurement_perf_mau trước mỗi lần đo.
BASE=http://localhost:5001
for i in $(seq 1 60); do curl -s "$BASE/health" >/dev/null && break; sleep 2; done
LOGIN=$(curl -s -X POST "$BASE/api/auth/login" -H 'Content-Type: application/json' -d '{"userName":"admin","password":"Admin@2026"}')
TOKEN=$(echo "$LOGIN" | python -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
BHA=$(echo "$LOGIN" | python -c "import sys,json; print([c['id'] for c in json.load(sys.stdin)['user']['companies'] if c['code']=='BHA'][0])")
PERIOD=$(PGPASSWORD=proc_dev_only psql -h localhost -U proc -d procurement_perf -tAc "select p.\"Id\" from \"InventoryPeriod\" p join \"Company\" c on c.\"Id\"=p.\"CompanyId\" where c.\"Code\"='BHA' and p.\"Month\"=10")
curl -s -o /tmp/close.json -w 'Đóng kỳ 10/2026: HTTP %{http_code}, %{time_total} giây\n' --max-time 900 -X POST \
  -H "Authorization: Bearer $TOKEN" -H "X-Company-Id: $BHA" "$BASE/api/inventory-periods/$PERIOD/close"
head -c 300 /tmp/close.json 2>/dev/null; echo
