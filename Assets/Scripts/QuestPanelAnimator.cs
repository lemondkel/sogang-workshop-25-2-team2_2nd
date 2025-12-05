using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class QuestPanelAnimator : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    public float animationDuration = 0.3f;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            Debug.LogError("QuestPanelAnimator는 CanvasGroup 컴포넌트가 필요합니다!");
        }

        canvasGroup.alpha = 0f;
    }

    void OnEnable()
    {
        StartCoroutine(Fade(1f));
    }

    public IEnumerator StartCloseAnimation()
    {
        yield return StartCoroutine(Fade(0f));
    }

    IEnumerator Fade(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < animationDuration)
        {
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / animationDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
}