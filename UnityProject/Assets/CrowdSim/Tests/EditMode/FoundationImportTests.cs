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
    }
}
