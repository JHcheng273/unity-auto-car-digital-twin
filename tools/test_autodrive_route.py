"""
Simulate AutoDrive's global-route state machine (line-by-line Python replica).
==============================================================================
RoadNetwork.cs A* is already verified. What's NOT yet verified is the
*driving* logic in AutoDrive.cs, which has subtle traps:

  T1. ReplanRoute() drops the first junction when the car is already on it
      -- otherwise the car targets a junction under its own wheels and spins.
  T2. The periodic replan (every replanInterval) must NOT livelock:
      the car must make forward progress, not oscillate.
  T3. Arriving at the destination must stop the car (stopAtDestination=true).
  T4. If the user picks a NEW target mid-drive, the route must switch.
  T5. Blocking a junction mid-drive must make the car reroute.

We simulate the car as a point that moves toward the current route target at
a fixed speed, mimicking transform.Translate + steering (we ignore steering
dynamics -- only the target-selection logic matters here).
"""
import heapq
import math

# ---------------- copy of RoadNetwork data & A* ----------------
JUNCTIONS = [
    ("R",   8.49,  -3.10), ("T",  -1.51,   6.90), ("L", -11.51,  -3.10),
    ("B",  -1.51, -13.10), ("S",   1.00,  12.00), ("O1", 14.00,  10.00),
    ("O2",-14.00,  10.00), ("O3",-14.00, -19.00), ("O4", 14.00, -19.00),
    ("E1",  1.00,  22.00), ("E2", 22.00,  -3.00), ("E3",-22.00,  -3.00),
    ("E4",  1.00, -26.00),
]
BRANCHES = [
    (0, 1), (1, 2), (2, 3), (3, 0),
    (4, 1), (4, 6),
    (5, 6), (6, 7), (7, 8), (8, 5),
    (1, 5), (1, 6), (3, 7), (3, 8),
    (9, 4), (10, 0), (11, 2), (12, 3),
    (0, 8), (2, 7),
]
NAME = [j[0] for j in JUNCTIONS]
POS = [(j[1], j[2]) for j in JUNCTIONS]
TURN_PENALTY = 4.0
JUNCTION_REACH = 4.0


def dist(a, b):
    """distance between two junction indices"""
    return math.dist(POS[a], POS[b])


def wdist(a, b):
    """distance between two world points"""
    return math.dist(a, b)


def build_adjacency(blocked):
    n = len(JUNCTIONS)
    adj = [[] for _ in range(n)]
    for a, b in BRANCHES:
        if a in blocked or b in blocked:
            continue
        w = dist(a, b)
        adj[a].append((b, w))
        adj[b].append((a, w))
    return adj


def find_route(frm, to, blocked):
    n = len(JUNCTIONS)
    if frm == to:
        return [frm]
    if to in blocked:
        return None
    adj = build_adjacency(blocked)
    g = [float("inf")] * n
    f = [float("inf")] * n
    came = [-1] * n
    st = [0] * n
    g[frm] = 0.0
    f[frm] = dist(frm, to)
    pq = [(f[frm], frm)]
    st[frm] = 1
    found = False
    while pq:
        _, cur = heapq.heappop(pq)
        if st[cur] == 2:
            continue
        if cur == to:
            found = True
            break
        st[cur] = 2
        for nxt, edge in adj[cur]:
            turn = 0.0 if cur == frm else TURN_PENALTY
            t = g[cur] + edge + turn
            if t < g[nxt]:
                g[nxt] = t
                f[nxt] = t + dist(nxt, to)
                came[nxt] = cur
                if st[nxt] != 1:
                    heapq.heappush(pq, (f[nxt], nxt))
                    st[nxt] = 1
    if not found:
        return None
    r = []
    cur = to
    while cur != -1:
        r.append(cur)
        if cur == frm:
            break
        cur = came[cur]
    r.reverse()
    return r


def nearest_junction(p):
    best, bd = -1, float("inf")
    for i, (x, z) in enumerate(POS):
        d = (x - p[0]) ** 2 + (z - p[1]) ** 2
        if d < bd:
            bd, best = d, i
    return best


