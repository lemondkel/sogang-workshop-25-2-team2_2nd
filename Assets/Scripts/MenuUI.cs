using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// IntroScene의 추가 화면들을 코드로 만듭니다.
/// - 타이틀 하단 메뉴 (고양이 앨범 / 게임 방법 / 설정)
/// - 레벨 선택 (고양이 사진 카드, 별, 최고 기록, 잠금)
/// - 고양이 앨범 (찾은 고양이 사진과 단서 모음)
/// - 게임 방법
/// </summary>
public class MenuUI : MonoBehaviour
{
    private Canvas canvas;
    private RectTransform titleBar;
    private RectTransform screenRoot;
    private Action onReplayStory;
    private string gameSceneName;

    public void Build(string gameScene, Action replayStory)
    {
        gameSceneName = gameScene;
        onReplayStory = replayStory;

        canvas = UIKit.CreateCanvas("MenuUI", 60);
        canvas.transform.SetParent(transform, false);

        BuildTitleBar();
        screenRoot = UIKit.Stretch(UIKit.Rect(canvas.transform, "Screens"));
    }

    // 타이틀 화면에서만 보이는 메뉴.
    // 씬의 시작/나가기 버튼이 화면 가운데 아래(y -300, -450)에 있으므로 겹치지 않게 오른쪽 아래 구석에 세로로 둡니다.
    void BuildTitleBar()
    {
        titleBar = UIKit.Fixed(UIKit.Rect(canvas.transform, "TitleMenu"), new Vector2(1, 0), new Vector2(-40, 40), new Vector2(320, 276));
        VerticalLayoutGroup v = titleBar.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 18;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = true;

        // 기존 시작/끝내기 버튼처럼 흰 바탕에 갈색 글씨
        Button[] buttons =
        {
            UIKit.Button(titleBar, "고양이 앨범", ShowAlbum, Color.white, 34),
            UIKit.Button(titleBar, "게임 방법", ShowHowToPlay, Color.white, 34),
            UIKit.Button(titleBar, "설정", () => SettingsPanel.Build(screenRoot, null, null), Color.white, 34),
        };
        foreach (Button b in buttons) UIKit.ButtonLabel(b).color = UIKit.Brown;
    }

    public void SetTitleMenuVisible(bool visible)
    {
        titleBar.gameObject.SetActive(visible);
    }

    public void ShowSettings()
    {
        SettingsPanel.Build(screenRoot, null, null);
    }

    // 열려 있는 화면을 모두 닫고 타이틀로 돌아갑니다.
    public void CloseScreens()
    {
        Clear();
        SetTitleMenuVisible(true);
    }

    void Clear()
    {
        for (int i = screenRoot.childCount - 1; i >= 0; i--) Destroy(screenRoot.GetChild(i).gameObject);
    }

    Image FullScreen(string name, string title, Action onBack)
    {
        Clear();
        Image bg = UIKit.Panel(screenRoot, name, UIKit.CreamDark);
        UIKit.Stretch(bg.rectTransform);

        Text header = UIKit.Label(bg.transform, title, 76, UIKit.Brown);
        UIKit.Place(header.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(0, -30));
        Outline o = header.gameObject.AddComponent<Outline>();
        o.effectColor = Color.white;
        o.effectDistance = new Vector2(3, -3);

        Button back = UIKit.Button(bg.transform, "◀ 뒤로", () =>
        {
            Destroy(bg.gameObject);
            if (onBack != null) onBack();
        }, UIKit.Gray, 36);
        UIKit.Fixed((RectTransform)back.transform, new Vector2(0, 1), new Vector2(40, -40), new Vector2(220, 90));
        return bg;
    }

    // ===== 레벨 선택 =====

