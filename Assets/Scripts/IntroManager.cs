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

        // 씬에 있던 그림 버튼(Start btn.png / Quit btn.png)은 쓰지 않고, MenuUI가 다른 메뉴와 같은 모양의 버튼을 만듭니다.
        foreach (string name in new[] { "StartButton", "QuitButton" })
        {
            Transform t = titlePanel.transform.Find(name);
            if (t != null) t.gameObject.SetActive(false);
        }

        menu = gameObject.AddComponent<MenuUI>();
        menu.Build(gameSceneName, ReplayStory, OnClickStart, OnClickQuit);

        // 게임에서 '레벨 선택'으로 돌아온 경우 바로 레벨 선택 화면을 엽니다.
        if (GameSession.OpenLevelSelectOnIntro)
        {
            GameSession.OpenLevelSelectOnIntro = false;
            menu.ShowLevelSelect();
        }
    }

    void Update()
    {
        bool escape = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);
        bool storyShowing = currentIndex >= 0 && !titlePanel.activeSelf;

        if (storyShowing)
        {
            // 스토리는 클릭 외에 ESC·백스페이스·스페이스·엔터로도 넘어갑니다.
            if (escape || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnClickNextIntroPanel();
            }
        }
        else if (escape && menu != null && menu.IsScreenOpen)
        {
            // 앨범/게임 방법/설정/레벨 선택 화면은 ESC·백스페이스로 닫고 타이틀로 돌아갑니다.
            UIKit.PlayClick();
            menu.CloseScreens();
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

    public void ReplayStory()
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
            currentIndex = -1;
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

