"""
诊断 3：逐行复刻新的 5 射线检测逻辑，验证它真的不漏检
======================================================
Unity 编译要等窗口焦点，反馈慢。检测是纯几何问题 ——
用 Python 把 MyFrontDetector 的逻辑复刻一遍，直接跑测试。

复刻的规则（严格照 C# 代码）：
  · 5 条射线，间距 0.8，横向位置 = (i - 2) * 0.8  → [-1.6,-0.8,0,0.8,1.6]
  · 每条射线从 (offset, 0) 沿 +Z 走
  · 障碍物 = 圆，圆心 (obs_x, obs_z)，半径 0.8
  · 取所有命中里**最近**的当 ObstacleDistance
  · LateralOffset = 命中那条射线的 offset
"""
import math

SPACING = 0.8
NRAYS   = 5
OBS_R   = 0.8
RANGE   = 20.0
CAR_HALF = 0.8
TRIGGER = 5.0

OFFSETS = [(i - (NRAYS - 1) / 2.0) * SPACING for i in range(NRAYS)]


def ray_vs_circle(offset, cx, cz, r):
    """射线 (x=offset, 沿 +Z) 打圆 (cx,cz,r)。返回命中距离或 None"""
    # 射线到圆心的最近点：把圆心投影到射线上
    t = cz                       # 因为射线沿 +Z，参数就是 z
    if t < 0:
        return None
    dx = offset - cx
    dist_sq = dx * dx            # 最近距离（在 z=cz 处）
    if dist_sq > r * r:
        return None
    # 命中点 z 要先于这个投影点（射线从 z=0 出发）
    half = math.sqrt(r * r - dist_sq)
    hit_z = cz - half
    if hit_z < 0:
        return None
    return hit_z


def detect(car_x, car_z, car_yaw_deg, obs_list):
    """
    返回 (has_obs, min_dist, lat_offset, hit_ray_idx)
    car_yaw: 0 = 朝 +Z, 90 = 朝 +X （和 Unity 一致）
    obs_list: [(x, z)] 世界坐标
    """
    best = RANGE
    best_i = -1
    hits = 0

    yaw = math.radians(car_yaw_deg)
    fx, fz = math.sin(yaw), math.cos(yaw)      # 车头方向
    rx, rz = math.cos(yaw), -math.sin(yaw)     # 车右侧

    for ox, oz in obs_list:
        # 转到车体坐标系
        dx, dz = ox - car_x, oz - car_z
        lx = dx * rx + dz * rz      # 横向（右为正）
        lz = dx * fx + dz * fz      # 纵向（前为正）
        if lz <= 0:
            continue                # 在车后面

        for i, off in enumerate(OFFSETS):
            d = ray_vs_circle(off, lx, lz, OBS_R)
            if d is None:
                continue
            hits += 1
            if d < best:
                best, best_i = d, i

    lat = OFFSETS[best_i] if best_i >= 0 else 0.0
    return (best_i >= 0 and best <= TRIGGER), best, lat, best_i


def old_detect(car_x, car_z, car_yaw_deg, obs_list):
    """旧版单射线，用来对比"""
    yaw = math.radians(car_yaw_deg)
    fx, fz = math.sin(yaw), math.cos(yaw)
    rx, rz = math.cos(yaw), -math.sin(yaw)
    best = RANGE
    for ox, oz in obs_list:
        dx, dz = ox - car_x, oz - car_z
        lx = dx * rx + dz * rz
        lz = dx * fx + dz * fz
        if lz <= 0:
            continue
        d = ray_vs_circle(0.0, lx, lz, OBS_R)
        if d is not None and d < best:
            best = d
    return best < RANGE, best


