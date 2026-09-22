#!/usr/bin/env bash
# 一次性推送脚本（Gitee 版）：建仓库 + 推送全部分支
#
# 用法：
#   bash push_to_gitee.sh <你的Gitee私人令牌> [仓库名]
#
# 令牌申请：https://gitee.com/profile/personal_access_tokens
#           权限勾选 projects（或全勾），生成后复制那串 32 位字符串
#
# 为什么有这个脚本：本机连 github.com 不稳定（POST 常被掐），
# 但 gitee.com 和它的 API 完全通，所以 Gitee 是更省事的选择。

set -e

TOKEN="$1"
REPO="${2:-unity-auto-car-digital-twin}"

if [ -z "$TOKEN" ]; then
  echo "用法：bash push_to_gitee.sh <Gitee私人令牌> [仓库名]"
  echo "令牌申请页：https://gitee.com/profile/personal_access_tokens"
  exit 1
fi

cd "$(dirname "$0")"

echo "== 1/4 获取用户信息 =="
USER=$(curl -sS --max-time 30 "https://gitee.com/api/v5/user?access_token=${TOKEN}" \
       | grep -o '"login":"[^"]*"' | head -1 | cut -d'"' -f4)
if [ -z "$USER" ]; then
  echo "!! 令牌无效或已过期，取不到用户名"
  exit 1
fi
echo "   用户名：${USER}"

echo "== 2/4 创建仓库 ${REPO} =="
curl -sS --max-time 30 -X POST "https://gitee.com/api/v5/user/repos" \
  -d "access_token=${TOKEN}" \
  -d "name=${REPO}" \
  -d "description=Unity 自动驾驶小车数字孪生（三人课设）" \
  -d "private=true" \
  -d "auto_init=false" > /dev/null
echo "   已创建（私库）"

echo "== 3/4 推送 main =="
git remote remove origin 2>/dev/null || true
git remote add origin "https://${USER}:${TOKEN}@gitee.com/${USER}/${REPO}.git"
git push -u origin main

echo "== 4/4 推送 dev-A / dev-B / dev-C =="
for b in dev-A dev-B dev-C; do
  git push -u origin "$b"
done

# 把带令牌的远程地址换成干净的，避免令牌长期留在配置里
git remote set-url origin "https://gitee.com/${USER}/${REPO}.git"
git checkout dev-B

echo ""
echo "== 完成 =="
echo "   仓库：https://gitee.com/${USER}/${REPO}"
echo "   当前分支：$(git rev-parse --abbrev-ref HEAD)（成员 B 的分支）"
echo "   注意：令牌已从 remote 地址中移除；确认推送成功后本文件可删"