    public void ShowLevelSelect()
    {
        SetTitleMenuVisible(false);
        LevelDatabase db = LevelDatabase.Load();
        Image bg = FullScreen("LevelSelect", "어떤 고양이를 찾을까?", () => SetTitleMenuVisible(true));

        // 카드는 기본 330x560이지만 좁은 화면(4:3)이나 낮은 화면(울트라와이드)에서는 min 크기까지 함께 줄어듭니다.
        RectTransform grid = UIKit.Place(UIKit.Rect(bg.transform, "Grid"), Vector2.zero, Vector2.one, new Vector2(80, 150), new Vector2(-80, -180));
        HorizontalLayoutGroup g = grid.gameObject.AddComponent<HorizontalLayoutGroup>();
        g.spacing = 30;
        g.childAlignment = TextAnchor.MiddleCenter;
        g.childControlWidth = true;
        g.childControlHeight = true;
        g.childForceExpandWidth = false;
        g.childForceExpandHeight = false;

        for (int i = 0; i < db.LevelCount; i++)
        {
            BuildLevelCard(grid, i, db.GetLevel(i));
        }

        // 하단: 진행도 + 스토리 다시 보기
        int totalStars = 0;
        for (int i = 0; i < db.LevelCount; i++) totalStars += GameSession.GetStars(i);
        Text progress = UIKit.Label(bg.transform, $"모은 별  <color=#FFC428>★</color> {totalStars} / {db.LevelCount * 3}", 40, UIKit.Brown);
        UIKit.Fixed(progress.rectTransform, new Vector2(0, 0), new Vector2(80, 40), new Vector2(600, 90));
        progress.alignment = TextAnchor.MiddleLeft;

        Button story = UIKit.Button(bg.transform, "스토리 다시 보기", () =>
        {
            Clear();
            if (onReplayStory != null) onReplayStory();
        }, UIKit.BrownLight, 34);
        UIKit.Fixed((RectTransform)story.transform, new Vector2(1, 0), new Vector2(-80, 40), new Vector2(360, 90));
    }

    void BuildLevelCard(Transform parent, int index, CatLevel level)
    {
        bool unlocked = GameSession.IsUnlocked(index);

        Image card = UIKit.Card(parent, "Level" + (index + 1));
        LayoutElement size = card.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = 330;
        size.minWidth = 230;
        size.preferredHeight = 560;
        size.minHeight = 460;

        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card;
        button.interactable = unlocked;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.93f, 0.85f);
        colors.disabledColor = new Color(0.9f, 0.88f, 0.84f, 1f); // 잠긴 카드도 글자가 읽히도록 살짝만 어둡게
        button.colors = colors;
        button.onClick.AddListener(() =>
        {
            UIKit.PlayClick();
            GameSession.SelectedLevel = index;
            SceneManager.LoadScene(gameSceneName);
        });

        Text number = UIKit.Label(card.transform, $"LEVEL {index + 1}", 34, unlocked ? UIKit.Orange : UIKit.BrownLight);
        UIKit.Place(number.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(0, -15));

        // 사진: 위 75px, 아래 240px(제목/정보/기록 영역)를 뺀 나머지를 채웁니다.
        RawImage photo = UIKit.Photo(card.transform, level.photo);
        UIKit.Place((RectTransform)photo.transform.parent, Vector2.zero, Vector2.one, new Vector2(20, 240), new Vector2(-20, -75));
        if (!unlocked)
        {
            photo.color = new Color(0.25f, 0.22f, 0.2f);
            Text lockText = UIKit.Label(photo.transform.parent, "잠김", 48, Color.white);
            UIKit.Stretch(lockText.rectTransform);
            lockText.gameObject.AddComponent<Outline>().effectColor = UIKit.Brown;
        }

        Text title = UIKit.Label(card.transform, unlocked ? level.title : "???", 36, UIKit.Brown);
        UIKit.Place(title.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(15, 175), new Vector2(-15, 235));
        FitText(title, 26, 36);

