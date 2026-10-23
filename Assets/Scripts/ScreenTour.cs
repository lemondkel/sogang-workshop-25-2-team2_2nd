using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 화면 점검용 자동 순회 모드입니다.
/// 빌드한 플레이어를 `CatGame.exe -screentour <저장폴더>` 로 실행하면
/// 모든 화면을 차례로 띄우며 스크린샷을 저장한 뒤 종료합니다. 일반 실행에는 아무 영향이 없습니다.
/// </summary>
public class ScreenTour : MonoBehaviour
{
    private static string outDir;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-screentour") outDir = args[i + 1];
        }
        if (string.IsNullOrEmpty(outDir)) return;

        Directory.CreateDirectory(outDir);
        GameObject go = new GameObject("ScreenTour");
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenTour>();
    }

    IEnumerator Start()
    {
        Debug.Log("[ScreenTour] 시작: " + outDir);

        // 처음 플레이하는 상태로 맞춥니다.
        GameSession.ResetProgress();
        GameSession.StorySeen = false;
        GameSession.OpenLevelSelectOnIntro = false;

        yield return Load("IntroScene");
        IntroManager intro = FindObjectOfType<IntroManager>();
        MenuUI menu = FindObjectOfType<MenuUI>();

        yield return Shot("01_title");

        menu.ShowHowToPlay();
        yield return Shot("02_howtoplay");
        menu.CloseScreens();

        menu.ShowSettings();
        yield return Shot("03_settings");
        menu.CloseScreens();

        menu.ShowAlbum();
        yield return Shot("04_album_empty");
        menu.CloseScreens();

        intro.ReplayStory();
        yield return Shot("05_story1");
        intro.OnClickNextIntroPanel();
        yield return Shot("06_story2");
        intro.OnClickNextIntroPanel();
        yield return Shot("07_story3");
        intro.OnClickNextIntroPanel(); // 마지막 → 레벨 선택
        yield return Shot("08_levelselect_new");

        // 진행도가 있는 상태
        GameSession.SaveClear(0, 47f, 3);
        GameSession.SaveClear(1, 150f, 2);
        GameSession.MarkClueFound(0);
        GameSession.MarkClueFound(2);
        menu.CloseScreens();
        menu.ShowLevelSelect();
        yield return Shot("09_levelselect_progress");
        menu.CloseScreens();
        menu.ShowAlbum();
        yield return Shot("10_album_progress");

        // 레벨 1 플레이 화면들
        GameSession.SelectedLevel = 0;
        yield return Load("MainScene");
        GameManager gm = GameManager.Instance;
        yield return Shot("11_briefing_level1");

        gm.BeginPlay();
        yield return Wait(0.6f);
        yield return Shot("12_hud");

        gm.UseHint();
        yield return Wait(0.4f);
        yield return Shot("13_hint");

        gm.Pause();
        yield return Shot("14_pause");
        gm.Resume();

        gm.DebugEndLevel(true);
        yield return Wait(0.5f);
        yield return Shot("15_result_clear");

        // 레벨 4 (옆집 고양이) 안내 + 시간 초과 화면
        GameSession.SelectedLevel = 3;
        yield return Load("MainScene");
        gm = GameManager.Instance;
        yield return Shot("16_briefing_level4");

        gm.BeginPlay();
        yield return Wait(0.3f);
        gm.DebugEndLevel(false);
        yield return Wait(0.5f);
        yield return Shot("17_gameover");

        yield return Load("OutroScene");
        yield return Wait(0.5f);
        yield return Shot("18_outro");

        Debug.Log("[ScreenTour] 완료");
        Application.Quit();
    }

    IEnumerator Load(string scene)
    {
        SceneManager.LoadScene(scene);
        yield return null;
        yield return null;
        yield return Wait(0.8f);
    }

    IEnumerator Wait(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
    }

    IEnumerator Shot(string name)
    {
        yield return Wait(0.3f);
        yield return new WaitForEndOfFrame();
        string path = Path.Combine(outDir, name + ".png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log("[ScreenTour] " + path);
        yield return Wait(0.5f);
    }
}
