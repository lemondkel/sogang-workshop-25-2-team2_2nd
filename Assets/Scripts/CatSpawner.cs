using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 집 안에서 고양이가 숨을 만한 위치(바닥, 가구 위)를 자동으로 찾습니다.
/// 1) 플레이어 시작 위치에서 걸어갈 수 있는 바닥 격자를 BFS로 탐색하고
/// 2) 그 주변에서 고양이가 들어갈 공간이 있는 평평한 표면을 후보로 모은 뒤
/// 3) 시작 위치에서 보이는지/안 보이는지에 따라 골라냅니다.
/// 매 판 위치가 달라지므로 같은 레벨도 다시 할 때마다 새롭습니다.
/// </summary>
public static class CatSpawner
{
    const float CellSize = 0.5f;
    const float SearchRadius = 18f;
    const float MinDistanceFromPlayer = 4.0f;
    const float MinDistanceBetweenCats = 2.2f;

    // 고양이(Clickable), UI, 퀘스트 레이어, Ignore Raycast는 지형 판정에서 제외
    public static int EnvironmentMask
    {
        get
        {
            int mask = Physics.DefaultRaycastLayers;
            mask &= ~(1 << 2);
            mask &= ~(1 << 5);
            int clickable = LayerMask.NameToLayer("Clickable");
            if (clickable >= 0) mask &= ~(1 << clickable);
            for (int i = 1; i <= 5; i++)
            {
                int quest = LayerMask.NameToLayer("Quest" + i);
                if (quest >= 0) mask &= ~(1 << quest);
            }
            return mask;
        }
    }

    public struct Spot
    {
        public Vector3 position;
        public bool hiddenFromStart;
        public int cover; // 주변이 얼마나 가려져 있는지 (0~10)
    }

    /// <summary>마지막 FindSpots에서 찾은 숨을 자리 후보 전부. 고양이가 돌아다닐 때 갈 곳으로 씁니다.</summary>
    public static List<Spot> LastCandidates { get; private set; } = new List<Spot>();

    /// <param name="feet">플레이어 발 위치</param>
    /// <param name="eye">플레이어 눈(카메라) 위치</param>
    public static List<Spot> FindSpots(Vector3 feet, Vector3 eye, int count, float hiddenRatio, IList<Vector3> preferred)
    {
        return FindSpots(feet, eye, count, hiddenRatio, preferred, 0);
    }

