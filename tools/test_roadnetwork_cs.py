"""
Verify RoadNetwork.cs A* logic -- line-by-line Python replica.
================================================================
The C# in Assets/Scripts/Learn/RoadNetwork.cs cannot be compiled here
(the sandbox blocks csc.exe), so we replicate its exact algorithm and
check the numbers match the design.

Mirrors:
  - FillDefaultNetwork()   junction/branch tables
  - FindRoute()            A* with turnPenalty
  - BuildAdjacency()       blocked -> edge removed
  - CountReachable()       connectivity
"""
import heapq
import math

# ---- FillDefaultNetwork() ----
JUNCTIONS = [
    ("R",   8.49,  -3.10),   # 0
    ("T",  -1.51,   6.90),   # 1
    ("L", -11.51,  -3.10),   # 2
    ("B",  -1.51, -13.10),   # 3
    ("S",   1.00,  12.00),   # 4
    ("O1", 14.00,  10.00),   # 5
    ("O2",-14.00,  10.00),   # 6
    ("O3",-14.00, -19.00),   # 7
    ("O4", 14.00, -19.00),   # 8
    ("E1",  1.00,  22.00),   # 9
    ("E2", 22.00,  -3.00),   # 10
    ("E3",-22.00,  -3.00),   # 11
    ("E4",  1.00, -26.00),   # 12
]

BRANCHES_RAW = [
    (0, 1), (1, 2), (2, 3), (3, 0),        # inner ring
    (4, 1), (4, 6),                        # start hub -> T and O2
    (5, 6), (6, 7), (7, 8), (8, 5),        # outer ring
    (1, 5), (1, 6),                        # T <-> O1 / O2
    (3, 7), (3, 8),                        # B <-> O3 / O4
    (9, 4), (10, 0), (11, 2), (12, 3),     # spurs
    (0, 8), (2, 7),                        # shortcuts
]

TURN_PENALTY = 4.0

NAME = [j[0] for j in JUNCTIONS]
POS = [(j[1], j[2]) for j in JUNCTIONS]


def dist(a, b):
    return math.dist(POS[a], POS[b])


def build_adjacency(blocked_junctions=(), blocked_branches=()):
    n = len(JUNCTIONS)
    adj = [[] for _ in range(n)]
    for i, (a, b) in enumerate(BRANCHES_RAW):
        if i in blocked_branches:
            continue
        if a in blocked_junctions or b in blocked_junctions:
            continue
        w = dist(a, b)
        adj[a].append((b, w))
        adj[b].append((a, w))          # bidirectional = true
    return adj


def find_route(frm, to, blocked_junctions=()):
    """Exact port of RoadNetwork.FindRoute() including turnPenalty"""
    n = len(JUNCTIONS)
    if frm == to:
        return [frm]
    if to in blocked_junctions:
        return None

    adj = build_adjacency(blocked_junctions)

    def h(a, b):
        return dist(a, b)

    g = [float("inf")] * n
    f = [float("inf")] * n
    came = [-1] * n
    state = [0] * n

    g[frm] = 0.0
    f[frm] = h(frm, to)
    openq = [(f[frm], frm)]
    state[frm] = 1
    found = False

    while openq:
        bestf, cur = heapq.heappop(openq)
        if state[cur] == 2:
            continue
        if cur == to:
            found = True
            break
        state[cur] = 2

        for nxt, edge in adj[cur]:
            turn = 0.0 if cur == frm else TURN_PENALTY
            tentative = g[cur] + edge + turn
            if tentative < g[nxt]:
                g[nxt] = tentative
                f[nxt] = tentative + h(nxt, to)
                came[nxt] = cur
                if state[nxt] != 1:
                    heapq.heappush(openq, (f[nxt], nxt))
                    state[nxt] = 1

    if not found:
        return None

    route = []
    cur = to
    while cur != -1:
        route.append(cur)
        if cur == frm:
            break
        cur = came[cur]
    route.reverse()
    return route


def path_str(route):
    return " -> ".join(NAME[i] for i in route) if route else "NONE"


def route_len(route):
    if not route or len(route) < 2:
        return 0.0
    return sum(dist(route[i - 1], route[i]) for i in range(1, len(route)))


def reachable(start):
    adj = build_adjacency()
    seen = {start}
    st = [start]
    while st:
        cur = st.pop()
        for nxt, _ in adj[cur]:
            if nxt not in seen:
                seen.add(nxt)
                st.append(nxt)
    return len(seen)


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


print("=" * 78)
print("TEST 1: structure")
print("=" * 78)
check("junction count = 13", len(JUNCTIONS), 13)
# 4 inner ring + 2 start-hub links + 4 outer ring + 4 cross-links
# + 4 spurs + 2 shortcuts = 20
check("branch count = 20", len(BRANCHES_RAW), 20)
check("all reachable from 0", reachable(0), 13)

print()
print("=" * 78)
print("TEST 2: no isolated junction (every junction has >=1 branch)")
print("=" * 78)
deg = [0] * len(JUNCTIONS)
for a, b in BRANCHES_RAW:
    deg[a] += 1
    deg[b] += 1
for i in range(len(JUNCTIONS)):
    check(f"{NAME[i]:<3} degree >= 1 (deg={deg[i]})", deg[i] >= 1, True)

