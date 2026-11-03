using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 코드로 UI(패널, 텍스트, 버튼, 슬라이더)를 만드는 도우미입니다.
/// 게임 화면들의 색/폰트를 한 곳에서 맞춥니다.
/// </summary>
public static class UIKit
{
    // ===== 테마 색상 (인트로 이미지의 크림/주황 톤) =====
    public static readonly Color Cream = new Color32(255, 248, 231, 255);
    public static readonly Color CreamDark = new Color32(246, 233, 205, 255);
    public static readonly Color Orange = new Color32(240, 128, 72, 255);
    public static readonly Color OrangeDark = new Color32(214, 98, 46, 255);
    public static readonly Color Pink = new Color32(244, 114, 182, 255);
    public static readonly Color Brown = new Color32(74, 52, 38, 255);
    public static readonly Color BrownLight = new Color32(140, 108, 86, 255);
    public static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
    public static readonly Color Gray = new Color32(190, 182, 170, 255);
    public static readonly Color Green = new Color32(98, 178, 110, 255);
    public static readonly Color Star = new Color32(255, 196, 40, 255);

    private static Font boldFont;
    private static Font lightFont;
    private static AudioSource uiAudio;

    public static Font Bold
    {
        get
        {
            if (boldFont == null)
            {
                boldFont = LevelDatabase.Load().boldFont;
                if (boldFont == null) boldFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return boldFont;
        }
    }

    public static Font Light
    {
        get
        {
            if (lightFont == null)
            {
                lightFont = LevelDatabase.Load().lightFont;
                if (lightFont == null) lightFont = Bold;
            }
            return lightFont;
        }
    }

    // ===== 기본 구성 =====

    public static Canvas CreateCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current == null && UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    public static RectTransform Rect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // 부모 기준 앵커(0~1)로 위치/크기를 지정합니다.
    public static RectTransform Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt)
    {
        return Place(rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    // 부모 안의 고정 크기 박스 (pivot = anchor)
    public static RectTransform Fixed(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Panel(Transform parent, string name, Color color)
    {
        RectTransform rt = Rect(parent, name);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    // 크림색 카드 + 주황 테두리 (인트로 대사창 스타일)
    public static Image Card(Transform parent, string name)
    {
        Image card = Panel(parent, name, Cream);
        Outline outline = card.gameObject.AddComponent<Outline>();
        outline.effectColor = Orange;
        outline.effectDistance = new Vector2(5, -5);
        Shadow shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.25f);
        shadow.effectDistance = new Vector2(0, -10);
        return card;
    }

    public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true)
    {
        RectTransform rt = Rect(parent, "Label");
        Text label = rt.gameObject.AddComponent<Text>();
        label.font = bold ? Bold : Light;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.supportRichText = true;
        return label;
    }

    public static Button Button(Transform parent, string text, Action onClick, Color color, int fontSize = 40)
    {
        Image bg = Panel(parent, "Button_" + text, color);
        Shadow shadow = bg.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.25f);
        shadow.effectDistance = new Vector2(0, -6);

        Button button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.93f, 0.85f);
        colors.pressedColor = new Color(0.85f, 0.8f, 0.75f);
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
        button.colors = colors;

        Text label = Label(bg.transform, text, fontSize, Color.white);
        Stretch(label.rectTransform);

        if (onClick != null)
        {
            button.onClick.AddListener(() =>
            {
                PlayClick();
                onClick();
            });
        }
        return button;
    }

    public static Text ButtonLabel(Button button)
    {
        return button.GetComponentInChildren<Text>();
    }

    public static RawImage Photo(Transform parent, Texture texture, bool keepAspect = true)
    {
        RectTransform frame = Rect(parent, "PhotoFrame");
        Image frameBg = frame.gameObject.AddComponent<Image>();
        frameBg.color = Color.white;
        frame.gameObject.AddComponent<RectMask2D>();

        RectTransform inner = Stretch(Rect(frame, "Photo"));
        RawImage raw = inner.gameObject.AddComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;
        if (texture == null)
        {
            raw.color = CreamDark;
        }
        else if (keepAspect)
        {
            // 사진이 프레임을 꽉 채우도록 (잘리는 부분은 마스크)
            AspectRatioFitter fitter = inner.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)texture.width / Mathf.Max(1, texture.height);
        }
        return raw;
    }

    public static Slider Slider(Transform parent, float min, float max, float value, Action<float> onChanged)
    {
        RectTransform root = Rect(parent, "Slider");
        Slider slider = root.gameObject.AddComponent<Slider>();

        Image background = Panel(root, "Background", CreamDark);
        Place(background.rectTransform, new Vector2(0, 0.35f), new Vector2(1, 0.65f), Vector2.zero, Vector2.zero);

        RectTransform fillArea = Place(Rect(root, "Fill Area"), new Vector2(0, 0.35f), new Vector2(1, 0.65f), Vector2.zero, Vector2.zero);
        Image fill = Panel(fillArea, "Fill", Orange);
        Stretch(fill.rectTransform);

        RectTransform handleArea = Place(Rect(root, "Handle Slide Area"), Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-20, 0));
        Image handle = Panel(handleArea, "Handle", OrangeDark);
        handle.rectTransform.sizeDelta = new Vector2(40, 0);
        handle.rectTransform.anchorMin = new Vector2(0, 0);
        handle.rectTransform.anchorMax = new Vector2(0, 1);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
        return slider;
    }

    public static VerticalLayoutGroup VerticalLayout(GameObject go, float spacing, int padding)
    {
        VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static LayoutElement Height(Component c, float height)
    {
        LayoutElement le = c.gameObject.GetComponent<LayoutElement>();
        if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
        return le;
    }

    public static string Stars(int count, int max = 3)
    {
        string filled = "<color=#FFC428>★</color>";
        string empty = "<color=#D8CFC0>★</color>";
        string s = "";
        for (int i = 0; i < max; i++) s += i < count ? filled : empty;
        return s;
    }

    // ===== 소리 =====

    public static void PlayClick()
    {
        PlayOneShot(LevelDatabase.Load().clickClip, 0.6f);
    }

    public static void PlayOneShot(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        if (uiAudio == null)
        {
            GameObject go = new GameObject("UIAudio");
            UnityEngine.Object.DontDestroyOnLoad(go);
            uiAudio = go.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false;
            uiAudio.spatialBlend = 0f;
            uiAudio.ignoreListenerPause = true;
        }
        uiAudio.PlayOneShot(clip, volume);
    }

    public static bool IsWebGL
    {
        get { return Application.platform == RuntimePlatform.WebGLPlayer; }
    }
}
