using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 고양이용 길찾기. CatSpawner가 찾은 걸어갈 수 있는 바닥 칸(0.5m 격자)을 고양이 키 높이에서 서로 이어 보고(가구·벽·닫힌 문에 막히지 않는 칸끼리),
/// 두 지점 사이를 BFS로 잇고 꺾이는 점만 남겨 경로를 만듭니다. 숨는 자리는 가구에 둘러싸여 직선으로는 못 가는 경우가 많아서 필요합니다.
/// </summary>
public static class CatNav
{
    const float CellSize = 0.5f;
    const float CatHeight = 0.25f;  // 캐스트 높이 (고양이 몸통 중심)
    const float CatRadius = 0.14f;

    static List<Vector3> cells;
    static Dictionary<Vector2Int, int> index;
    static List<int>[] adjacency;
    static Vector3 origin;
    static int mask;

    public static bool Ready { get { return cells != null && cells.Count > 0; } }

    public static void Build(List<Vector3> walkable, Vector3 gridOrigin, int layerMask)
    {
        cells = walkable;
        origin = gridOrigin;
        mask = layerMask;
        index = new Dictionary<Vector2Int, int>();
        if (cells == null) return;
        for (int i = 0; i < cells.Count; i++) index[Key(cells[i])] = i;

        adjacency = new List<int>[cells.Count];
        for (int i = 0; i < cells.Count; i++) adjacency[i] = new List<int>(8);
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int k = Key(cells[i]);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                int j;
                if (!index.TryGetValue(new Vector2Int(k.x + dx, k.y + dz), out j) || j <= i) continue;
                if (!Clear(cells[i], cells[j])) continue;
                adjacency[i].Add(j);
                adjacency[j].Add(i);
            }
        }
    }

    static Vector2Int Key(Vector3 p)
    {
        return new Vector2Int(Mathf.RoundToInt((p.x - origin.x) / CellSize), Mathf.RoundToInt((p.z - origin.z) / CellSize));
    }

    /// <summary>고양이 키 높이에서 a→b가 막히지 않고 같은 높이인지.</summary>
    public static bool Clear(Vector3 a, Vector3 b)
    {
        if (Mathf.Abs(a.y - b.y) > 0.25f) return false;
        Vector3 d = b - a;
        d.y = 0f;
        float len = d.magnitude;
        if (len < 0.01f) return true;
        return !Physics.SphereCast(a + Vector3.up * CatHeight, CatRadius, d / len, out _, len, mask, QueryTriggerInteraction.Ignore);
    }

    static int Nearest(Vector3 p)
    {
        Vector2Int k = Key(p);
        int best = -1;
        float bestDist = float.MaxValue;
        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            int i;
            if (!index.TryGetValue(new Vector2Int(k.x + dx, k.y + dz), out i)) continue;
            float d = Vector3.Distance(cells[i], p);
            if (d >= bestDist) continue;
            if (d > 0.05f && !Clear(p, cells[i])) continue;
            best = i;
            bestDist = d;
        }
        return best;
    }

    static readonly Dictionary<int, int> cameFrom = new Dictionary<int, int>();
    static readonly Queue<int> queue = new Queue<int>();
    static readonly List<Vector3> points = new List<Vector3>();

    /// <summary>
    /// from에서 to까지의 경로(꺾이는 점들, 마지막은 to)를 path에 담습니다. 못 가면 false.
    /// </summary>
    public static bool FindPath(Vector3 from, Vector3 to, List<Vector3> path, int maxExpand = 1500)
    {
        path.Clear();
        if (!Ready) return false;
        int start = Nearest(from);
        int goal = Nearest(to);
        if (start < 0 || goal < 0) return false;
        if (!Clear(cells[goal], to)) return false;

        cameFrom.Clear();
        queue.Clear();
        cameFrom[start] = start;
        queue.Enqueue(start);
        int expanded = 0;
        bool found = start == goal;
        while (queue.Count > 0 && !found && expanded++ < maxExpand)
        {
            int cur = queue.Dequeue();
            foreach (int next in adjacency[cur])
            {
                if (cameFrom.ContainsKey(next)) continue;
                cameFrom[next] = cur;
                if (next == goal) { found = true; break; }
                queue.Enqueue(next);
            }
        }
        if (!found) return false;

        // 칸 목록으로 되돌리고
        points.Clear();
        points.Add(to);
        for (int c = goal; c != start; c = cameFrom[c]) points.Add(cells[c]);
        points.Add(cells[start]);
        points.Add(from);
        points.Reverse(); // from, start칸, ..., goal칸, to

        // 꺾이는 점만 남기기: 현재 점에서 직선으로 갈 수 있는 가장 먼 점으로 건너뜁니다
        int i = 0;
        while (i < points.Count - 1)
        {
            int j = points.Count - 1;
            while (j > i + 1 && !Clear(points[i], points[j])) j--;
            path.Add(points[j]);
            i = j;
        }
        return path.Count > 0;
    }
}
