"""
路口过路口点（pass point）几何离线校验
======================================
【为什么测这个】
  "让车保持在路面上行驶"的核心是 GetRoutePoint()：不瞄路口正中心，
  而是瞄「进/出方向角平分线」上往前推的一个点。
  这套东西全是**向量运算**，符号写反了、除法除到零了，
  在 Unity 里表现为"车突然在原地打转"或者"直接冲进草丛"——
  但看代码看不出来。用 Python 复刻一遍，直接验算。

复刻的是 AutoDrive.cs 的 GetRoutePoint()。
"""

import math

# ---------------------------------------------------------------- 路口坐标（抄 RoadNetwork.cs 的 FillDefaultNetwork）
J = {
    "R":  (  8.49,  -3.10),   # 0
    "T":  ( -1.51,   6.90),   # 1
    "L":  (-11.51,  -3.10),   # 2
    "B":  ( -1.51, -13.10),   # 3
    "S":  (  1.00,  12.00),   # 4
    "O1": ( 14.00,  10.00),   # 5
    "O2": (-14.00,  10.00),   # 6
    "O3": (-14.00, -19.00),   # 7
    "O4": ( 14.00, -19.00),   # 8
    "E1": (  1.00,  22.00),   # 9
    "E2": ( 22.00,  -3.00),   # 10
    "E3": (-22.00,  -3.00),   # 11
    "E4": (  1.00, -26.00),   # 12
}
IDX = ["R", "T", "L", "B", "S", "O1", "O2", "O3", "O4", "E1", "E2", "E3", "E4"]

PASS_AHEAD = 3.0     # 和 AutoDrive.passAhead 默认值一致


def pos(i):
    return J[IDX[i]]


def vsub(a, b):
    return (a[0] - b[0], a[1] - b[1])


def vlen(v):
    return math.hypot(v[0], v[1])


def vnorm(v):
    n = vlen(v)
    if n < 1e-6:
        return None
    return (v[0] / n, v[1] / n)


def get_route_point(route, i, use_passing=True, pass_ahead=PASS_AHEAD):
    """
    完全复刻 AutoDrive.GetRoutePoint()。
    route 是路口下标序列，返回 (x, z) —— 车该瞄准的点。
    """
    if i < 0:
        i = 0
    if i >= len(route):
        i = len(route) - 1

    here = pos(route[i])

    if not use_passing or i == 0:
        return here

    # 进来方向：上一站 → 本站
    prev = pos(route[i - 1])
    in_dir = vnorm(vsub(here, prev))
    if in_dir is None:
        return here

    # 出去方向
    if i + 1 < len(route):
        nxt = pos(route[i + 1])
        out_dir = vnorm(vsub(nxt, here))
        if out_dir is None:
            out_dir = in_dir
    else:
        out_dir = in_dir

    # 角平分线
    bis = (in_dir[0] + out_dir[0], in_dir[1] + out_dir[1])
    n = vlen(bis)
    if n < 1e-4:
        # 掉头 → 用侧向切入（复刻 C# 的 Cross(inDir, up)）
        # Cross((x,0,z),(0,1,0)) = (-z, 0, x)  → 水平分量 (-z, x)
        bis = (-in_dir[1], in_dir[0])
        n = vlen(bis)
        if n < 1e-4:
            return here
    bis = (bis[0] / n, bis[1] / n)

    return (here[0] + bis[0] * pass_ahead, here[1] + bis[1] * pass_ahead)


FAILS = []


def check(name, cond, detail=""):
    tag = "✔" if cond else "✘"
    print(f"  {tag} {name}" + (f"   {detail}" if detail else ""))
    if not cond:
        FAILS.append(name)


print("=" * 74)
print("路口「过路口点」几何校验")
print("=" * 74)

# ---------------------------------------------------------------- TEST 1
print("\nTEST 1 — 第一站和关闭开关时，必须返回路口中心")
r = [1, 0, 10]   # T → R → E2
p0 = get_route_point(r, 0)
check("第一站 = 路口中心（还没到路口，先奔中心）",
      abs(p0[0] - pos(1)[0]) < 1e-9 and abs(p0[1] - pos(1)[1]) < 1e-9,
      f"{p0}")
p_off = get_route_point(r, 1, use_passing=False)
check("关掉 useJunctionPassing 时 = 路口中心",
      abs(p_off[0] - pos(0)[0]) < 1e-9 and abs(p_off[1] - pos(0)[1]) < 1e-9,
      f"{p_off}")

