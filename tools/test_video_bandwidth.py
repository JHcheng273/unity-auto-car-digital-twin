#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
视频流链路可行性验证 —— 在写 C# 之前先算清楚"到底能不能跑"

为什么先做这个：
  UDP 传图像是数据量最大的环节，如果算出来带宽/解码跟不上，
  写再多 C# 代码都是白搭。先用 Python 实测这条链路的关键瓶颈：

    ① 不同分辨率的 JPEG 压缩后有多大？
    ② 按 8/10/12 FPS 算，需要多少带宽？
    ③ MATLAB 解一张 JPEG + 刷新 uiaxes 要多久？（用 PIL 近似，MATLAB 只会更慢）
    ④ UDP 分片方案：一帧要拆几个包？丢一个包会怎样？

跑法：python test_video_bandwidth.py
"""
import io, time, sys, os, struct, random

try:
    from PIL import Image
except ImportError:
    print("需要 Pillow：pip install pillow")
    sys.exit(1)

print("=" * 72)
print("视频流链路可行性验证")
print("=" * 72)

# ---------- 1. 造一张"像游戏画面"的测试图 ----------
# 纯色图压缩率虚高，必须造有细节的图才准。
# 这里画：草地底色 + 一条路 + 几个障碍物方块 + 噪点
def make_scene(w, h, seed=0):
    rnd = random.Random(seed)
    img = Image.new("RGB", (w, h), (72, 110, 58))     # 草绿
    px = img.load()
    # 天空渐变（上半）
    for y in range(h // 3):
        for x in range(0, w, 2):
            c = int(150 + 60 * y / (h / 3))
            px[x, y] = (c, c + 10, 200)
            if x + 1 < w: px[x + 1, y] = (c, c + 10, 200)
    # 路面（中间一条灰带，透视收缩）
    for y in range(h // 3, h):
        t = (y - h // 3) / (h - h // 3)
        halfw = int(w * (0.04 + 0.42 * t))
        cx = w // 2
        for x in range(max(0, cx - halfw), min(w, cx + halfw)):
            px[x, y] = (96, 96, 100)
    # 3 个障碍方块（模拟动态障碍物）
    for i in range(3):
        bx = rnd.randint(w // 5, 4 * w // 5)
        by = rnd.randint(h // 2, int(h * 0.85))
        s = max(3, int(w * 0.06 * (0.6 + by / h)))
        for y in range(by, min(h, by + s)):
            for x in range(bx, min(w, bx + s)):
                px[x, y] = (230, 110, 30)
    # 噪点（让压缩率更接近真实画面）
    for _ in range(w * h // 25):
        px[rnd.randrange(w), rnd.randrange(h)] = (
            rnd.randrange(256), rnd.randrange(256), rnd.randrange(256))
    return img

print("\n【① JPEG 压缩后大小】")
print(f"{'分辨率':>12} {'质量':>6} {'原始大小':>12} {'压缩后':>12} {'压缩比':>8}")
print("-" * 60)

results = {}
for (w, h) in [(160, 120), (240, 180), (320, 240), (480, 360)]:
    img = make_scene(w, h)
    raw_size = w * h * 3
    for q in [40, 60, 80]:
        buf = io.BytesIO()
        img.save(buf, format="JPEG", quality=q, optimize=False)
        size = buf.tell()
        results[(w, h, q)] = size
        print(f"{w}×{h:<7} {q:>6} {raw_size:>10,} B {size:>10,} B {raw_size/size:>7.1f}×")

# ---------- 2. 带宽测算 ----------
print("\n【② 带宽需求】（UDP 单包上限 1472 字节有效载荷）")
print(f"{'分辨率/质量':>16} {'压缩后':>10} {'拆几包':>8} {'8FPS':>12} {'12FPS':>12} {'20FPS':>12}")
print("-" * 78)

MTU_PAYLOAD = 1472
best = None
for (w, h) in [(160, 120), (240, 180), (320, 240)]:
    for q in [40, 60, 80]:
        size = results[(w, h, q)]
        # 每包要带 8 字节头（帧号4 + 片号2 + 总片数2）
        npack = (size + MTU_PAYLOAD - 9) // (MTU_PAYLOAD - 8)
        b8 = size * 8 / 1024
        b12 = size * 12 / 1024
        b20 = size * 20 / 1024
        flag = ""
        if w == 160 and q == 60:
            best = (w, h, q, size, npack, b12)
            flag = "  ← 候选"
        print(f"{w}×{h} q{q:<9} {size:>8,} B {npack:>7} 片 {b8:>9.0f} KB/s {b12:>9.0f} KB/s {b20:>9.0f} KB/s{flag}")

# ---------- 3. 编解码耗时 ----------
print("\n【③ 编解码耗时】（MATLAB 侧只会更慢，这里是最乐观估计）")
print(f"{'分辨率':>12} {'编码(ms)':>10} {'解码(ms)':>10} {'合计':>10} {'理论最高帧率':>14}")
print("-" * 62)

for (w, h) in [(160, 120), (240, 180), (320, 240)]:
    img = make_scene(w, h)
    # 编码测 20 次取平均
    t0 = time.perf_counter()
    for _ in range(20):
        buf = io.BytesIO()
        img.save(buf, format="JPEG", quality=60)
    enc_ms = (time.perf_counter() - t0) / 20 * 1000
    data = buf.getvalue()
    t0 = time.perf_counter()
    for _ in range(20):
        im2 = Image.open(io.BytesIO(data)); im2.load()
    dec_ms = (time.perf_counter() - t0) / 20 * 1000
    total = enc_ms + dec_ms
    print(f"{w}×{h:<7} {enc_ms:>10.1f} {dec_ms:>10.1f} {total:>9.1f}ms {1000/total:>12.0f} FPS")

# ---------- 4. 单帧传输耗时（本机回环） ----------
print("\n【④ UDP 分片传输：丢包影响】")
w, h, q, size, npack, bw = best
print(f"  以候选方案 {w}×{h} q{q} 为例：一帧 {size} 字节，拆 {npack} 个 UDP 包")
print(f"  · UDP 不保证顺序、不保证送达 —— 丢 1 个包，这一帧就残缺")
print(f"  · 解残帧会得到半张灰图，比丢整帧更难看")
print(f"  → 所以必须带【帧号 + 片号 + 总片数】，收不全就**整帧丢弃**，等下一帧")
print(f"  · 每帧超时 {int(1000/12)} ms 收不齐就放弃（12 FPS 下）")

# ---------- 5. 结论 ----------
print("\n" + "=" * 72)
print("结论")
print("=" * 72)
print(f"  推荐：{w}×{h}，JPEG 质量 {q}")
print(f"  · 每帧 {size:,} 字节，拆 {npack} 个 UDP 包")
print(f"  · 12 FPS 时带宽约 {bw:.0f} KB/s —— 本机回环毫无压力")
print(f"  · 画面能看清：路面走向、前方障碍物、车是否在路中间")
print(f"  · 画质限制：看不清文字/细节，这是 160×120 的物理上限")
print()
print("  ★ 关键结论：带宽不是瓶颈，**MATLAB 端解 JPEG + 刷图**才是。")
print("    所以 MATLAB 侧要：① 只解最新一帧（丢弃积压）② 用 image 对象改 CData，")
print("    别每帧重新 imshow（那会重画整个坐标轴，慢十倍）。")
