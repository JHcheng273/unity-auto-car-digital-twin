#!/usr/bin/env bash
# 一次性推送脚本：把本地仓库推到 GitHub 并推送全部分支
#
# 用法（在 Git Bash 里跑）：
#   bash push_to_github.sh <你的GitHub用户名> [仓库名]
#   bash push_to_github.sh cheng
#   bash push_to_github.sh cheng my-car-project
#
# 前置条件（在浏览器做完这两步再来跑）：
#   1. https://github.com/new 建好空仓库（不要勾 README / .gitignore / license）
#   2. https://github.com/settings/ssh/new 添加了 SSH 公钥

set -e

USER="$1"
REPO="${2:-unity-auto-car-digital-twin}"

if [ -z "$USER" ]; then
  echo "用法：bash push_to_github.sh <GitHub用户名> [仓库名]"
  exit 1
fi

cd "$(dirname "$0")"

echo "== 目标：git@github.com:${USER}/${REPO}.git =="

# 先探一下 SSH 通不通
if ! ssh -o BatchMode=yes -o ConnectTimeout=15 -T git@github.com 2>&1 | grep -q "successfully authenticated\|Hi "; then
  echo ""
  echo "!! SSH 还没通。先去 https://github.com/settings/ssh/new 添加公钥："
  cat ~/.ssh/github_ed25519.pub 2>/dev/null || echo "   （公钥文件不在 ~/.ssh/github_ed25519.pub）"
  exit 1
fi

git remote remove origin 2>/dev/null || true
git remote add origin "git@github.com:${USER}/${REPO}.git"

git push -u origin main
for b in dev-A dev-B dev-C; do
  git push -u origin "$b"
done

git checkout dev-B

echo ""
echo "== 完成 =="
echo "   仓库：https://github.com/${USER}/${REPO}"
echo "   当前分支：$(git rev-parse --abbrev-ref HEAD)（成员 B 的分支）"
echo "   提示：确认推送成功后，本文件可以删掉"