# ---------------------------------------------------------------- TEST 2
print("\nTEST 2 — 过路口点必须落在「进来→出去」的夹角内侧（关键几何性质）")
# T → R → E2：R 是中间路口
# 进来方向 R-T，出去方向 E2-R
i_mid = 1
p = get_route_point(r, i_mid)
c = pos(0)   # R 的中心
in_d = vnorm(vsub(c, pos(1)))
out_d = vnorm(vsub(pos(10), c))
print(f"    路口 R 中心 = {c}")
print(f"    进来方向 (从 T) = ({in_d[0]:.3f}, {in_d[1]:.3f})")
print(f"    出去方向 (去 E2) = ({out_d[0]:.3f}, {out_d[1]:.3f})")
print(f"    过路口点 = ({p[0]:.3f}, {p[1]:.3f})")

to_p = vnorm(vsub(p, c))
# 关键性质：过路口点应该在"进来方向"和"出去方向"之间（点积都为正）
dot_in = to_p[0] * in_d[0] + to_p[1] * in_d[1]
dot_out = to_p[0] * out_d[0] + to_p[1] * out_d[1]
check("过路口点在进来方向这一侧（dot > 0）", dot_in > 0, f"dot_in={dot_in:.3f}")
check("过路口点在出去方向这一侧（dot > 0）", dot_out > 0, f"dot_out={dot_out:.3f}")
check("偏移距离 = passAhead", abs(vlen(vsub(p, c)) - PASS_AHEAD) < 1e-6,
      f"{vlen(vsub(p,c)):.4f} vs {PASS_AHEAD}")

# ★ 更强的一条：夹角内的"等角"性质 ——
#   过路口点到 in/out 两个方向的夹角应该相等
ang_in = math.degrees(math.acos(max(-1, min(1, to_p[0]*in_d[0] + to_p[1]*in_d[1]))))
ang_out = math.degrees(math.acos(max(-1, min(1, to_p[0]*out_d[0] + to_p[1]*out_d[1]))))
check("过路口点平分进/出夹角（两个夹角相等）",
      abs(ang_in - ang_out) < 1e-6,
      f"in={ang_in:.4f}°  out={ang_out:.4f}°")

# ---------------------------------------------------------------- TEST 3
print("\nTEST 3 — 直行时不该横偏（防止直路也画蛇）")
# 构造一条「完全直」的路线：O2(-14,10) → S(1,12)? 不是直的。
# 用真正共线的三点：手工找 —— R(8.49,-3.10), T(-1.51,6.90), L(-11.51,-3.10) 不共线
# 换成数学上共线的：用 (0,0) → (0,10) → (0,20) 这种，直接测函数
# 这里改成用三个真实坐标里近似共线的 —— 或者直接构造
import copy
saved = dict(J)
J["A"] = (0.0, 0.0)
J["B"] = (0.0, 10.0)
J["C"] = (0.0, 20.0)
IDX.extend(["A", "B", "C"])
straight = [13, 14, 15]
p_s = get_route_point(straight, 1)
c_s = pos(14)
lateral = abs(p_s[0] - c_s[0])
check("直线路线上，过路口点不横向偏移", lateral < 1e-6,
      f"横向偏移 {lateral:.6f} m（应该是 0）")
check("直线路线上，过路口点沿前进方向前移",
      abs(p_s[1] - c_s[1] - PASS_AHEAD) < 1e-6,
      f"前移 {p_s[1]-c_s[1]:.3f} m")

# ---------------------------------------------------------------- TEST 4
print("\nTEST 4 — 掉头（in 和 out 完全相反）不能崩 / 不能返回 NaN")
J["D"] = (0.0, 30.0)
IDX.append("D")
uturn = [13, 14, 13]     # A → B → A（原路返回）
try:
    p_u = get_route_point(uturn, 1)
    ok = not (math.isnan(p_u[0]) or math.isnan(p_u[1]))
    check("掉头时返回有限坐标（不除零）", ok, f"{p_u}")
    # 掉头时应该往侧面切，不是原地不动
    moved = vlen(vsub(p_u, pos(14)))
    check("掉头时确实偏移了（给了个侧向切入点）", moved > 1e-6,
          f"偏移 {moved:.3f} m")
except Exception as e:
    check("掉头时不抛异常", False, f"{type(e).__name__}: {e}")

# ---------------------------------------------------------------- TEST 5
print("\nTEST 5 — 终点站：没有出去方向时，沿进来方向继续往前")
# 最后一站 i = len-1
r5 = [1, 0]     # T → R，R 是终点
p5 = get_route_point(r5, 1)
c5 = pos(0)
in5 = vnorm(vsub(c5, pos(1)))
expect = (c5[0] + in5[0] * PASS_AHEAD, c5[1] + in5[1] * PASS_AHEAD)
check("终点站的过路口点 = 中心 + 进来方向 × passAhead",
      abs(p5[0]-expect[0]) < 1e-6 and abs(p5[1]-expect[1]) < 1e-6,
      f"{p5} vs {expect}")
