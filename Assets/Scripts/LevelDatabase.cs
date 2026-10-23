using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨에서 찾아야 하는 고양이 한 마리의 정보입니다.
/// </summary>
[Serializable]
public class CatTarget
{
    public string catName = "토리";
    [Tooltip("고양이 모델의 색을 바꿔 사진 속 고양이와 비슷하게 만듭니다.")]
    public Color tint = Color.white;
}

/// <summary>
/// 레벨 하나의 정보입니다. Resources/LevelDatabase 에셋의 Inspector에서 수정할 수 있습니다.
/// </summary>
[Serializable]
public class CatLevel
{
    public string title = "토리를 찾아라";
    [TextArea(2, 4)]
    public string description = "";
    [Tooltip("레벨 선택/미션 화면에 보여줄 고양이 사진")]
    public Texture2D photo;

    [Tooltip("이번 레벨에서 찾아야 하는 고양이들")]
    public CatTarget[] targets = new CatTarget[] { new CatTarget() };

    [Tooltip("사진 속 고양이가 아닌 '옆집 고양이' 수 (잘못 누르면 시간 감점)")]
    public int decoyCount = 0;

    [Tooltip("제한 시간(초). 0이면 무제한")]
    public float timeLimit = 0f;

    [Tooltip("사용 가능한 힌트 횟수 (H 키)")]
    public int hintCount = 3;

    [Tooltip("시작 위치에서 바로 보이지 않는 곳에 숨길 고양이 비율 (0~1)")]
    [Range(0f, 1f)]
    public float hiddenRatio = 0f;

    [Tooltip("별 3개를 받기 위한 목표 시간(초)")]
    public float parTime = 60f;

    [Tooltip("비워두면 집 안에서 숨을 곳을 자동으로 찾습니다. 직접 지정하면 이 위치들을 먼저 사용합니다.")]
    public Vector3[] fixedSpawnPositions = new Vector3[0];
}

/// <summary>
/// 단서(퀘스트 오브젝트) 정보입니다. 고양이 앨범 화면에 사용됩니다.
/// </summary>
[Serializable]
public class ClueInfo
{
    [TextArea(1, 3)]
    public string text = "";
    public Texture2D image;
}

/// <summary>
/// 모든 레벨/공용 리소스를 담는 데이터 에셋입니다. (Assets/Resources/LevelDatabase.asset)
/// </summary>
[CreateAssetMenu(fileName = "LevelDatabase", menuName = "CatGame/Level Database")]
public class LevelDatabase : ScriptableObject
{
    [Header("Fonts")]
    public Font boldFont;
    public Font lightFont;

    [Header("Cat")]
    public GameObject catPrefab;
    public AudioClip meowClip;

    [Header("Sounds")]
    public AudioClip clickClip;
    public AudioClip successClip;
    public AudioClip failClip;

    [Header("Levels")]
    public List<CatLevel> levels = new List<CatLevel>();

    [Header("Clues (퀘스트 1~5 순서)")]
    public List<ClueInfo> clues = new List<ClueInfo>();

    [Header("Decoy")]
    public string decoyName = "옆집 고양이";
    public Color decoyTint = new Color(0.55f, 0.62f, 0.8f);
    public float decoyPenaltySeconds = 15f;

    private static LevelDatabase cached;

    public static LevelDatabase Load()
    {
        if (cached == null)
        {
            cached = Resources.Load<LevelDatabase>("LevelDatabase");
            if (cached == null)
            {
                Debug.LogError("[LevelDatabase] Resources/LevelDatabase 에셋을 찾을 수 없습니다.");
                cached = CreateInstance<LevelDatabase>();
            }
        }
        return cached;
    }

    public int LevelCount { get { return levels.Count; } }

    public CatLevel GetLevel(int index)
    {
        if (levels.Count == 0) return new CatLevel();
        return levels[Mathf.Clamp(index, 0, levels.Count - 1)];
    }
}
