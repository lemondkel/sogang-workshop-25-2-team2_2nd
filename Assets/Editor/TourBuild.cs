using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 화면 점검용 Windows 빌드를 만듭니다.
/// 메뉴: CatGame/Build Screen Tour (Windows)
/// 명령줄: Unity.exe -batchmode -quit -projectPath <프로젝트> -executeMethod TourBuild.Build
/// 결과: Build/Tour/CatGame.exe  (실행: CatGame.exe -screentour <스크린샷 폴더>)
/// </summary>
public static class TourBuild
{
    [MenuItem("CatGame/Build Screen Tour (Windows)")]
    public static void Build()
    {
        string[] scenes =
        {
            "Assets/Scenes/IntroScene.unity",
            "Assets/Scenes/MainScene.unity",
            "Assets/Scenes/OutroScene.unity",
        };

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/Tour/CatGame.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("[TourBuild] 빌드 성공: " + options.locationPathName);
        }
        else
        {
            Debug.LogError("[TourBuild] 빌드 실패: " + report.summary.result);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
