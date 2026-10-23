using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MainScene의 게임 화면 UI를 코드로 만듭니다.
/// - 미션 안내 화면 (찾을 고양이 사진)
/// - HUD (레벨, 타이머, 찾은 고양이 수, 단서 수, 힌트)
/// - 알림 토스트, 힌트 방향 화살표
/// - 일시정지 / 설정 / 결과 / 시간 초과 화면
/// </summary>
public class GameHUD : MonoBehaviour
{
    private GameManager gm;
    private Canvas canvas;
    private RectTransform overlayRoot;

    private Text timerText;
    private Text catsText;
    private Text cluesText;
    private Text hintText;
    private Text toastText;
    private CanvasGroup toastGroup;
    private RectTransform hintArrow;
    private Text hintDistance;
    private Transform hintTarget;
    private float hintUntil;
    private Coroutine toastRoutine;

    public void Build(GameManager manager)
    {
        gm = manager;
        canvas = UIKit.CreateCanvas("GameHUD", 50);
        canvas.transform.SetParent(transform, false);

        BuildTopBar();
        BuildToast();
        BuildHintArrow();

        overlayRoot = UIKit.Stretch(UIKit.Rect(canvas.transform, "Overlays"));
    }

    // ===== HUD =====

    void BuildTopBar()
    {
        CatLevel level = gm.Level;

        // 왼쪽 위: 찾을 고양이 사진 + 레벨 이름
        Image left = UIKit.Card(canvas.transform, "LevelInfo");
        UIKit.Fixed(left.rectTransform, new Vector2(0, 1), new Vector2(30, -30), new Vector2(520, 150));
        RawImage photo = UIKit.Photo(left.transform, level.photo);
        UIKit.Fixed((RectTransform)photo.transform.parent, new Vector2(0, 0.5f), new Vector2(15, 0), new Vector2(120, 120));
        Text levelName = UIKit.Label(left.transform, $"LEVEL {gm.LevelIndex + 1}\n<size=40>{level.title}</size>", 28, UIKit.Brown, TextAnchor.MiddleLeft);
        UIKit.Place(levelName.rectTransform, Vector2.zero, Vector2.one, new Vector2(150, 10), new Vector2(-15, -10));

        // 가운데 위: 타이머
        Image timerBox = UIKit.Card(canvas.transform, "Timer");
        UIKit.Fixed(timerBox.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(260, 100));
        timerText = UIKit.Label(timerBox.transform, "00:00", 56, UIKit.Brown);
        UIKit.Stretch(timerText.rectTransform);

        // 오른쪽 위: 고양이/단서 카운트
        Image right = UIKit.Card(canvas.transform, "Counters");
        UIKit.Fixed(right.rectTransform, new Vector2(1, 1), new Vector2(-30, -30), new Vector2(380, 150));
        catsText = UIKit.Label(right.transform, "", 40, UIKit.Brown, TextAnchor.MiddleLeft);
        UIKit.Place(catsText.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(25, 0), new Vector2(-15, -8));
        cluesText = UIKit.Label(right.transform, "", 32, UIKit.BrownLight, TextAnchor.MiddleLeft);
        UIKit.Place(cluesText.rectTransform, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(25, 8), new Vector2(-15, 0));

        // 아래: 조작 안내 + 힌트
        Image bottom = UIKit.Panel(canvas.transform, "Controls", new Color(0, 0, 0, 0.35f));
        UIKit.Fixed(bottom.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(1100, 60));
        hintText = UIKit.Label(bottom.transform, "", 28, Color.white);
        UIKit.Stretch(hintText.rectTransform);
    }

