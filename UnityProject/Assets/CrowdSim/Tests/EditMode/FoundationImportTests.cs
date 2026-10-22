using System.IO;
using CrowdSim.Migration;
using CrowdSim.Unity.Foundation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace CrowdSim.Unity.Tests
{
    public sealed class FoundationImportTests
    {
        [Test]
        public void ApprovedEditorAndUniversalPipelineAreConfigured()
        {
            Assert.That(Application.unityVersion, Is.EqualTo(MigrationContract.EditorVersion));
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.TypeOf<UniversalRenderPipelineAsset>());
            Assert.That(PlayerSettings.GetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.Standalone),
                Is.EqualTo(ApiCompatibilityLevel.NET_Standard));
        }

        [Test]
        public void BootstrapSceneContainsTheControllerAndCameraWithoutMissingScripts()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(MigrationContract.BootstrapScene, OpenSceneMode.Single);
                int controllers = 0;
                int cameras = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    {
                        Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero);
                    }
                    controllers += root.GetComponentsInChildren<BootstrapController>(true).Length;
                    cameras += root.GetComponentsInChildren<Camera>(true).Length;
                }
                Assert.That(controllers, Is.EqualTo(1));
                Assert.That(cameras, Is.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        [Test]
        public void StartupResourcesAndBundledScenariosAreImported()
        {
            Assert.That(Resources.Load<VisualTreeAsset>("CrowdSim/Startup"), Is.Not.Null);
            Assert.That(Resources.Load<ThemeStyleSheet>("CrowdSim/FoundationTheme"), Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(Application.streamingAssetsPath, "Scenarios/legacy_v2.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(Application.streamingAssetsPath, "Scenarios/toxic_fume_205.json")), Is.True);
        }

        [Test]
        public void StandaloneIdentityBackendAndVersionMatchThePortableReleaseContract()
        {
            Assert.That(PlayerSettings.bundleVersion, Is.EqualTo(MigrationContract.ApplicationVersion));
            Assert.That(PlayerSettings.companyName, Is.EqualTo("CrowdSim"));
            Assert.That(PlayerSettings.productName, Is.EqualTo("CrowdSim Unity"));
            Assert.That(PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone), Is.EqualTo(ScriptingImplementation.Mono2x));
            Assert.That(PlayerSettings.GetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Standalone), Is.EqualTo(ManagedStrippingLevel.Disabled));
        }

        [Test]
        public void LocalHelpAndFullThirdPartyLicenseTextsAreBundled()
        {
            string docs = Path.Combine(Application.streamingAssetsPath, "Documentation");
            Assert.That(File.ReadAllText(Path.Combine(docs, "USER_GUIDE.txt")), Does.Contain("QUIT"));
            Assert.That(File.ReadAllText(Path.Combine(docs, "THIRD_PARTY_NOTICES.txt")), Does.Contain("James Newton-King"));
            Assert.That(File.ReadAllText(Path.Combine(docs, "Notices/AtkinsonHyperlegible-OFL.txt")),
                Is.EqualTo(File.ReadAllText("Assets/CrowdSim/Resources/CrowdSim/Fonts/OFL.txt")));
            Assert.That(File.ReadAllText(Path.Combine(docs, "Notices/Newtonsoft.Json-LICENSE.txt")), Does.Contain("Permission is hereby granted"));
        }

        [Test]
        public void RuntimeCreatedWorldMaterialShadersAreAlwaysIncludedForPlayerBuilds()
        {
            SerializedObject graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty included = graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach (string name in new[] { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Unlit" })
            {
                bool retained = false;
                for (int i = 0; i < included.arraySize; i++)
                {
                    Shader shader = included.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                    if (shader != null && shader.name == name) retained = true;
                }
                Assert.That(retained, Is.True, name + " is used by runtime-created materials.");
            }
        }
    }
}
