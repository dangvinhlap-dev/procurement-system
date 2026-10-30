#!/bin/sh
# Sao lưu CSDL mỗi ngày lúc BACKUP_TIME (giờ Việt Nam) vào /backups, giữ BACKUP_KEEP_DAYS ngày gần nhất.
# Chạy trong container "backup" của docker-compose.yml; thông tin kết nối lấy từ PGHOST/PGUSER/PGPASSWORD/PGDATABASE.
# Khôi phục: pg_restore --clean --if-exists -d <database> <file.dump> (README.md mục "Triển khai server").
set -eu

: "${BACKUP_TIME:=01:00}"
: "${BACKUP_KEEP_DAYS:=14}"

backup_once() {
  file="/backups/${PGDATABASE}_$(date +%Y%m%d_%H%M%S).dump"
  # Ghi ra file tạm rồi mới đổi tên: bản sao lưu dở dang (mất điện, lỗi) không bị nhầm là bản tốt.
  if pg_dump --format=custom --file="$file.part"; then
    mv "$file.part" "$file"
    echo "$(date '+%d/%m/%Y %H:%M') Đã sao lưu $file"
  else
    rm -f "$file.part"
    echo "$(date '+%d/%m/%Y %H:%M') LỖI sao lưu CSDL $PGDATABASE" >&2
  fi
  find /backups -name "${PGDATABASE}_*.dump" -mtime +"$BACKUP_KEEP_DAYS" -delete
}

while true; do
  now=$(date +%s)
  next=$(date -d "$BACKUP_TIME" +%s)
  [ "$next" -le "$now" ] && next=$(date -d "tomorrow $BACKUP_TIME" +%s)
  sleep $((next - now))
  backup_once
done
