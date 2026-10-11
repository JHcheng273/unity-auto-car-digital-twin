"""
诊断 1：单射线检测为什么会"漏检"→ 车穿模
=========================================

现状：MyFrontDetector 从车头中心发**一条**射线（transform.forward）。
问题：障碍物只要不在车头正前方那条线上，射线就打不到 → HasObstacle=false
      → AvoidanceDriver 完全不知道有障碍 → 车直接开过去 → 穿模。

本脚本量化"漏检角度"有多大。
"""
import math

# 车/障碍物的尺寸（来自 ObstacleManager 默认值）
CAR_HALF_WIDTH = 0.85      # 车宽 1.7 的一半（Learn 版 Cube 约 1.7 宽）
OBS_HALF_WIDTH = 0.8       # boxWidth 1.6 的一半
EMERGENCY = 4.0            # AvoidanceDriver.emergencyDistance

def ray_hits_obstacle(dist_car_to_obs, lateral_offset, obs_half=OBS_HALF_WIDTH):
    """
    单条射线（沿 +Z 车头方向）能否打到障碍物？
    障碍物简化成：圆心在 (lateral_offset, dist_car_to_obs)、半径 obs_half 的圆。
    射线是 x=0 的正 Z 轴。
    返回 True/False。
    """
    # 射线到障碍物圆心的垂距就是 |lateral_offset|
    return abs(lateral_offset) <= obs_half

print("=" * 68)
print("单射线检测的「漏检横向偏移」临界值")
print("=" * 68)
print(f"障碍物半宽 = {OBS_HALF_WIDTH} m  →  横向偏移超过 {OBS_HALF_WIDTH} m 就打不到")
print()

print("=" * 68)
print("真车宽 vs 单射线覆盖 —— 这才是穿模的核心")
print("=" * 68)
# 车往前走，车体本身有宽度。只要障碍物和车体有交叠，就算"挡路"。
# 但单射线只能覆盖 ±obs_half 这一条窄缝。
cover = 2 * OBS_HALF_WIDTH           # 射线能覆盖的横向宽度
body  = 2 * (CAR_HALF_WIDTH + OBS_HALF_WIDTH)   # 真正构成碰撞的横向宽度
print(f"单射线能覆盖的横向宽度 : {cover:.2f} m")
print(f"实际会撞上的横向宽度   : {body:.2f} m （车半宽{CAR_HALF_WIDTH} + 障碍半宽{OBS_HALF_WIDTH}）")
print(f"→ 盲区占比             : {100*(1-cover/body):.0f}%   ← 就是说有一半多的碰撞场景射线看不见")
print()

print("=" * 68)
print("具体例子：障碍物横向偏 1.2 m（车头没正对，但车体会撞上）")
print("=" * 68)
for lat in [0.0, 0.5, 0.8, 0.9, 1.2, 1.5]:
    hit = ray_hits_obstacle(5.0, lat)
    collide = abs(lat) < (CAR_HALF_WIDTH + OBS_HALF_WIDTH)
    verdict = "射线能打到 OK" if hit else "★ 射线打不到 —— 但车会撞上！" if collide else "擦不到，无所谓"
    print(f"  横向偏移 {lat:.1f} m → {verdict}")
print()

print("=" * 68)
print("另一个漏检来源：障碍物「突然出现」在急停距离内")
print("=" * 68)
print("ObstacleManager.SpawnRandom 的生成距离：spawnMinAhead = 6.0 m")
print(f"AvoidanceDriver 的急停距离      ：emergencyDistance = {EMERGENCY} m")
print()
print(f"  生成距离 6.0 m > 急停距离 {EMERGENCY} m → 看起来够")
print("  但！生成是**横向 ±3.5 m 随机 + 强制至少偏 1.5 m**，")
print("  也就是说障碍物**天生就不会出现在车头正前方**：")
print("     if (|side| < 1.5) side = ±1.5    ← ObstacleManager.cs L129")
print("  后果：O 键按下去，障碍物永远偏在侧面 1.5~3.5 m，")
print("        单射线（±0.8 m）**永远打不到** → 车完全无视它 → 穿模。")
print()
print("=" * 68)
print("结论")
print("=" * 68)
print("① 检测要从「单条细射线」升级成「多射线扇形 + 最近命中」")
print("   至少覆盖车身全宽（±1.65 m），最好再宽一点留余量。")
print("② ObstacleManager 不该强制把障碍物推到 1.5 m 以外 ——")
print("   那样正好落在检测盲区里。应该允许挡住车的正前方。")
print("③ 新增障碍物后，**所有状态**都要立刻响应（现在只有 Detouring 查 Dirty）。")
