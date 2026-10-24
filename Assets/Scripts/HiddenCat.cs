using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨에 숨겨진 고양이 한 마리입니다. 찾아야 하는 고양이(target)인지 옆집 고양이(decoy)인지 구분합니다.
/// 돌아다니기가 켜지면 숨을 자리 사이를 어슬렁거리고, 플레이어가 가까이 오면 다른 자리로 도망칩니다.
/// 안아주면 플레이어 품에 안긴 자세로 멈춥니다.
/// </summary>
public class HiddenCat : MonoBehaviour
{
    public string catName;
    public bool isDecoy;
    public bool found;

    enum Mode { Rest, Walk, Flee, Held, Gone }

    const float WalkSpeed = 1.0f;       // 어슬렁거리는 속도 (m/s)
    const float FleeSpeed = 2.3f;       // 도망치는 속도
    const float SpotRadius = 7f;        // 이 거리 안의 자리로만 옮겨 다닙니다
    const float AlertDistance = 3.0f;   // 플레이어가 이 안에 들어오면 도망
    const float SneakDistance = 1.6f;   // 플레이어가 앉아서 다가오면 이 안까지는 눈치채지 못함
    const float TiredSeconds = 9f;      // 한 번 도망친 뒤 다시 도망치지 않는 시간 (이때 잡을 수 있음)
    const int MaxFlees = 3;             // 한 판에 최대 도망 횟수 (영원히 못 잡는 일이 없도록)

    private static readonly string[] IdleStates = { "idle", "sit", "sitting" };

    private Animator animator;
    private AudioSource audioSource;

    private Mode mode = Mode.Rest;
    private bool canWander;
    private int minCover;
    private List<CatSpawner.Spot> spots;
    private Transform player;
    private FirstPersonController controller;
    private readonly List<Vector3> path = new List<Vector3>();
    private int pathIndex;
    private bool replanned;
    private int senseStreak;
    private Vector3 target;
    private float moveSpeed;
    private float restUntil, tiredUntil, nextSenseTime;
    private int fleeCount;
    private bool vanishOnArrive;
    private Vector3 baseScale;

    public void Setup(string name, Color tint, bool decoy, AudioClip meow)
    {
        catName = name;
        isDecoy = decoy;
        baseScale = transform.localScale;

        animator = GetComponentInChildren<Animator>();

        // 프리팹 콜라이더(반지름 0.15×스케일 2.03 = 0.3m)는 몸보다 넓어 가구 밑에서 가구와 겹치는 것으로 잡혔습니다. 몸 크기에 맞춥니다 (반지름 약 0.22m).
        CapsuleCollider body = GetComponent<CapsuleCollider>();
        if (body != null)
        {
            body.radius = 0.11f;
            body.height = 0.1f;
            body.center = new Vector3(0f, 0.1f, -0.03f);
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.clip = meow;
        audioSource.spatialBlend = 1f;          // 3D 사운드: 소리 나는 방향으로 찾을 수 있게
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 25f;

        // 사진 속 고양이와 비슷하게 색을 입힙니다 (머티리얼 복제 없이)
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            block.SetColor("_Color", tint);
            r.SetPropertyBlock(block);
        }

        if (animator != null)
        {
            animator.Play(IdleStates[Random.Range(0, IdleStates.Length)], 0, Random.value);
        }
    }

    /// <summary>
    /// 돌아다니기를 켭니다. hideSpots는 집 안의 숨을 자리 후보(바닥·가구 위)이고, 그중 같은 높이·가림 정도가 minCover 이상인 곳만 오갑니다.
    /// </summary>
    public void EnableWander(List<CatSpawner.Spot> hideSpots, Transform playerTransform, int minCoverForSpots)
    {
        spots = hideSpots;
        player = playerTransform;
        controller = playerTransform != null ? playerTransform.GetComponent<FirstPersonController>() : null;
        minCover = minCoverForSpots;
        canWander = spots != null && spots.Count > 0 && player != null && CatNav.Ready;
        restUntil = Time.time + Random.Range(3f, 10f);
        if (!canWander) Debug.LogWarning("[HiddenCat] " + name + " 돌아다니기 꺼짐: 자리 " + (spots != null ? spots.Count : 0) + "개, 플레이어 " + (player != null) + ", 길 " + CatNav.Ready);
    }

