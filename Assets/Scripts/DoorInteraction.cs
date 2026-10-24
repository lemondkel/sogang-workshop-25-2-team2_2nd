using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 아파트 키트의 문/서랍 스크립트(opencloseDoor, ClosetopencloseDoor, Drawer_Pull_* 등)를 E 키 상호작용에 연결합니다.
/// 이 스크립트들은 모두 public bool open, private IEnumerator opening()/closing() 를 가지고 있어서
/// 종류와 상관없이 같은 방식으로 다룰 수 있습니다. 원래 들어 있던 마우스 오버 입력(Player 필드가 있어야 동작)은
/// 끄고, FirstPersonController의 시선 레이캐스트 + E/클릭으로만 여닫습니다 (두 경로가 같이 동작하면 두 번 토글돼 도로 닫힙니다).
/// </summary>
public static class DoorInteraction
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static bool TryGet(Collider collider, out MonoBehaviour door)
    {
        door = null;
        if (collider == null) return false;
        int depth = 0;
        for (Transform t = collider.transform; t != null && depth < 4; t = t.parent, depth++)
        {
            foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb != null && IsDoorScript(mb)) { door = mb; return true; }
            }
        }
        return false;
    }

    public static bool IsDoor(Collider collider)
    {
        MonoBehaviour d;
        return TryGet(collider, out d);
    }

    /// <summary>플레이어가 열고 지나갈 수 있는 것(문·옷장·미닫이). 창문·오븐·접이식 테이블은 아닙니다.</summary>
    public static bool IsPassable(Collider collider)
    {
        MonoBehaviour d;
        if (!TryGet(collider, out d)) return false;
        string n = d.GetType().Name;
        return n.IndexOf("Window", System.StringComparison.OrdinalIgnoreCase) < 0
            && n.IndexOf("Oven", System.StringComparison.OrdinalIgnoreCase) < 0
            && n.IndexOf("Flip", System.StringComparison.OrdinalIgnoreCase) < 0;
    }

    static bool IsDoorScript(MonoBehaviour mb)
    {
        System.Type type = mb.GetType();
        FieldInfo open = type.GetField("open", Flags);
        if (open == null || open.FieldType != typeof(bool)) return false;
        MethodInfo opening = type.GetMethod("opening", Flags);
        return opening != null && typeof(IEnumerator).IsAssignableFrom(opening.ReturnType);
    }

    public static bool IsOpen(MonoBehaviour door)
    {
        FieldInfo open = door.GetType().GetField("open", Flags);
        return open != null && (bool)open.GetValue(door);
    }

    public static string Label(MonoBehaviour door)
    {
        string n = door.GetType().Name;
        bool open = IsOpen(door);
        string what = "문";
        if (n.StartsWith("Drawer")) what = "서랍";
        else if (n.IndexOf("Window", System.StringComparison.OrdinalIgnoreCase) >= 0) what = "창문";
        else if (n.IndexOf("Oven", System.StringComparison.OrdinalIgnoreCase) >= 0) what = "오븐";
        else if (n.IndexOf("Flip", System.StringComparison.OrdinalIgnoreCase) >= 0) return open ? "접기" : "펼치기"; // 접이식 테이블
        return what + (open ? " 닫기" : " 열기");
    }

    public static void Toggle(MonoBehaviour door)
    {
        if (door == null || !door.gameObject.activeInHierarchy) return;
        door.StopAllCoroutines();
        door.StartCoroutine(IsOpen(door) ? "closing" : "opening");
    }

    /// <summary>문 스크립트에 들어 있는 자체 마우스 입력을 끕니다 (Player 참조가 없으면 아무것도 하지 않는 구조).</summary>
    public static int DisableBuiltInInput()
    {
        int count = 0;
        foreach (MonoBehaviour mb in Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (mb == null || !IsDoorScript(mb)) continue;
            FieldInfo player = mb.GetType().GetField("Player", Flags);
            if (player != null && player.FieldType == typeof(Transform)) player.SetValue(mb, null);
            count++;
        }
        return count;
    }
}
