using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections; // 코루틴(IEnumerator) 사용을 위해 추가
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    // 싱글턴 인스턴스 (MainScene마다 새로 만들어집니다)
    public static GameManager Instance { get; private set; }

    // 게임 상태
    public enum GameState
    {
        Starting, // 미션 안내 화면 (시작 버튼 대기)
        Playing, // 플레이 중
        Paused,// 일시정지
        GameOver // 게임 종료 (클리어 또는 시간 초과)
    }
    public GameState CurrentGameState { get; private set; }

    private int score = 0;
    public int Score { get { return score; } set { score = value; Debug.Log("[GameManager] 점수 업데이트: " + score); } }

    private int remainingObjectCount = 0; // 남은 고양이 수

    [Header("Object Spawner")]
    [Tooltip("LevelDatabase에 고양이 프리팹이 없을 때 사용합니다.")]
    public GameObject prefabToSpawn;
    [Tooltip("레벨에 위치가 지정되지 않았을 때 첫 번째 고양이를 우선 배치할 위치입니다. 비우면 자동 탐색합니다.")]
    public Vector3[] spawnPositionsFromInspector;

    [Header("Target Visuals (Crosshair/Focus Object)")]
    [Tooltip("게임 상태에 따라 비주얼이 변경될 대상 GameObject (SpriteRenderer 또는 Image 컴포넌트가 필요)")]
    public GameObject targetVisualObject;
    [Tooltip("Playing 상태가 아닐 때(기본값) 사용할 Sprite입니다.")]
    public Sprite defaultSprite;
    [Tooltip("클릭 가능한 오브젝트에 시선을 맞췄을 때 사용할 강조 Sprite입니다.")]
    public Sprite highlightedSprite;

    [Tooltip("퀘스트1 패널")]
    public GameObject quest1Object;

    [Tooltip("퀘스트2 패널")]
    public GameObject quest2Object;

    [Tooltip("퀘스트3 패널")]
    public GameObject quest3Object;

    [Tooltip("퀘스트4 패널")]
    public GameObject quest4Object;

    [Tooltip("퀘스트5 패널")]
    public GameObject quest5Object;

    // ===== 레벨 진행 상태 =====
    public int LevelIndex { get; private set; }
    public CatLevel Level { get; private set; }
    public float ElapsedTime { get; private set; }
    public float PenaltyTime { get; private set; }
    public int HintsLeft { get; private set; }
    public int HintsUsed { get; private set; }
    public int WrongClicks { get; private set; }
    public int CatsTotal { get; private set; }
    public int CatsFound { get { return CatsTotal - remainingObjectCount; } }
    public int CluesFound { get { return foundClues.Count; } }
    public const int ClueCount = 5;

    // 제한 시간이 있으면 남은 시간, 없으면 -1
    public float TimeLeft
    {
        get { return Level != null && Level.timeLimit > 0f ? Mathf.Max(0f, Level.timeLimit - ElapsedTime - PenaltyTime) : -1f; }
    }

    private readonly List<HiddenCat> cats = new List<HiddenCat>();
    private readonly HashSet<int> foundClues = new HashSet<int>();
    private GameHUD hud;
    public GameHUD Hud { get { return hud; } }
    private bool usingPawCursor;
    private FirstPersonController player;
    private bool pointerWasLocked;

    void Awake()
    {
        // 이전 버전은 DontDestroyOnLoad로 남아 있어서, 다시 플레이할 때 이전 씬의 패널을 참조하는 문제가 있었습니다.
        // 이제는 씬마다 새 GameManager를 사용합니다.
        Instance = this;
        CurrentGameState = GameState.Starting;
        Time.timeScale = 1f;
        GameSession.ApplySettings();
    }

    void Start()
    {
        LevelDatabase db = LevelDatabase.Load();
        LevelIndex = Mathf.Clamp(GameSession.SelectedLevel, 0, Mathf.Max(0, db.LevelCount - 1));
        Level = db.GetLevel(LevelIndex);
        HintsLeft = Level.hintCount;

        player = FindObjectOfType<FirstPersonController>();
        if (player != null) player.ApplySettings();

        SpawnLevelCats(db);

        hud = gameObject.AddComponent<GameHUD>();
        hud.Build(this);

        SetupPawCursor();

        // 미션 안내 화면에서 '시작!'을 누르면 플레이 시작 (WebGL에서 마우스 잠금에 클릭이 필요하기도 합니다)
        SetCursorFree(true);
        hud.ShowBriefing();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (CurrentGameState == GameState.Playing)
        {
            ElapsedTime += Time.deltaTime;

            if (Level.timeLimit > 0f && TimeLeft <= 0f)
            {
                EndLevel(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.H)) UseHint();
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) Pause();

            // WebGL에서는 ESC를 누르면 브라우저가 마우스 잠금을 먼저 풀어버리므로, 잠금이 풀린 것을 보고 일시정지합니다.
            if (UIKit.IsWebGL)
            {
                if (Cursor.lockState == CursorLockMode.Locked) pointerWasLocked = true;
                else if (pointerWasLocked) Pause();
            }
        }
        else if (CurrentGameState == GameState.Paused)
        {
            if (Input.GetKeyDown(KeyCode.P)) Resume();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        // 브라우저 탭을 벗어나면 자동 일시정지
        if (!hasFocus && CurrentGameState == GameState.Playing) Pause();
    }

    // ===== 레벨 준비 =====

    void SpawnLevelCats(LevelDatabase db)
    {
        GameObject prefab = db.catPrefab != null ? db.catPrefab : prefabToSpawn;
        if (prefab == null)
        {
            Debug.LogError("생성할 고양이 프리팹이 LevelDatabase 또는 GameManager Inspector에 할당되지 않았습니다!");
            return;
        }

        Vector3 feet = player != null ? player.transform.position : Vector3.zero;
        Vector3 eye = Camera.main != null ? Camera.main.transform.position : feet + Vector3.up * 1.6f;

        int targetCount = Level.targets.Length;
        int total = targetCount + Mathf.Max(0, Level.decoyCount);

        List<Vector3> preferred = new List<Vector3>();
        if (Level.fixedSpawnPositions != null) preferred.AddRange(Level.fixedSpawnPositions);
        if (preferred.Count == 0 && LevelIndex == 0 && spawnPositionsFromInspector != null) preferred.AddRange(spawnPositionsFromInspector);

        // 자동 탐색 중 플레이어 콜라이더가 방해하지 않도록 잠시 끕니다.
        CharacterController cc = player != null ? player.GetComponent<CharacterController>() : null;
        if (cc != null) cc.enabled = false;
        List<CatSpawner.Spot> spots = CatSpawner.FindSpots(feet, eye, total, Level.hiddenRatio, preferred);
        if (cc != null) cc.enabled = true;

        // FindSpots는 지정 위치 → 숨겨진 자리 순으로 돌려주므로, 앞쪽은 진짜 고양이, 뒤쪽은 옆집 고양이가 차지합니다.
        for (int i = 0; i < total && i < spots.Count; i++)
        {
            bool decoy = i >= targetCount;
            Quaternion rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            GameObject go = Instantiate(prefab, spots[i].position, rotation);

            if (go.GetComponent<ClickableObject>() == null) go.AddComponent<ClickableObject>();
            HiddenCat cat = go.AddComponent<HiddenCat>();
            if (decoy)
            {
                cat.Setup(db.decoyName, db.decoyTint, true, db.meowClip);
                go.name = "DecoyCat_" + i;
            }
            else
            {
                CatTarget t = Level.targets[i];
                cat.Setup(t.catName, t.tint, false, db.meowClip);
                go.name = "Cat_" + t.catName;
            }
            cats.Add(cat);
        }

        CatsTotal = Mathf.Min(targetCount, spots.Count);
        remainingObjectCount = CatsTotal;
    }

    // ===== 상태 전환 =====

    public void BeginPlay()
    {
        if (CurrentGameState != GameState.Starting) return;
        SetCursorFree(false);
        ChangeState(GameState.Playing);
    }

    public void Pause()
    {
        if (CurrentGameState != GameState.Playing) return;
        ChangeState(GameState.Paused);
        SetCursorFree(true);
        if (hud != null) hud.ShowPause();
    }

    public void Resume()
    {
        if (CurrentGameState != GameState.Paused) return;
        if (hud != null) hud.HideOverlays();
        SetCursorFree(false);
        ChangeState(GameState.Playing);
    }

    void SetCursorFree(bool free)
    {
        pointerWasLocked = false;
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    // ===== 클릭 처리 =====

    public void OnObjectClicked(GameObject clickedObject)
    {
        if (CurrentGameState != GameState.Playing) return; // 플레이 중이 아니면 무시

        HiddenCat cat = clickedObject.GetComponent<HiddenCat>();
        if (cat == null)
        {
            // 레벨 고양이가 아닌 일반 클릭 오브젝트
            Debug.Log($"[GameManager] 오브젝트 클릭됨: {clickedObject.name}");
            Score += 10;
            Destroy(clickedObject);
            return;
        }
        if (cat.found) return;

        if (cat.isDecoy)
        {
            WrongClicks++;
            PenaltyTime += LevelDatabase.Load().decoyPenaltySeconds;
            Score = Mathf.Max(0, Score - 5);
            UIKit.PlayOneShot(LevelDatabase.Load().failClip, 0.8f);
            cat.Meow();
            cat.PlayRunAway();
            cats.Remove(cat);
            string penalty = Level.timeLimit > 0f ? $" -{LevelDatabase.Load().decoyPenaltySeconds:0}초" : "";
            hud.Toast($"앗, {cat.catName}였어요!{penalty}", UIKit.Pink);
            return;
        }

        Debug.Log($"[GameManager] 고양이 찾음: {cat.catName}");
        Score += 100;
        remainingObjectCount--;
        if (remainingObjectCount < 0) remainingObjectCount = 0; // 안전장치
        cats.Remove(cat);
        cat.PlayFound();
        UIKit.PlayOneShot(LevelDatabase.Load().successClip, 0.7f);

        if (remainingObjectCount == 0)
        {
            Debug.Log("모든 고양이를 찾았습니다!");
            hud.Toast($"{cat.catName} 찾았다!", UIKit.Green);
            StartCoroutine(EndLevelAfter(1.0f, true));
        }
        else
        {
            hud.Toast($"{cat.catName} 찾았다! (남은 고양이 {remainingObjectCount})", UIKit.Green);
        }
    }

    public void OnObjectClickedQuest(GameObject clickedObject, int questIndex)
    {
        if (CurrentGameState != GameState.Playing) return; // 플레이 중이 아니면 무시

        Debug.Log($"[GameManager] 오브젝트 클릭됨: {clickedObject.name}");

        if (foundClues.Add(questIndex))
        {
            Score += 20;
            GameSession.MarkClueFound(questIndex);
        }

        GameObject targetQuestObject = null;

        // 퀘스트 인덱스에 따라 타겟 오브젝트를 설정합니다.
        if (questIndex == 0) targetQuestObject = quest1Object;
        else if (questIndex == 1) targetQuestObject = quest2Object;
        else if (questIndex == 2) targetQuestObject = quest3Object;
        else if (questIndex == 3) targetQuestObject = quest4Object;
        else if (questIndex == 4) targetQuestObject = quest5Object;

        if (targetQuestObject != null)
        {
            Debug.Log($"퀘스트 {questIndex + 1} 패널 활성화.");

            // 이미 열려 있는 패널을 다시 누른 경우 처음부터 다시 보여줍니다.
            targetQuestObject.SetActive(false);
            targetQuestObject.SetActive(true);

            // 2초 지연 및 비활성화 코루틴 시작
            StartCoroutine(DeactivateQuestPanelAfterDelay(targetQuestObject, 2.0f));
        }
    }

    /// <summary>
    /// 지정된 딜레이 후 퀘스트 패널을 부드럽게 비활성화하는 코루틴입니다.
    /// </summary>
    /// <param name="questPanel">활성/비활성화할 퀘스트 패널</param>
    /// <param name="delay">패널이 표시될 시간 (초)</param>
    IEnumerator DeactivateQuestPanelAfterDelay(GameObject questPanel, float delay)
    {
        // 1. 지정된 시간(2초) 동안 대기
        yield return new WaitForSeconds(delay);

        // 2. 부드러운 비활성화 애니메이션 시작 (QuestPanelAnimator 스크립트 호출)
        if (questPanel != null && questPanel.activeInHierarchy && questPanel.TryGetComponent<QuestPanelAnimator>(out var animator))
        {
            // 애니메이션이 끝날 때까지 기다립니다.
            yield return animator.StartCloseAnimation();
        }

        // 3. 최종적으로 GameObject 비활성화 (SetActive(false))
        if (questPanel != null)
        {
            questPanel.SetActive(false);
        }
    }

    // ===== 힌트 =====

    public void UseHint()
    {
        if (CurrentGameState != GameState.Playing) return;
        if (HintsLeft <= 0)
        {
            hud.Toast("힌트를 모두 사용했어요", UIKit.BrownLight);
            return;
        }

        HiddenCat nearest = NearestTarget();
        if (nearest == null) return;

        HintsLeft--;
        HintsUsed++;
        nearest.Meow();
        hud.ShowHintArrow(nearest.transform);
    }

    HiddenCat NearestTarget()
    {
        Vector3 from = player != null ? player.transform.position : Vector3.zero;
        HiddenCat best = null;
        float bestDistance = float.MaxValue;
        foreach (HiddenCat c in cats)
        {
            if (c == null || c.isDecoy || c.found) continue;
            float d = Vector3.Distance(from, c.transform.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = c;
            }
        }
        return best;
    }

    public Transform PlayerTransform
    {
        get { return Camera.main != null ? Camera.main.transform : (player != null ? player.transform : null); }
    }

    // ===== 레벨 종료 =====

    IEnumerator EndLevelAfter(float delay, bool cleared)
    {
        // 클리어 순간 타이머를 멈춥니다.
        ChangeState(GameState.GameOver);
        Time.timeScale = 1f; // 고양이 애니메이션은 계속
        yield return new WaitForSecondsRealtime(delay);
        EndLevel(cleared);
    }

    // 화면 점검(ScreenTour)용: 결과/시간 초과 화면을 바로 띄웁니다.
    public void DebugEndLevel(bool cleared)
    {
        if (CurrentGameState == GameState.GameOver) return;
        EndLevel(cleared);
    }

    void EndLevel(bool cleared)
    {
        ChangeState(GameState.GameOver);
        SetCursorFree(true);

        float time = ElapsedTime + PenaltyTime;
        LevelResult result = new LevelResult
        {
            levelIndex = LevelIndex,
            cleared = cleared,
            time = time,
            hintsUsed = HintsUsed,
            wrongClicks = WrongClicks,
            cluesFound = CluesFound,
        };

        if (cleared)
        {
            int stars = 3;
            if (time > Level.parTime) stars--;
            if (HintsUsed > 0 || WrongClicks > 0) stars--;
            result.stars = Mathf.Max(1, stars);
            Score += result.stars * 100 + (Level.timeLimit > 0f ? Mathf.RoundToInt(TimeLeft) * 2 : 0);
            result.newBest = GameSession.SaveClear(LevelIndex, time, result.stars);
        }
        result.score = Score;
        GameSession.LastResult = result;

        hud.ShowResult(result);
    }

    public bool IsLastLevel
    {
        get { return LevelIndex >= LevelDatabase.Load().LevelCount - 1; }
    }

    public void PlayNextLevel()
    {
        GameSession.SelectedLevel = LevelIndex + 1;
        LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToLevelSelect()
    {
        GameSession.OpenLevelSelectOnIntro = true;
        LoadScene("IntroScene");
    }

    public void GoToEnding()
    {
        LoadScene("OutroScene");
    }

    // ===== 기존 기능 =====

    // FPS 조준점 대신 고양이 발바닥 커서를 씁니다. 찾을 수 있는 고양이를 보면 커지면서 주황색으로 바뀝니다.
    void SetupPawCursor()
    {
        Sprite paw = Resources.Load<Sprite>("UI/paw");
        if (paw == null || targetVisualObject == null) return;

        Image image = targetVisualObject.GetComponent<Image>();
        if (image == null) return;

        defaultSprite = paw;
        highlightedSprite = paw;
        image.sprite = paw;
        image.preserveAspect = true;
        image.rectTransform.sizeDelta = new Vector2(46, 46);
        image.color = new Color(1f, 1f, 1f, 0.55f);
        usingPawCursor = true;
    }

    public void SetCrosshairVisuals(bool isTargetingClickable)
    {
        if (targetVisualObject == null || defaultSprite == null || highlightedSprite == null)
        {
            return;
        }

        if (usingPawCursor)
        {
            Image paw = targetVisualObject.GetComponent<Image>();
            Color wanted = isTargetingClickable ? UIKit.Orange : new Color(1f, 1f, 1f, 0.55f);
            float scale = isTargetingClickable ? 1.35f : 1f;
            float k = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            paw.color = Color.Lerp(paw.color, wanted, k);
            paw.rectTransform.localScale = Vector3.Lerp(paw.rectTransform.localScale, Vector3.one * scale, k);
            return;
        }

        Sprite newSprite = isTargetingClickable ? highlightedSprite : defaultSprite;

        Image imageComponent = targetVisualObject.GetComponent<Image>();
        SpriteRenderer spriteRenderer = targetVisualObject.GetComponent<SpriteRenderer>();

        if (imageComponent != null)
        {
            if (imageComponent.sprite != newSprite) imageComponent.sprite = newSprite;
        }
        else if (spriteRenderer != null)
        {
            if (spriteRenderer.sprite != newSprite) spriteRenderer.sprite = newSprite;
        }
    }

    public void ChangeState(GameState newState)
    {
        if (CurrentGameState == newState) return;

        CurrentGameState = newState;
        Debug.Log("[GameManager] 상태 변경: " + newState);

        switch (newState)
        {
            case GameState.Starting:
                Time.timeScale = 1f;
                break;
            case GameState.Playing:
                Time.timeScale = 1f;
                break;
            case GameState.Paused:
                Time.timeScale = 0f;
                break;
            case GameState.GameOver:
                Time.timeScale = 0f;
                Debug.Log("게임 종료! 최종 점수: " + score);
                break;
        }
    }

    public void TogglePause()
    {
        if (CurrentGameState == GameState.Playing) Pause();
        else if (CurrentGameState == GameState.Paused) Resume();
    }

    public void LoadScene(string sceneName)
    {
        Debug.Log("[GameManager] 씬 로드: " + sceneName);
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    public void LoadNextLevel()
    {
        if (IsLastLevel) GoToEnding();
        else PlayNextLevel();
    }

    public void RestartLevel()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        Debug.Log("[GameManager] 현재 씬 재시작: " + currentSceneName);
        LoadScene(currentSceneName);
    }
}
