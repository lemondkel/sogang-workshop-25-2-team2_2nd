using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 윈도우 실행 파일(.exe) 빌드를 만듭니다. 결과는 Build/Windows/CatGame.exe 입니다.
/// 메뉴: CatGame/Build Windows
/// 명령줄: Unity.exe -batchmode -quit -projectPath <프로젝트> -executeMethod WindowsBuild.Build -logFile <로그>
/// 아이콘: Assets/Images/AppIcon.png (Tools/make_icon.py 로 다시 그릴 수 있음)
/// </summary>
public static class WindowsBuild
{
    public const string OutputDir = "Build/Windows";
    public const string ExeName = "CatGame.exe";
    public const string IconPath = "Assets/Images/AppIcon.png";
    public const string GameTitle = "고양이찾기";

    [MenuItem("CatGame/Build Windows")]
    public static void Build()
    {
        ApplyIcon();

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        // 창 제목을 한글 게임 이름으로. 다른 빌드(WebGL)의 저장 위치가 바뀌지 않게 빌드 후 되돌립니다.
        string oldProductName = PlayerSettings.productName;
        PlayerSettings.productName = GameTitle;

        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        Directory.CreateDirectory(OutputDir);
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputDir + "/" + ExeName,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(options);
        }
        finally
        {
            PlayerSettings.productName = oldProductName;
            AssetDatabase.SaveAssets();
        }

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("[WindowsBuild] 빌드 성공: " + options.locationPathName + " (" + (report.summary.totalSize / (1024 * 1024)) + " MB)");
        }
        else
        {
            Debug.LogError("[WindowsBuild] 빌드 실패: " + report.summary.result);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    /// <summary>AppIcon.png 를 플레이어 기본 아이콘으로 지정합니다 (exe 파일 아이콘이 됨).</summary>
    public static void ApplyIcon()
    {
        AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.maxTextureSize != 1024) { importer.maxTextureSize = 1024; changed = true; }
            if (changed) importer.SaveAndReimport();
        }

        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null)
        {
            Debug.LogWarning("[WindowsBuild] 아이콘을 찾지 못했습니다: " + IconPath);
            return;
        }
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
        AssetDatabase.SaveAssets();
    }
}
