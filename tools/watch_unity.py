# -*- coding: utf-8 -*-
"""
实时监视：Unity 到底有没有在往 5005 发包？

原理：占住 MATLAB 的监听端口 5005，然后静止等待。
  - Unity 正在 Play 且桥接正常 → 每 50ms 就能收到一个状态包
  - Unity 没在 Play 或桥接没启起来 → 一个字也收不到

用法：
  python watch_unity.py            # 听 10 秒
  python watch_unity.py 20         # 听 20 秒

注意：跑之前先把 MATLAB 的仪表盘关掉，否则 5005 被它占着，这里绑不上。
"""
import json
import socket
import sys
import time

LISTEN_PORT = 5005
SECONDS = int(sys.argv[1]) if len(sys.argv) > 1 else 10

s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
try:
    s.bind(("0.0.0.0", LISTEN_PORT))
except OSError as e:
    print("[!] 绑不上 %d：%s" % (LISTEN_PORT, e))
    print("    端口被占着 —— 先把 MATLAB 的仪表盘图窗关掉，再运行本脚本。")
    sys.exit(1)

print("[*] 正在监听 UDP %d，等待 Unity 状态包…（最多 %d 秒）" % (LISTEN_PORT, SECONDS))
print("    现在回 Unity 按 ▶ Play 试试。\n")

s.settimeout(1.0)
t0 = time.time()
n = 0
first = None
last = None

while time.time() - t0 < SECONDS:
    try:
        data, addr = s.recvfrom(8192)
        n += 1
        if first is None:
            first = time.time()
        last = time.time()
        if n <= 3 or n % 20 == 0:
            txt = data.decode("utf-8", errors="replace")
            try:
                d = json.loads(txt)
                print("  #%d 来自 %s:%d  speed=%.2f  motor=%.2f  wp=%s  state=%s"
                      % (n, addr[0], addr[1], d.get("speed", -1),
                         d.get("motor", -1), d.get("wp"), d.get("state")))
            except ValueError:
                print("  #%d 收到非 JSON 数据：%s" % (n, txt[:80]))
    except socket.timeout:
        pass

print()
if n == 0:
    print("[X] %d 秒内一个包都没收到。" % SECONDS)
    print("    → Unity 没有在 Play，或者 Matlab Bridge 的 Enable Network 被关掉了。")
    print("    → 回 Unity 看 Console 有没有「[MatlabBridge] 已启动…」这一行。")
    sys.exit(2)

dt = last - first
rate = (n - 1) / dt if dt > 0 else 0
print("[OK] 收到 %d 个包，用时 %.1f 秒，约 %.1f 包/秒" % (n, dt, rate))
print("     Unity 侧通信完全正常 —— 问题不在 Unity，在 MATLAB 那边没起来。")