    void Update()
    {
        if (mode == Mode.Held) { HoldPose(); return; }
        if (!canWander || found || mode == Mode.Gone) return;
        if (GameManager.Instance != null && GameManager.Instance.CurrentGameState != GameManager.GameState.Playing) return;

        switch (mode)
        {
            case Mode.Rest:
                if (Time.time >= nextSenseTime)
                {
                    nextSenseTime = Time.time + 0.2f;
                    // 두 번 연속(0.4초) 보여야 도망칩니다 (스쳐 지나가는 플레이어에 바로 튀지 않도록)
                    senseStreak = SensesPlayer() ? senseStreak + 1 : 0;
                    if (senseStreak >= 2 && TryFlee(false)) { senseStreak = 0; break; }
                }
                if (Time.time >= restUntil)
                {
                    if (!TryWander()) restUntil = Time.time + Random.Range(4f, 9f);
                }
                break;
            case Mode.Walk:
            case Mode.Flee:
                Step();
                break;
        }
    }

    // ===== 돌아다니기 =====

    bool SensesPlayer()
    {
        if (Time.time < tiredUntil || fleeCount >= MaxFlees || player == null) return false;
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        bool sneaking = controller != null && controller.IsCrouching;
        float alert = sneaking ? SneakDistance : AlertDistance;
        if (toPlayer.magnitude > alert) return false;
        // 벽·가구 너머의 플레이어는 보지 못합니다
        Vector3 eye = transform.position + Vector3.up * 0.3f;
        Vector3 chest = player.position + Vector3.up * 1.0f;
        return !Physics.Linecast(eye, chest, CatSpawner.EnvironmentMask, QueryTriggerInteraction.Ignore);
    }

    struct Candidate { public Vector3 position; public float score; }
    private static readonly List<Candidate> candidates = new List<Candidate>();
    private static readonly List<HiddenCat> all = new List<HiddenCat>();

