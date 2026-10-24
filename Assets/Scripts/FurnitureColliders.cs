using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 콜라이더가 없는 가구/소품에 플레이 시작 시 자동으로 콜라이더를 붙입니다.
/// 폴리곤이 적은 모델(책장·책상·의자 등)은 임포트 설정(Generate Colliders)으로 MeshCollider가 생기지만,
/// AI로 만든 고폴리 모델(캣타워·캣휠·화장실 등)은 메시 콜라이더가 너무 무거워서 렌더러 경계 상자(BoxCollider)를 씁니다.
/// 덕분에 캐릭터가 가구를 통과하지 않고, 3인칭 카메라도 가구에 가려지며, 고양이도 가구 속에 숨지 않습니다.
/// </summary>
public static class FurnitureColliders
{
    public static int EnsureColliders()
    {
        int added = 0;
        var done = new HashSet<Transform>();

        // 밀리는 가구(여행가방 등)는 고정합니다. 캐릭터가 부딪혀 가구가 날아다니면 이상해 보입니다.
        foreach (Rigidbody body in Object.FindObjectsOfType<Rigidbody>())
        {
            if (body.isKinematic) continue;
            if (body.GetComponentInParent<FirstPersonController>() != null || body.GetComponentInParent<HiddenCat>() != null) continue;
            body.isKinematic = true;
        }

        foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (ShouldSkip(renderer.transform)) continue;
            // 커튼은 몸으로 지나갈 수 있어야 자연스럽습니다.
            if (renderer.name.IndexOf("curtain", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (HasColliderAround(renderer.transform)) continue;

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            // 손바닥보다 작은 소품(컵, 펜 등)은 걸을 때 방해만 되니 건너뜁니다.
            Vector3 worldSize = renderer.bounds.size;
            if (worldSize.x < 0.12f && worldSize.z < 0.12f) continue;
            if (worldSize.y < 0.05f) continue;
            // 집 바깥(외벽·지붕)과 큰 덩어리는 건너뜁니다. 외벽 모서리 메시의 경계 상자가 방 안까지 1m 넘게 들어옵니다.
            if (IsExterior(renderer.transform)) continue;
            int bigAxes = (worldSize.x > 2f ? 1 : 0) + (worldSize.y > 2f ? 1 : 0) + (worldSize.z > 2f ? 1 : 0);
            if (bigAxes >= 2) continue;
            // 머리 위(천장 조명 등)는 부딪힐 일이 없습니다.
            if (renderer.bounds.min.y > 2.0f) continue;

            // 좌우 반전(음수 스케일)된 오브젝트는 BoxCollider가 경고를 내므로 건너뜁니다.
            Vector3 ls = renderer.transform.lossyScale;
            if (ls.x < 0f || ls.y < 0f || ls.z < 0f) continue;

            Bounds local = filter.sharedMesh.bounds;
            BoxCollider box = renderer.gameObject.AddComponent<BoxCollider>();
            box.center = local.center;
            box.size = local.size;
            done.Add(renderer.transform);
            added++;
        }

        if (added > 0) Physics.SyncTransforms();
        return added;
    }

    static bool IsExterior(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            string n = p.name;
            if (n.StartsWith("Ext_") || n.IndexOf("exterior", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("roof", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    // 플레이어, 고양이, 단서(클릭 대상), UI는 손대지 않습니다.
    static bool ShouldSkip(Transform t)
    {
        for (Transform p = t; p != null; p = p.parent)
        {
            string layer = LayerMask.LayerToName(p.gameObject.layer);
            if (layer == "Clickable" || layer == "UI" || layer == "Ignore Raycast" || layer.StartsWith("Quest")) return true;
            if (p.GetComponent<ClickableObject>() != null || p.GetComponent<HiddenCat>() != null) return true;
            if (p.GetComponent<FirstPersonController>() != null || p.GetComponent<Canvas>() != null) return true;
        }
        return false;
    }

    // 자기 자신, 자식, 또는 가까운 부모(같은 가구) 어딘가에 이미 콜라이더가 있으면 그대로 둡니다.
    static bool HasColliderAround(Transform t)
    {
        if (t.GetComponentInChildren<Collider>(true) != null) return true;
        int depth = 0;
        for (Transform p = t.parent; p != null && depth < 3; p = p.parent, depth++)
        {
            if (p.GetComponent<Collider>() != null) return true;
        }
        return false;
    }
}