    /// <param name="minCover">숨는 자리의 최소 가림 정도(0~8). 부족하면 한 단계씩 낮춰서 다시 고릅니다.</param>
    public static List<Spot> FindSpots(Vector3 feet, Vector3 eye, int count, float hiddenRatio, IList<Vector3> preferred, int minCover)
    {
        int mask = EnvironmentMask;
        List<Spot> result = new List<Spot>();

        // 0) 직접 지정한 위치가 있으면 먼저 사용
        if (preferred != null)
        {
            foreach (Vector3 p in preferred)
            {
                if (result.Count >= count) break;
                result.Add(new Spot { position = p, hiddenFromStart = IsHidden(eye, p, mask) });
            }
        }
        if (result.Count >= count) return result;

        // 1) 걸어갈 수 있는 바닥 격자 탐색
        List<Vector3> walkable = FloodWalkable(feet, mask);

        // 2) 후보 위치 수집
        List<Spot> candidates = new List<Spot>();
        int attempts = Mathf.Min(1500, walkable.Count * 4);
        for (int i = 0; i < attempts && walkable.Count > 0; i++)
        {
            Vector3 cell = walkable[Random.Range(0, walkable.Count)];
            Vector2 offset = Random.insideUnitCircle * 1.2f;
            Vector3 sample = new Vector3(cell.x + offset.x, feet.y + 2.0f, cell.z + offset.y);

            Vector3 spot;
            if (!TryFindSurface(sample, feet.y, mask, out spot)) continue;
            if (Flat(spot - feet).magnitude < MinDistanceFromPlayer) continue;
            // 걸어갈 수 있는 칸에서 벽을 통과하지 않고 닿는 자리만 (얇은 외벽 너머 바깥이나 다른 집에 숨지 않도록). 문은 열 수 있으니 통과로 칩니다.
            if (BlockedLine(cell + Vector3.up * 0.3f, spot + Vector3.up * 0.3f, mask)) continue;

            candidates.Add(new Spot { position = spot, hiddenFromStart = IsHidden(eye, spot, mask), cover = CoverScore(spot, mask) });
        }
        LastCandidates = new List<Spot>(candidates);

        // 3) 가림 정도(minCover) 이상인 자리 중에서 숨김 비율에 맞춰 선택. 자리가 모자라면 가림 기준을 한 단계씩 낮춥니다.
        int remaining = count - result.Count;
        int wantHidden = Mathf.RoundToInt(remaining * Mathf.Clamp01(hiddenRatio));
        Shuffle(candidates);

        for (int cover = Mathf.Clamp(minCover, 0, 8); cover >= 0 && result.Count < count; cover--)
        {
            PickFrom(candidates, result, wantHidden - CountHidden(result), true, MinDistanceBetweenCats, cover);
            PickFrom(candidates, result, count - result.Count, false, MinDistanceBetweenCats, cover);
            PickFrom(candidates, result, count - result.Count, null, MinDistanceBetweenCats, cover);
        }

        // 4) 그래도 부족하면(작은 방 등) 고양이 사이 간격을 조금씩 줄여서 다시 고릅니다.
        float[] relaxed = { 1.4f, 0.9f, 0.6f };
        for (int r = 0; r < relaxed.Length && result.Count < count; r++)
        {
            PickFrom(candidates, result, count - result.Count, null, relaxed[r]);
        }

        // 5) 그래도 부족하면 플레이어 주변을 더 넓게 뒤져서 검증된 표면만 씁니다 (벽 속에 넣지 않습니다).
        int guard = 0;
        while (result.Count < count && guard++ < 400)
        {
            Vector3 p = feet + Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * Random.Range(1.5f, 8f);
            Vector3 surface;
            if (!TryFindSurface(new Vector3(p.x, feet.y + 2.0f, p.z), feet.y, mask, out surface)) continue;
            if (TooClose(surface, result, 0.6f)) continue;
            if (BlockedLine(feet + Vector3.up * 0.3f, surface + Vector3.up * 0.3f, mask)) continue;
            result.Add(new Spot { position = surface, hiddenFromStart = IsHidden(eye, surface, mask) });
        }
        if (result.Count < count)
        {
            Debug.LogWarning("[CatSpawner] 숨길 자리가 부족합니다: " + result.Count + "/" + count);
        }
        return result;
    }

    /// <summary>
    /// 생성된 고양이가 벽·가구 콜라이더와 겹치면 수평으로 밀어냅니다 (반쯤 벽에 박힌 고양이 방지).
    /// </summary>
    public static void PushOutOfObstacles(GameObject cat)
    {
        Collider body = cat.GetComponentInChildren<Collider>();
        if (body == null) return;
        int mask = EnvironmentMask;
        Vector3 original = cat.transform.position;
        Collider[] near = new Collider[16];
        for (int iter = 0; iter < 4; iter++)
        {
            bool moved = false;
            int n = Physics.OverlapSphereNonAlloc(body.bounds.center, 0.8f, near, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider other = near[i];
                if (other == null || other.transform.IsChildOf(cat.transform)) continue;
                Vector3 dir; float dist;
                if (!Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                                                other, other.transform.position, other.transform.rotation, out dir, out dist)) continue;
                dir.y = 0f; // 바닥과의 겹침은 무시 (수평으로만 밀어냄)
                if (dir.sqrMagnitude < 1e-6f || dist < 0.005f) continue;
                cat.transform.position += dir.normalized * (dist + 0.02f);
                moved = true;
            }
            if (!moved) break;
        }