    void OnEnable() { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    /// <summary>다른 고양이가 있거나 가고 있는 자리인지 (여러 마리가 한 자리로 몰리지 않게).</summary>
    bool TakenByOther(Vector3 p)
    {
        foreach (HiddenCat c in all)
        {
            if (c == this || c == null) continue;
            if (Flat(c.transform.position - p).magnitude < 1.2f) return true;
            if ((c.mode == Mode.Walk || c.mode == Mode.Flee) && c.path.Count > 0 && Flat(c.path[c.path.Count - 1] - p).magnitude < 1.2f) return true;
        }
        return false;
    }

    bool TryWander()
    {
        Vector3 here = transform.position;
        candidates.Clear();
        foreach (CatSpawner.Spot s in spots)
        {
            if (s.cover < minCover) continue;
            Vector3 p = s.position;
            if (Mathf.Abs(p.y - here.y) > 0.3f) continue;                 // 같은 높이(바닥끼리, 같은 선반끼리)만
            float d = Flat(p - here).magnitude;
            if (d < 1.5f || d > SpotRadius) continue;
            if (player != null && Flat(p - player.position).magnitude < 3.0f) continue; // 플레이어 쪽으로는 가지 않습니다
            if (TakenByOther(p)) continue;
            candidates.Add(new Candidate { position = p, score = Random.value * 2f + s.cover * 0.3f });
        }
        return StartMoveToBest(Mode.Walk);
    }

    bool TryFlee(bool force)
    {
        Vector3 here = transform.position;
        candidates.Clear();
        foreach (CatSpawner.Spot s in spots)
        {
            if (!force && s.cover < minCover) continue;
            Vector3 p = s.position;
            if (Mathf.Abs(p.y - here.y) > 0.3f) continue;
            float d = Flat(p - here).magnitude;
            if (d < 1.5f || d > SpotRadius) continue;
            float fromPlayer = player != null ? Flat(p - player.position).magnitude : 10f;
            if (fromPlayer < 3.5f) continue;
            if (TakenByOther(p)) continue;
            // 플레이어에게서 멀어지는 방향, 너무 멀지 않은 자리, 잘 가려진 자리를 좋아합니다
            candidates.Add(new Candidate { position = p, score = fromPlayer - d * 0.4f + s.cover * 0.4f + Random.value });
        }
        if (!StartMoveToBest(Mode.Flee)) return false; // 구석에 몰림: 그대로 있다가 잡힙니다
        fleeCount++;
        if (player != null)
        {
            Debug.Log("[HiddenCat] " + name + " 도망 " + fleeCount + "회: 플레이어 거리 " + Flat(player.position - here).magnitude.ToString("0.00")
                + "m, 앉음 " + (controller != null && controller.IsCrouching) + ", 목적지 " + path[path.Count - 1]);
        }
        return true;
    }

    /// <summary>점수가 높은 자리부터 길이 있는지 확인해서 첫 번째로 갈 수 있는 자리로 출발합니다.</summary>
    bool StartMoveToBest(Mode moveMode)
    {
        candidates.Sort((a, b) => b.score.CompareTo(a.score));
        int tries = Mathf.Min(candidates.Count, 8);
        for (int i = 0; i < tries; i++)
        {
            if (!CatNav.FindPath(transform.position, candidates[i].position, path)) continue;
            pathIndex = 0;
            replanned = false;
            target = path[0];
            mode = moveMode;
            moveSpeed = moveMode == Mode.Flee ? FleeSpeed : WalkSpeed;
            if (animator != null)
            {
                animator.Play("walk", 0, Random.value);
                animator.speed = moveMode == Mode.Flee ? 1.8f : 1.0f;
            }
            return true;
        }
        return false;
    }

    void Step()
    {
        int mask = CatSpawner.EnvironmentMask;
        Vector3 to = target - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist < 0.08f)
        {
            pathIndex++;
            if (pathIndex >= path.Count) { Arrive(); return; }
            target = path[pathIndex];
            return;
        }
        Vector3 dir = to / dist;

        // 바로 앞이 막혔으면(문이 닫혔거나 뭔가 놓여 있으면) 한 번 길을 다시 찾고, 그래도 막히면 거기서 멈춥니다 (길찾기와 같은 몸 크기 상자로 확인)
        if (CatNav.Blocked(transform.position, dir, Mathf.Min(0.3f, dist)))
        {
            Vector3 destination = path[path.Count - 1];
            if (!replanned && CatNav.FindPath(transform.position, destination, path))
            {
                replanned = true;
                pathIndex = 0;
                target = path[0];
                return;
            }
            Debug.Log("[HiddenCat] " + name + " 멈춤(앞 막힘) " + transform.position + " → " + destination);
            Arrive();
            return;
        }

        float step = Mathf.Min(moveSpeed * Time.deltaTime, dist);
        Vector3 next = transform.position + dir * step;

        // 바닥(또는 선반)에 발을 맞춥니다. 발밑이 사라지면(가장자리) 멈춥니다.
        // 침대·탁자 밑(0.5m 아래)을 지날 수 있도록 0.2m 높이에서 아래로 쏩니다 (높이서 쏘면 가구 윗면에 맞아 멈춥니다).
        RaycastHit ground;
        if (Physics.Raycast(next + Vector3.up * 0.2f, Vector3.down, out ground, 0.6f, mask, QueryTriggerInteraction.Ignore)
            && Mathf.Abs(ground.point.y - transform.position.y) < 0.2f)
        {
            next.y = ground.point.y;
        }
        else
        {
            Debug.Log("[HiddenCat] " + name + " 멈춤(발밑 없음) " + transform.position);
            Arrive();
            return;
        }

        transform.position = next;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 540f * Time.deltaTime);

