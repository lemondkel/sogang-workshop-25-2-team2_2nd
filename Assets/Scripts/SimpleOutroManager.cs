using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleOutroManager : MonoBehaviour
{
    [Header("얼마나 보여줄지 (초)")]
    public float duration = 5f;  // 아웃트로 이미지 + BGM 유지 시간

    [Header("끝나고 돌아갈 씬 이름")]
    public string introSceneName = "IntroScene";  // 인트로 씬 이름 그대로 적기

    private float timer = 0f;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        timer += Time.deltaTime;

        // 클릭하면 바로 넘어갈 수 있게 (1초 후부터)
        bool skip = timer > 1f && (Input.GetMouseButtonDown(0) || Input.anyKeyDown);

        if (timer >= duration || skip)
        {
            if (!string.IsNullOrEmpty(introSceneName))
            {
                SceneManager.LoadScene(introSceneName);
                enabled = false;
            }
        }
    }
}

