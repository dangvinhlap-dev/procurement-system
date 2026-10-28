#!/usr/bin/env bash
# Chạy một lần khi Codespace được tạo.
set -e

echo ">> Cài Claude Code"
curl -fsSL https://claude.ai/install.sh | bash
if ! grep -q '.local/bin' ~/.bashrc; then
  echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.bashrc
fi

echo ">> Cài công cụ EF Core migrations"
dotnet tool install --global dotnet-ef || dotnet tool update --global dotnet-ef
if ! grep -q '.dotnet/tools' ~/.bashrc; then
  echo 'export PATH="$HOME/.dotnet/tools:$PATH"' >> ~/.bashrc
fi

echo ">> Kiểm tra phiên bản"
dotnet --version
node --version
docker --version

echo ">> Xong. Mở terminal mới rồi gõ: claude"
