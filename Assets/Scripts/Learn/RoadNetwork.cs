using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 路网 + A* 全局寻路 —— 让小车"自己算该走哪条支路"。
///
/// 【它和 AStarGrid 的分工（★ 别搞混）】
///   AStarGrid     —— **局部**绕障。在一小块栅格上算"怎么绕过前面这个障碍物"。
///                    地图是临时的，只在遇到障碍时用。
///   RoadNetwork   —— **全局**选路。在一张路口图上算"从起点到目标该经过哪些路口"。
///                    地图是固定的（你手画的支路），一直都在。
///
///   两层配合：RoadNetwork 给出"该走哪几个路口"，
///             AutoDrive 沿路口开，中途遇到障碍再叫 AStarGrid 来局部绕。
///
/// 【为什么全局也用 A*，而不是直接连直线】
///   因为有路！车必须走在路上。从右上角到左下角，直线会穿过草坪。
///   所以要在"路口图"上找最短路 —— 这正是 A* 的主场：
///   有岔路口就有选择，有选择就要算哪条最短。
///
/// 【图长什么样】
///   节点 = 路口（Junction，有世界坐标）
///   边   = 两个路口之间的直路（支路）
///   边长 = 两路口的实际距离（米）—— A* 的代价
///   启发式 = 到目标的直线距离（欧氏距离）—— 保证能找最短，且扩展得快
///
/// 【★ 和"沿固定环线跑"的区别】
///   原来 4 个路标点是一个固定闭环，车只能一条道走到黑。
///   现在有了支路，A* 会挑一条更短的组合 —— 这就是"最短路径"。
///   封掉某个路口，A* 自动改走别的支路，不会卡死。
///
/// 【坐标系】Unity XZ 平面，y 恒为 0（地面）。
/// </summary>
public class RoadNetwork : MonoBehaviour
{
    /// <summary>一个路口</summary>
    [System.Serializable]
    public class Junction
    {
        [Tooltip("名字，画图和日志里用")]
        public string name = "J";

        [Tooltip("世界坐标（y 会被强制成 0）")]
        public Vector3 position;

        [Tooltip("★ 手动封路：勾上 = 这个路口不可通行（A* 会绕开）。" +
                 "演示「路线随时变化」时可以勾/取消它")]
        public bool blocked;

        public Junction() { }
        public Junction(string n, float x, float z)
        {
            name = n;
            position = new Vector3(x, 0f, z);
        }
    }

    /// <summary>一条支路（两个路口之间的直路）</summary>
    [System.Serializable]
    public class Branch
    {
        [Tooltip("起点路口下标")]
        public int from;

        [Tooltip("终点路口下标")]
        public int to;

        [Tooltip("★ 手动封路：勾上 = 这条路断了")]
        public bool blocked;

        public Branch() { }
        public Branch(int a, int b) { from = a; to = b; }
    }

    [Header("路口（节点）")]
    public List<Junction> junctions = new List<Junction>();

    [Header("支路（边）")]
    public List<Branch> branches = new List<Branch>();

    [Header("寻路参数")]
    [Tooltip("★ 拐弯惩罚：每经过一个路口额外加这么多「等效米数」。" +
             "不设的话，A* 会算出「拐 8 个弯的 30 米」和「直走 30 米」一样好，" +
             "车就会一路画蛇。给一点惩罚，它会偏好少拐弯的路")]
    public float turnPenalty = 4f;

    [Tooltip("自动把每条支路也当成回程（无向图）。路网一般都能双向走")]
    public bool bidirectional = true;

    [Header("调试")]
    public bool logStats = true;
    [Tooltip("跑一次自检：连通性、孤立点、坐标合理性")]
    public bool runSelfTest = false;

    // ==================== 对外接口 ====================

    public int JunctionCount => junctions.Count;