    void BuildToast()
    {
        Image toast = UIKit.Card(canvas.transform, "Toast");
        UIKit.Fixed(toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 220), new Vector2(900, 110));
        toastText = UIKit.Label(toast.transform, "", 44, UIKit.Brown);
        UIKit.Stretch(toastText.rectTransform);
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
    }

    void BuildHintArrow()
    {
        RectTransform root = UIKit.Fixed(UIKit.Rect(canvas.transform, "HintArrow"), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(240, 240));
        hintArrow = UIKit.Fixed(UIKit.Rect(root, "Arrow"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 240));
        Text arrow = UIKit.Label(hintArrow, "▲", 110, UIKit.Orange);
        UIKit.Place(arrow.rectTransform, new Vector2(0, 0.55f), new Vector2(1, 1), Vector2.zero, Vector2.zero);
        Outline o = arrow.gameObject.AddComponent<Outline>();
        o.effectColor = Color.white;
        o.effectDistance = new Vector2(3, -3);
        hintDistance = UIKit.Label(root, "", 34, Color.white);
        UIKit.Fixed(hintDistance.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -60), new Vector2(400, 60));
        hintDistance.gameObject.AddComponent<Outline>().effectColor = UIKit.Brown;
        root.gameObject.SetActive(false);
    }

    void Update()
    {
        if (gm == null) return;

        float left = gm.TimeLeft;
        if (left >= 0f)
        {
            timerText.text = GameSession.FormatTime(Mathf.Ceil(left));
            timerText.color = left <= 20f && Mathf.FloorToInt(Time.unscaledTime * 2) % 2 == 0 ? UIKit.Pink : UIKit.Brown;
        }
        else
        {
            timerText.text = GameSession.FormatTime(gm.ElapsedTime + gm.PenaltyTime);
        }

        catsText.text = $"고양이  <color=#F08048>{gm.CatsFound}</color> / {gm.CatsTotal}";
        cluesText.text = $"단서  {gm.CluesFound} / {GameManager.ClueCount}";
        hintText.text = $"WASD 이동 · 클릭/E 찾기 · <color=#FFC428>H 힌트 ({gm.HintsLeft})</color> · ESC 일시정지";

        UpdateHintArrow();
    }

    void UpdateHintArrow()
    {
        GameObject root = hintArrow.parent.gameObject;
        bool show = hintTarget != null && Time.unscaledTime < hintUntil && gm.CurrentGameState == GameManager.GameState.Playing;
        if (root.activeSelf != show) root.SetActive(show);
        if (!show) return;

        Transform viewer = gm.PlayerTransform;
        if (viewer == null) return;
        Vector3 to = hintTarget.position - viewer.position;
        Vector3 flat = new Vector3(to.x, 0, to.z);
        Vector3 forward = new Vector3(viewer.forward.x, 0, viewer.forward.z);
        float angle = Vector3.SignedAngle(forward, flat, Vector3.up);
        hintArrow.localRotation = Quaternion.Euler(0, 0, -angle);

        string height = to.y > 0.8f ? " (위쪽)" : (to.y < -1.2f ? " (아래쪽)" : "");
        hintDistance.text = $"약 {flat.magnitude:0}m{height}";
    }

    public void ShowHintArrow(Transform target)
    {
        hintTarget = target;
        hintUntil = Time.unscaledTime + 4f;
        Toast("야옹~ 소리가 나는 쪽을 찾아보세요!", UIKit.Orange);
    }

    public void Toast(string message, Color color)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(message, color));
    }

    IEnumerator ToastRoutine(string message, Color color)
    {
        toastText.text = message;
        toastText.color = color;
        float t = 0f;
        while (t < 0.15f) { t += Time.unscaledDeltaTime; toastGroup.alpha = t / 0.15f; yield return null; }
        toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(1.8f);
        t = 0f;
        while (t < 0.4f) { t += Time.unscaledDeltaTime; toastGroup.alpha = 1f - t / 0.4f; yield return null; }
        toastGroup.alpha = 0f;
    }

    // ===== 오버레이 화면 =====

    public void HideOverlays()
    {
        for (int i = overlayRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(overlayRoot.GetChild(i).gameObject);
        }
    }

    Image NewScreen(string name, Vector2 cardSize, out Image card)
    {
        HideOverlays();
        Image dim = UIKit.Panel(overlayRoot, name, UIKit.Dim);
        UIKit.Stretch(dim.rectTransform);
        card = UIKit.Card(dim.transform, "Card");
        UIKit.Fixed(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, cardSize);
        return dim;
    }

    /// <summary>
    /// 미션 안내: 이번 레벨에서 찾을 고양이 사진과 규칙을 보여줍니다.
    /// </summary>
    public void ShowBriefing()
    {
        CatLevel level = gm.Level;
        Image card;
        NewScreen("Briefing", new Vector2(1300, 760), out card);

        RawImage photo = UIKit.Photo(card.transform, level.photo);
        UIKit.Fixed((RectTransform)photo.transform.parent, new Vector2(0, 0.5f), new Vector2(50, 30), new Vector2(520, 560));

        RectTransform right = UIKit.Place(UIKit.Rect(card.transform, "Info"), new Vector2(0, 0), new Vector2(1, 1), new Vector2(610, 170), new Vector2(-50, -50));
        Text header = UIKit.Label(right, $"<size=34><color=#F08048>LEVEL {gm.LevelIndex + 1}</color></size>\n{level.title}", 60, UIKit.Brown, TextAnchor.UpperLeft);
        UIKit.Place(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -170), Vector2.zero);

        string names = "";
        foreach (CatTarget t in level.targets) names += (names.Length > 0 ? ", " : "") + t.catName;

        string rules = $"<color=#F08048>찾을 고양이</color>  {names}  ({level.targets.Length}마리)\n";
        rules += $"<color=#F08048>제한 시간</color>  {(level.timeLimit > 0 ? GameSession.FormatTime(level.timeLimit) : "없음")}\n";
        rules += $"<color=#F08048>힌트</color>  {level.hintCount}번 (H 키)\n";
        if (level.decoyCount > 0)
            rules += $"<color=#F472B6>주의!</color>  사진과 다른 {LevelDatabase.Load().decoyName}가 {level.decoyCount}마리 섞여 있어요\n";
        rules += $"\n<size=30>{level.description}</size>";

        Text body = UIKit.Label(right, rules, 36, UIKit.Brown, TextAnchor.UpperLeft, false);
        UIKit.Place(body.rectTransform, Vector2.zero, new Vector2(1, 1), Vector2.zero, new Vector2(0, -190));
        body.lineSpacing = 1.15f;

        Button back = UIKit.Button(card.transform, "레벨 선택", () => gm.GoToLevelSelect(), UIKit.Gray, 38);
        UIKit.Fixed((RectTransform)back.transform, new Vector2(1, 0), new Vector2(-440, 50), new Vector2(280, 100));
        Button start = UIKit.Button(card.transform, "찾으러 가기!", () =>
        {
            HideOverlays();
            gm.BeginPlay();
        }, UIKit.Orange, 44);
        UIKit.Fixed((RectTransform)start.transform, new Vector2(1, 0), new Vector2(-50, 50), new Vector2(360, 100));
    }

    public void ShowPause()
    {
        Image card;
        NewScreen("Pause", new Vector2(700, 760), out card);

        Text title = UIKit.Label(card.transform, "일시정지", 64, UIKit.Brown);
        UIKit.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140), new Vector2(0, -30));

        RectTransform list = UIKit.Place(UIKit.Rect(card.transform, "Buttons"), Vector2.zero, Vector2.one, new Vector2(90, 60), new Vector2(-90, -160));
        UIKit.VerticalLayout(list.gameObject, 22, 0);

        UIKit.Height(UIKit.Button(list, "계속하기", () => gm.Resume(), UIKit.Orange, 42), 96);
        UIKit.Height(UIKit.Button(list, "다시 시작", () => gm.RestartLevel(), UIKit.BrownLight, 42), 96);
        UIKit.Height(UIKit.Button(list, "설정", () =>
        {
            SettingsPanel.Build(overlayRoot, null, () =>
            {
                FirstPersonController p = FindObjectOfType<FirstPersonController>();
                if (p != null) p.ApplySettings();
            });
        }, UIKit.BrownLight, 42), 96);
        UIKit.Height(UIKit.Button(list, "레벨 선택으로", () => gm.GoToLevelSelect(), UIKit.Gray, 42), 96);
    }

    public void ShowResult(LevelResult r)
    {
        Image card;
        Image dim = NewScreen(r.cleared ? "Result" : "GameOver", new Vector2(1000, 820), out card);

        string titleText = r.cleared ? "찾았다!" : "시간 초과...";
        Text title = UIKit.Label(card.transform, titleText, 80, r.cleared ? UIKit.Orange : UIKit.BrownLight);
        UIKit.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(0, -30));

        if (r.cleared)
        {
            Text stars = UIKit.Label(card.transform, UIKit.Stars(r.stars), 110, UIKit.Star);
            UIKit.Place(stars.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -290), new Vector2(0, -150));
        }
        else
        {
            Text sub = UIKit.Label(card.transform, "고양이가 아직 숨어 있어요. 다시 찾아볼까요?", 38, UIKit.Brown);
            UIKit.Place(sub.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -260), new Vector2(0, -160));
        }

        float best = GameSession.GetBestTime(r.levelIndex);
        string stats = "";
        stats += $"걸린 시간   <color=#F08048>{GameSession.FormatTime(r.time)}</color>{(r.newBest ? "  <color=#F472B6>최고 기록!</color>" : "")}\n";
        stats += $"최고 기록   {GameSession.FormatTime(best)}\n";
        stats += $"찾은 고양이   {gm.CatsFound} / {gm.CatsTotal}\n";
        stats += $"찾은 단서   {r.cluesFound} / {GameManager.ClueCount}\n";
        stats += $"힌트 사용   {r.hintsUsed}번" + (r.wrongClicks > 0 ? $"   ·   잘못 누름 {r.wrongClicks}번" : "") + "\n";
        stats += $"점수   <color=#F08048>{r.score}</color>";

        Text body = UIKit.Label(card.transform, stats, 38, UIKit.Brown, TextAnchor.UpperCenter, false);
        UIKit.Place(body.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(60, 180), new Vector2(-60, -310));
        body.lineSpacing = 1.2f;

        RectTransform buttons = UIKit.Place(UIKit.Rect(card.transform, "Buttons"), new Vector2(0, 0), new Vector2(1, 0), new Vector2(50, 50), new Vector2(-50, 150));
        HorizontalLayoutGroup h = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 24;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        UIKit.Button(buttons, "레벨 선택", () => gm.GoToLevelSelect(), UIKit.Gray, 38);
        UIKit.Button(buttons, "다시 하기", () => gm.RestartLevel(), UIKit.BrownLight, 38);
        if (r.cleared)
        {
            if (gm.IsLastLevel) UIKit.Button(buttons, "엔딩 보기", () => gm.GoToEnding(), UIKit.Orange, 38);
            else UIKit.Button(buttons, "다음 레벨", () => gm.PlayNextLevel(), UIKit.Orange, 38);
        }

        StartCoroutine(PopIn(card.rectTransform));
    }

    IEnumerator PopIn(RectTransform rt)
    {
        float t = 0f;
        while (t < 0.25f && rt != null)
        {
            t += Time.unscaledDeltaTime;
            float k = t / 0.25f;
            rt.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, 1f - (1f - k) * (1f - k));
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;
    }
}