# ---------------- AutoDrive replica ----------------
class Car:
    def __init__(self, start, speed=4.0, replan_interval=2.0, stop_at_dest=True):
        self.pos = list(start)
        self.speed = speed
        self.replan_interval = replan_interval
        self.stop_at_dest = stop_at_dest

        self.dest = -1
        self.route = None
        self.route_index = 0
        self.replan_timer = 0.0
        self.finished = False
        self.replan_count = 0
        self.blocked = set()

        # trace
        self.visited_targets = []

    # --- RoadNetwork.NearestJunction ---
    def nearest(self):
        return nearest_junction(self.pos)

    # --- AutoDrive.ReplanRoute ---
    def replan(self):
        if self.dest < 0:
            return False
        frm = self.nearest()
        if frm < 0:
            return False

        if frm == self.dest:
            if self.stop_at_dest:
                self.route = [frm]
                self.route_index = 0
                self.finished = True
            else:
                self.route = None
            return True

        r = find_route(frm, self.dest, self.blocked)
        if r is None or len(r) < 2:
            return False

        # ★ T1: drop the first junction if the car is already on it
        here = POS[r[0]]
        if wdist(self.pos, here) < JUNCTION_REACH * 0.9:
            r = r[1:]

        self.route = r
        self.route_index = 0
        self.replan_count += 1
        return True

    # --- AutoDrive.SetGlobalDestination ---
    def set_dest(self, j):
        self.dest = j
        self.replan_timer = 0.0
        self.finished = False
        self.replan()

    # --- AutoDrive.Update + AdvanceWaypoint ---
    def step(self, dt):
        if self.finished:
            return
        if self.dest < 0:
            return

        # periodic replan  +  ★ per-frame staleness check (mirrors the C# fix)
        route_stale = False
        if self.route is not None:
            for i in range(self.route_index, len(self.route)):
                if self.route[i] in self.blocked:
                    route_stale = True
                    break

        self.replan_timer += dt
        if route_stale or self.replan_timer >= self.replan_interval:
            self.replan_timer = 0.0
            self.replan()

        if self.route is None or self.route_index >= len(self.route):
            return

        target = POS[self.route[self.route_index]]
        self.visited_targets.append(self.route[self.route_index])

        d = wdist(self.pos, target)
        # AtTarget(): distance < reach  OR  (behind car and distance < reach*2)
        if d < JUNCTION_REACH:
            # AdvanceWaypoint()
            self.route_index += 1
            if self.route_index >= len(self.route):
                if self.stop_at_dest:
                    self.finished = True
                else:
                    self.replan()
            return

        # move toward target
        step = self.speed * dt
        if step >= d:
            self.pos = list(target)
        else:
            ux = (target[0] - self.pos[0]) / d
            uz = (target[1] - self.pos[1]) / d
            self.pos[0] += ux * step
            self.pos[1] += uz * step


# ============================================================
fails = 0


def check(label, got, want):
    global fails
    ok = got == want
    if not ok:
        fails += 1
    print(f"  {'PASS' if ok else 'FAIL'}  {label}")
    if not ok:
        print(f"        got  = {got}")
        print(f"        want = {want}")


def run(car, seconds, dt=0.02, log_every=0):
    n = int(seconds / dt)
    for i in range(n):
        car.step(dt)
        if log_every and i % int(log_every / dt) == 0:
            tgt = car.route[car.route_index] if car.route and car.route_index < len(car.route) else None
            print(f"      t={i*dt:5.1f}s pos=({car.pos[0]:6.1f},{car.pos[1]:6.1f}) "
                  f"junc={NAME[car.nearest()]:<3} dest={NAME[car.dest] if car.dest>=0 else '-':<3} "
                  f"target={NAME[tgt] if tgt is not None else '-':<3} "
                  f"idx={car.route_index}/{len(car.route) if car.route else 0} "
                  f"{'FINISHED' if car.finished else ''}")


print("=" * 78)
print("T1: first junction is dropped when the car already sits on it")
print("=" * 78)
c = Car((1.0, 12.0))            # exactly on S (idx 4)
c.set_dest(0)                    # go to R
print(f"  car at S, dest=R")
print(f"  route = {[NAME[i] for i in c.route] if c.route else None}")
check("route does not start with S (the junction under the wheels)",
      c.route[0] != 4, True)
check("route ends at R", c.route[-1], 0)

print()
print("=" * 78)
print("T2: drive S -> R and finish (no livelock)")
print("=" * 78)
c = Car((1.0, 12.0))
c.set_dest(0)
run(c, 40.0)
print(f"  final pos = ({c.pos[0]:.1f}, {c.pos[1]:.1f}), nearest = {NAME[c.nearest()]}")
print(f"  finished = {c.finished}, replans = {c.replan_count}")
check("car reached destination R", c.finished, True)
check("car is at junction R", c.nearest(), 0)
# count distinct targets -- a livelock would show a tiny set repeated hundreds of times
check("replan count is sane (< 40)", c.replan_count < 40, True)