    /// <summary>车现在"归属"哪个路口（最近的那个）</summary>
    public int NearestJunction(Vector3 worldPos)
    {
        int best = -1;
        float bestD = float.MaxValue;
        for (int i = 0; i < junctions.Count; i++)
        {
            // 只看水平距离，y 不参与
            float dx = junctions[i].position.x - worldPos.x;
            float dz = junctions[i].position.z - worldPos.z;
            float d = dx * dx + dz * dz;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    public Vector3 GetPosition(int index)
    {
        if (index < 0 || index >= junctions.Count) return Vector3.zero;
        return junctions[index].position;
    }

    public string GetName(int index)
    {
        if (index < 0 || index >= junctions.Count) return "?";
        return junctions[index].name;
    }

    /// <summary>某个路口能不能走（没被手动封掉）</summary>
    public bool IsJunctionOpen(int index)
    {
        if (index < 0 || index >= junctions.Count) return false;
        return !junctions[index].blocked;
    }

    /// <summary>显示用：把路口序列拼成 "T -> R -> B"</summary>
    public string PathToString(IReadOnlyList<int> route)
    {
        if (route == null || route.Count == 0) return "（空）";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < route.Count; i++)
        {
            if (i > 0) sb.Append(" -> ");
            sb.Append(GetName(route[i]));
        }
        return sb.ToString();
    }

    /// <summary>一条路线的实际长度（米）</summary>
    public float RouteLength(IReadOnlyList<int> route)
    {
        if (route == null || route.Count < 2) return 0f;
        float sum = 0f;
        for (int i = 1; i < route.Count; i++)
            sum += Vector3.Distance(GetPosition(route[i - 1]), GetPosition(route[i]));
        return sum;
    }

    // ==================== A* 主体 ====================

    /// <summary>
    /// 全局 A*：算一条从 fromIndex 到 toIndex 的最短路口序列。
    /// 找不到（被堵死 / 下标非法）返回 null。
    ///
    /// 返回的列表**包含**起点和终点，直接就是"按顺序要通过的路口"。
    /// </summary>
    public List<int> FindRoute(int fromIndex, int toIndex)
    {
        int n = junctions.Count;
        if (n == 0) return null;
        if (fromIndex < 0 || fromIndex >= n) return null;
        if (toIndex < 0 || toIndex >= n) return null;
        if (fromIndex == toIndex) return new List<int> { fromIndex };

        // ---- 终点被封 → 没戏（起点被封时还能"从起点开出去"，所以不拦）----
        if (!IsJunctionOpen(toIndex)) return null;

        // ---- 邻接表：每次规划现建。路口就十几个，重建的开销可以忽略，
        //      换来的是"封路改了立刻生效"，不用维护缓存失效逻辑 ----
        var adj = BuildAdjacency();

        // ---- A* 三张表 ----
        var gScore   = new float[n];
        var fScore   = new float[n];
        var cameFrom = new int[n];
        var state    = new byte[n];      // 0 没碰过 / 1 在待办 / 2 已处理
        var cameJunc = new int[n];       // 是从哪个路口拐过来的（算拐弯惩罚用）

        for (int i = 0; i < n; i++)
        {
            gScore[i] = float.MaxValue;
            fScore[i] = float.MaxValue;
            cameFrom[i] = -1;
            cameJunc[i] = -1;
        }

        gScore[fromIndex] = 0f;
        fScore[fromIndex] = Heuristic(fromIndex, toIndex);

        // 待办表。路口十几个，线性找最小完全够用，省掉一个优先队列依赖
        var open = new List<int>(n) { fromIndex };
        state[fromIndex] = 1;

        bool found = false;

        while (open.Count > 0)
        {
            // 取 fScore 最小的
            int bestI = 0;
            float bestF = fScore[open[0]];
            for (int i = 1; i < open.Count; i++)
            {
                float f = fScore[open[i]];
                if (f < bestF) { bestF = f; bestI = i; }
            }
            int cur = open[bestI];
            open.RemoveAt(bestI);

            if (cur == toIndex) { found = true; break; }

            state[cur] = 2;

            var neighbours = adj[cur];
            for (int k = 0; k < neighbours.Count; k++)
            {
                int nxt = neighbours[k].node;
                float edge = neighbours[k].cost;

                // ★ 拐弯惩罚：只有在"要拐弯"的时候才加。
                //   判据：当前这一步的方向 和 上一步的方向 不是同一个路口对。
                //   简化实现：只要经过一个中间路口就算一次拐弯 ——
                //   因为路口本来就是"可能要拐"的地方，直着过去的情况很少，
                //   给个小的固定值比算角度便宜、也够用。
                float turn = 0f;
                if (cur != fromIndex) turn = turnPenalty;

                float tentative = gScore[cur] + edge + turn;

                if (tentative < gScore[nxt])
                {
                    gScore[nxt]   = tentative;
                    fScore[nxt]   = tentative + Heuristic(nxt, toIndex);
                    cameFrom[nxt] = cur;
                    if (state[nxt] != 1) { open.Add(nxt); state[nxt] = 1; }
                }
            }
        }

        if (!found) return null;

        // ---- 回溯 ----
        var route = new List<int>();
        for (int cur = toIndex; cur != -1; cur = cameFrom[cur])
        {
            route.Add(cur);
            if (cur == fromIndex) break;
        }
        route.Reverse();
        return route;
    }

    struct Neighbour
    {
        public int node;
        public float cost;
    }

    /// <summary>按当前封路状态建邻接表</summary>
    List<Neighbour>[] BuildAdjacency()
    {
        int n = junctions.Count;
        var adj = new List<Neighbour>[n];
        for (int i = 0; i < n; i++) adj[i] = new List<Neighbour>(4);

        for (int i = 0; i < branches.Count; i++)
        {
            Branch b = branches[i];
            if (b.blocked) continue;

            if (b.from < 0 || b.from >= n || b.to < 0 || b.to >= n) continue;
            if (b.from == b.to) continue;

            // 两端有任一端封了，这条路就不能走
            if (!IsJunctionOpen(b.from) || !IsJunctionOpen(b.to)) continue;

            float w = Vector3.Distance(junctions[b.from].position, junctions[b.to].position);
            if (w < 0.01f) continue;

            adj[b.from].Add(new Neighbour { node = b.to, cost = w });
            if (bidirectional)
                adj[b.to].Add(new Neighbour { node = b.from, cost = w });
        }
        return adj;
    }

    /// <summary>启发式：到目标的直线距离（欧氏）。
    /// 它永远 ≤ 实际要走的路，所以 A* 保证能找到最短 —— 这是可采纳性(admissible)。</summary>
    float Heuristic(int a, int b)
    {
        Vector3 pa = junctions[a].position;
        Vector3 pb = junctions[b].position;
        float dx = pa.x - pb.x;
        float dz = pa.z - pb.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // ==================== 自检 ====================

    /// <summary>
    /// 跑一遍体检：找孤立路口、检查坐标、跑几条示例路线。
    /// Inspector 里勾 runSelfTest，或者从菜单调。
    /// </summary>
    public void SelfTest()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== 路网自检 =====");
        sb.AppendLine($"路口 {junctions.Count} 个，支路 {branches.Count} 条");

        // ---- 1. 下标越界 / 自环 ----
        int badEdge = 0;
        for (int i = 0; i < branches.Count; i++)
        {
            Branch b = branches[i];
            if (b.from < 0 || b.from >= junctions.Count ||
                b.to < 0 || b.to >= junctions.Count || b.from == b.to)
            {
                sb.AppendLine($"  ✘ 支路 #{i}（{b.from}->{b.to}）下标非法或自环");
                badEdge++;
            }
        }
        if (badEdge == 0) sb.AppendLine("  ✔ 所有支路下标合法");

        // ---- 2. 孤立路口 ----
        var deg = new int[junctions.Count];
        for (int i = 0; i < branches.Count; i++)
        {
            Branch b = branches[i];
            if (b.from >= 0 && b.from < junctions.Count) deg[b.from]++;
            if (b.to >= 0 && b.to < junctions.Count) deg[b.to]++;
        }
        int isolated = 0;
        for (int i = 0; i < junctions.Count; i++)
        {
            if (deg[i] == 0)
            {
                sb.AppendLine($"  ✘ 路口 {GetName(i)}（#{i}）没有任何支路连着 —— A* 永远走不到它");
                isolated++;
            }
        }
        if (isolated == 0) sb.AppendLine("  ✔ 没有孤立路口");

        // ---- 3. 连通性（从 0 号路口能到几个）----
        int reach = CountReachable(0);
        sb.AppendLine($"  从 {GetName(0)} 出发能到达 {reach}/{junctions.Count} 个路口");
        if (reach < junctions.Count)
            sb.AppendLine("    ⚠ 图不连通 —— 有路口在另一个孤岛上");

        // ---- 4. 跑几条示例路线 ----
        sb.AppendLine();
        sb.AppendLine("----- 示例路线（全局 A*）-----");
        int[,] demos = { { 0, 5 }, { 0, 6 }, { 2, 4 }, { 5, 7 } };
        for (int d = 0; d < demos.GetLength(0); d++)
        {
            int a = demos[d, 0], b = demos[d, 1];
            if (a >= junctions.Count || b >= junctions.Count) continue;
            var r = FindRoute(a, b);
            sb.AppendLine(r == null
                ? $"  {GetName(a)} -> {GetName(b)} : 无解"
                : $"  {GetName(a)} -> {GetName(b)} : {PathToString(r)}  ({RouteLength(r):F1} 米)");
        }

        // ---- 5. 封一个路口再跑，看会不会改道 ----
        if (junctions.Count > 3)
        {
            sb.AppendLine();
            sb.AppendLine("----- 封路测试 -----");
            var before = FindRoute(0, 5);
            int seal = before != null && before.Count > 2 ? before[1] : -1;
            if (seal >= 0)
            {
                junctions[seal].blocked = true;
                var after = FindRoute(0, 5);
                sb.AppendLine($"  封掉「{GetName(seal)}」之前：{PathToString(before)}");
                sb.AppendLine($"  封掉「{GetName(seal)}」之后：{(after == null ? "无解" : PathToString(after))}");
                junctions[seal].blocked = false;    // 还原
            }
        }

        Debug.Log(sb.ToString(), this);
    }

    int CountReachable(int start)
    {
        if (junctions.Count == 0) return 0;
        var adj = BuildAdjacency();
        var seen = new bool[junctions.Count];
        var st = new Stack<int>();
        st.Push(start);
        seen[start] = true;
        int count = 0;
        while (st.Count > 0)
        {
            int cur = st.Pop();
            count++;
            var nb = adj[cur];
            for (int i = 0; i < nb.Count; i++)
            {
                if (!seen[nb[i].node]) { seen[nb[i].node] = true; st.Push(nb[i].node); }
            }
        }
        return count;
    }

    [ContextMenu("跑一遍自检")]
    void MenuSelfTest() => SelfTest();

    [ContextMenu("★ 填成 mycar 场景的默认路网")]
    void MenuFillDefault()
    {
        FillDefaultNetwork();
        Debug.Log("[RoadNetwork] 已填入默认路网（13 路口 / 22 支路）", this);
    }

    /// <summary>
    /// 按 mycar.unity 的真实坐标填一份默认路网。
    /// 编辑器菜单和运行时都可以调 —— 幂等，重复调不会重复加。
    /// </summary>
    public void FillDefaultNetwork()
    {
        junctions.Clear();
        branches.Clear();

        // ---- 内环：原来那 4 个路标点 ----
        junctions.Add(new Junction("R",   8.49f,  -3.10f));   // 0  右  (== WP0)
        junctions.Add(new Junction("T",  -1.51f,   6.90f));   // 1  上  (== WP1)
        junctions.Add(new Junction("L", -11.51f,  -3.10f));   // 2  左  (== WP2)
        junctions.Add(new Junction("B",  -1.51f, -13.10f));   // 3  下  (== WP3)
        junctions.Add(new Junction("S",   1.00f,  12.00f));   // 4  起点枢纽（车从北边来）

        // ---- 外环：4 个外圈路口，围出一个大环 ----
        junctions.Add(new Junction("O1",  14.0f,  10.0f));    // 5
        junctions.Add(new Junction("O2", -14.0f,  10.0f));    // 6
        junctions.Add(new Junction("O3", -14.0f, -19.0f));    // 7
        junctions.Add(new Junction("O4",  14.0f, -19.0f));    // 8

        // ---- 远端终点：4 个辐射支路的尽头 ----
        junctions.Add(new Junction("E1",   1.0f,  22.0f));    // 9
        junctions.Add(new Junction("E2",  22.0f,  -3.0f));    // 10
        junctions.Add(new Junction("E3", -22.0f,  -3.0f));    // 11
        junctions.Add(new Junction("E4",   1.0f, -26.0f));    // 12

        // ---- 支路 ----
        // 内环（原来那个方块闭环）
        AddBranch(0, 1); AddBranch(1, 2); AddBranch(2, 3); AddBranch(3, 0);

        // 起点枢纽 → ★ 连两个路口。只连一个的话，那个路口一封车就彻底出不去
        AddBranch(4, 1);   // S -> T
        AddBranch(4, 6);   // S -> O2

        // 外环
        AddBranch(5, 6); AddBranch(6, 7); AddBranch(7, 8); AddBranch(8, 5);

        // 内外环之间的横向连接
        AddBranch(1, 5); AddBranch(1, 6);   // T <-> O1 / O2
        AddBranch(3, 7); AddBranch(3, 8);   // B <-> O3 / O4

        // 辐射支路（通往远端的 4 个终点）
        AddBranch(9, 4);    // E1 - S   北
        AddBranch(10, 0);   // E2 - R   东
        AddBranch(11, 2);   // E3 - L   西
        AddBranch(12, 3);   // E4 - B   南

        // ★ 两条捷径（斜穿，只有这里有）
        AddBranch(0, 8);    // R -> O4   右下斜穿
        AddBranch(2, 7);    // L -> O3   左下斜穿
    }

    void AddBranch(int a, int b)
    {
        branches.Add(new Branch(a, b));
    }

    void OnValidate()
    {
        if (runSelfTest)
        {
            runSelfTest = false;
            // OnValidate 里不能直接 Debug.Log 太多，推迟一帧
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) SelfTest();
            };
        }
    }

    // ==================== 调试绘制 ====================

    void OnDrawGizmosSelected()
    {
        // 支路
        for (int i = 0; i < branches.Count; i++)
        {
            Branch b = branches[i];
            if (b.from < 0 || b.from >= junctions.Count) continue;
            if (b.to < 0 || b.to >= junctions.Count) continue;

            Vector3 a = junctions[b.from].position;
            Vector3 c = junctions[b.to].position;

            bool usable = !b.blocked && IsJunctionOpen(b.from) && IsJunctionOpen(b.to);
            Gizmos.color = usable ? new Color(0.25f, 0.75f, 0.35f, 0.9f)
                                  : new Color(0.85f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawLine(a, c);

            // 中点画个小方块，一眼看出这条边通不通
            Gizmos.DrawCube(Vector3.Lerp(a, c, 0.5f), new Vector3(0.5f, 0.05f, 0.5f));
        }

        // 路口
        for (int i = 0; i < junctions.Count; i++)
        {
            Vector3 p = junctions[i].position;
            Gizmos.color = junctions[i].blocked
                ? new Color(0.9f, 0.15f, 0.15f, 1f)
                : new Color(0.2f, 0.55f, 1f, 1f);
            Gizmos.DrawWireSphere(p, 1.0f);
            Gizmos.DrawCube(p, new Vector3(0.4f, 0.08f, 0.4f));
        }
    }
}
