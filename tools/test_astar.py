#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
A* 绕障 —— 严格验证（v2）

v1 的教训：
  · 我在 tests.append 里把障碍格数硬编码成 0，导致"标了 0 格"这种假象
  · 场景 3 暴露了真 bug：规划前无条件清起点周围，把障碍物自己也清掉了，
    于是 A* 在障碍物身上开出通道 → 车穿模。已在 C# 侧改成"只挪出发点"。

v2 新增最关键的校验：
  ★ 路径上的每一个采样点都不许落在障碍圈内（含 padding）
    这一条才能抓住"穿模"这类致命错误。

用法：python test_astar.py
"""
import math

CELL = 0.75
EXTENT = 60.0
COLS = ROWS = int(round(EXTENT * 2 / CELL))
ORIGIN = (-EXTENT, -EXTENT)
PADDING = 1.0

DIRS = [(-1,-1),(-1,0),(-1,1),(0,-1),(0,1),(1,-1),(1,0),(1,1)]
STEPS = [1.414,1.0,1.414,1.0,1.0,1.414,1.0,1.414]

CAR_HALF = 0.85   # 车半宽，用来判断"贴着过会不会蹭到"


class Grid:
    def __init__(self):
        self.blocked = [[False]*COLS for _ in range(ROWS)]
        self.n_blocked = 0
        # 记录所有障碍圆，最后用来做"路径不许穿过障碍"的硬校验
        self.circles = []

    def w2c(self, x, z):
        return math.floor((x-ORIGIN[0])/CELL), math.floor((z-ORIGIN[1])/CELL)
    def c2w(self, c, r):
        return ORIGIN[0]+(c+0.5)*CELL, ORIGIN[1]+(r+0.5)*CELL
    def inb(self, c, r):
        return 0 <= c < COLS and 0 <= r < ROWS
    def is_blocked(self, c, r):
        return True if not self.inb(c, r) else self.blocked[r][c]
    def unblock(self, c, r):
        if self.inb(c, r): self.blocked[r][c] = False

    def block_circle(self, cx, cz, radius, padding=0.0):
        r = radius + padding
        # ★ v2 修正：改成"格子外接碰到圆就标黑"
        half_diag = CELL * 0.7071
        reach = r + half_diag
        self.circles.append((cx, cz, r))
        c0 = max(math.floor((cx-reach-ORIGIN[0])/CELL), 0)
        c1 = min(math.floor((cx+reach-ORIGIN[0])/CELL), COLS-1)
        r0 = max(math.floor((cz-reach-ORIGIN[1])/CELL), 0)
        r1 = min(math.floor((cz+reach-ORIGIN[1])/CELL), ROWS-1)
        for row in range(r0, r1+1):
            for col in range(c0, c1+1):
                wx, wz = self.c2w(col, row)
                if (wx-cx)**2 + (wz-cz)**2 <= reach*reach:
                    if not self.blocked[row][col]:
                        self.blocked[row][col] = True
                        self.n_blocked += 1

    def nearest_free(self, c, r):
        for rad in range(1, 7):
            for dc in range(-rad, rad+1):
                for dr in range(-rad, rad+1):
                    if abs(dc) != rad and abs(dr) != rad: continue
                    cc, rr = c+dc, r+dr
                    if self.inb(cc, rr) and not self.is_blocked(cc, rr):
                        return cc, rr
        return None

    def heuristic(self, c1, r1, c2, r2):
        dc, dr = abs(c1-c2), abs(r1-r2)
        return 1.414*min(dc, dr) + abs(dc-dr)

    def find_path(self, sx, sz, gx, gz):
        sc, sr = self.w2c(sx, sz)
        gc, gr = self.w2c(gx, gz)

        if self.is_blocked(sc, sr):
            nf = self.nearest_free(sc, sr)
            if nf is None: return None, "起点被完全封死"
            sc, sr = nf
        if self.is_blocked(gc, gr):
            nf = self.nearest_free(gc, gr)
            if nf is None: return None, "终点被完全封死"
            gc, gr = nf

        N = COLS*ROWS
        g = [float('inf')]*N; f = [float('inf')]*N
        came = [-1]*N; state = [0]*N
        s_idx, g_idx = sr*COLS+sc, gr*COLS+gc
        g[s_idx] = 0.0
        f[s_idx] = self.heuristic(sc, sr, gc, gr)
        openl = [s_idx]; state[s_idx] = 1
        found = False; expanded = 0

        while openl:
            bi = 0; bf = f[openl[0]]
            for i in range(1, len(openl)):
                if f[openl[i]] < bf: bf = f[openl[i]]; bi = i
            cur = openl.pop(bi); expanded += 1
            if cur == g_idx: found = True; break
            state[cur] = 2
            cc, cr = cur % COLS, cur // COLS
            for (dc, dr), step in zip(DIRS, STEPS):
                nc, nr = cc+dc, cr+dr
                if not self.inb(nc, nr) or self.is_blocked(nc, nr): continue
                if dc != 0 and dr != 0:
                    if self.is_blocked(cc+dc, cr) or self.is_blocked(cc, cr+dr): continue
                ni = nr*COLS+nc
                if state[ni] == 2: continue
                tent = g[cur] + step
                if tent < g[ni]:
                    g[ni] = tent
                    f[ni] = tent + self.heuristic(nc, nr, gc, gr)
                    came[ni] = cur
                    if state[ni] != 1:
                        openl.append(ni); state[ni] = 1

        if not found:
            return None, f"无解（扩展 {expanded} 格）"

        cells = []; cur = g_idx
        while cur != -1:
            cells.append(cur)
            if cur == s_idx: break
            cur = came[cur]
        cells.reverse()

        pts = [(sx, sz)]
        for i in range(1, len(cells)):
            pts.append(self.c2w(cells[i] % COLS, cells[i] // COLS))
        return self.smooth(pts), f"扩展 {expanded} 格"

    def los(self, a, b):
        dist = math.hypot(b[0]-a[0], b[1]-a[1])
        steps = max(1, math.ceil(dist/(CELL*0.5)))
        for i in range(steps+1):
            t = i/steps
            p = (a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t)
            c, r = self.w2c(*p)
            if not self.inb(c, r) or self.is_blocked(c, r): return False
        return True

    def smooth(self, pts):
        if len(pts) <= 2: return pts
        out = [pts[0]]; anchor = 0
        for i in range(2, len(pts)):
            if not self.los(pts[anchor], pts[i]):
                out.append(pts[i-1]); anchor = i-1
        out.append(pts[-1])
        return out

    def path_hits_obstacle(self, path, escape_radius=0.0):
        """
        ★ 硬校验：路径采样点有没有落进障碍圈（用**原始半径**，不含 padding）

        escape_radius：从起点算起，这个距离内的路径段不检查。
        为什么需要它：车可能本来就已经贴着障碍（比如用户把障碍点到车脸上），
        这时"从当前位置挪出去"的那一小段必然在圈内 —— 那是几何上没法避免的，
        不是算法错。真正要保证的是"车走起来之后不会撞"。
        设成 3 米就够覆盖"挪一格 + 起步"的距离。
        """
        # 累计走过的距离，超过 escape_radius 之后才开始检查
        travelled = 0.0
        for i in range(len(path)-1):
            a, b = path[i], path[i+1]
            dist = math.hypot(b[0]-a[0], b[1]-a[1])
            steps = max(1, math.ceil(dist/(CELL*0.3)))
            for k in range(steps+1):
                t = k/steps
                seg_done = travelled + dist*t
                if seg_done < escape_radius:
                    continue
                px = a[0]+(b[0]-a[0])*t
                pz = a[1]+(b[1]-a[1])*t
                for (cx, cz, r) in self.circles:
                    if (px-cx)**2 + (pz-cz)**2 < r*r:
                        return (px, pz), (cx, cz, r)
            travelled += dist
        return None, None

    def min_clearance(self, path):
        """路径上离任一障碍圆心最近的距离，再减去该障碍半径 = 最小间隙"""
        best = float('inf')
        for i in range(len(path)-1):
            a, b = path[i], path[i+1]
            dist = math.hypot(b[0]-a[0], b[1]-a[1])
            steps = max(1, math.ceil(dist/(CELL*0.3)))
            for k in range(steps+1):
                t = k/steps
                px = a[0]+(b[0]-a[0])*t
                pz = a[1]+(b[1]-a[1])*t
                for (cx, cz, r) in self.circles:
                    d = math.hypot(px-cx, pz-cz) - r
                    if d < best: best = d
        return best


# ================= 场景 =================
WP = {0:(8.49,-3.10), 1:(-1.51,6.90), 2:(-11.51,-3.10), 3:(-1.51,-13.10)}
CAR = (1.0, 20.0)
FIXED = (7.8, 6.0, 1.5)     # 场景里原有 Obstacle（实际是 3×1 的方块，用外接圆 1.5 近似）
DYN   = (1.0, 10.0, 1.0)    # 用户按 O 键加的

results = []

def run(name, circles, start, goal, expect_ok=True, escape=0.0):
    g = Grid()
    for (cx, cz, r) in circles:
        g.block_circle(cx, cz, r, PADDING)
    path, info = g.find_path(start[0], start[1], goal[0], goal[1])

    if path is None:
        status = "OK-无解" if not expect_ok else "FAIL-不该无解"
        results.append((name, status, info, None, None))
        return

    hit, circ = g.path_hits_obstacle(path, escape_radius=escape)
    clear = g.min_clearance(path)
    ln = sum(math.hypot(path[i][0]-path[i-1][0], path[i][1]-path[i-1][1])
             for i in range(1, len(path)))

    if hit:
        status = "FAIL-穿模!"
        info2 = f"路径点 ({hit[0]:.2f},{hit[1]:.2f}) 落进障碍圈 圆心({circ[0]},{circ[1]}) r={circ[2]:.2f}"
    elif clear < CAR_HALF - 0.5:
        status = "WARN-贴太近"
        info2 = f"最小间隙 {clear:.2f} 米（车半宽 {CAR_HALF}）"
    else:
        status = "OK"
        info2 = f"{len(path)} 点 / {ln:.1f} 米 / 最小间隙 {clear:.2f} 米"

    results.append((name, status, info2 if hit else info + "；" + info2, path, clear))


# 场景 1：只有原有固定障碍
run("1. 车→WP0，仅原有障碍", [FIXED], CAR, WP[0])

# 场景 2：再加一个动态障碍正挡在车前
run("2. 车→WP0，+动态障碍挡路", [FIXED, DYN], CAR, WP[0])

# 场景 3：障碍怼在车脸前 1.5 米（车自己就在膨胀圈里 → skip_first）
run("3. 障碍怼在车脸前 1.5 米", [FIXED, (1.0, 21.5, 1.0)], CAR, WP[0], escape=3.0)

# 场景 4：障碍正好压着车（起点完全在膨胀圈里）
run("4. 障碍压着车自己", [FIXED, (1.0, 20.0, 1.0)], CAR, WP[0], escape=3.0)

# 场景 5：障碍压着目标点 WP0
run("5. 障碍压着目标点 WP0", [FIXED, (8.49, -3.10, 1.2)], CAR, WP[0])

# 场景 6：一大堆障碍（用户乱按 O 键）
run("6. 场上 8 个障碍物", [FIXED, (1,10,1.0), (-2,14,0.9), (4,16,1.1),
                          (2,5,1.0), (-3,-2,1.0), (6,-6,1.2), (12,0,1.0)], CAR, WP[0])

# 场景 7：菱形全程 5 段
print("=" * 74)
print("A* 绕障验证 v2")
print("=" * 74)
print()
for r in results:
    name, status, info, path, clear = r
    mark = "[OK]  " if status.startswith("OK") else ("[!!]  " if "FAIL" in status else "[??]  ")
    print(f"{mark}{name}")
    print(f"        {status}  |  {info}")
    if path and len(path) <= 8:
        print("        路径: " + " → ".join(f"({x:.1f},{z:.1f})" for x, z in path))
    print()

# 场景 7 单独跑
g7 = Grid()
g7.block_circle(*FIXED, PADDING)
seq = [CAR, WP[0], WP[1], WP[2], WP[3], WP[0]]
print("[--]  7. 菱形路径 5 段连走")
seg_ok = True
for i in range(len(seq)-1):
    p, info = g7.find_path(seq[i][0], seq[i][1], seq[i+1][0], seq[i+1][1])
    if p is None:
        print(f"        段{i+1}: FAIL {info}")
        seg_ok = False
    else:
        hit, circ = g7.path_hits_obstacle(p)
        tag = "FAIL-穿模" if hit else "OK"
        if hit: seg_ok = False
        print(f"        段{i+1}: {tag}  {len(p)} 点  {info}")
print()

# 汇总
print("=" * 74)
fails = [r for r in results if "FAIL" in r[1]]
warns = [r for r in results if "WARN" in r[1]]
print(f"汇总：{len(results)} 个场景 —— 失败 {len(fails)}，警告 {len(warns)}")
for f in fails: print(f"   [FAIL] {f[0]}: {f[2]}")
for w in warns: print(f"   [WARN] {w[0]}: {w[2]}")
if not fails and not warns:
    print("   全部通过 —— 没有穿模，间隙都够。")
