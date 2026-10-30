using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 웹(WebGL) 빌드를 만듭니다. 결과는 Build/WebGL 폴더이고, 어떤 정적 웹 서버로든 바로 열 수 있습니다.
/// 메뉴: CatGame/Build WebGL
/// 명령줄: Unity.exe -batchmode -quit -projectPath <프로젝트> -executeMethod WebGLBuild.Build
/// 서버 예: python Tools/serve_webgl.py  (0.0.0.0:5173)
/// </summary>
public static class WebGLBuild
{
    public const string OutputDir = "Build/WebGL";

    [MenuItem("CatGame/Build WebGL")]
    public static void Build()
    {
        string[] scenes =
        {
            "Assets/Scenes/IntroScene.unity",
            "Assets/Scenes/MainScene.unity",
            "Assets/Scenes/OutroScene.unity",
        };

        // 서버가 Content-Encoding 헤더를 못 붙여도 열리도록: gzip 압축 + 브라우저 쪽 압축 해제(decompression fallback)
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("[WebGLBuild] 빌드 성공: " + OutputDir + " (" + (report.summary.totalSize / (1024 * 1024)) + " MB)");
        }
        else
        {
            Debug.LogError("[WebGLBuild] 빌드 실패: " + report.summary.result);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
