using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Keske.Editor
{
    [InitializeOnLoad]
    public sealed class KeskeProjectSetup : IPreprocessBuildWithReport
    {
        public const string ScenePath = "Assets/Keske/Scenes/KeskeBirlesik.unity";
        public int callbackOrder { get { return 0; } }

        static KeskeProjectSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("Keske.Setup", false)) return;
                SessionState.SetBool("Keske.Setup", true);
                PrepareMaterials();
                // Unity has no previous scene selection when this ZIP is first opened.
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(scene.path) && !scene.isDirty) OpenScene();
            };
        }

        [MenuItem("Tools/Keske/Birlesik sahneyi ac")]
        public static void OpenScene()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Keske/Malzemeleri hazirla")]
        public static void PrepareMaterials()
        {
            LowPolyInteriors.LPI_Pipeline.Convert();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit != null && LowPolyInteriors.LPI_Pipeline.Detect() == LowPolyInteriors.LPI_Pipeline.Pipe.URP)
                foreach (string path in AssetDatabase.GetDependencies(ScenePath, true))
                {
                    if (!path.EndsWith(".mat", System.StringComparison.OrdinalIgnoreCase)) continue;
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null || material.shader.name != "Standard") continue;
                    // Transparent Standard materials need their own pipeline-specific asset.
                    if (material.HasProperty("_Mode") && material.GetFloat("_Mode") != 0) continue;
                    Texture texture = material.GetTexture("_MainTex");
                    Vector2 tiling = material.GetTextureScale("_MainTex");
                    Vector2 offset = material.GetTextureOffset("_MainTex");
                    Color color = material.GetColor("_Color");
                    float smoothness = material.GetFloat("_Glossiness");
                    material.shader = lit;
                    material.SetTexture("_BaseMap", texture);
                    material.SetTextureScale("_BaseMap", tiling);
                    material.SetTextureOffset("_BaseMap", offset);
                    material.SetColor("_BaseColor", color);
                    material.SetFloat("_Smoothness", smoothness);
                    EditorUtility.SetDirty(material);
                }
            AssetDatabase.SaveAssets();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            PrepareMaterials();
        }
    }
}