        if (vanishOnArrive)
        {
            // 옆집 고양이가 도망칠 때는 멀어질수록 작아지다가 사라집니다
            float remaining = dist;
            for (int i = pathIndex + 1; i < path.Count; i++) remaining += Vector3.Distance(path[i - 1], path[i]);
            transform.localScale = baseScale * Mathf.Clamp01(remaining / 2.5f + 0.15f);
        }
        if (animator != null)
        {
            AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
            if (!st.IsName("walk") && !animator.IsInTransition(0)) animator.Play("walk", 0, 0f);
        }
    }

    void Arrive()
    {
        if (vanishOnArrive) { mode = Mode.Gone; Destroy(gameObject); return; }
        if (mode == Mode.Flee) tiredUntil = Time.time + TiredSeconds;
        mode = Mode.Rest;
        senseStreak = 0;
        restUntil = Time.time + Random.Range(6f, 14f);
        if (animator != null)
        {
            animator.speed = 1f;
            animator.Play(Random.value < 0.5f ? "idle" : "sit", 0, 0f);
        }
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // ===== 소리·찾음·도망 =====

    public void Meow()
    {
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.pitch = isDecoy ? 0.8f : Random.Range(0.95f, 1.15f);
            audioSource.Play();
        }
        if (animator != null && mode != Mode.Held)
        {
            animator.speed = 1f;
            animator.Play("miau", 0, 0f);
        }
    }

    /// <summary>
    /// 찾았을 때: 더는 움직이지 않고 플레이어 품에 안길 준비를 합니다 (위치는 FirstPersonController의 안아주기가 움직입니다).
    /// </summary>
    public void PlayFound()
    {
        found = true;
        StopAllCoroutines();
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        mode = Mode.Held;
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.pitch = Random.Range(1.0f, 1.15f);
            audioSource.Play();
        }
        if (animator != null)
        {
            animator.speed = 1f;
            animator.Play("sitting", 0, 0f); // 앉는 동작을 하다가 마지막 자세(얌전히 앉은 자세)로 멈춥니다
        }
    }

    void HoldPose()
    {
        if (animator == null) return;
        AnimatorStateInfo st = animator.GetCurrentAnimatorStateInfo(0);
        if (st.IsName("sitting"))
        {
            if (st.normalizedTime >= 0.98f) animator.speed = 0f;
        }
        else if (!animator.IsInTransition(0))
        {
            animator.Play("sitting", 0, 1f);
            animator.speed = 0f;
        }
    }

    /// <summary>안아준 뒤 반짝 하고 사라집니다.</summary>
    public void PlayPopAndHide()
    {
        mode = Mode.Gone;
        StartCoroutine(PopAndHide());
    }

    public void PlayRunAway()
    {
        StopAllCoroutines();
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        // 숨을 자리를 알면 진짜로 멀리 뛰어가서 숨고, 모르면 아무 방향으로 사라집니다
        if (canWander && TryFlee(true))
        {
            vanishOnArrive = true;
            return;
        }
        mode = Mode.Gone;
        if (animator != null) { animator.speed = 1.8f; animator.Play("walk", 0, 0f); }
        StartCoroutine(RunAway());
    }

    IEnumerator PopAndHide()
    {
        Vector3 start = transform.localScale;
        Vector3 pos = transform.position;
        float t = 0f;
        while (t < 0.7f)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.7f;
            float bounce = 1f + Mathf.Sin(k * Mathf.PI) * 0.35f;
            transform.localScale = start * bounce * (1f - k * k);
            pos += Vector3.up * Time.unscaledDeltaTime * 0.6f;
            transform.position = pos;
            yield return null;
        }
        Destroy(gameObject);
    }

    IEnumerator RunAway()
    {
        float t = 0f;
        Vector3 dir = Quaternion.Euler(0, Random.Range(0f, 360f), 0) * Vector3.forward;
        transform.rotation = Quaternion.LookRotation(dir);
        Vector3 start = transform.localScale;
        while (t < 0.8f)
        {
            t += Time.unscaledDeltaTime;
            transform.position += dir * Time.unscaledDeltaTime * 2.5f;
            transform.localScale = start * (1f - t / 0.8f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
