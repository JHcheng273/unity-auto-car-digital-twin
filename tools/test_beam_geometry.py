"""
光锥投影几何离线校验
====================
Unity 编译要等窗口焦点（反馈慢），而光锥的几何是**纯三角函数**问题 ——
用 Python 逐行复刻 ProjectionBeam.cs 里的算法，直接验算。

验的是四件事（全是"肉眼看着不对但说不清哪不对"的那类 bug）：
  1. 光锥远端**不能穿到地面以下**（穿了看起来像插进路里的一坨怪东西）
  2. 地面光斑的四个角要和光锥落地点**对得上**（对不上会看到光斑和光柱错位）
  3. 观察相机视场角**能装下整个光锥**（装不下 MATLAB 就只能看到半截）
  4. 各种尺寸组合下都不出事（把参数扫一遍，而不是只看默认值）
"""

import math

# ---------------------------------------------------------------- 几何复刻
# 完全对应 ProjectionBeam.cs 的 BuildVolumeMesh / BuildFloorMesh

def beam_corners(start_ahead, start_height, beam_length, near_w, far_w,
                 tilt_deg, floor_lift):
    """
    返回 (near_left, near_right, far_left, far_right, clamped_tilt_deg)
    全是 (x, y, z)，坐标在车的局部空间，+Z 是车头方向。
    """
    rad = math.radians(tilt_deg)
    near_y = start_height
    near_z = start_ahead
    n_half = near_w * 0.5

    far_z = start_ahead + beam_length * math.cos(rad)
    far_y = start_height - beam_length * math.sin(rad)
    f_half = far_w * 0.5

    # ★ 夹紧：远端不能穿地（对应 C# 里那段 if (farY < minY)）
    MIN_Y = 0.05
    if far_y < MIN_Y:
        fixed_sin = (start_height - MIN_Y) / beam_length
        fixed_sin = max(0.0, min(1.0, fixed_sin))
        rad = math.asin(fixed_sin)
        far_z = start_ahead + beam_length * math.cos(rad)
        far_y = MIN_Y

    nl = (-n_half, near_y, near_z)
    nr = ( n_half, near_y, near_z)
    fl = (-f_half, far_y,  far_z)
    fr = ( f_half, far_y,  far_z)
    return nl, nr, fl, fr, math.degrees(rad)


def floor_corners(start_ahead, start_height, beam_length, near_w, far_w,
                  tilt_deg, floor_lift=0.02):
    """地面光斑四角。必须和 beam_corners 的落地点一致。"""
    rad = math.radians(tilt_deg)
    far_z = start_ahead + beam_length * math.cos(rad)
    far_y = start_height - beam_length * math.sin(rad)
    if far_y < 0.05:
        s = max(0.0, min(1.0, (start_height - 0.05) / beam_length))
        rad = math.asin(s)
        far_z = start_ahead + beam_length * math.cos(rad)

    z0, z1 = start_ahead, far_z
    w0, w1 = near_w * 0.85, far_w
    y = floor_lift
    return ( (-w0 * .5, y, z0), (w0 * .5, y, z0),
             ( w1 * .5, y, z1), (-w1 * .5, y, z1) )


def look_at_dir(eye, target):
    """复刻 Unity 的 Transform.LookAt 得到的前向（+Z）。"""
    fx, fy, fz = (target[0]-eye[0], target[1]-eye[1], target[2]-eye[2])
    n = math.sqrt(fx*fx + fy*fy + fz*fz)
    return (fx/n, fy/n, fz/n)


def angle_between(a, b):
    d = a[0]*b[0] + a[1]*b[1] + a[2]*b[2]
    d = max(-1.0, min(1.0, d))
    return math.degrees(math.acos(d))


def cone_half_angle(eye, corners):
    """从 eye 看这四个角，最大的那个偏角（度）。"""
    fwd = look_at_dir(eye, LOOK_AT)
    worst = 0.0
    for c in corners:
        d = look_at_dir(eye, c)
        worst = max(worst, angle_between(fwd, d))
    return worst


# ---------------------------------------------------------------- 参数（和脚本默认值一致）
# ★ 注意：原来这里 tilt_deg=22 是被夹紧吃掉的（实际只有 5.36°），
#   说明"拍一个角度"这种做法本身就不对。正确做法是**由落地点反推角度**。
#   下面先算"要让光锥在离车头 D 米处落地，需要多少角度"。
def tilt_for_landing(start_height, start_ahead, land_dist, floor_y=0.05):
    """
    已知起点高 start_height、要从车头前 start_ahead 出发，
    要求在离车头 land_dist 米处落到地面，求下压角（度）。
    水平距离 = land_dist - start_ahead，下落高度 = start_height - floor_y
    """
    horiz = land_dist - start_ahead
    drop = start_height - floor_y
    if horiz <= 0:
        return 90.0
    return math.degrees(math.atan2(drop, horiz))


