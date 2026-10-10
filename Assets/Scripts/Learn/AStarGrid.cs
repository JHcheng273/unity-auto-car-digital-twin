using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* 栅格寻路 —— 车辆绕障用。
///
/// 【参考来源】
///   结构借鉴了 GitHub 上的 LostTrainDude/astar-pathfinding-unity（MIT 协议），
///   它实现的是 Amit Patel 那套经典 A*（https://www.redblobgames.com/pathfinding/a-star/introduction.html）。
///   我在它的基础上改了三处：
///     1. 去掉 SimplePriorityQueue 依赖（那是 700 行的外部库，这里用 List 手写个够用的）
///     2. 障碍查询从 List.Contains 换成 HashSet，否则每个邻居都线性扫一遍，格子一多就卡
///     3. 加了路径平滑（网格路径是锯齿形的，车照着走会一顿一顿）
///
/// 【怎么用】
///   var grid = new AStarGrid(origin, cellSize, cols, rows);
///   grid.Block(circleCenter, radius);                  // 把障碍物占的格子标黑
///   var path = grid.FindPath(车的位置, 目标点);
///   // path 是 List<Vector3>，已经平滑过，车可以直接照着开
///
/// 【坐标系约定】
///   工程里车在 XZ 平面上跑（y 是高度）。所以栅格用 (col, row) 对应 (x, z)，
///   高度 y 一律沿用传入的世界坐标 —— 这样路径点直接能用，不用再贴地。
/// </summary>
public class AStarGrid
{
    // ---------- 配置 ----------
    public readonly Vector3 Origin;   // 栅格 (0,0) 格对应的世界坐标
    public readonly float   CellSize; // 每格边长（米）
    public readonly int     Cols;     // x 方向多少格
    public readonly int     Rows;     // z 方向多少格

    // 允许斜着走吗？允许的话路径更自然，但贴角过障碍时要额外判"两格都空"
    public bool AllowDiagonal = true;

    // 每个格子是不是被挡住
    readonly bool[] _blocked;

    // 相邻查询用的静态方向表（8 邻域），放静态避免每次都新建 List
    static readonly int[] Dx = { -1,  0,  1, -1, 1, -1, 0, 1 };
    static readonly int[] Dz = { -1, -1, -1,  0, 0,  1, 1, 1 };
    // 对应的移动代价：直走 1，斜走 √2
    static readonly float[] StepCost = { 1f, 1f, 1f, 1.414f, 1.414f, 1f, 1f, 1f };

    public AStarGrid(Vector3 origin, float cellSize, int cols, int rows)
    {
        Origin   = origin;
        CellSize = Mathf.Max(cellSize, 0.05f);
        Cols     = Mathf.Max(cols, 2);
        Rows     = Mathf.Max(rows, 2);
        _blocked = new bool[Cols * Rows];
    }

    // ==================== 格子 ↔ 世界坐标 ====================

    /// <summary>世界坐标 → 格子下标。返回 false 表示在栅格外面</summary>
    public bool WorldToCell(Vector3 world, out int col, out int row)
    {
        col = Mathf.FloorToInt((world.x - Origin.x) / CellSize);
        row = Mathf.FloorToInt((world.z - Origin.z) / CellSize);
        return col >= 0 && col < Cols && row >= 0 && row < Rows;
    }

    /// <summary>格子下标 → 格子中心的世界坐标</summary>
    public Vector3 CellToWorld(int col, int row)
    {
        return new Vector3(
            Origin.x + (col + 0.5f) * CellSize,
            Origin.y,
            Origin.z + (row + 0.5f) * CellSize);
    }

    int Index(int col, int row) => row * Cols + col;

    public bool InBounds(int col, int row) => col >= 0 && col < Cols && row >= 0 && row < Rows;

    public bool IsBlocked(int col, int row)
    {
        if (!InBounds(col, row)) return true;      // 界外一律当墙，省得每处都判边界
        return _blocked[Index(col, row)];
    }

    // ==================== 标障碍 ====================

    /// <summary>把一整个格子标为障碍</summary>
    public void BlockCell(int col, int row)
    {
        if (InBounds(col, row)) _blocked[Index(col, row)] = true;
    }

    /// <summary>把格子恢复成可通行（绕障前要先把车自己那圈清出来）</summary>
    public void UnblockCell(int col, int row)
    {
        if (InBounds(col, row)) _blocked[Index(col, row)] = false;
    }

    /// <summary>清空所有障碍</summary>
    public void ClearAll() => System.Array.Clear(_blocked, 0, _blocked.Length);

