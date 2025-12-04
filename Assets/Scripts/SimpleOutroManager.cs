using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleOutroManager : MonoBehaviour
{
    [Header("얼마나 보여줄지 (초)")]
    public float duration = 5f;  // 아웃트로 이미지 + BGM 유지 시간

    [Header("끝나고 돌아갈 씬 이름")]
    public string introSceneName = "IntroScene";  // 인트로 씬 이름 그대로 적기

    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= duration)
        {
            if (!string.IsNullOrEmpty(introSceneName))
            {
                SceneManager.LoadScene(introSceneName);
            }
        }
    }
}