# 我想让光斑落在离车头 7.5 米处 → 反推角度
LAND_DIST = 7.5
ANG = tilt_for_landing(0.75, 0.30, LAND_DIST)
# 有了角度再反推需要的光锥长度（斜边长）
HORIZ = LAND_DIST - 0.30
DROP = 0.75 - 0.05
LEN = math.sqrt(HORIZ * HORIZ + DROP * DROP)

print(f"【参数反推】想让光斑落在离车头 {LAND_DIST} m 处：")
print(f"    水平距离 {HORIZ:.2f} m，下落 {DROP:.2f} m"
      f"  →  下压角 = {ANG:.1f}°，光锥长（斜边）= {LEN:.2f} m\n")

DEF = dict(start_ahead=0.30, start_height=0.75, beam_length=LEN,
           near_w=1.20, far_w=4.80, tilt_deg=ANG, floor_lift=0.02)

OBS_OFFSET = (-2.60, 3.20, -4.40)
LOOK_AT    = ( 0.00, 0.25,  3.60)
OBS_FOV    = 55.0

FAILS = []

def check(name, cond, detail=""):
    tag = "✔" if cond else "✘"
    print(f"  {tag} {name}" + (f"   {detail}" if detail else ""))
    if not cond:
        FAILS.append(name)


print("=" * 74)
print("光锥投影几何离线校验")
print("=" * 74)

# ---------------------------------------------------------------- TEST 1
print("\nTEST 1 — 光锥远端不穿地")
nl, nr, fl, fr, eff_tilt = beam_corners(**DEF)
print(f"    近端 y = {nl[1]:.3f} m   z = {nl[2]:.3f} m")
print(f"    远端 y = {fl[1]:.3f} m   z = {fl[2]:.3f} m   （有效下压 {eff_tilt:.2f}°）")
check("远端 y >= 0.05（没插进地面）", fl[1] >= 0.05 - 1e-9, f"y={fl[1]:.4f}")
check("远端比近端更靠前（真的往前伸了）", fl[2] > nl[2], f"{fl[2]:.2f} > {nl[2]:.2f}")
check("远端比近端更低（真的往下压了）", fl[1] < nl[1], f"{fl[1]:.2f} < {nl[1]:.2f}")
check("远端比近端更宽（光散开了）", abs(fl[0]) > abs(nl[0]),
      f"{2*abs(fl[0]):.2f} m > {2*abs(nl[0]):.2f} m")
check("左右对称", abs(nl[0] + nr[0]) < 1e-9 and abs(fl[0] + fr[0]) < 1e-9)
# ★ 这条是新增的关键断言：默认参数**不该**被夹紧吃掉。
#   被夹紧说明"设的角度"和"实际生效的角度"对不上，
#   用户调滑杆时会发现"怎么调都不动"——第一版就踩了这个。
check("默认参数没有被夹紧逻辑改写（设多少就是多少）",
      abs(eff_tilt - DEF['tilt_deg']) < 0.01,
      f"设定 {DEF['tilt_deg']:.2f}° vs 实际 {eff_tilt:.2f}°")
check("光斑确实落在预期位置（离车头 %.1f m）" % LAND_DIST,
      abs(fl[2] - LAND_DIST) < 0.05, f"实测 {fl[2]:.3f} m")

# ---------------------------------------------------------------- TEST 2
print("\nTEST 2 — 地面光斑与光锥落地点对齐")
fc = floor_corners(**DEF)
print(f"    光斑近边 y = {fc[0][1]:.3f}  z = {fc[0][2]:.3f}")
print(f"    光斑远边 y = {fc[3][1]:.3f}  z = {fc[3][2]:.3f}")
print(f"    光锥落点 y = {fl[1]:.3f}  z = {fl[2]:.3f}")
check("光斑远端 z 与光锥远端 z 一致（不错位）",
      abs(fc[3][2] - fl[2]) < 1e-6, f"{fc[3][2]:.4f} vs {fl[2]:.4f}")
check("光斑远边宽度 = 远端宽度",
      abs((fc[2][0] - fc[3][0]) - (fr[0] - fl[0])) < 1e-6)
check("光斑贴地（y 很小，不飘在空中）", fc[0][1] <= 0.05, f"y={fc[0][1]}")
check("光斑近边比远边窄（梯形近窄远宽）",
      (fc[1][0] - fc[0][0]) < (fc[2][0] - fc[3][0]))

# ---------------------------------------------------------------- TEST 3
print("\nTEST 3 — 观察相机视场角能装下整个光锥")
eye = OBS_OFFSET
half = cone_half_angle(eye, [nl, nr, fl, fr])
print(f"    相机位置 = {OBS_OFFSET}")
print(f"    看向     = {LOOK_AT}")
print(f"    光锥四个角相对相机中心的最大偏角 = {half:.1f}°")
print(f"    视场角 {OBS_FOV}° → 半视场 {OBS_FOV/2:.1f}°")
check("光锥完整落在视野内（留 5° 余量）", half < OBS_FOV / 2 - 5.0,
      f"{half:.1f}° < {OBS_FOV/2 - 5:.1f}°")
