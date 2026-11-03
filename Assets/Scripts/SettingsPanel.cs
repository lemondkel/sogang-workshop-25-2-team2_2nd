using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 화면 (음량, 마우스 감도, 마우스 상하 반전, 전체화면, 기록 초기화).
/// 타이틀과 일시정지 화면에서 같이 사용합니다.
/// </summary>
public static class SettingsPanel
{
    public static GameObject Build(Transform parent, Action onClose, Action onChanged)
    {
        Image dim = UIKit.Panel(parent, "SettingsScreen", UIKit.Dim);
        UIKit.Stretch(dim.rectTransform);

        Image card = UIKit.Card(dim.transform, "SettingsCard");
        UIKit.Fixed(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 780));

        Text title = UIKit.Label(card.transform, "설정", 64, UIKit.Brown);
        UIKit.Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -130), new Vector2(0, -30));

        RectTransform body = UIKit.Place(UIKit.Rect(card.transform, "Body"), new Vector2(0, 0), new Vector2(1, 1), new Vector2(70, 150), new Vector2(-70, -150));
        UIKit.VerticalLayout(body.gameObject, 18, 0);

        // 음량
        Text volumeLabel = Row(body, "");
        Action refreshVolume = () => volumeLabel.text = $"전체 음량  <color=#F08048>{Mathf.RoundToInt(GameSession.MasterVolume * 100)}%</color>";
        refreshVolume();
        Slider volume = UIKit.Slider(body, 0f, 1f, GameSession.MasterVolume, v =>
        {
            GameSession.MasterVolume = v;
            refreshVolume();
        });
        UIKit.Height(volume, 50);

        // 마우스 감도
        Text sensLabel = Row(body, "");
        Action refreshSens = () => sensLabel.text = $"마우스 감도  <color=#F08048>{GameSession.MouseSensitivity:0.0}</color>";
        refreshSens();
        Slider sens = UIKit.Slider(body, 0.2f, 6f, GameSession.MouseSensitivity, v =>
        {
            GameSession.MouseSensitivity = v;
            refreshSens();
            if (onChanged != null) onChanged();
        });
        UIKit.Height(sens, 50);

        // 토글 버튼들
        RectTransform toggles = UIKit.Rect(body, "Toggles");
        UIKit.Height(toggles, 90);
        HorizontalLayoutGroup h = toggles.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 20;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = true;

        Button invert = null;
        invert = UIKit.Button(toggles, "", () =>
        {
            GameSession.InvertY = !GameSession.InvertY;
            UIKit.ButtonLabel(invert).text = InvertText();
            if (onChanged != null) onChanged();
        }, UIKit.BrownLight, 34);
        UIKit.ButtonLabel(invert).text = InvertText();

        Button fullscreen = null;
        fullscreen = UIKit.Button(toggles, "전체화면", () =>
        {
            Screen.fullScreen = !Screen.fullScreen;
        }, UIKit.BrownLight, 34);

        // 기록 초기화 (두 번 눌러야 실행)
        Button reset = null;
        bool armed = false;
        reset = UIKit.Button(body, "기록 초기화", () =>
        {
            if (!armed)
            {
                armed = true;
                UIKit.ButtonLabel(reset).text = "정말 초기화할까요? 한 번 더 누르세요";
                return;
            }
            GameSession.ResetProgress();
            armed = false;
            UIKit.ButtonLabel(reset).text = "초기화했어요";
            if (onChanged != null) onChanged();
        }, UIKit.Gray, 32);
        UIKit.Height(reset, 80);

        Button close = UIKit.Button(card.transform, "닫기", () =>
        {
            if (onClose != null) onClose();
            UnityEngine.Object.Destroy(dim.gameObject);
        }, UIKit.Orange, 42);
        UIKit.Fixed((RectTransform)close.transform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(320, 90));

        return dim.gameObject;
    }

    static string InvertText()
    {
        return GameSession.InvertY ? "상하 반전: 켜짐" : "상하 반전: 꺼짐";
    }

    static Text Row(Transform parent, string text)
    {
        Text label = UIKit.Label(parent, text, 38, UIKit.Brown, TextAnchor.MiddleLeft);
        UIKit.Height(label, 50);
        return label;
    }
}
