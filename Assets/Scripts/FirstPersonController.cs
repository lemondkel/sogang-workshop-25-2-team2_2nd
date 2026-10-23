using UnityEngine;

/// <summary>
/// 고양이 찾기용 탐색 조작입니다. (클래스 이름은 씬 참조 유지를 위해 그대로 둡니다)
/// - 기본: 캐릭터가 보이는 어깨 너머 3인칭 시점. 캐릭터가 이동 방향으로 부드럽게 돌아서서 걷습니다.
/// - V 키: 1인칭 시점으로 전환. 머리는 숨기고 두 손이 화면 앞에 보이도록 팔을 올립니다.
/// - 이동: WASD / ↑↓ (A·D는 옆걸음, ←→는 시점 회전), 마우스로 둘러보기, 클릭 또는 E로 찾기.
/// 점프·앉기는 없앴습니다. 느긋하게 집 안을 둘러보는 느낌에 맞춰 속도와 가감속을 낮췄습니다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    public enum ViewMode { ThirdPerson, FirstPerson }

    [Header("Movement")]
    public float walkSpeed = 2.6f;
    [Tooltip("가속/감속 (m/s²). 낮을수록 미끄러지듯 부드럽게 움직입니다.")]
    public float acceleration = 9f;
    [Tooltip("캐릭터가 이동 방향으로 도는 속도 (도/초)")]
    public float turnSpeed = 480f;
    public float gravity = 20f;

    [Header("Look")]
    public float mouseSensitivity = 2.0f;
    [Tooltip("←→ 키로 시점을 돌리는 속도 (도/초)")]
    public float keyTurnSpeed = 110f;
    [Tooltip("시점 보간 속도. 높을수록 즉각적입니다.")]
    public float lookSmoothing = 14f;

    [Header("Camera")]
    public ViewMode viewMode = ViewMode.ThirdPerson;
    public float cameraDistance = 2.0f;
    public float cameraHeight = 1.5f;
    [Tooltip("조준점(화면 중앙)이 캐릭터를 가리지 않도록 캐릭터를 화면 왼쪽으로 비켜 세우는 거리")]
    public float aimSideOffset = 0.42f;
    public float shoulderOffset = 0.35f;
    public float cameraRadius = 0.16f;
    public float fieldOfView = 55f;
    public float firstPersonFieldOfView = 65f;
    [Tooltip("벽에 밀려 카메라가 이 거리보다 가까워지면 몸을 숨깁니다 (몸이 화면을 가리지 않게)")]
    public float hideBodyDistance = 0.75f;
    [Tooltip("다시 이 거리보다 멀어지면 몸을 보입니다 (깜빡임 방지용 여유)")]
    public float showBodyDistance = 0.95f;
    private bool bodyHidden;

    [Header("Interaction")]
    [Tooltip("카메라에서 찾을 수 있는 최대 거리")]
    public float raycastHitDistance = 3f;
    [Tooltip("캐릭터 몸에서 손이 닿는 거리 (3인칭)")]
    public float reachDistance = 2.8f;
    [Tooltip("클릭 가능한 오브젝트가 위치한 Layer Mask입니다. (ClickableObject 스크립트가 붙은 오브젝트만 확인)")]
    public LayerMask clickableLayerMask; // Inspector에서 설정해야 합니다.

    private CharacterController characterController;
    private Transform cam;
    private Animator animator;
    private Transform head, leftArm, leftForeArm, leftHand, rightArm, rightForeArm, rightHand;

    private float yaw, pitch, targetYaw, targetPitch;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float currentCameraDistance;
    private bool invertY;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    // 1인칭 손 위치 (카메라 기준). 오른손은 간식을 든 것처럼 조금 더 앞에.
    private static readonly Vector3 RightHandOffset = new Vector3(0.21f, -0.13f, 0.46f);
    private static readonly Vector3 LeftHandOffset = new Vector3(-0.23f, -0.16f, 0.42f);
    private SkinnedMeshRenderer[] bodyRenderers;
    private Camera camera3d;

    int EnvironmentMask
    {
        get
        {
            int mask = Physics.DefaultRaycastLayers & ~(1 << 2) & ~(1 << 5);
            mask &= ~(1 << gameObject.layer);
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

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        // 캐릭터 모델(약 1.8m)에 맞는 캡슐
        characterController.height = 1.75f;
        characterController.center = new Vector3(0, 0.9f, 0);
        characterController.radius = 0.3f;
        characterController.stepOffset = 0.35f;

        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // 몸을 숨기거나 카메라가 몸 안에 있어도 애니메이션(과 LateUpdate의 팔 IK)이 계속 돌도록
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        FindBones();
        bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        LevelDatabase db = LevelDatabase.Load();
        foreach (SkinnedMeshRenderer smr in bodyRenderers)
        {
            smr.updateWhenOffscreen = true; // 1인칭에서 카메라가 몸 안에 있어도 팔이 그려지도록

            // 스토리 그림(잠옷 차림)과 어울리도록 옷 텍스처를 바꿉니다 (LevelDatabase에서 지정)
            if (db.playerBodyTexture != null)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                smr.GetPropertyBlock(block);
                block.SetTexture("_MainTex", db.playerBodyTexture);
                if (db.playerBodyNormal != null) block.SetTexture("_BumpMap", db.playerBodyNormal);
                smr.SetPropertyBlock(block);
            }
        }

        if (Camera.main != null)
        {
            camera3d = Camera.main;
            cam = camera3d.transform;
            // 카메라를 캐릭터에서 떼어 내 LateUpdate에서 직접 배치합니다.
            cam.SetParent(null, true);
            camera3d.fieldOfView = fieldOfView;
            camera3d.nearClipPlane = 0.05f;
        }
        else
        {
            Debug.LogError("Error: Main Camera를 찾을 수 없습니다.");
        }

        yaw = targetYaw = transform.eulerAngles.y;
        pitch = targetPitch = 12f;
        currentCameraDistance = cameraDistance;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        ApplySettings();
    }

    // 설정 화면에서 바꾼 마우스 감도/반전을 적용합니다.
    public void ApplySettings()
    {
        mouseSensitivity = GameSession.MouseSensitivity;
        invertY = GameSession.InvertY;
    }

    void FindBones()
    {
        head = FindDeep("Head");
        leftArm = FindDeep("LeftArm");
        leftForeArm = FindDeep("LeftForeArm");
        leftHand = FindDeep("LeftHand");
        rightArm = FindDeep("RightArm");
        rightForeArm = FindDeep("RightForeArm");
        rightHand = FindDeep("RightHand");
    }

    // 뼈 이름은 리그마다 접두사가 달라서(genSuit_Head, mixamorig:Head ...) 끝부분으로 찾습니다.
    Transform FindDeep(string suffix)
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name.EndsWith(suffix) || t.name.EndsWith(":" + suffix) || t.name.EndsWith("_" + suffix)) return t;
        }
        return null;
    }

    bool IsPlaying
    {
        get { return GameManager.Instance == null || GameManager.Instance.CurrentGameState == GameManager.GameState.Playing; }
    }

    void Update()
    {
        if (!IsPlaying)
        {
            // 미션 안내/일시정지/결과 화면에서는 멈춰 서 있습니다.
            planarVelocity = Vector3.zero;
            if (animator != null) { animator.SetBool("IsWalking", false); animator.speed = 1f; }
            return;
        }

        if (Input.GetKeyDown(KeyCode.V)) ToggleView();

        UpdateLook();
        UpdateMove();
        UpdateInteraction();
    }

    public void ToggleView()
    {
        viewMode = viewMode == ViewMode.ThirdPerson ? ViewMode.FirstPerson : ViewMode.ThirdPerson;
        if (viewMode == ViewMode.ThirdPerson) RestoreHead();
        bodyHidden = false;
        SetBodyVisible(true);
        targetPitch = Mathf.Clamp(targetPitch, PitchMin, PitchMax);
        if (GameManager.Instance != null && GameManager.Instance.Hud != null)
        {
            GameManager.Instance.Hud.Toast(viewMode == ViewMode.FirstPerson ? "1인칭 시점 (V로 전환)" : "3인칭 시점 (V로 전환)", UIKit.BrownLight);
        }
    }

    float PitchMin { get { return viewMode == ViewMode.ThirdPerson ? -30f : -80f; } }
    float PitchMax { get { return viewMode == ViewMode.ThirdPerson ? 65f : 80f; } }

    void UpdateLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * (invertY ? -1f : 1f);
        float keyTurn = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);

        targetYaw += mouseX + keyTurn * keyTurnSpeed * Time.deltaTime;
        targetPitch = Mathf.Clamp(targetPitch - mouseY, PitchMin, PitchMax);

        float k = 1f - Mathf.Exp(-lookSmoothing * Time.deltaTime);
        yaw = Mathf.LerpAngle(yaw, targetYaw, k);
        pitch = Mathf.Lerp(pitch, targetPitch, k);
    }

    void UpdateMove()
    {
        // ↑↓/W/S는 앞뒤, A/D만 옆걸음 (←→는 시점 회전에 사용하므로 Horizontal 축을 쓰지 않습니다)
        float forwardInput = Input.GetAxisRaw("Vertical");
        float strafeInput = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);

        Quaternion viewRotation = Quaternion.Euler(0, yaw, 0);
        Vector3 wish = viewRotation * new Vector3(strafeInput, 0, forwardInput);
        wish = Vector3.ClampMagnitude(wish, 1f) * walkSpeed;

        planarVelocity = Vector3.MoveTowards(planarVelocity, wish, acceleration * Time.deltaTime);

        if (characterController.isGrounded) verticalVelocity = -2f;
        else verticalVelocity -= gravity * Time.deltaTime;

        characterController.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);

        // 캐릭터 몸 방향
        if (viewMode == ViewMode.ThirdPerson)
        {
            if (planarVelocity.sqrMagnitude > 0.05f)
            {
                Quaternion face = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnSpeed * Time.deltaTime);
            }
        }
        else
        {
            transform.rotation = viewRotation;
        }

        if (animator != null)
        {
            bool moving = planarVelocity.magnitude > 0.25f;
            animator.SetBool("IsWalking", moving);
            // 걷기 애니메이션(약 1.4m/s 기준)을 실제 속도에 맞춰 발이 미끄러지지 않게
            animator.speed = moving ? Mathf.Clamp(planarVelocity.magnitude / 1.4f, 0.7f, 1.5f) : 1f;
        }
    }

    void UpdateInteraction()
    {
        if (cam == null || GameManager.Instance == null) return;

        Ray ray = new Ray(cam.position, cam.forward);
        float maxDistance = raycastHitDistance + (viewMode == ViewMode.ThirdPerson ? currentCameraDistance : 0f);
        int count = Physics.RaycastNonAlloc(ray, hits, maxDistance, clickableLayerMask);

        ClickableObject target = null;
        float best = float.MaxValue;
        Vector3 reachFrom = transform.position + Vector3.up * 1.2f;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider.transform.IsChildOf(transform)) continue; // 내 몸은 제외
            if (!hit.collider.enabled || hit.distance >= best) continue;
            if (viewMode == ViewMode.ThirdPerson && Vector3.Distance(hit.point, reachFrom) > reachDistance) continue;
            if (hit.collider.gameObject.TryGetComponent<ClickableObject>(out var clickable))
            {
                best = hit.distance;
                target = clickable;
            }
        }

        if (target != null && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E)))
        {
            target.HandleClick();
        }
        GameManager.Instance.SetCrosshairVisuals(target != null);
    }

    void LateUpdate()
    {
        if (cam == null) return;
        if (viewMode == ViewMode.ThirdPerson) PlaceThirdPersonCamera();
        else PlaceFirstPersonCamera();

        if (camera3d != null)
        {
            float wantedFov = viewMode == ViewMode.ThirdPerson ? fieldOfView : firstPersonFieldOfView;
            camera3d.fieldOfView = Mathf.Lerp(camera3d.fieldOfView, wantedFov, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
    }

    void SetBodyVisible(bool visible)
    {
        if (bodyRenderers == null) return;
        foreach (SkinnedMeshRenderer smr in bodyRenderers)
        {
            if (smr != null && smr.enabled != visible) smr.enabled = visible;
        }
    }

    void PlaceThirdPersonCamera()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        int mask = EnvironmentMask;
        RaycastHit hit;

        // 회전 중심을 캐릭터 오른쪽으로 비켜 두면 캐릭터는 화면 왼쪽에, 조준점은 캐릭터 옆 빈 공간에 놓입니다.
        // 오른쪽이 벽이면 그만큼만 비킵니다 (회전 중심이 벽 속으로 들어가면 카메라도 벽 속에 생깁니다).
        Vector3 center = transform.position + Vector3.up * cameraHeight;
        Vector3 side = Quaternion.Euler(0, yaw, 0) * Vector3.right;
        float sideLength = aimSideOffset;
        if (Physics.SphereCast(center, cameraRadius, side, out hit, aimSideOffset, mask, QueryTriggerInteraction.Ignore))
        {
            sideLength = Mathf.Max(0f, hit.distance - 0.05f);
        }
        Vector3 pivot = center + side * sideLength;

        Vector3 desired = pivot + rotation * new Vector3(shoulderOffset, 0, -cameraDistance);
        Vector3 dir = desired - pivot;
        float wanted = dir.magnitude;
        dir /= wanted;

        // 벽이나 가구에 카메라가 파묻히지 않게 당깁니다.
        if (Physics.SphereCast(pivot, cameraRadius, dir, out hit, wanted, mask, QueryTriggerInteraction.Ignore))
        {
            wanted = Mathf.Max(hit.distance, 0.25f);
        }
        // 가까워질 때는 바로, 멀어질 때는 천천히
        currentCameraDistance = wanted < currentCameraDistance
            ? wanted
            : Mathf.Lerp(currentCameraDistance, wanted, 1f - Mathf.Exp(-4f * Time.deltaTime));

        cam.position = pivot + dir * currentCameraDistance;
        cam.rotation = rotation;

        // 좁은 방에서 카메라가 몸에 붙으면 몸이 화면을 다 가리므로 잠시 숨깁니다. (깜빡임 방지를 위해 숨김/표시 거리를 다르게)
        if (!bodyHidden && currentCameraDistance < hideBodyDistance) bodyHidden = true;
        else if (bodyHidden && currentCameraDistance > showBodyDistance) bodyHidden = false;
        SetBodyVisible(!bodyHidden);
    }

    void PlaceFirstPersonCamera()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 eye = head != null ? head.position : transform.position + Vector3.up * 1.65f;
        cam.position = eye + rotation * new Vector3(0, 0.05f, 0.08f);
        cam.rotation = rotation;

        // 머리는 안 보이게 줄이고(카메라와 겹침 방지), 두 손은 화면 앞으로
        if (head != null) head.localScale = Vector3.one * 0.001f;
        Vector3 shoulderCenter = transform.position + Vector3.up * 1.4f;
        if (rightArm != null && rightForeArm != null && rightHand != null)
        {
            Vector3 target = cam.position + rotation * RightHandOffset;
            Vector3 pole = shoulderCenter + rotation * new Vector3(0.6f, -0.5f, -0.1f);
            TwoBoneIK(rightArm, rightForeArm, rightHand, target, pole);
        }
        if (leftArm != null && leftForeArm != null && leftHand != null)
        {
            Vector3 target = cam.position + rotation * LeftHandOffset;
            Vector3 pole = shoulderCenter + rotation * new Vector3(-0.6f, -0.5f, -0.1f);
            TwoBoneIK(leftArm, leftForeArm, leftHand, target, pole);
        }
    }

    void RestoreHead()
    {
        if (head != null) head.localScale = Vector3.one;
    }

    /// <summary>
    /// 간단한 두 뼈(윗팔-아랫팔-손) IK. 손이 target에 닿도록 윗팔/아랫팔을 회전합니다. 팔꿈치는 pole 쪽을 향합니다.
    /// </summary>
    static void TwoBoneIK(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
    {
        const float eps = 0.001f;
        Vector3 a = upper.position, b = lower.position, c = end.position;
        float lab = (b - a).magnitude;
        float lcb = (c - b).magnitude;
        float lat = Mathf.Clamp((target - a).magnitude, eps, lab + lcb - eps);

        float ac_ab_0 = Vector3.Angle(c - a, b - a);
        float ba_bc_0 = Vector3.Angle(a - b, c - b);
        float ac_at_0 = Vector3.Angle(c - a, target - a);

        float ac_ab_1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f)) * Mathf.Rad2Deg;
        float ba_bc_1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f)) * Mathf.Rad2Deg;

        Vector3 axis0 = Vector3.Cross(c - a, b - a);
        if (axis0.sqrMagnitude < 1e-6f) axis0 = Vector3.Cross(c - a, pole - a); // 팔이 곧게 펴진 경우
        axis0.Normalize();
        Vector3 axis1 = Vector3.Cross(c - a, target - a).normalized;

        upper.rotation = Quaternion.AngleAxis(ac_ab_1 - ac_ab_0, axis0) * upper.rotation;
        lower.rotation = Quaternion.AngleAxis(ba_bc_1 - ba_bc_0, axis0) * lower.rotation;
        upper.rotation = Quaternion.AngleAxis(ac_at_0, axis1) * upper.rotation;

        // 팔꿈치 방향을 pole 쪽으로
        Vector3 n = (target - a).normalized;
        Vector3 elbowDir = Vector3.ProjectOnPlane(lower.position - a, n);
        Vector3 poleDir = Vector3.ProjectOnPlane(pole - a, n);
        if (elbowDir.sqrMagnitude > 1e-6f && poleDir.sqrMagnitude > 1e-6f)
        {
            float angle = Vector3.SignedAngle(elbowDir, poleDir, n);
            upper.rotation = Quaternion.AngleAxis(angle, n) * upper.rotation;
        }
    }
}