# 顺便算一下光锥在画面里占多大 —— 太小就白推了
occupy = (half / (OBS_FOV / 2)) * 100
print(f"    光锥在画面里约占半宽的 {occupy:.0f}%")
check("光锥在画面里占比合理（>25%，不然看不清）", occupy > 25, f"{occupy:.0f}%")

# ---------------------------------------------------------------- TEST 4
print("\nTEST 4 — 参数扫描（不能只在默认值下对）")
cases = []
for tilt in [5, 10, 15, 22, 30, 40, 50]:
    for blen in [4.0, 6.0, 7.5, 10.0, 14.0]:
        for sh in [0.4, 0.75, 1.2]:
            cases.append(dict(start_ahead=0.30, start_height=sh,
                              beam_length=blen, near_w=1.20, far_w=4.80,
                              tilt_deg=float(tilt), floor_lift=0.02))

bad_ground = 0
bad_forward = 0
y_vals = []
for c in cases:
    a, b, cc, d, t = beam_corners(**c)
    if cc[1] < 0.05 - 1e-9:
        bad_ground += 1
    if cc[2] <= a[2]:
        bad_forward += 1
    y_vals.append((c['tilt_deg'], c['beam_length'], c['start_height'], cc[1]))

check(f"{len(cases)} 组参数组合中，无一穿地", bad_ground == 0, f"穿地 {bad_ground} 组")
check("所有组合都往前伸了", bad_forward == 0, f"不前进 {bad_forward} 组")

# 打印几个"极端"例子，说明夹紧生效了
print("\n    ★ 夹紧生效的例子（本来是穿地的，被夹到 y=0.05）：")
shown = 0
for tilt, blen, sh, fy in sorted(y_vals, key=lambda x: x[3]):
    raw_rad = math.radians(tilt)
    raw_y = sh - blen * math.sin(raw_rad)
    if raw_y < 0.05 and shown < 4:
        print(f"      下压 {tilt:>2}° / 长 {blen:>4} m / 起点高 {sh} m ："
              f"原始落点 y={raw_y:+.2f} → 夹紧后 y={fy:.2f}")
        shown += 1
if shown == 0:
    print("      （这组参数里没有会穿地的，说明默认值本身就很安全）")

# ---------------------------------------------------------------- TEST 5
print("\nTEST 5 — 观察相机在车后上方，不会穿进车里/钻到地下")
eye_y = OBS_OFFSET[1]
eye_z = OBS_OFFSET[2]
check("相机在地面之上", eye_y > 0.5, f"y={eye_y}")
check("相机在车后方（z < 0）", eye_z < 0, f"z={eye_z}")
check("相机不会太远导致光锥太小", abs(OBS_OFFSET[0]) < 6 and abs(eye_z) < 8,
      f"偏移 {OBS_OFFSET}")
check("相机看向车头前方（target z > 0）", LOOK_AT[2] > 0, f"z={LOOK_AT[2]}")

# ---------------------------------------------------------------- 汇总
print("\n" + "=" * 74)
if FAILS:
    print(f"✘ 有 {len(FAILS)} 项没过：")
    for f in FAILS:
        print(f"    · {f}")
else:
    print("✔ 全部通过（5 组检查）")
print("=" * 74)

# ---------------------------------------------------------------- 顺手给一张"俯视示意图"
print("\n【俯视示意】光锥在地面上的投影（+Z 朝上 = 车头方向）")
W, H = 41, 14
grid = [[' '] * W for _ in range(H)]
def put(z, x, ch):
    # z 从 start_ahead 到 far_z 映射到行，x 从 -3..3 映射到列
    z0, z1 = DEF['start_ahead'], fc[3][2]
    r = int(round((z - z0) / (z1 - z0) * (H - 1)))
    c = int(round((x + 3.0) / 6.0 * (W - 1)))
    if 0 <= r < H and 0 <= c < W:
        grid[r][c] = ch

for i in range(21):
    t = i / 20
    z = DEF['start_ahead'] + t * (fc[3][2] - DEF['start_ahead'])
    w = (DEF['near_w'] * 0.85) + t * (DEF['far_w'] - DEF['near_w'] * 0.85)
    put(z, -w / 2, '#')
    put(z,  w / 2, '#')

for r in range(H - 1, -1, -1):
    print("    |" + "".join(grid[r]) + "|" +
          ("  ← 车头（近端）" if r == 0 else ("  ← 远端" if r == H - 1 else "")))
print("     光斑水平尺度：近端 %.2f m → 远端 %.2f m" %
      (DEF['near_w'] * 0.85, DEF['far_w']))
