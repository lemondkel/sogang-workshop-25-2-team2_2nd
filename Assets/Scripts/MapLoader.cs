using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 레벨마다 다른 집(맵)에서 플레이할 수 있게 합니다.
/// - 기본 집은 MainScene 안의 "Apart"(Apartment Kit Structure 01)입니다.
/// - 레벨에 mapScene이 지정되면 그 씬(예: Apartment Kit의 Scene_01/Scene_02)을 추가로 불러오고, 기본 집은 치웁니다.
///   불러온 씬의 카메라·조명·이벤트 시스템은 끄고 구조물(벽·바닥·가구·문)만 씁니다.
/// - 집이 바뀌면 MainScene에 놓여 있던 소품·단서(세탁기, 서랍장, 고양이 용품 등)는 새 집의 빈 자리로 옮깁니다.
/// - 시작(리스폰) 위치는 걸어갈 수 있는 바닥 가운데 트인 자리에서 매 판 무작위로 고릅니다.
/// </summary>
public static class MapLoader
{
    // MainScene에서 집과 상관없이 늘 두는 것들
    static readonly HashSet<string> KeepRoots = new HashSet<string> { "EventSystem", "Character", "Directional Light", "GameManager", "Canvas", "BGMPlayer" };
    const string DefaultHouseRoot = "Apart";

    public static bool MapLoaded { get; private set; }
    public static Scene MapScene { get; private set; }

    /// <summary>mapScene을 추가로 불러오고 기본 집을 치웁니다. 실패하면 기본 집을 그대로 둡니다.</summary>
    public static IEnumerator Load(string mapScene)
    {
        MapLoaded = false;
        if (string.IsNullOrEmpty(mapScene)) yield break;
        if (!Application.CanStreamedLevelBeLoaded(mapScene))
        {
            Debug.LogWarning("[MapLoader] 빌드에 없는 맵 씬: " + mapScene + " (기본 집을 사용합니다)");
            yield break;
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(mapScene, LoadSceneMode.Additive);
        if (op == null) yield break;
        while (!op.isDone) yield return null;
        MapScene = SceneManager.GetSceneByName(mapScene);
        if (!MapScene.IsValid() || !MapScene.isLoaded) yield break;

        // 불러온 씬의 카메라/플레이어/조명/이벤트 시스템은 쓰지 않습니다 (MainScene 것이 있음)
        foreach (GameObject root in MapScene.GetRootGameObjects())
        {
            foreach (Camera c in root.GetComponentsInChildren<Camera>(true)) Object.Destroy(c.gameObject);
            foreach (AudioListener a in root.GetComponentsInChildren<AudioListener>(true)) Object.Destroy(a.gameObject);
            foreach (CharacterController cc in root.GetComponentsInChildren<CharacterController>(true)) Object.Destroy(cc.gameObject);
            foreach (UnityEngine.EventSystems.EventSystem es in root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)) Object.Destroy(es.gameObject);
            foreach (Light l in root.GetComponentsInChildren<Light>(true))
            {
                if (l.type == LightType.Directional) l.enabled = false;
            }
            foreach (Canvas cv in root.GetComponentsInChildren<Canvas>(true)) cv.gameObject.SetActive(false);
        }

        // 기본 집 치우기
        Scene main = SceneManager.GetActiveScene();
        foreach (GameObject root in main.GetRootGameObjects())
        {
            if (root.name == DefaultHouseRoot) Object.Destroy(root);
        }
        MapLoaded = true;
        // Destroy는 프레임 끝에 적용되므로 한 프레임 쉬어 콜라이더가 사라진 뒤에 이어갑니다
        yield return null;
        Physics.SyncTransforms();
        Debug.Log("[MapLoader] 맵 불러옴: " + mapScene);
    }

    /// <summary>
    /// 시작 위치를 고릅니다. seed(기본 집의 캐릭터 위치)에서 걸어갈 수 있는 바닥을 찾거나,
    /// seed가 쓸모없으면(다른 집) 집 전체를 훑어 가장 넓게 걸어갈 수 있는 바닥을 찾은 뒤, 그중 트인 자리 하나를 무작위로 돌려줍니다.
    /// </summary>
    public static Vector3 PickStart(Vector3 seed, bool seedIsValid, out List<Vector3> walkable)
    {
        int mask = CatSpawner.EnvironmentMask;
        walkable = null;
        if (seedIsValid && IsStandable(seed, mask))
        {
            walkable = CatSpawner.FloodWalkable(seed, mask);
        }
        if (walkable == null || walkable.Count < 30)
        {
            Vector3 best = seed;
            List<Vector3> bestCells = walkable;
            Bounds area = SceneBounds();
            int tested = 0;
            for (int i = 0; i < 600 && tested < 10; i++)
            {
                Vector3 p = new Vector3(Random.Range(area.min.x, area.max.x), 0f, Random.Range(area.min.z, area.max.z));
                Vector3 floor;
                if (!FindIndoorFloor(p, area, mask, out floor)) continue;
                tested++;
                List<Vector3> cells = CatSpawner.FloodWalkable(floor, mask);
                if (bestCells == null || cells.Count > bestCells.Count) { bestCells = cells; best = floor; }
                if (bestCells.Count > 600) break;
            }
            walkable = bestCells ?? new List<Vector3> { seed };
            if (bestCells == null) Debug.LogWarning("[MapLoader] 설 수 있는 바닥을 찾지 못해 기본 위치를 씁니다");
        }

        // 벽·가구에서 떨어진 트인 자리(가림 점수 0~1) 중 하나
        List<Vector3> open = new List<Vector3>();
        foreach (Vector3 c in walkable)
        {
            if (CatSpawner.CoverScore(c, mask) <= 1 && IsStandable(c, mask)) open.Add(c);
        }
        List<Vector3> pool = open.Count > 0 ? open : walkable;
        return pool[Random.Range(0, pool.Count)];
    }

