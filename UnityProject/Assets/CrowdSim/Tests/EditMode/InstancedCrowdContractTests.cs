using CrowdSim.Diagnostics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CrowdSim.Unity.Tests
{
    public sealed class InstancedCrowdContractTests
    {
        [Test]
        public void BundledShadersRetainInstancedVariantsAndConservativeBlockSizing()
        {
            Assert.That(Resources.Load<Shader>("CrowdSim/CrowdInstanced"), Is.Not.Null);
            Assert.That(Resources.Load<Shader>("CrowdSim/HumanOutline"), Is.Not.Null);
            SerializedObject graphics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            SerializedProperty included = graphics.FindProperty("m_AlwaysIncludedShaders");
            bool body = false, outline = false;
            for (int i = 0; i < included.arraySize; i++)
            {
                Shader shader = included.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader != null) { body |= shader.name == "CrowdSim/CrowdInstanced"; outline |= shader.name == "CrowdSim/HumanOutline"; }
            }
            Assert.That(body && outline, Is.True);
            Assert.That(graphics.FindProperty("m_InstancingStripping").intValue, Is.EqualTo(2), "Keep All runtime instancing variants.");
            Assert.That(InstanceBatchPlan.CountInBatch(513, 2), Is.EqualTo(1));
            Assert.That(InstanceBatchPlan.Capacity, Is.EqualTo(256));
        }
    }
}
