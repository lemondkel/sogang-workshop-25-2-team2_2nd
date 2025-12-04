using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject titlePanel;        // TitlePanel
    public GameObject[] introPanels;     // IntroPanel1, 2, 3 ...

    [Header("Scene Name")]
    public string gameSceneName = "GameScene";

    private int currentIndex = -1;   // 아직 아무 인트로 패널 안 보여주는 상태

    void Start()
    {
        // 처음에는 타이틀 화면만 보이게
        titlePanel.SetActive(true);
        HideAllIntroPanels();
    }

    // [시작하기] 버튼에서 호출
    public void OnClickStart()
    {
        titlePanel.SetActive(false);

        currentIndex = 0;
        ShowIntroPanel(currentIndex);
    }

    // 인트로 화면(패널)을 클릭했을 때 호출
    public void OnClickNextIntroPanel()
    {
        currentIndex++;

        if (currentIndex < introPanels.Length)
        {
            ShowIntroPanel(currentIndex);
        }
        else
        {
            // 마지막 패널까지 본 뒤 게임 씬으로 이동
            SceneManager.LoadScene(gameSceneName);
        }
    }

    // [나가기] 버튼에서 호출 (옵션)
    public void OnClickQuit()
    {
        Application.Quit();

        // 에디터에서 테스트할 때는 이 코드가 플레이를 멈추게 해줌
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ===== 내부용 함수들 =====

    void HideAllIntroPanels()
    {
        if (introPanels == null) return;

        for (int i = 0; i < introPanels.Length; i++)
        {
            if (introPanels[i] != null)
                introPanels[i].SetActive(false);
        }
    }

    void ShowIntroPanel(int index)
    {
        HideAllIntroPanels();

        if (introPanels != null &&
            index >= 0 &&
            index < introPanels.Length &&
            introPanels[index] != null)
        {
            introPanels[index].SetActive(true);
        }
    }
}