print("=" * 74)
print("测试 1：障碍物在正前方不同横向偏移 —— 新旧对比")
print("=" * 74)
print(f"{'横向偏移':>10} {'旧(单射线)':>14} {'新(5射线)':>14} {'会撞?':>10}")
print("-" * 74)
fail_old = 0
fail_new = 0
for lat in [0.0, 0.4, 0.8, 1.0, 1.2, 1.4, 1.6, 2.0, 2.4, 2.6, 3.0]:
    obs = [(lat, 3.5)]     # 车在原点朝 +Z，障碍物在正前方 3.5 米、偏 lat（在触发距离 5m 内）
    oh, od = old_detect(0, 0, 0, obs)
    nh, nd, nlat, ni = detect(0, 0, 0, obs)
    will_hit = abs(lat) < (CAR_HALF + OBS_R)
    if will_hit and not oh: fail_old += 1
    if will_hit and not nh: fail_new += 1
    print(f"{lat:>9.1f}m {('看到' if oh else '★漏检'):>14} "
          f"{('看到 d=%.1f' % nd) if nh else '★漏检':>14} "
          f"{('会' if will_hit else '不会'):>10}")
print()
print(f"  「会撞上但漏检」的次数 →  旧: {fail_old}   新: {fail_new}")

print()
print("=" * 74)
print("测试 2：模拟真实按 O 键 —— 8 米前方横向随机 ±3.5 米")
print("=" * 74)
import random
random.seed(42)
miss_old = miss_new = collide_cases = 0
for _ in range(200):
    lat = random.uniform(-3.5, 3.5)
    obs = [(lat, 3.5)]
    will_hit = abs(lat) < (CAR_HALF + OBS_R)
    if not will_hit:
        continue
    collide_cases += 1
    oh, _ = old_detect(0, 0, 0, obs)
    nh, _, _, _ = detect(0, 0, 0, obs)
    if not oh: miss_old += 1
    if not nh: miss_new += 1
print(f"  会撞上的场景共 {collide_cases} 个")
print(f"  旧版漏检 {miss_old} 个（{100*miss_old/max(collide_cases,1):.0f}%）")
print(f"  新版漏检 {miss_new} 个（{100*miss_new/max(collide_cases,1):.0f}%）")

print()
print("=" * 74)
print("测试 3：车斜着开（yaw 变化）时还能不能检测到")
print("=" * 74)
for yaw in [0, 15, 30, 45, -20, -40]:
    # 障碍物放在"车正前方 3.5 米"的位置（跟着车的朝向走）
    yr = math.radians(yaw)
    ox = 0 + math.sin(yr) * 3.5
    oz = 0 + math.cos(yr) * 3.5
    nh, nd, nlat, ni = detect(0, 0, yaw, [(ox, oz)])
    print(f"  yaw={yaw:>4}° 正前方 3.5 米 → {'✔ 看到' if nh else '✘ 漏检'} "
          f"(d={nd:.1f}, 横向={nlat:+.1f}, 第{ni}条)")

print()
print("=" * 74)
print("测试 4：LateralOffset 准不准（绕障要靠它决定往哪边躲）")
print("=" * 74)
print(f"{'真实横向':>10} {'检测横向':>10} {'误差':>8}")
print("-" * 74)
for lat in [-1.6, -1.0, -0.5, 0.0, 0.5, 1.0, 1.6]:
    nh, nd, nlat, ni = detect(0, 0, 0, [(lat, 3.5)])
    print(f"{lat:>9.1f}m {nlat:>9.1f}m {abs(nlat-lat):>7.1f}m")

print()
print("=" * 74)
print("测试 5：两个障碍物，取最近的")
print("=" * 74)
obs = [(-1.0, 6.0), (0.8, 3.0)]     # 近的在右边 0.8（3米），远的在左边（6米）
nh, nd, nlat, ni = detect(0, 0, 0, obs)
# 障碍 B 中心 3 米、半径 0.8 → 射线命中约 2.2 米
expect = 3.0 - OBS_R
ok5 = abs(nd - expect) < 0.6 and abs(nlat - 0.8) < 0.01
print(f"  障碍A 偏左1.0 距离6米 / 障碍B 偏右0.8 距离3米")
print(f"  → 检测 d={nd:.1f}m（期望≈{expect:.1f}），横向={nlat:+.1f}（期望 +0.8）"
      f"  {'✔ 正确取了近的' if ok5 else '✘ 取错了'}")
print()
print("  ★ 注意：检测到的距离是**射线命中点**的距离，不是障碍物中心的距离。")
print(f"     障碍中心 3.0 米、半径 {OBS_R} → 命中点约 {expect:.1f} 米。这是对的。")