        // 밀려난 자리가 선반/탁자 가장자리를 벗어났으면 바닥에 다시 맞추고, 아래에 아무것도 없으면 원래 자리로 돌립니다.
        if (cat.transform.position != original)
        {
            RaycastHit ground;
            if (Physics.Raycast(cat.transform.position + Vector3.up * 0.3f, Vector3.down, out ground, 1.5f, mask, QueryTriggerInteraction.Ignore))
            {
                Vector3 p = cat.transform.position;
                p.y = ground.point.y;
                cat.transform.position = p;
            }
            else
            {
                cat.transform.position = original;
            }
        }
    }

    static void PickFrom(List<Spot> candidates, List<Spot> result, int want, bool? hidden, float minDistance = MinDistanceBetweenCats, int minCover = 0)
    {
        if (want <= 0) return;
        int picked = 0;
        foreach (Spot c in candidates)
        {
            if (picked >= want) break;
            if (hidden.HasValue && c.hiddenFromStart != hidden.Value) continue;
            if (c.cover < minCover) continue;
            if (TooClose(c.position, result, minDistance)) continue;
            result.Add(c);
            picked++;
        }
    }

    static int CountHidden(List<Spot> spots)
    {
        int n = 0;
        foreach (Spot s in spots) if (s.hiddenFromStart) n++;
        return n;
    }

    /// <summary>
    /// 자리 주변 8방향(고양이 눈높이 0.3m, 1m 안)에 벽·가구가 몇 개 있는지 + 위(0.55m에서 1.2m 안)가 막혀 있으면 +2.
    /// 구석·가구 밑·선반 안쪽일수록 높습니다 (최대 10).
    /// </summary>
    public static int CoverScore(Vector3 spot, int mask)
    {
        int score = 0;
        Vector3 from = spot + Vector3.up * 0.3f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Quaternion.Euler(0, i * 45f, 0) * Vector3.forward;
            if (Physics.Raycast(from, dir, 1.0f, mask, QueryTriggerInteraction.Ignore)) score++;
        }
        if (Physics.Raycast(spot + Vector3.up * 0.55f, Vector3.up, 1.2f, mask, QueryTriggerInteraction.Ignore)) score += 2;
        return score;
    }

    static bool TooClose(Vector3 p, List<Spot> chosen, float minDistance = MinDistanceBetweenCats)
    {
        foreach (Spot s in chosen)
        {
            if (Vector3.Distance(s.position, p) < minDistance) return true;
        }
        return false;
    }

    /// <summary>
    /// 위에서 아래로 레이를 쏴서 고양이가 앉을 수 있는 평평한 표면을 찾습니다.
    /// </summary>
    static bool TryFindSurface(Vector3 from, float floorY, int mask, out Vector3 spot)
    {
        spot = Vector3.zero;
        RaycastHit hit;
        if (!Physics.Raycast(from, Vector3.down, out hit, 3.5f, mask, QueryTriggerInteraction.Ignore)) return false;
        if (hit.normal.y < 0.85f) return false;
        if (hit.collider is CharacterController) return false;

        // 바닥 ~ 눈높이 정도의 표면만 (너무 높으면 찾기 어려움)
        float height = hit.point.y - floorY;
        if (height < -0.5f || height > 1.6f) return false;

        // 고양이가 들어갈 공간이 있는지: 고양이 콜라이더(반지름 0.22m, 높이 0.42m까지)보다 조금 큰 상자(바닥 위 0.03~0.52m, 좌우 ±0.24m)가 비어 있어야 합니다.
        // 침대·책상 밑처럼 낮은 틈에도 숨을 수 있게 0.52m까지만 봅니다. 상자라서 변기·수납장 밑단처럼 낮은 가구 몸체도 걸러집니다.
        Vector3 center = hit.point + Vector3.up * 0.275f;
        if (Physics.CheckBox(center, new Vector3(0.24f, 0.245f, 0.24f), Quaternion.identity, mask, QueryTriggerInteraction.Ignore)) return false;

        // 실내인지 (위에 천장이 있어야 함)
        if (!Physics.Raycast(hit.point + Vector3.up * 0.2f, Vector3.up, 6f, mask, QueryTriggerInteraction.Ignore)) return false;

        spot = hit.point;
        return true;
    }

    static bool IsHidden(Vector3 eye, Vector3 spot, int mask)
    {
        Vector3 target = spot + Vector3.up * 0.3f;
        return Physics.Linecast(eye, target, mask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// 시작 위치에서 벽을 통과하지 않고 걸어갈 수 있는 바닥 칸들을 BFS로 찾습니다.
    /// </summary>
    static List<Vector3> FloodWalkable(Vector3 feet, int mask)
    {
        List<Vector3> cells = new List<Vector3>();
        Dictionary<Vector2Int, Vector3> visited = new Dictionary<Vector2Int, Vector3>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        Vector2Int start = new Vector2Int(0, 0);
        visited[start] = feet;
        queue.Enqueue(start);
        cells.Add(feet);

        int maxCells = Mathf.CeilToInt((SearchRadius * 2 / CellSize) * (SearchRadius * 2 / CellSize));
        Vector2Int[] dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        while (queue.Count > 0 && cells.Count < maxCells)
        {
            Vector2Int cur = queue.Dequeue();
            Vector3 curPos = visited[cur];

            foreach (Vector2Int d in dirs)
            {
                Vector2Int next = cur + d;
                if (visited.ContainsKey(next)) continue;
                if (Mathf.Abs(next.x) * CellSize > SearchRadius || Mathf.Abs(next.y) * CellSize > SearchRadius) continue;

                Vector3 probe = new Vector3(feet.x + next.x * CellSize, feet.y + 1.0f, feet.z + next.y * CellSize);
                RaycastHit hit;
                if (!Physics.Raycast(probe, Vector3.down, out hit, 1.6f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider is CharacterController) continue;
                if (hit.normal.y < 0.8f || Mathf.Abs(hit.point.y - feet.y) > 0.45f) continue;

                // 사람이 설 수 있는 공간인지 (닫힌 문은 열고 지나갈 수 있으므로 장애물로 치지 않습니다)
                Vector3 floor = hit.point;
                if (BlockedCapsule(floor + Vector3.up * 0.45f, floor + Vector3.up * 1.5f, 0.2f, mask)) continue;

                // 칸 사이에 벽이 있는지 (얇은 벽 통과 방지)
                Vector3 a = curPos + Vector3.up * 0.6f;
                Vector3 b = floor + Vector3.up * 0.6f;
                if (BlockedLine(a, b, mask)) continue;
                if (BlockedLine(a + Vector3.up * 0.7f, b + Vector3.up * 0.7f, mask)) continue;

                visited[next] = floor;
                queue.Enqueue(next);
                cells.Add(floor);
            }
        }
        return cells;
    }

    static readonly RaycastHit[] lineHits = new RaycastHit[16];
    static readonly Collider[] capsuleHits = new Collider[16];

    // 문(여닫이/미닫이/옷장 문/서랍)이 아닌 것에 막히는지
    static bool BlockedLine(Vector3 a, Vector3 b, int mask)
    {
        Vector3 d = b - a;
        int n = Physics.RaycastNonAlloc(a, d.normalized, lineHits, d.magnitude, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (lineHits[i].collider is CharacterController) continue;
            if (!DoorInteraction.IsPassable(lineHits[i].collider)) return true;
        }
        return false;
    }

    static bool BlockedCapsule(Vector3 p0, Vector3 p1, float radius, int mask)
    {
        int n = Physics.OverlapCapsuleNonAlloc(p0, p1, radius, capsuleHits, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            if (capsuleHits[i] is CharacterController) continue;
            if (!DoorInteraction.IsPassable(capsuleHits[i])) return true;
        }
        return false;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
