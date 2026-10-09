# -*- coding: utf-8 -*-
"""
链路自检：假装自己是 Unity，往 MATLAB 的监听端口发一个状态包，
看 MATLAB 会不会把控制指令回发到 Unity 的监听端口。

用途：当仪表盘显示"无连接"时，用它区分两种完全不同的故障：
  A. 收到了回包 → 端口链路是通的，问题在"启动顺序"或"Unity 没在 Play"
  B. 超时没回包 → MATLAB 脚本没在跑（或没在监听），是真断了

用法：
  python probe_link.py            # 默认探测 127.0.0.1:5005 / 5006
  python probe_link.py 5005 5006  # 自定义端口
"""
import json
import socket
import sys

MATLAB_PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 5005
UNITY_PORT = int(sys.argv[2]) if len(sys.argv) > 2 else 5006

# 和 Unity 侧 UnityStatePacket 字段名一一对应
STATE = {
    "t": 1.0, "x": 0.0, "y": 0.0, "z": 0.0, "yaw": 0.0,
    "speed": 1.5, "steer": 0.0, "front_dist": 20.0,
    "wp": 0, "lap": 0, "has_front": False,
    "motor": 0.3, "braking": False, "state": 0,
    "accept": True, "max_speed": 5.0,
}


def main():
    # 1. 先占住 Unity 的接收端口，等 MATLAB 回话
    recv = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    recv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    try:
        recv.bind(("127.0.0.1", UNITY_PORT))
    except OSError as e:
        print("[!] 绑不上 %d：%s" % (UNITY_PORT, e))
        print("    Unity 正在 Play 的话，这个端口被它占着 —— 先停掉 Unity 再测。")
        return 1
    recv.settimeout(4.0)

    # 2. 发一个状态包给 MATLAB
    send = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    payload = json.dumps(STATE, ensure_ascii=False).encode("utf-8")
    send.sendto(payload, ("127.0.0.1", MATLAB_PORT))
    print("[>] 已向 127.0.0.1:%d 发送状态包（%d 字节）" % (MATLAB_PORT, len(payload)))

    # 3. 等 MATLAB 的控制指令
    try:
        data, addr = recv.recvfrom(4096)
    except socket.timeout:
        print("[X] 超时 4 秒，没收到任何回包。")
        print("    → MATLAB 的 unity_dashboard 没有在监听 %d。" % MATLAB_PORT)
        print("    → 检查：MATLAB 命令窗口是否还在跑？窗口是不是被关了？")
        return 2

    text = data.decode("utf-8", errors="replace")
    print("[<] 收到来自 %s:%d 的回包：" % addr)
    print("    " + text)
    try:
        cmd = json.loads(text)
        print("[OK] 链路完全正常。MATLAB 要求：")
        print("     期望速度 v_des = %s m/s" % cmd.get("v_des"))
        print("     期望转向 steer_des = %s （999 = 交给 Unity）" % cmd.get("steer_des"))
        print("     刹车 brake = %s" % cmd.get("brake"))
        print("     序号 seq = %s" % cmd.get("seq"))
    except ValueError:
        print("[OK] 收到回包，但 JSON 解析失败（内容见上）。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
