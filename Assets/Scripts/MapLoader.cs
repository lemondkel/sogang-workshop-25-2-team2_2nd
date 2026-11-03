using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
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
    /// <summary>불러온 집의 실내 조명(점·스팟). 시작 위치를 불 켜진 곳에서 고르는 데 씁니다.</summary>
    static readonly List<Light> mapLights = new List<Light>();

    /// <summary>mapScene을 추가로 불러오고 기본 집을 치웁니다. 실패하면 기본 집을 그대로 둡니다.</summary>
    public static IEnumerator Load(string mapScene)
    {
        MapLoaded = false;
        mapLights.Clear();
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
                else if (l.enabled && l.gameObject.activeInHierarchy) mapLights.Add(l);
            }
            foreach (Canvas cv in root.GetComponentsInChildren<Canvas>(true)) cv.gameObject.SetActive(false);
        }
        BrightenInterior();

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
    /// 불러온 집은 여러 층짜리라 햇빛이 안 들어와 실내가 거의 검게 보입니다.
    /// 환경광을 밝히고, 집에 있는 점·스팟 조명이 여러 개 동시에 픽셀 조명으로 그려지게 합니다.
    /// </summary>
    static void BrightenInterior()
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.60f, 0.62f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.46f, 0.48f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.23f, 0.22f);
        if (QualitySettings.pixelLightCount < 6) QualitySettings.pixelLightCount = 6;
    }

    /// <summary>캐릭터를 따라다니는 은은한 전등. 조명이 없는 복도·구석에서도 벽과 가구가 보이게 합니다.</summary>
    public static void AttachLamp(Transform player)
    {
        if (player == null || player.Find("PlayerLamp") != null) return;
        GameObject go = new GameObject("PlayerLamp");
        go.transform.SetParent(player, false);
        go.transform.localPosition = new Vector3(0f, 1.8f, 0f);
        Light lamp = go.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 9f;
        lamp.intensity = 0.75f;
        lamp.color = new Color(1f, 0.96f, 0.88f);
        lamp.shadows = LightShadows.None;
    }

    // 그 칸이 집의 조명(같은 층, 범위 안)에 닿는지. 윗층 조명은 천장에 막히므로 높이 차 4.2m까지만 봅니다.
    static bool IsLit(Vector3 cell)
    {
        foreach (Light l in mapLights)
        {
            if (l == null) continue;
            Vector3 lp = l.transform.position;
            float dy = lp.y - cell.y;
            if (dy < -0.5f || dy > 4.2f) continue;
            if ((lp - cell).sqrMagnitude <= l.range * l.range * 0.81f) return true;
        }
        return false;
    }

    static int CountLit(List<Vector3> cells)
    {
        if (mapLights.Count == 0) return 0;
        int n = 0;
        foreach (Vector3 c in cells) if (IsLit(c)) n++;
        return n;
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
            // 집 전체를 훑어 층마다 걸어갈 수 있는 바닥을 찾고, 조명이 닿는 칸이 가장 많은(없으면 가장 넓은) 층을 고릅니다.
            // 불러온 집은 층이 여러 개라 불 꺼진 층(지하 주차장·맨 위층)에서 시작하면 화면이 거의 검게 보입니다.
            List<Vector3> bestCells = walkable;
            int bestLit = bestCells != null ? CountLit(bestCells) : -1;
            Bounds area = SceneBounds();
            int tested = 0;
            HashSet<int> seenFloors = new HashSet<int>();
            for (int i = 0; i < 900 && tested < 14; i++)
            {
                Vector3 p = new Vector3(Random.Range(area.min.x, area.max.x), 0f, Random.Range(area.min.z, area.max.z));
                Vector3 floor;
                if (!FindIndoorFloor(p, area, mask, out floor)) continue;
                tested++;
                List<Vector3> cells = CatSpawner.FloodWalkable(floor, mask);
                if (cells.Count < 30) continue;
                int key = Mathf.RoundToInt(floor.y * 2f);
                int litCount = CountLit(cells);
                if (bestCells == null || litCount > bestLit || (litCount == bestLit && cells.Count > bestCells.Count)) { bestCells = cells; bestLit = litCount; }
                seenFloors.Add(key);
                if (seenFloors.Count >= 4 && bestLit > 60) break;
            }
            walkable = bestCells ?? new List<Vector3> { seed };
            if (bestCells == null) Debug.LogWarning("[MapLoader] 설 수 있는 바닥을 찾지 못해 기본 위치를 씁니다");
            else Debug.Log("[MapLoader] 시작 층 y=" + walkable[0].y.ToString("0.0") + " 칸 " + walkable.Count + "개, 조명 닿는 칸 " + bestLit + "개");
        }

        // 벽·가구에서 떨어진 트인 자리(가림 점수 0~2) 중 하나. 조명이 닿는 칸이 있으면 그중에서 고릅니다.
        List<Vector3> open = new List<Vector3>();
        List<Vector3> lit = new List<Vector3>();
        foreach (Vector3 c in walkable)
        {
            if (CatSpawner.CoverScore(c, mask) > 2 || !IsStandable(c, mask)) continue;
            open.Add(c);
            if (IsLit(c)) lit.Add(c);
        }
        List<Vector3> pool = lit.Count >= 8 ? lit : (open.Count > 0 ? open : walkable);
        Vector3 pick = pool[Random.Range(0, pool.Count)];
        // 칸(0.5m) 안에서 조금 흔들어 매번 똑같은 자리가 아니게
        Vector2 jitter = Random.insideUnitCircle * 0.15f;
        Vector3 jittered = new Vector3(pick.x + jitter.x, pick.y, pick.z + jitter.y);
        return IsStandable(jittered, mask) ? jittered : pick;
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

        // 콜라이더가 없는 가구(탁자 등)나 이미 옮긴 소품과도 겹치지 않도록 렌더러 경계 상자로도 확인합니다
        List<Bounds> occupied = new List<Bounds>();
        if (MapScene.IsValid() && MapScene.isLoaded)
        {
            foreach (GameObject root in MapScene.GetRootGameObjects())
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
                {
                    Bounds rb = r.bounds;
                    if (rb.size.magnitude > 6f || rb.size.y < 0.15f) continue; // 벽 덩어리·바닥·천장은 제외 (콜라이더가 처리)
                    occupied.Add(rb);
                }
            }
        }

        int moved = 0, hidden = 0;
        foreach (GameObject go in items)
        {
            if (!go.activeInHierarchy) continue;
            if (Place(go, walkable, playerStart, occupied)) moved++;
            else { go.SetActive(false); hidden++; }
        }
        Physics.SyncTransforms();
        Debug.Log("[MapLoader] 소품 옮김 " + moved + "개, 자리 없어 숨김 " + hidden + "개");
    }

    static bool Place(GameObject go, List<Vector3> walkable, Vector3 playerStart, List<Bounds> occupied)
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
            // 경계 상자(80%)끼리도 겹치지 않게. 바닥에서 조금 띄워 올려놓은 가구 표면과는 닿아도 됩니다.
            Bounds nb = new Bounds(center + Vector3.up * (size.y * 0.1f), size * 0.8f);
            if (Overlaps(nb, occupied)) continue;

            // 바닥에 닿게: 바운드의 바닥 중심을 hit.point로
            Vector3 anchor = new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.position += hit.point + Vector3.up * 0.01f - anchor;
            occupied.Add(new Bounds(center, size));
            return true;
        }
        return false;
    }

    static bool Overlaps(Bounds b, List<Bounds> occupied)
    {
        for (int i = 0; i < occupied.Count; i++)
        {
            if (occupied[i].Intersects(b)) return true;
        }
        return false;
    }
}
