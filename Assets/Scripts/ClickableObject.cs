using UnityEngine;
using UnityEngine.EventSystems; // 마우스 오버 감지를 위해 추가

/// <summary>
/// 클릭 가능한 오브젝트에 부착되어, 마우스 인터랙션과 클릭 시 즉시 파괴를 처리합니다.
/// </summary>
public class ClickableObject : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Collider objectCollider;
    private bool isClicked = false;

    // 이 오브젝트의 레이어를 미리 가져와 로직에 사용합니다.
    private string myLayerName;

    void Awake()
    {
        // AudioSource 관련 코드는 모두 제거되었습니다.
        objectCollider = GetComponent<Collider>();
        myLayerName = LayerMask.LayerToName(gameObject.layer);
    }

    // ------------------------------------
    // 마우스 오버 / 이탈 처리 (크로스헤어 변경)
    // ------------------------------------

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isClicked && GameManager.Instance != null && GameManager.Instance.CurrentGameState == GameManager.GameState.Playing)
        {
            // GameManager에게 강조된 크로스헤어를 표시하도록 요청합니다.
            GameManager.Instance.SetCrosshairVisuals(true);

            // 특정 레이어에 따른 추가 액션 (예시)
            if (myLayerName == "Quest1")
            {
                Debug.Log("Quest1 레이어 감지됨: 특수 시각 효과 준비.");
            }
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 클릭되지 않은 상태일 때만 기본 크로스헤어로 되돌립니다.
        if (!isClicked && GameManager.Instance != null)
        {
            GameManager.Instance.SetCrosshairVisuals(false);
        }
    }

    // ------------------------------------
    // 클릭 감지 및 로직
    // ------------------------------------

    private void OnMouseDown()
    {
        // 🚨 순환 호출 및 중복 클릭 방지 (가장 중요)
        if (isClicked || GameManager.Instance == null || GameManager.Instance.CurrentGameState != GameManager.GameState.Playing)
        {
            return;
        }

        // 클릭 플래그 설정 및 크로스헤어 리셋
        isClicked = true;
        GameManager.Instance.SetCrosshairVisuals(false);

        // 특정 레이어에 따른 클릭 액션 (예시)
        if (myLayerName == "Quest1")
        {
            GameManager.Instance.OnObjectClickedQuest(gameObject, 0);
        }
        else if (myLayerName == "Quest2")
        {
            GameManager.Instance.OnObjectClickedQuest(gameObject, 1);
        }
         else if (myLayerName == "Quest3")
        {
            GameManager.Instance.OnObjectClickedQuest(gameObject, 2);
        }
         else if (myLayerName == "Quest4")
        {
            GameManager.Instance.OnObjectClickedQuest(gameObject, 3);
        }
         else if (myLayerName == "Quest5")
        {
            GameManager.Instance.OnObjectClickedQuest(gameObject, 4);
        }
        else
        {
            GameManager.Instance.OnObjectClicked(gameObject);
        }

    }
    /// <summary>
    /// GameManager에서 호출되어 오브젝트 파괴 시퀀스를 시작합니다.
    /// 소리 재생 없이 즉시 파괴를 수행합니다.
    /// </summary>
    public void StartDestructionSequence()
    {
        // 1. 추가 클릭 방지를 위해 콜라이더 비활성화
        if (objectCollider != null)
        {
            objectCollider.enabled = false;
        }

        // 2. 오브젝트 즉시 파괴 (지연 없음)
        Destroy(gameObject);
    }
}