    static bool IsStandable(Vector3 feet, int mask)
    {
        RaycastHit hit;
        if (!Physics.Raycast(feet + Vector3.up * 0.5f, Vector3.down, out hit, 1.0f, mask, QueryTriggerInteraction.Ignore)) return false;
        if (hit.normal.y < 0.8f) return false;
        if (Physics.CheckCapsule(hit.point + Vector3.up * 0.6f, hit.point + Vector3.up * 1.3f, 0.36f, mask, QueryTriggerInteraction.Ignore)) return false;
        return Physics.Raycast(hit.point + Vector3.up * 0.3f, Vector3.up, 6f, mask, QueryTriggerInteraction.Ignore); // 실내(천장 있음)
    }

    static readonly RaycastHit[] downHits = new RaycastHit[32];

    // 위에서 아래로 훑어 천장이 있고 사람이 설 수 있는 실내 바닥을 찾습니다 (지붕·바깥 땅은 제외)
    static bool FindIndoorFloor(Vector3 xz, Bounds area, int mask, out Vector3 floor)
    {
        floor = Vector3.zero;
        Vector3 from = new Vector3(xz.x, area.max.y + 1f, xz.z);
        int n = Physics.RaycastNonAlloc(from, Vector3.down, downHits, area.size.y + 2f, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = downHits[i];
            if (h.normal.y < 0.85f) continue;
            if (!IsStandable(h.point + Vector3.up * 0.05f, mask)) continue;
            floor = h.point;
            return true;
        }
        return false;
    }

    static Bounds SceneBounds()
    {
        Bounds b = new Bounds();
        bool any = false;
        foreach (Renderer r in Object.FindObjectsOfType<Renderer>())
        {
            if (r.bounds.size.magnitude > 80f) continue; // 바깥 땅 같은 거대한 것은 제외
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any ? b : new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f));
    }

    /// <summary>
    /// MainScene에 놓여 있던 소품·단서·고양이 용품을 새 집의 빈 자리로 옮깁니다 (작은 것은 가구 위에도, 큰 것은 바닥에만).
    /// 자리를 못 찾으면 숨깁니다.
    /// </summary>
    public static void RelocateDecor(List<Vector3> walkable, Vector3 playerStart)
    {
        if (walkable == null || walkable.Count == 0) return;
        Scene main = SceneManager.GetActiveScene();
        List<GameObject> items = new List<GameObject>();
        foreach (GameObject root in main.GetRootGameObjects())
        {
            if (KeepRoots.Contains(root.name) || root.name == DefaultHouseRoot) continue;
            if (root.GetComponentInChildren<Camera>(true) != null || root.GetComponent<FirstPersonController>() != null) continue;
            if (root.name == "Cat_Assets")
            {
                foreach (Transform child in root.transform) items.Add(child.gameObject);
            }
            else items.Add(root);
        }

        int moved = 0, hidden = 0;
        foreach (GameObject go in items)
        {
            if (!go.activeInHierarchy) continue;
            if (Place(go, walkable, playerStart)) moved++;
            else { go.SetActive(false); hidden++; }
        }
        Physics.SyncTransforms();
        Debug.Log("[MapLoader] 소품 옮김 " + moved + "개, 자리 없어 숨김 " + hidden + "개");
    }

    static bool Place(GameObject go, List<Vector3> walkable, Vector3 playerStart)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        Vector3 size = b.size;
        bool big = size.x > 0.6f || size.z > 0.6f || size.y > 0.8f;
        int mask = Physics.DefaultRaycastLayers & ~(1 << 2) & ~(1 << 5); // 소품끼리도 겹치지 않도록 모든 레이어 (UI·IgnoreRaycast 제외)
        int envMask = CatSpawner.EnvironmentMask;

        for (int attempt = 0; attempt < 200; attempt++)
        {
            Vector3 cell = walkable[Random.Range(0, walkable.Count)];
            Vector2 off = Random.insideUnitCircle * (big ? 0.4f : 1.2f);
            Vector3 from = new Vector3(cell.x + off.x, cell.y + 2.2f, cell.z + off.y);
            RaycastHit hit;
            if (!Physics.Raycast(from, Vector3.down, out hit, 2.4f, envMask, QueryTriggerInteraction.Ignore)) continue;
            if (hit.normal.y < 0.9f) continue;
            float height = hit.point.y - cell.y;
            if (height < -0.2f || height > (big ? 0.1f : 1.3f)) continue;
            if (Vector3.Distance(hit.point, playerStart) < (big ? 2.0f : 1.2f)) continue;
            if (!Physics.Raycast(hit.point + Vector3.up * 0.2f, Vector3.up, 6f, envMask, QueryTriggerInteraction.Ignore)) continue; // 실내

            Vector3 center = hit.point + Vector3.up * (size.y * 0.5f + 0.02f);
            if (Physics.CheckBox(center, size * 0.5f * 0.95f, Quaternion.identity, mask, QueryTriggerInteraction.Ignore)) continue;

            // 바닥에 닿게: 바운드의 바닥 중심을 hit.point로
            Vector3 anchor = new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.position += hit.point + Vector3.up * 0.01f - anchor;
            return true;
        }
        return false;
    }
}