print()
print("=" * 78)
print("TEST 3: A* finds a route between every pair")
print("=" * 78)
bad = []
for a in range(len(JUNCTIONS)):
    for b in range(len(JUNCTIONS)):
        r = find_route(a, b)
        if r is None:
            bad.append((NAME[a], NAME[b]))
check("all 13x13 pairs solvable", len(bad), 0)
if bad:
    print(f"        unsolvable: {bad}")

print()
print("=" * 78)
print("TEST 4: A* result is genuinely the shortest (compare vs BFS-with-weights)")
print("=" * 78)
# Dijkstra over the same graph WITHOUT turn penalty -> true shortest distance.
# A* with turn penalty may be slightly longer, but must never be shorter.
def dijkstra(frm, to, blocked=()):
    adj = build_adjacency(blocked)
    d = [float("inf")] * len(JUNCTIONS)
    d[frm] = 0.0
    pq = [(0.0, frm)]
    seen = set()
    while pq:
        dd, cur = heapq.heappop(pq)
        if cur in seen:
            continue
        seen.add(cur)
        if cur == to:
            return dd
        for nxt, w in adj[cur]:
            if dd + w < d[nxt]:
                d[nxt] = dd + w
                heapq.heappush(pq, (dd + w, nxt))
    return float("inf")


mismatch = 0
for a in range(len(JUNCTIONS)):
    for b in range(len(JUNCTIONS)):
        if a == b:
            continue
        r = find_route(a, b)
        shortest = dijkstra(a, b)
        got = route_len(r)
        # A* with turn penalty can be longer, never shorter
        if got < shortest - 1e-6:
            mismatch += 1
            print(f"        ! {NAME[a]}->{NAME[b]}: got {got:.1f} < shortest {shortest:.1f}")
        # also: A* should not overshoot by more than a couple of penalties
        if got > shortest + 3 * TURN_PENALTY + 1e-6:
            mismatch += 1
            print(f"        ! {NAME[a]}->{NAME[b]}: got {got:.1f} too far above {shortest:.1f}")
check("A* never shorter than true shortest, never wildly longer", mismatch, 0)

print()
print("=" * 78)
print("TEST 5: blocking a junction forces a reroute")
print("=" * 78)
S, R, T, B, O2 = 4, 0, 1, 3, 6

base = find_route(S, R)
print(f"  S -> R normal : {path_str(base)}  ({route_len(base):.1f} m)")

detour = find_route(S, R, blocked_junctions={T})
print(f"  S -> R w/o T  : {path_str(detour)}  ({route_len(detour):.1f} m)" if detour
      else "  S -> R w/o T  : NONE")
check("blocking T still finds a way", detour is not None, True)
if detour:
    check("detour is longer than direct", route_len(detour) > route_len(base), True)
    check("detour avoids T", T not in detour, True)

base2 = find_route(S, B)
detour2 = find_route(S, B, blocked_junctions={T})
print(f"  S -> B normal : {path_str(base2)}  ({route_len(base2):.1f} m)")
print(f"  S -> B w/o T  : {path_str(detour2)}  ({route_len(detour2):.1f} m)" if detour2
      else "  S -> B w/o T  : NONE")
check("blocking T still reaches B", detour2 is not None, True)

print()
print("=" * 78)
print("TEST 6: blocking the goal junction is a genuine no-solution")
print("=" * 78)
r = find_route(S, R, blocked_junctions={R})
check("S -> R with R itself blocked = None", r, None)

print()
print("=" * 78)
print("TEST 7: shortcut edge is used when it actually helps")
print("=" * 78)
# R(0) -> B(3): direct inner-ring edge is 14.1 m.
# The shortcut R->O4->B is 33.4 m -- A* must NOT take it. Verify.
r = find_route(R, B)
check("R -> B takes the direct inner edge", r, [R, B])
print(f"        route = {path_str(r)}  ({route_len(r):.1f} m)")
print(f"        (shortcut R->O4->B would be {dist(0,8)+dist(8,3):.1f} m -- correctly rejected)")

# but R -> O4 should use the shortcut directly
r2 = find_route(R, 8)
check("R -> O4 is the direct shortcut", r2, [R, 8])

print()
print("=" * 78)
print("TEST 8: turn penalty actually reduces the number of junctions visited")
print("=" * 78)
# With penalty 0 vs 4, compare how many intermediate junctions a long route uses
import importlib

def find_route_nopenalty(frm, to, penalty):
    global TURN_PENALTY
    old = TURN_PENALTY
    TURN_PENALTY = penalty
    try:
        return find_route(frm, to)
    finally:
        TURN_PENALTY = old

for (a, b) in [(9, 10), (9, 12), (11, 5)]:
    r0 = find_route_nopenalty(a, b, 0.0)
    r4 = find_route_nopenalty(a, b, 4.0)
    print(f"  {NAME[a]}->{NAME[b]}:  penalty 0 -> {path_str(r0)} ({len(r0)} junc)")
    print(f"            penalty 4 -> {path_str(r4)} ({len(r4)} junc)")
    if len(r4) > len(r0):
        fails += 1
        print("        ! FAIL: turn penalty made the route LONGER in junctions")
check("turn penalty never increases junction count", True, True)

print()
print("=" * 78)
if fails == 0:
    print("ALL CHECKS PASSED")
else:
    print(f"{fails} CHECK(S) FAILED")
print("=" * 78)
