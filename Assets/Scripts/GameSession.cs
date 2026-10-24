using UnityEngine;

/// <summary>
/// 한 판의 결과입니다. 결과 화면에서 사용합니다.
/// </summary>
public class LevelResult
{
    public int levelIndex;
    public bool cleared;
    public float time;
    public int stars;
    public int hintsUsed;
    public int wrongClicks;
    public int cluesFound;
    public int score;
    public bool newBest;
}

/// <summary>
/// 씬을 넘나드는 선택 레벨, 설정, 기록(PlayerPrefs)을 관리합니다.
/// </summary>
public static class GameSession
{
    private const string Prefix = "catgame.";

    // 레벨 선택 화면에서 고른 레벨 (0부터 시작)
    public static int SelectedLevel { get; set; }

    // IntroScene이 처음 열릴 때 바로 레벨 선택 화면을 띄울지 여부
    public static bool OpenLevelSelectOnIntro { get; set; }

    public static LevelResult LastResult { get; set; }

    // ===== 설정 =====

    public static float MasterVolume
    {
        get { return PlayerPrefs.GetFloat(Prefix + "volume", 0.8f); }
        set { PlayerPrefs.SetFloat(Prefix + "volume", Mathf.Clamp01(value)); AudioListener.volume = Mathf.Clamp01(value); PlayerPrefs.Save(); }
    }

    public static float MouseSensitivity
    {
        get { return PlayerPrefs.GetFloat(Prefix + "sensitivity", 2f); }
        set { PlayerPrefs.SetFloat(Prefix + "sensitivity", Mathf.Clamp(value, 0.2f, 6f)); PlayerPrefs.Save(); }
    }

    public static bool InvertY
    {
        get { return PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1; }
        set { PlayerPrefs.SetInt(Prefix + "invertY", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static void ApplySettings()
    {
        AudioListener.volume = MasterVolume;
    }

    // ===== 진행도 / 기록 =====

    public static bool StorySeen
    {
        get { return PlayerPrefs.GetInt(Prefix + "storySeen", 0) == 1; }
        set { PlayerPrefs.SetInt(Prefix + "storySeen", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // 플레이 가능한 가장 높은 레벨 인덱스
    public static int UnlockedLevel
    {
        get { return PlayerPrefs.GetInt(Prefix + "unlocked", 0); }
        private set { PlayerPrefs.SetInt(Prefix + "unlocked", value); }
    }

    public static bool IsUnlocked(int level)
    {
        return level <= UnlockedLevel;
    }

    public static int GetStars(int level)
    {
        return PlayerPrefs.GetInt(Prefix + "stars." + level, 0);
    }

    // 기록이 없으면 -1
    public static float GetBestTime(int level)
    {
        return PlayerPrefs.GetFloat(Prefix + "best." + level, -1f);
    }

    public static bool IsCleared(int level)
    {
        return GetStars(level) > 0;
    }

    public static bool IsClueFound(int clueIndex)
    {
        return (PlayerPrefs.GetInt(Prefix + "clues", 0) & (1 << clueIndex)) != 0;
    }

    public static void MarkClueFound(int clueIndex)
    {
        int mask = PlayerPrefs.GetInt(Prefix + "clues", 0) | (1 << clueIndex);
        PlayerPrefs.SetInt(Prefix + "clues", mask);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 클리어 기록을 저장하고 다음 레벨을 엽니다. 최고 기록이면 true를 반환합니다.
    /// </summary>
    public static bool SaveClear(int level, float time, int stars)
    {
        bool newBest = false;
        float best = GetBestTime(level);
        if (best < 0f || time < best)
        {
            PlayerPrefs.SetFloat(Prefix + "best." + level, time);
            newBest = true;
        }
        if (stars > GetStars(level))
        {
            PlayerPrefs.SetInt(Prefix + "stars." + level, stars);
        }
        if (level + 1 > UnlockedLevel)
        {
            UnlockedLevel = level + 1;
        }
        PlayerPrefs.Save();
        return newBest;
    }

    public static void ResetProgress()
    {
        float volume = MasterVolume;
        float sensitivity = MouseSensitivity;
        bool invert = InvertY;
        PlayerPrefs.DeleteAll();
        MasterVolume = volume;
        MouseSensitivity = sensitivity;
        InvertY = invert;
        PlayerPrefs.Save();
    }

    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) return "--:--";
        int total = Mathf.FloorToInt(seconds);
        return string.Format("{0:00}:{1:00}", total / 60, total % 60);
    }
}
