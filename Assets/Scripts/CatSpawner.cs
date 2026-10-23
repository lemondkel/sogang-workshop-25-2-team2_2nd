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
    const float MinDistanceFromPlayer = 2.5f;
    const float MinDistanceBetweenCats = 2.2f;

    // 고양이(Clickable), UI, 퀘스트 레이어, Ignore Raycast는 지형 판정에서 제외
    static int EnvironmentMask
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
    }

    /// <param name="feet">플레이어 발 위치</param>
    /// <param name="eye">플레이어 눈(카메라) 위치</param>
    public static List<Spot> FindSpots(Vector3 feet, Vector3 eye, int count, float hiddenRatio, IList<Vector3> preferred)
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

            candidates.Add(new Spot { position = spot, hiddenFromStart = IsHidden(eye, spot, mask) });
        }

        // 3) 숨김 비율에 맞춰 선택
        int remaining = count - result.Count;
        int wantHidden = Mathf.RoundToInt(remaining * Mathf.Clamp01(hiddenRatio));
        Shuffle(candidates);

        PickFrom(candidates, result, wantHidden, true);
        PickFrom(candidates, result, count - result.Count, false);
        PickFrom(candidates, result, count - result.Count, null);

        // 4) 그래도 부족하면 시작 위치 앞쪽에 배치 (씬에 콜라이더가 거의 없는 경우 대비)
        int guard = 0;
        while (result.Count < count && guard++ < 50)
        {
            Vector3 p = feet + Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward * Random.Range(2.5f, 5f);
            Vector3 surface;
            if (TryFindSurface(new Vector3(p.x, feet.y + 2.0f, p.z), feet.y, mask, out surface)) p = surface;
            result.Add(new Spot { position = p, hiddenFromStart = false });
        }
        return result;
    }

    static void PickFrom(List<Spot> candidates, List<Spot> result, int want, bool? hidden)
    {
        if (want <= 0) return;
        int picked = 0;
        foreach (Spot c in candidates)
        {
            if (picked >= want) break;
            if (hidden.HasValue && c.hiddenFromStart != hidden.Value) continue;
            if (TooClose(c.position, result)) continue;
            result.Add(c);
            picked++;
        }
    }

    static bool TooClose(Vector3 p, List<Spot> chosen)
    {
        foreach (Spot s in chosen)
        {
            if (Vector3.Distance(s.position, p) < MinDistanceBetweenCats) return true;
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

        // 고양이가 들어갈 공간이 있는지
        Vector3 center = hit.point + Vector3.up * 0.4f;
        if (Physics.CheckBox(center, new Vector3(0.28f, 0.25f, 0.28f), Quaternion.identity, mask, QueryTriggerInteraction.Ignore)) return false;

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

                // 사람이 설 수 있는 공간인지
                Vector3 floor = hit.point;
                if (Physics.CheckCapsule(floor + Vector3.up * 0.45f, floor + Vector3.up * 1.5f, 0.2f, mask, QueryTriggerInteraction.Ignore)) continue;

                // 칸 사이에 벽이 있는지 (얇은 벽 통과 방지)
                Vector3 a = curPos + Vector3.up * 0.6f;
                Vector3 b = floor + Vector3.up * 0.6f;
                if (Physics.Linecast(a, b, mask, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(a + Vector3.up * 0.7f, b + Vector3.up * 0.7f, mask, QueryTriggerInteraction.Ignore)) continue;

                visited[next] = floor;
                queue.Enqueue(next);
                cells.Add(floor);
            }
        }
        return cells;
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
