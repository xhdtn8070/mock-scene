#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Tikim.MockScene.Tests
{
    public sealed class MockSceneSceneSettingsTests
    {
        [Test]
        public void Synchronize_UpdatesSceneMetadataAndClearsMissingActivePreset()
        {
            var stalePreset = ScriptableObject.CreateInstance<MockScenePreset>();
            stalePreset.presetName = "Stale";

            var settings = ScriptableObject.CreateInstance<MockSceneSceneSettings>();
            settings.scenePath = "Assets/Scenes/OldScene.unity";
            settings.sceneName = "OldScene";
            settings.activePreset = stalePreset;
            settings.presets = new List<MockScenePreset> { stalePreset };

            settings.Synchronize("Assets/Scenes/SampleScene.unity", new List<MockScenePreset>());

            Assert.That(settings.scenePath, Is.EqualTo("Assets/Scenes/SampleScene.unity"));
            Assert.That(settings.sceneName, Is.EqualTo("SampleScene"));
            Assert.That(settings.activePreset, Is.Null);
            Assert.That(settings.presets, Is.Empty);
        }

        [Test]
        public void Synchronize_DeduplicatesPresetReferences()
        {
            var preset = ScriptableObject.CreateInstance<MockScenePreset>();
            preset.presetName = "Shared";

            var settings = ScriptableObject.CreateInstance<MockSceneSceneSettings>();
            settings.presets = new List<MockScenePreset>();

            settings.Synchronize(
                "Assets/Scenes/SampleScene.unity",
                new List<MockScenePreset> { preset, preset });

            Assert.That(settings.presets.Count, Is.EqualTo(1));
            Assert.That(settings.presets[0], Is.SameAs(preset));
        }
    }
}
