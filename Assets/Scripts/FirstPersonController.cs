using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    // 이동 및 회전 관련 변수
    public float walkingSpeed = 5f;
    public float rotationSpeed = 150f;
    public float jumpSpeed = 8f;
    public float gravity = 20f;

    // 🌟 마우스 시점 조작 변수 🌟
    public float mouseSensitivity = 2.0f;
    public float minimumXRotation = -90f;
    public float maximumXRotation = 90f;

    // 앉기 관련 변수
    public KeyCode crouchKey = KeyCode.LeftControl;
    public float normalHeight = 2.0f;
    public float crouchHeight = 1.0f;
    public float crouchTransitionTime = 0.1f;
    public float crouchSpeedMultiplier = 0.5f;

    // 🌟 [추가] 오브젝트 상호작용을 위한 변수 🌟
    [Header("Interaction")]
    [Tooltip("상호작용 가능한 최대 거리입니다.")]
    public float raycastHitDistance = 3f;
    [Tooltip("클릭 가능한 오브젝트가 위치한 Layer Mask입니다. (ClickableObject 스크립트가 붙은 오브젝트만 확인)")]
    public LayerMask clickableLayerMask; // Inspector에서 설정해야 합니다.

    private CharacterController characterController;
    private Transform playerCamera;
    private Vector3 moveDirection = Vector3.zero;
    private float targetHeight;
    private bool isCrouching = false;
    private float xRotation = 0f;
    private bool invertY = false;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        targetHeight = normalHeight;

        // 카메라 찾기: 메인 카메라가 플레이어 GameObject의 자식이어야 합니다.
        if (Camera.main != null && Camera.main.transform.parent == transform)
        {
            playerCamera = Camera.main.transform;
        }
        else
        {
            Debug.LogError("Error: Main Camera must be a child of the Player GameObject!");
        }

        // 마우스 커서 잠금 및 숨기기
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

    void Update()
    {
        // 미션 안내/일시정지/결과 화면에서는 시점과 이동을 멈춥니다.
        if (GameManager.Instance != null && GameManager.Instance.CurrentGameState != GameManager.GameState.Playing)
        {
            return;
        }

        // 🌟 마우스 시점 조작 🌟
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * (invertY ? -1f : 1f);

        // 1. 좌우 회전 (플레이어 본체)
        transform.Rotate(Vector3.up * mouseX);

        // 2. 상하 시점 (카메라)
        if (playerCamera != null)
        {
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, minimumXRotation, maximumXRotation);
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        // --- 상호작용 및 크로스헤어 시각화 처리 ---
        if (playerCamera != null && GameManager.Instance != null && GameManager.Instance.CurrentGameState == GameManager.GameState.Playing)
        {
            // Raycast를 생성하여 시선이 닿는 곳을 확인합니다.
            Ray ray = new Ray(playerCamera.position, playerCamera.forward);
            RaycastHit hit;
            bool isTargetingClickable = false;

            // 1. Raycast 실행 (LayerMask를 사용하여 효율적으로 검사)
            if (Physics.Raycast(ray, out hit, raycastHitDistance, clickableLayerMask))
            {
                // 2. ClickableObject 컴포넌트가 있고 Collider가 활성화되어 있는지 확인
                if (hit.collider.gameObject.TryGetComponent<ClickableObject>(out var clickableObject) && hit.collider.enabled)
                {
                    isTargetingClickable = true;

                    // 3. 클릭 입력 처리
                    if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E)) // 마우스 왼쪽 버튼 클릭
                    {
                        // 4. ClickableObject를 통해 클릭 처리 (OnMouseDown과 같은 경로라 중복 처리되지 않습니다)
                        clickableObject.HandleClick();
                    }
                }
            }

            // 5. GameManager를 호출하여 크로스헤어 비주얼 업데이트 요청
            GameManager.Instance.SetCrosshairVisuals(isTargetingClickable);
        }


        float currentSpeed = isCrouching ? walkingSpeed * crouchSpeedMultiplier : walkingSpeed;

        // --- W/S 앞뒤 이동, A/D 옆걸음 처리 ---
        if (characterController.isGrounded)
        {
            float verticalInput = Input.GetAxis("Vertical");
            float horizontalInput = Input.GetAxis("Horizontal");
            Vector3 input = Vector3.ClampMagnitude(new Vector3(horizontalInput, 0, verticalInput), 1f);

            moveDirection = transform.TransformDirection(input) * currentSpeed;

            // 점프 처리 
            if (Input.GetButton("Jump") && !isCrouching)
            {
                moveDirection.y = jumpSpeed;
            }
        }

        // --- A/D 회전 처리 (마우스 회전을 사용한다면 이 코드는 비활성화됩니다.) ---
        /*
        float rotationInput = Input.GetAxis("Horizontal");
        transform.Rotate(0, rotationInput * rotationSpeed * Time.deltaTime, 0);
        */

        // --- 앉기(Crouch) 처리 ---
        bool crouchInput = Input.GetKey(crouchKey);

        if (crouchInput && !isCrouching)
        {
            isCrouching = true;
            targetHeight = crouchHeight;
        }
        else if (!crouchInput && isCrouching)
        {
            isCrouching = false;
            targetHeight = normalHeight;
            // TODO: 천장에 막혀있는지 확인하여 일어서지 못하도록 하는 로직 추가 필요
        }

        characterController.height = Mathf.Lerp(characterController.height, targetHeight, Time.deltaTime / crouchTransitionTime);
        characterController.center = Vector3.Lerp(characterController.center, new Vector3(0, targetHeight / 2, 0), Time.deltaTime / crouchTransitionTime);

        // --- 중력 적용 및 이동 실행 ---
        moveDirection.y -= gravity * Time.deltaTime;
        characterController.Move(moveDirection * Time.deltaTime);
    }
}