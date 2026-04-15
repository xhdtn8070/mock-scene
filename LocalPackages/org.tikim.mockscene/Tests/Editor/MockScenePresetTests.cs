#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Tikim.MockScene.Tests
{
    public sealed class MockScenePresetTests
    {
        [Test]
        public void Normalize_FillsDisplayNameAndCleansEquipmentList()
        {
            var preset = ScriptableObject.CreateInstance<MockScenePreset>();
            preset.name = "Combat Loadout";
            preset.presetName = "   ";
            preset.startingEquipments = new List<string>
            {
                " Sword ",
                "",
                "Shield",
                "Sword"
            };

            preset.Normalize();

            Assert.That(preset.presetName, Is.EqualTo("Combat Loadout"));
            Assert.That(preset.startingEquipments, Is.EqualTo(new[] { "Sword", "Shield" }));
        }

        [Test]
        public void Normalize_ResetsGoldWhenOverrideIsDisabled()
        {
            var preset = ScriptableObject.CreateInstance<MockScenePreset>();
            preset.modifyGold = false;
            preset.startingGold = 200;

            preset.Normalize();

            Assert.That(preset.startingGold, Is.Zero);
        }
    }
}
