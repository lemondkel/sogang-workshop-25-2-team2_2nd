using UnityEngine;

/// <summary>
/// 고양이 찾기용 탐색 조작입니다. (클래스 이름은 씬 참조 유지를 위해 그대로 둡니다)
/// - 기본: 캐릭터가 보이는 어깨 너머 3인칭 시점. 캐릭터가 이동 방향으로 부드럽게 돌아서서 걷습니다.
/// - V 키: 1인칭 시점으로 전환. 머리는 숨기고 두 손이 화면 앞에 보이도록 팔을 올립니다.
/// - 이동: WASD / ↑↓ (A·D는 옆걸음, ←→는 시점 회전), 마우스로 둘러보기, E(또는 클릭)로 상호작용.
/// - Space 점프, Ctrl 앉기(가구 밑을 들여다보기). 모델에 앉기 동작이 없어서 앉은 자세는 뼈를 직접 굽혀 만듭니다.
/// 느긋하게 집 안을 둘러보는 느낌에 맞춰 속도와 가감속을 낮췄습니다.
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
    public float jumpSpeed = 5f;

    [Header("Crouch")]
    public KeyCode crouchKey = KeyCode.LeftControl;
    public float standHeight = 1.75f;
    public float crouchHeight = 1.05f;
    public float crouchSpeedMultiplier = 0.55f;
    [Tooltip("앉았을 때 엉덩이(Hips)를 내리는 높이")]
    public float crouchHipsDrop = 0.42f;
    private bool isCrouching;
    private float crouchBlend; // 0 서 있음 ~ 1 앉음 (부드럽게 전환)

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
    private Transform hips, spine, leftUpLeg, leftLeg, leftFoot, rightUpLeg, rightLeg, rightFoot;

    private float yaw, pitch, targetYaw, targetPitch;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float currentCameraDistance;
    private bool invertY;
    private readonly RaycastHit[] hits = new RaycastHit[16];
    private readonly RaycastHit[] blockers = new RaycastHit[16];

    // 앉기 포즈를 덮어쓴 뼈들. 애니메이터가 매 프레임 다시 쓰지 않는 뼈(예: Hips 위치)는
    // 우리가 더한 값이 프레임마다 쌓이므로, 지난 프레임에 쓴 값을 기억했다가 되돌립니다.
    private Transform[] crouchBones;
    private Vector3[] crouchPrePos, crouchPostPos;
    private Quaternion[] crouchPreRot, crouchPostRot;
    private bool crouchPoseWritten;

    // 1인칭 손 위치 (카메라 기준). 오른손은 간식을 든 것처럼 조금 더 앞에.
    // 팔이 쭉 펴진 '좀비 자세'가 되지 않도록 조금 가깝고 낮게 (화면 아래쪽 1/3에 보입니다)
    private static readonly Vector3 RightHandOffset = new Vector3(0.20f, -0.17f, 0.40f);
    private static readonly Vector3 LeftHandOffset = new Vector3(-0.22f, -0.19f, 0.38f);
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

    // 상호작용 레이캐스트 마스크: Inspector 값에 더해 Clickable과 단서 레이어(Quest1~5)는 항상 포함합니다.
    int InteractMask
    {
        get
        {
            int mask = clickableLayerMask.value;
            int clickable = LayerMask.NameToLayer("Clickable");
            if (clickable >= 0) mask |= 1 << clickable;
            for (int i = 1; i <= 5; i++)
            {
                int quest = LayerMask.NameToLayer("Quest" + i);
                if (quest >= 0) mask |= 1 << quest;
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
        // 몸 메시의 어깨 반폭이 0.39m라 반지름이 작으면 팔이 벽에 파묻힙니다.
        characterController.radius = 0.38f;
        characterController.skinWidth = 0.035f; // 반지름의 10% 권장. 기본값 0.08은 벽에서 8cm 떨어져 멈추고 틈에 끼기 쉽습니다.
        characterController.minMoveDistance = 0f;
        characterController.stepOffset = 0.15f; // 밥그릇(19cm)·낮은 가구에 올라타지 않도록

        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // 몸을 숨기거나 카메라가 몸 안에 있어도 애니메이션(과 LateUpdate의 팔 IK)이 계속 돌도록
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        FindBones();
        crouchBones = new Transform[] { hips, spine, leftUpLeg, leftLeg, leftFoot, rightUpLeg, rightLeg, rightFoot };
        crouchPrePos = new Vector3[crouchBones.Length];
        crouchPostPos = new Vector3[crouchBones.Length];
        crouchPreRot = new Quaternion[crouchBones.Length];
        crouchPostRot = new Quaternion[crouchBones.Length];
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
        hips = FindDeep("Hips");
        spine = FindDeep("Spine");
        leftUpLeg = FindDeep("LeftUpLeg");
        leftLeg = FindDeep("LeftLeg");
        leftFoot = FindDeep("LeftFoot");
        rightUpLeg = FindDeep("RightUpLeg");
        rightLeg = FindDeep("RightLeg");
        rightFoot = FindDeep("RightFoot");
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
        UpdateCrouch();
        UpdateMove();
        UpdateInteraction();
    }

    void UpdateCrouch()
    {
        bool wantCrouch = Input.GetKey(crouchKey);
        if (wantCrouch && !isCrouching)
        {
            isCrouching = true;
        }
        else if (!wantCrouch && isCrouching)
        {
            // 천장(가구 밑)에 막혀 있으면 일어나지 않습니다.
            Vector3 bottom = transform.position + Vector3.up * (crouchHeight - 0.1f);
            Vector3 top = transform.position + Vector3.up * (standHeight - 0.3f);
            if (!Physics.CheckCapsule(bottom, top, characterController.radius * 0.9f, EnvironmentMask, QueryTriggerInteraction.Ignore))
            {
                isCrouching = false;
            }
        }

        crouchBlend = Mathf.MoveTowards(crouchBlend, isCrouching ? 1f : 0f, Time.deltaTime * 6f);
        float height = Mathf.Lerp(standHeight, crouchHeight, crouchBlend);
        characterController.height = height;
        characterController.center = new Vector3(0, height * 0.5f + 0.02f, 0);
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
        float speed = walkSpeed * Mathf.Lerp(1f, crouchSpeedMultiplier, crouchBlend);
        wish = Vector3.ClampMagnitude(wish, 1f) * speed;

        planarVelocity = Vector3.MoveTowards(planarVelocity, wish, acceleration * Time.deltaTime);

        if (characterController.isGrounded)
        {
            verticalVelocity = -2f;
            if (Input.GetButtonDown("Jump") && !isCrouching) verticalVelocity = jumpSpeed;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

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
        int count = Physics.RaycastNonAlloc(ray, hits, maxDistance, InteractMask);

        // 벽·가구 뒤에 숨은 고양이가 안내 문구로 들통나지 않도록, 시선과 대상 사이를 가리는 것이 있는지 봅니다.
        float nearestBlocker = float.MaxValue;
        int blockerCount = Physics.RaycastNonAlloc(ray, blockers, maxDistance, EnvironmentMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < blockerCount; i++)
        {
            if (blockers[i].collider.transform.IsChildOf(transform)) continue;
            if (blockers[i].distance < nearestBlocker) nearestBlocker = blockers[i].distance;
        }

        ClickableObject target = null;
        float best = float.MaxValue;
        Vector3 reachFrom = transform.position + Vector3.up * 1.2f;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider.transform.IsChildOf(transform)) continue; // 내 몸은 제외
            if (!hit.collider.enabled || hit.distance >= best) continue;
            if (hit.distance > nearestBlocker + 0.02f) continue; // 가려져 있음
            if (viewMode == ViewMode.ThirdPerson && Vector3.Distance(hit.point, reachFrom) > reachDistance) continue;
            if (hit.collider.gameObject.TryGetComponent<ClickableObject>(out var clickable))
            {
                best = hit.distance;
                target = clickable;
            }
        }

        // 화면 아래 "E 안아주기" 안내 (옆집 고양이가 들통나지 않도록 이름은 쓰지 않습니다)
        string label = null;
        if (target != null)
        {
            label = target.GetComponent<HiddenCat>() != null ? "안아주기" : "살펴보기";
        }
        if (GameManager.Instance.Hud != null) GameManager.Instance.Hud.SetInteractPrompt(label);

        if (target != null && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E)))
        {
            if (GameManager.Instance.Hud != null) GameManager.Instance.Hud.PulseInteractPrompt();
            target.HandleClick();
        }
        GameManager.Instance.SetCrosshairVisuals(target != null);
    }

    void LateUpdate()
    {
        if (cam == null) return;
        ApplyCrouchPose();
        if (viewMode == ViewMode.ThirdPerson) PlaceThirdPersonCamera();
        else PlaceFirstPersonCamera();

        if (camera3d != null)
        {
            float wantedFov = viewMode == ViewMode.ThirdPerson ? fieldOfView : firstPersonFieldOfView;
            camera3d.fieldOfView = Mathf.Lerp(camera3d.fieldOfView, wantedFov, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
    }

    /// <summary>
    /// 모델에 앉기 애니메이션이 없어서, 애니메이터가 포즈를 쓴 뒤(LateUpdate) 엉덩이를 내리고
    /// 허벅지·무릎·발목을 굽혀 쪼그려 앉은 자세를 만듭니다. crouchBlend로 서서히 섞입니다.
    /// </summary>
    void ApplyCrouchPose()
    {
        RestoreCrouchPose();
        if (crouchBlend <= 0.001f || hips == null) return;

        for (int i = 0; i < crouchBones.Length; i++)
        {
            if (crouchBones[i] == null) continue;
            crouchPrePos[i] = crouchBones[i].localPosition;
            crouchPreRot[i] = crouchBones[i].localRotation;
        }

        Vector3 right = transform.right;
        hips.position += Vector3.down * (crouchHipsDrop * crouchBlend);
        if (spine != null) spine.rotation = Quaternion.AngleAxis(22f * crouchBlend, right) * spine.rotation;

        BendLeg(leftUpLeg, leftLeg, leftFoot, right);
        BendLeg(rightUpLeg, rightLeg, rightFoot, right);

        for (int i = 0; i < crouchBones.Length; i++)
        {
            if (crouchBones[i] == null) continue;
            crouchPostPos[i] = crouchBones[i].localPosition;
            crouchPostRot[i] = crouchBones[i].localRotation;
        }
        crouchPoseWritten = true;
    }

    /// <summary>
    /// 지난 프레임에 앉기 포즈로 덮어쓴 뼈가 애니메이터에 의해 다시 쓰이지 않았다면(값이 그대로라면)
    /// 덮어쓰기 전 포즈로 되돌립니다. 그래야 엉덩이 내림/관절 굽힘이 프레임마다 누적되지 않습니다.
    /// </summary>
    void RestoreCrouchPose()
    {
        if (!crouchPoseWritten || crouchBones == null) return;
        for (int i = 0; i < crouchBones.Length; i++)
        {
            Transform bone = crouchBones[i];
            if (bone == null) continue;
            // 위치와 회전은 따로 봅니다 (애니메이션이 Hips 회전만 쓰고 위치는 안 쓰는 경우가 흔합니다).
            if ((bone.localPosition - crouchPostPos[i]).sqrMagnitude < 1e-8f) bone.localPosition = crouchPrePos[i];
            if (Quaternion.Angle(bone.localRotation, crouchPostRot[i]) < 0.01f) bone.localRotation = crouchPreRot[i];
        }
        crouchPoseWritten = false;
    }

    void BendLeg(Transform upLeg, Transform leg, Transform foot, Vector3 right)
    {
        if (upLeg == null || leg == null) return;
        // 허벅지는 앞으로 올리고(-), 무릎은 뒤로 접고(+), 발은 바닥에 맞게 되돌립니다.
        upLeg.rotation = Quaternion.AngleAxis(-80f * crouchBlend, right) * upLeg.rotation;
        leg.rotation = Quaternion.AngleAxis(100f * crouchBlend, right) * leg.rotation;
        if (foot != null) foot.rotation = Quaternion.AngleAxis(-20f * crouchBlend, right) * foot.rotation;
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
        Vector3 center = transform.position + Vector3.up * Mathf.Lerp(cameraHeight, cameraHeight - crouchHipsDrop, crouchBlend);
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
