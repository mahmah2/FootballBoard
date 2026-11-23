using FootballBoard.Presentation;
using TMPro;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FootballBoard.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Match.unity from scratch: camera rig, lighting baseline, post-processing,
    /// template materials and the game objects. Safe to re-run; it overwrites the scene.
    /// </summary>
    public static class MatchSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Match.unity";
        private const string MaterialFolder = "Assets/Art/Materials";
        private const string VolumePath = "Assets/Settings/MatchVolume.asset";
        private const string RulesPath = "Assets/Resources/Rules/rules.json";
        private const float CameraPitch = 58f;
        private const float CameraDistance = 40f;

        [MenuItem("Football Board/Build Match Scene")]
        public static void Build()
        {
            EnsureTmpEssentials();
            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/Scenes");
            var opaque = Template(MaterialFolder + "/PieceOpaque.mat", "Universal Render Pipeline/Lit", false);
            var glass = Template(MaterialFolder + "/PieceGlass.mat", "Universal Render Pipeline/Unlit", true);
            var profile = VolumeProfileAsset();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Lighting baseline: warm key from the palette, trilight ambient, no template skybox.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.AmbientEquator;
            RenderSettings.ambientGroundColor = Palette.AmbientGround;
            RenderSettings.fog = false;

            var lightGo = new GameObject("Key Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Palette.KeyLight;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.65f;
            lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

            var volumeGo = new GameObject("Global Volume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // Camera: output camera + Cinemachine orthographic board camera following a focus point.
            var focus = new GameObject("CameraFocus").transform;
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Room;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            cameraGo.AddComponent<CinemachineBrain>();
            var cameraData = cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;

            var vcamGo = new GameObject("Board Camera");
            vcamGo.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Follow = focus;
            var lens = vcam.Lens;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = 9f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 200f;
            vcam.Lens = lens;
            var follow = vcamGo.AddComponent<CinemachineFollow>();
            follow.FollowOffset = Quaternion.Euler(CameraPitch, 0f, 0f) * new Vector3(0f, 0f, -CameraDistance);
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.35f, 0.35f, 0.35f);
            vcamGo.transform.position = follow.FollowOffset;
            cameraGo.transform.SetPositionAndRotation(vcamGo.transform.position, vcamGo.transform.rotation);

            // UI input.
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();

            // Game objects.
            var game = new GameObject("Game");
            var rig = game.AddComponent<CameraRig>();
            Set(rig, "virtualCamera", vcam);
            Set(rig, "focus", focus);
            Set(rig, "outputCamera", camera);
            SetFloat(rig, "pitchDegrees", CameraPitch);
            var pointer = game.AddComponent<PointerInput>();
            Set(pointer, "rig", rig);
            var hud = game.AddComponent<Hud>();
            var controller = game.AddComponent<MatchController>();
            Set(controller, "rulesJson", AssetDatabase.LoadAssetAtPath<TextAsset>(RulesPath));
            Set(controller, "opaqueTemplate", opaque);
            Set(controller, "transparentTemplate", glass);
            Set(controller, "cameraRig", rig);
            Set(controller, "pointer", pointer);
            Set(controller, "hud", hud);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[MatchSceneBuilder] Built " + ScenePath);
        }

        private static void EnsureTmpEssentials()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null) return;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();
        }

        private static Material Template(string path, string shaderName, bool transparent)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shaderName));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", Palette.Chalk);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetShaderPassEnabled("DepthOnly", false);
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Scene-lighting floor values; SDR pipeline, so bloom uses threshold 0.9 at half intensity.</summary>
        private static VolumeProfile VolumeProfileAsset()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }

            Override<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
            var bloom = Override<Bloom>(profile);
            bloom.intensity.Override(0.15f);
            bloom.threshold.Override(0.9f);
            Override<Vignette>(profile).intensity.Override(0.22f);
            var color = Override<ColorAdjustments>(profile);
            color.saturation.Override(8f);
            color.contrast.Override(5f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing)) return existing;
            var component = profile.Add<T>(true);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
