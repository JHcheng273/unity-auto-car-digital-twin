"""
诊断 2：多射线扇形检测的覆盖率验证
====================================
目标：设计一组射线，保证"凡是车体真会撞上的障碍物，一定能被检测到"。

车：宽 1.60 m（半宽 0.80）
障碍物：DynObstacle 默认 1.6 x 1.2，外接圆半径 = max(1.6,1.2)/2 = 0.80

碰撞判据：障碍物中心到车头中线的横向距离 < (车半宽 + 障碍半径) = 1.60 m
         （这才是"车体会碰到"的真实宽度）
"""
import math

CAR_HALF = 0.80
OBS_R    = 0.80
COLLIDE_LAT = CAR_HALF + OBS_R      # 1.60 m

def fan_coverage(offsets, max_dist, probe_dist=1.0):
    """
    在车头前方 probe_dist 米处横向排一串射线起点（offsets 是横向偏移列表），
    每条射线走 +Z。问：横向距离多远内的障碍物能被某条射线打到？

    第 i 条射线位于 x = offsets[i]，能打到 |lat - offsets[i]| <= OBS_R 的障碍。
    所以总覆盖 = [min(off)-OBS_R, max(off)+OBS_R]
    """
    lo = min(offsets) - OBS_R
    hi = max(offsets) + OBS_R
    return lo, hi

def report(name, offsets):
    lo, hi = fan_coverage(offsets, 20)
    covered = hi - lo
    need = 2 * COLLIDE_LAT
    print(f"{name}")
    print(f"   射线横向位置: {[round(o,2) for o in offsets]}")
    print(f"   覆盖横向区间: [{lo:+.2f}, {hi:+.2f}]  总宽 {covered:.2f} m")
    print(f"   需要覆盖    : ±{COLLIDE_LAT:.2f} m（总宽 {need:.2f} m）")
    ok = lo <= -COLLIDE_LAT and hi >= COLLIDE_LIT if False else (lo <= -COLLIDE_LAT and hi >= COLLIDE_LAT)
    print(f"   → {'✔ 全覆盖，不会漏检' if ok else '✘ 有盲区，还会穿模'}")
    print()
    return ok

print("=" * 70)
print("碰撞需要的横向覆盖宽度")
print("=" * 70)
print(f"  车半宽 {CAR_HALF} + 障碍半径 {OBS_R} = ±{COLLIDE_LAT:.2f} m")
print(f"  （原来的单射线只覆盖 ±{OBS_R:.2f} m → 盲区 {100*(1-OBS_R/COLLIDE_LAT):.0f}%）")
print()

print("=" * 70)
print("方案对比")
print("=" * 70)
report("【现状】单条射线 offsets=[0]",
       [0.0])

report("【方案A】3 条射线：中 + 左右 0.8",
       [-0.8, 0.0, 0.8])

report("【方案B】5 条射线：间距 0.8",
       [-1.6, -0.8, 0.0, 0.8, 1.6])

report("【方案C】7 条射线：间距 0.53",
       [-1.6, -1.07, -0.53, 0.0, 0.53, 1.07, 1.6])

print("=" * 70)
print("推荐：方案 B（5 条，间距 0.8 m）")
print("=" * 70)
print("  覆盖 ±2.40 m，比需要的 ±1.60 m 还多留 0.8 m 余量。")
print("  5 条射线的开销可以忽略（一次 Physics.Raycast 才几微秒）。")
print()
print("  ★ 关键设计：多条射线里取**最近的那个命中**当 ObstacleDistance，")
print("    并且记住命中的是左/中/右哪一条 —— 绕障时可以直接知道该往哪边躲。")
print()

print("=" * 70)
print("另外：还要按「射线的横向位置」算出「障碍物横向偏移」")
print("=" * 70)
print("  单射线做不到这件事；多射线可以：")
print("    命中第 i 条射线 → 障碍物横向大致在 offsets[i] ± OBS_R")
print("  这对 AvoidanceDriver.IsBlockingPath() 很有用 ——")
print("  它可以更准确地判断'挡没挡在路线上'，而不是靠'+ObstacleDistance 处取点'猜。")