# 语义：车会"开进路口再停"，而不是一头撞在路口中心
check("终点站也往前推了（不会撞在中心上）",
      vlen(vsub(p5, c5)) > PASS_AHEAD - 1e-6, f"偏移 {vlen(vsub(p5,c5)):.3f}")

# ---------------------------------------------------------------- TEST 6
print("\nTEST 6 — 扫描所有真实相邻路口对，都不该出现异常")
names = list(J.keys())[:13]  # 只扫真实的 13 个
bad = 0
max_off = 0.0
min_off = 1e9
for a in range(13):
    for b in range(13):
        for c in range(13):
            if a == b or b == c:
                continue
            route = [a, b, c]
            try:
                pt = get_route_point(route, 1)
                if math.isnan(pt[0]) or math.isnan(pt[1]):
                    bad += 1
                    continue
                off = vlen(vsub(pt, pos(b)))
                max_off = max(max_off, off)
                min_off = min(min_off, off)
            except Exception:
                bad += 1
check(f"{12*11*11} 组三元路线全部无异常", bad == 0, f"异常 {bad} 组")
check("偏移量恒等于 passAhead（不会忽大忽小）",
      abs(max_off - PASS_AHEAD) < 1e-5 and abs(min_off - PASS_AHEAD) < 1e-5,
      f"范围 [{min_off:.5f}, {max_off:.5f}]")

# ---------------------------------------------------------------- TEST 7
print("\nTEST 7 — 过路口点不会跑到「离路口太远」的地方（防止切到别的路段上）")
worst = 0.0
worst_case = None
for a in range(13):
    for b in range(13):
        for c in range(13):
            if a == b or b == c:
                continue
            pt = get_route_point([a, b, c], 1)
            d = vlen(vsub(pt, pos(b)))
            if d > worst:
                worst = d
                worst_case = (IDX[a], IDX[b], IDX[c])
check("最大偏移 = passAhead（可控）", abs(worst - PASS_AHEAD) < 1e-5,
      f"最大 {worst:.4f} m，最坏组合 {worst_case}")

# 顺带：算一下 passAhead 应该取多大才合理
print("\n    【passAhead 取值参考】")
print(f"    当前 passAhead = {PASS_AHEAD} m")
print(f"    车长 3.2 m（半长 1.6）、路口圆盘半径 2.5 m")
print(f"    → 3 m 大约是「半车长 + 一点余量」，比圆盘半径略大，")
print(f"      车瞄准的点落在路口边缘外侧一点，出弯自然。")
if PASS_AHEAD < 1.6:
    print("    ⚠ 比半车长还小 —— 车可能会在路口里才开始转向，太晚")
elif PASS_AHEAD > 6.0:
    print("    ⚠ 偏大 —— 车可能提前切到下一个路段，看起来像抄近路")

# ---------------------------------------------------------------- 汇总
print("\n" + "=" * 74)
if FAILS:
    print(f"✘ 有 {len(FAILS)} 项没过：")
    for f in FAILS:
        print(f"    · {f}")
else:
    print(f"✔ 全部通过（7 组检查）")
print("=" * 74)

# ---------------------------------------------------------------- ASCII 示意
print("\n【示意】T → R → E2 时，车瞄准的过路口点（俯视，+x 向右 / +z 向上）")
xs = [J[k][0] for k in ["T", "R", "E2"]]
zs = [J[k][1] for k in ["T", "R", "E2"]]
pad = 6
x0, x1 = min(xs) - pad, max(xs) + pad
z0, z1 = min(zs) - pad, max(zs) + pad
W, H = 46, 18
g = [[' '] * W for _ in range(H)]

def to_cell(x, z):
    c = int(round((x - x0) / (x1 - x0) * (W - 1)))
    r = int(round((z - z0) / (z1 - z0) * (H - 1)))
    return max(0, min(W-1, c)), max(0, min(H-1, r))

for nm, ch in [("T", "T"), ("R", "R"), ("E2", "E")]:
    cx, cz = J[nm]
    c, r = to_cell(cx, cz)
    g[r][c] = ch

pp = get_route_point([1, 0, 10], 1)
c, r = to_cell(pp[0], pp[1])
g[r][c] = "*"

for row in range(H - 1, -1, -1):
    print("    |" + "".join(g[row]) + "|")
print()
print("    字母 = 路口    * = 过路口点（车真正瞄的地方）")
print(f"    R 中心 = {J['R']}")
print(f"    过路口点 = ({pp[0]:.2f}, {pp[1]:.2f})  ← 落在 T→R→E2 的夹角内侧")
