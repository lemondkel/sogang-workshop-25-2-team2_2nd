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
    private MenuUI menu;              // 레벨 선택/앨범/설정 등 추가 화면

    void Start()
    {
        // 게임 화면에서 돌아왔을 때 마우스가 잠겨 있으면 버튼을 누를 수 없으므로 풀어줍니다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        GameSession.ApplySettings();

        // 처음에는 타이틀 화면만 보이게
        titlePanel.SetActive(true);
        HideAllIntroPanels();

        // 웹(WebGL)에서는 '나가기'가 동작하지 않으므로 숨깁니다.
        if (UIKit.IsWebGL)
        {
            Transform quit = titlePanel.transform.Find("QuitButton");
            if (quit != null) quit.gameObject.SetActive(false);
        }

        menu = gameObject.AddComponent<MenuUI>();
        menu.Build(gameSceneName, ReplayStory);

        // 게임에서 '레벨 선택'으로 돌아온 경우 바로 레벨 선택 화면을 엽니다.
        if (GameSession.OpenLevelSelectOnIntro)
        {
            GameSession.OpenLevelSelectOnIntro = false;
            menu.ShowLevelSelect();
        }
    }

    // [시작하기] 버튼에서 호출
    public void OnClickStart()
    {
        // 스토리를 이미 봤다면 바로 레벨 선택으로
        if (GameSession.StorySeen)
        {
            menu.ShowLevelSelect();
            return;
        }
        ReplayStory();
    }

    void ReplayStory()
    {
        titlePanel.SetActive(false);
        menu.SetTitleMenuVisible(false);

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
            // 마지막 패널까지 본 뒤 레벨 선택 화면으로 이동
            GameSession.StorySeen = true;
            HideAllIntroPanels();
            titlePanel.SetActive(true);
            menu.ShowLevelSelect();
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