print()
print("=" * 78)
print("T3: drive to a far end point E2 (east)")
print("=" * 78)
c = Car((1.0, 12.0))
c.set_dest(10)                   # E2 at (22,-3)
run(c, 90.0, log_every=15)
print(f"  final pos = ({c.pos[0]:.1f}, {c.pos[1]:.1f}), nearest = {NAME[c.nearest()]}")
check("car reached E2", c.finished, True)
check("car is at E2", c.nearest(), 10)

print()
print("=" * 78)
print("T4: switching target mid-drive works")
print("=" * 78)
c = Car((1.0, 12.0))
c.set_dest(10)                   # head east
run(c, 8.0)
mid = NAME[c.nearest()]
c.set_dest(11)                   # now head west instead
run(c, 90.0)
print(f"  after 8s car was near {mid}, then target switched to E3")
print(f"  final pos = ({c.pos[0]:.1f}, {c.pos[1]:.1f}), nearest = {NAME[c.nearest()]}")
check("car reached the NEW destination E3", c.nearest(), 11)
check("car finished at E3", c.finished, True)

print()
print("=" * 78)
print("T5: blocking a junction mid-drive forces a reroute")
print("=" * 78)
c = Car((1.0, 12.0))
c.set_dest(0)                    # go to R (normally via T)
# Block T *immediately*, before the car has moved. This is the harsh case:
# the pre-computed route ['T','R'] is already stale on frame 1.
c.blocked.add(1)
stale_route = [NAME[i] for i in c.route] if c.route else None
seen_blocked = []
for i in range(int(90 / 0.02)):
    c.step(0.02)
    if c.route and i == 0:
        stale_route = stale_route          # route before the staleness fix kicks in
    if c.route:
        seen_blocked.append(1 if 1 in c.route else 0)
first_replanned = None
c2chk = Car((1.0, 12.0))
c2chk.set_dest(0)
c2chk.blocked.add(1)
c2chk.step(0.02)                            # one frame -> staleness check fires
first_replanned = [NAME[i] for i in c2chk.route] if c2chk.route else None
print(f"  route before the block was detected : {stale_route}  (stale, has T)")
print(f"  route after one frame of driving    : {first_replanned}")
check("stale route did contain T", 'T' in (stale_route or []), True)
check("reroute after 1 frame avoids T", 'T' not in (first_replanned or []), True)
check("reroute goes via the outer ring (O2 -> O3 -> B)",
      first_replanned, ['O2', 'O3', 'B', 'R'])
print(f"  final pos = ({c.pos[0]:.1f}, {c.pos[1]:.1f}), nearest = {NAME[c.nearest()]}")
print(f"  any remaining route still containing blocked T? {any(seen_blocked)}")
check("no remaining route passes through blocked T", any(seen_blocked), False)
check("car still reached R despite the block", c.nearest(), 0)

# and a softer variant: block while the car is still far from the top junction
c2 = Car((1.0, 12.0))
c2.set_dest(12)                  # E4 south
c2.step(0.02)
print()
print("  softer case: car heading to E4, block T after one frame")
c2.blocked.add(1)
bad2 = []
for i in range(int(120 / 0.02)):
    c2.step(0.02)
    if c2.route:
        bad2.append(1 if 1 in c2.route else 0)
print(f"  final nearest = {NAME[c2.nearest()]}")
check("E4 trip never uses blocked T either", any(bad2), False)
check("E4 still reachable via the outer ring", c2.nearest(), 12)

print()
print("=" * 78)
print("T6: stopAtDestination = false keeps the car looping")
print("=" * 78)
c = Car((1.0, 12.0), stop_at_dest=False)
c.set_dest(0)
run(c, 40.0)
print(f"  finished = {c.finished} (expected False)")
check("car does not mark finished when stopAtDestination=false", c.finished, False)

print()
print("=" * 78)
print("T7: a target that gets blocked while en-route reports failure gracefully")
print("=" * 78)
c = Car((1.0, 12.0))
c.set_dest(12)                   # E4 south, single approach via B
c.blocked.add(3)                 # block B -- E4 becomes unreachable
c.replan_timer = 99
ok = c.replan()
print(f"  replan returned {ok} (expected False)")
check("replan reports failure for unreachable goal", ok, False)

print()
print("=" * 78)
if fails == 0:
    print("ALL CHECKS PASSED")
else:
    print(f"{fails} CHECK(S) FAILED")
print("=" * 78)