        string info;
        if (unlocked)
        {
            info = $"고양이 {level.targets.Length}마리";
            info += level.timeLimit > 0 ? $" · {GameSession.FormatTime(level.timeLimit)}" : " · 시간 무제한";
            if (level.decoyCount > 0) info += $"\n<color=#F472B6>옆집 고양이 {level.decoyCount}마리 주의</color>";
        }
        else
        {
            info = "이전 레벨을 깨면 열려요";
        }
        Text infoText = UIKit.Label(card.transform, info, 26, UIKit.BrownLight, TextAnchor.UpperCenter, false);
        UIKit.Place(infoText.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(15, 100), new Vector2(-15, 175));
        FitText(infoText, 20, 26);

        float best = GameSession.GetBestTime(index);
        string record = UIKit.Stars(GameSession.GetStars(index)) + (best >= 0 ? $"\n<size=24>최고 {GameSession.FormatTime(best)}</size>" : "");
        Text recordText = UIKit.Label(card.transform, record, 46, UIKit.Star, TextAnchor.UpperCenter);
        UIKit.Place(recordText.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 5), new Vector2(0, 105));
    }

    // 칸보다 글이 길면 글자 크기를 줄입니다.
    static void FitText(Text text, int min, int max)
    {
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = min;
        text.resizeTextMaxSize = max;
    }

    // ===== 고양이 앨범 =====

    public void ShowAlbum()
    {
        SetTitleMenuVisible(false);
        LevelDatabase db = LevelDatabase.Load();
        Image bg = FullScreen("Album", "고양이 앨범", () => SetTitleMenuVisible(true));

        // 화면 높이가 낮아도(울트라와이드) 다 들어가도록 세로는 비율(앵커)로 나눕니다.
        // 위: 레벨을 깨면 공개되는 고양이 사진
        Text catsHeader = UIKit.Label(bg.transform, "찾은 고양이", 40, UIKit.Orange, TextAnchor.MiddleLeft);
        UIKit.Place(catsHeader.rectTransform, new Vector2(0, 0.79f), new Vector2(1, 0.85f), new Vector2(100, 8), new Vector2(-100, 0));

        RectTransform catRow = UIKit.Place(UIKit.Rect(bg.transform, "Cats"), new Vector2(0, 0.5f), new Vector2(1, 0.79f), new Vector2(100, 0), new Vector2(-100, -4));
        HorizontalLayoutGroup h = catRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 30;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        for (int i = 0; i < db.LevelCount; i++)
        {
            CatLevel level = db.GetLevel(i);
            bool cleared = GameSession.IsCleared(i);

            Image card = UIKit.Card(catRow, "AlbumCat" + i);
            LayoutElement size = card.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = 300;
            size.minWidth = 200;
            size.preferredHeight = 300;
            size.minHeight = 200;

            RawImage photo = UIKit.Photo(card.transform, cleared ? level.photo : null);
            UIKit.Place((RectTransform)photo.transform.parent, Vector2.zero, Vector2.one, new Vector2(15, 65), new Vector2(-15, -15));
            if (!cleared)
            {
                Text q = UIKit.Label(photo.transform.parent, "?", 100, UIKit.BrownLight);
                UIKit.Stretch(q.rectTransform);
            }

            string names = "";
            foreach (CatTarget t in level.targets) names += (names.Length > 0 ? ", " : "") + t.catName;
            Text caption = UIKit.Label(card.transform, cleared ? names : $"LEVEL {i + 1}", 30, UIKit.Brown);
            UIKit.Place(caption.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(10, 5), new Vector2(-10, 60));
            FitText(caption, 22, 30);
        }

        // 아래: 집 안에서 찾은 단서 (2칸 x 3줄, 화면 크기에 맞춰 늘어나고 줄어듭니다)
        int found = 0;
        for (int i = 0; i < db.clues.Count; i++) if (GameSession.IsClueFound(i)) found++;
        Text cluesHeader = UIKit.Label(bg.transform, $"집 안의 단서  {found} / {db.clues.Count}", 40, UIKit.Orange, TextAnchor.MiddleLeft);
        UIKit.Place(cluesHeader.rectTransform, new Vector2(0, 0.42f), new Vector2(1, 0.48f), new Vector2(100, 8), new Vector2(-100, 0));

        RectTransform clueGrid = UIKit.Place(UIKit.Rect(bg.transform, "Clues"), new Vector2(0, 0.04f), new Vector2(1, 0.42f), new Vector2(100, 0), new Vector2(-100, -4));
        VerticalLayoutGroup rows = UIKit.VerticalLayout(clueGrid.gameObject, 16, 0);
        rows.childForceExpandHeight = true;

        RectTransform row = null;
        for (int i = 0; i < db.clues.Count; i++)
        {
            if (i % 2 == 0)
            {
                row = UIKit.Rect(clueGrid, "Row" + (i / 2));
                HorizontalLayoutGroup cells = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                cells.spacing = 30;
                cells.childControlWidth = true;
                cells.childControlHeight = true;
                cells.childForceExpandWidth = true;
                cells.childForceExpandHeight = true;
            }

            bool isFound = GameSession.IsClueFound(i);
            Image clue = UIKit.Card(row, "Clue" + i);
            string text = isFound ? db.clues[i].text : "아직 찾지 못한 단서예요. 집 안을 잘 살펴보세요!";
            Text label = UIKit.Label(clue.transform, $"{i + 1}.  {text}", 30, isFound ? UIKit.Brown : UIKit.Gray, TextAnchor.MiddleLeft, isFound);
            UIKit.Place(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(30, 5), new Vector2(-30, -5));
            FitText(label, 22, 30);

            // 홀수 개일 때 마지막 줄의 빈칸
            if (i == db.clues.Count - 1 && i % 2 == 0) UIKit.Rect(row, "Spacer");
        }
    }

    // ===== 게임 방법 =====

    public void ShowHowToPlay()
    {
        SetTitleMenuVisible(false);
        Image bg = FullScreen("HowToPlay", "게임 방법", () => SetTitleMenuVisible(true));

        string left =
            "<color=#F08048>목표</color>\n" +
            "레벨마다 보여주는 사진 속 고양이를 집 안에서 모두 찾아주세요.\n" +
            "레벨 2부터는 고양이가 판마다 다른 곳에 숨어요!\n\n" +
            "<color=#F08048>조작</color>\n" +
            "WASD 또는 ↑↓ 로 걷기\n" +
            "마우스 또는 ←→ 로 둘러보기\n" +
            "V 로 1인칭/3인칭 시점 전환\n" +
            "발바닥 커서가 주황색으로 커지면 클릭(또는 E)으로 찾기\n" +
            "ESC / P 일시정지";
        string right =
            "<color=#F08048>힌트 (H)</color>\n" +
            "가장 가까운 고양이가 \"야옹~\" 울고, 방향 화살표가 잠깐 나타나요.\n" +
            "힌트를 쓰지 않으면 별을 더 받을 수 있어요.\n\n" +
            "<color=#F472B6>옆집 고양이 주의</color>\n" +
            "사진과 다른 색의 고양이를 누르면 시간이 깎여요.\n\n" +
            "<color=#F08048>단서</color>\n" +
            "집 안 곳곳의 단서를 찾으면 점수와 앨범 기록이 남아요.";

        HowToCard(bg.transform, "Left", left, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(120, 70), new Vector2(-20, -190));
        HowToCard(bg.transform, "Right", right, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(20, 70), new Vector2(-120, -190));
    }

    void HowToCard(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        Image card = UIKit.Card(parent, name);
        UIKit.Place(card.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);

        Text body = UIKit.Label(card.transform, text, 36, UIKit.Brown, TextAnchor.UpperLeft, false);
        UIKit.Place(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(50, 40), new Vector2(-50, -40));
        body.lineSpacing = 1.1f;
        // 화면 비율이 달라도 카드 밖으로 넘치지 않게 글자 크기를 자동으로 줄입니다.
        body.verticalOverflow = VerticalWrapMode.Truncate;
        body.resizeTextForBestFit = true;
        body.resizeTextMinSize = 26;
        body.resizeTextMaxSize = 36;
    }
}