    /// <summary>
    /// 把一个圆形范围标成障碍（障碍物是世界里的方块，用外接圆近似）。
    ///
    /// padding 是额外膨胀量 —— ★ 这个很重要：
    ///   如果只按障碍物实际大小标格子，算出来的路径会**贴着障碍物边缘**，
    ///   车按路径开过去照样撞。留出半个车宽的余量才安全。
    ///
    /// ★ 判定用的是"格子**外接**是否碰到圆"，而不是"格心是否在圆内"。
    ///   踩过的坑：原来按格心判，会出现"格心在圆外一点点、但格子的一角伸进圆里"
    ///   的情况（实测 7 个格子有这毛病）。A* 于是认为这些格子可走，
    ///   平滑后的直线正好从圆边擦过去 —— 看起来就是"贴着障碍物蹭过去"。
    ///   改成格心距 ≤ r + 半对角线，等价于"格子任一角落碰到圆就标黑"，保守但正确。
    /// </summary>
    public void BlockCircle(Vector3 center, float radius, float padding = 0f)
    {
        float r = radius + padding;
        // 格子的半对角线长度：格心到任一角的距离
        float halfDiag = CellSize * 0.7071f;
        float reach = r + halfDiag;     // 只要格心距 ≤ 这个值，就认为格子跟圆有交叠

        // 先算出这个圆覆盖了哪几格，只遍历这一小块，不用扫全图
        int c0 = Mathf.FloorToInt((center.x - reach - Origin.x) / CellSize);
        int c1 = Mathf.FloorToInt((center.x + reach - Origin.x) / CellSize);
        int r0 = Mathf.FloorToInt((center.z - reach - Origin.z) / CellSize);
        int r1 = Mathf.FloorToInt((center.z + reach - Origin.z) / CellSize);

        c0 = Mathf.Max(c0, 0);            c1 = Mathf.Min(c1, Cols - 1);
        r0 = Mathf.Max(r0, 0);            r1 = Mathf.Min(r1, Rows - 1);

        float reachSq = reach * reach;

        for (int row = r0; row <= r1; row++)
        {
            for (int col = c0; col <= c1; col++)
            {
                // 格子中心到圆心的水平距离
                Vector3 c = CellToWorld(col, row);
                float dx = c.x - center.x;
                float dz = c.z - center.z;

                if (dx * dx + dz * dz <= reachSq)
                    _blocked[Index(col, row)] = true;
            }
        }
    }

    // ==================== A* 搜索 ====================

    /// <summary>
    /// 找一条从 from 到 to 的路。找不到返回 null。
    /// 返回的是**世界坐标点列表**，已经做过平滑，可以直接当路径点用。
    /// </summary>
    public List<Vector3> FindPath(Vector3 from, Vector3 to)
    {
        if (!WorldToCell(from, out int sc, out int sr)) return null;
        if (!WorldToCell(to,   out int gc, out int gr)) return null;

        // 起点/终点落在障碍里（比如车贴着障碍、或被膨胀圈包住）→ 就近挪到能走的格子。
        //
        // ★ 注意这里只是"挪出发点"，**不会去改障碍标记**。
        //   曾经试过"规划前把车周围清干净"，那样会把真障碍也挖掉，
        //   车就直接穿模冲过去了。挪出发点才是对的。
        if (IsBlocked(sc, sr) && !TryFindNearestFree(sc, sr, out sc, out sr)) return null;
        if (IsBlocked(gc, gr) && !TryFindNearestFree(gc, gr, out gc, out gr)) return null;

        int n = Cols * Rows;

        // ---- A* 的三张表 ----
        // gScore：从起点到该格的实际代价
        // fScore：gScore + 启发式（预估还要走多远），决定先扩展谁
        // cameFrom：记录每个格子是从哪来的，最后倒着回溯出路径
        var gScore   = new float[n];
        var fScore   = new float[n];
        var cameFrom = new int[n];
        var state    = new byte[n];   // 0=没碰过 1=在待办里 2=已处理

        for (int i = 0; i < n; i++) { gScore[i] = float.MaxValue; fScore[i] = float.MaxValue; cameFrom[i] = -1; }

        int startIdx = Index(sc, sr);
        int goalIdx  = Index(gc, gr);

        gScore[startIdx] = 0f;
        fScore[startIdx] = Heuristic(sc, sr, gc, gr);

        // 待办表。本来该用二叉堆，但栅格最多几百格，
        // 每次线性找最小值完全够用（几百次比较，微秒级），省掉一个外部依赖。
        var open = new List<int>(64) { startIdx };
        state[startIdx] = 1;

        bool found = false;

        while (open.Count > 0)
        {
            // ---- 取出 fScore 最小的那个 ----
            int bestI   = 0;
            float bestF = fScore[open[0]];
            for (int i = 1; i < open.Count; i++)
            {
                float f = fScore[open[i]];
                if (f < bestF) { bestF = f; bestI = i; }
            }
            int cur = open[bestI];
            open.RemoveAt(bestI);

            if (cur == goalIdx) { found = true; break; }

            state[cur] = 2;
            int cc = cur % Cols;
            int cr = cur / Cols;

            // ---- 扩展 8 个邻居 ----
            int dirCount = AllowDiagonal ? 8 : 4;
            for (int d = 0; d < dirCount; d++)
            {
                int nc = cc + Dx[d];
                int nr = cr + Dz[d];

                if (!InBounds(nc, nr)) continue;
                if (IsBlocked(nc, nr)) continue;

                // ★ 斜走防"穿墙角"：如果斜着走，要求横竖两格至少都通，
                //   否则车会从两个障碍的对角缝里"切"过去，看起来像穿模。
                if (AllowDiagonal && Dx[d] != 0 && Dz[d] != 0)
                {
                    if (IsBlocked(cc + Dx[d], cr) || IsBlocked(cc, cr + Dz[d]))
                        continue;
                }

                int ni = Index(nc, nr);
                if (state[ni] == 2) continue;      // 已定案，不再看

                float tentative = gScore[cur] + StepCost[d];

                if (tentative < gScore[ni])
                {
                    gScore[ni]   = tentative;
                    fScore[ni]   = tentative + Heuristic(nc, nr, gc, gr);
                    cameFrom[ni] = cur;

                    if (state[ni] != 1) { open.Add(ni); state[ni] = 1; }
                }
            }
        }

        if (!found) return null;

        // ---- 回溯 ----
        var cellPath = new List<int>();
        for (int cur = goalIdx; cur != -1; cur = cameFrom[cur])
        {
            cellPath.Add(cur);
            if (cur == startIdx) break;
        }
        cellPath.Reverse();

        // ---- 转成世界坐标 ----
        var raw = new List<Vector3>(cellPath.Count + 1);
        raw.Add(from);                                   // 第一点用车的真实位置，别用格心
        for (int i = 1; i < cellPath.Count; i++)         // 起点格跳过，终点格保留
            raw.Add(CellToWorld(cellPath[i] % Cols, cellPath[i] / Cols));

        return Smooth(raw);
    }

