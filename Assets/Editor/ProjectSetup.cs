using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FootballBoard.EditorTools
{
    /// <summary>
    /// One-time, idempotent project configuration. Run from the menu or in batch mode:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod FootballBoard.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string PipelinePath = SettingsFolder + "/URP-Pipeline.asset";
        private const string RendererPath = SettingsFolder + "/URP-Renderer.asset";

        [MenuItem("Football Board/Run Project Setup")]
        public static void Run()
        {
            ConfigureUrp();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Done.");
        }

        private static void ConfigureUrp()
        {
            if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets", "Settings");

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            // Tabletop board: soft shadows, MSAA for clean grid lines, modest settings for mobile.
            pipeline.msaaSampleCount = 4;
            pipeline.shadowDistance = 60f;
            pipeline.supportsHDR = false;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            var current = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "FootballBoard";
            PlayerSettings.productName = "Football Board";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;

            // Use the new Input System only (spec: Unity Input System for touch, mouse and keyboard).
            var playerSettings = Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (playerSettings.Length > 0)
            {
                var so = new SerializedObject(playerSettings[0]);
                var input = so.FindProperty("activeInputHandler");
                if (input != null)
                {
                    input.intValue = 1;
                    so.ApplyModifiedProperties();
                }
            }
        }
    }
}