    /// <summary>启发式：对角线距离。比曼哈顿距离更贴合"允许斜走"的场景，扩展的格子更少</summary>
    float Heuristic(int c1, int r1, int c2, int r2)
    {
        int dc = Mathf.Abs(c1 - c2);
        int dr = Mathf.Abs(r1 - r2);
        // 斜走部分按 √2 计，剩下的直线部分按 1 计
        return 1.414f * Mathf.Min(dc, dr) + Mathf.Abs(dc - dr);
    }

    /// <summary>周围找一格能走的（螺旋向外，最多找 5 格半径）</summary>
    bool TryFindNearestFree(int col, int row, out int fc, out int fr)
    {
        for (int radius = 1; radius <= 5; radius++)
        {
            for (int dc = -radius; dc <= radius; dc++)
            {
                for (int dr = -radius; dr <= radius; dr++)
                {
                    if (Mathf.Abs(dc) != radius && Mathf.Abs(dr) != radius) continue; // 只看这一圈
                    int c = col + dc, r = row + dr;
                    if (InBounds(c, r) && !IsBlocked(c, r)) { fc = c; fr = r; return true; }
                }
            }
        }
        fc = col; fr = row;
        return false;
    }

    /// <summary>
    /// 路径平滑：把"能一条直线走到的中间点"全删掉。
    ///
    /// 为什么必须做：A* 出来的路径是逐格连的，格子边界处会有小折角，
    /// 车照着走会左一下右一下地画蛇。用视线检测（LineOfSight）把
    /// 连续共线的点合并掉，路径就变成几条长直线。
    /// </summary>
    List<Vector3> Smooth(List<Vector3> pts)
    {
        if (pts == null || pts.Count <= 2) return pts;

        var outp = new List<Vector3> { pts[0] };
        int anchor = 0;

        for (int i = 2; i < pts.Count; i++)
        {
            if (!LineOfSight(pts[anchor], pts[i]))
            {
                // 走不通 → 上一个点必须保留
                outp.Add(pts[i - 1]);
                anchor = i - 1;
            }
        }
        outp.Add(pts[pts.Count - 1]);
        return outp;
    }

    /// <summary>两点之间连直线，会不会穿过障碍格？（逐格采样，够用且好懂）</summary>
    bool LineOfSight(Vector3 a, Vector3 b)
    {
        float dist  = Vector3.Distance(a, b);
        int   steps = Mathf.CeilToInt(dist / (CellSize * 0.5f));   // 半步一采，不漏格

        for (int i = 0; i <= steps; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, (float)i / steps);
            if (!WorldToCell(p, out int c, out int r)) return false;
            if (IsBlocked(c, r)) return false;
        }
        return true;
    }

    // ==================== 调试可视化 ====================

    /// <summary>在 Scene 视图里画出哪些格子被标黑了 —— 排查"为什么找不到路"就看它</summary>
    public void DrawGizmos()
    {
        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                Vector3 c = CellToWorld(col, row);
                if (IsBlocked(col, row))
                {
                    Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.25f);
                    Gizmos.DrawCube(c, new Vector3(CellSize, 0.02f, CellSize));
                }
                else
                {
                    Gizmos.color = new Color(0.3f, 0.8f, 0.4f, 0.08f);
                    Gizmos.DrawWireCube(c, new Vector3(CellSize, 0.02f, CellSize));
                }
            }
        }
    }
}
