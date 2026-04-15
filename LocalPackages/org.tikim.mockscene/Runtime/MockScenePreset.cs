#nullable enable

using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace Tikim.MockScene
{
    /// <summary>
    /// Describes a reusable mock gameplay preset that can be applied before entering play mode.
    /// </summary>
    [CreateAssetMenu(fileName = "MockScenePreset", menuName = "Tikim/MockScene/Preset")]
    public sealed class MockScenePreset : ScriptableObject
    {
        public enum TimeOfDay
        {
            Morning,
            Day,
            Night
        }

        [Tooltip("User-facing name displayed in the MockScene manager.")]
        public string presetName = string.Empty;

        [Tooltip("Mock time-of-day value to apply when this preset becomes active.")]
        public TimeOfDay startingTime = TimeOfDay.Day;

        [Tooltip("When enabled, the preset overrides the player's starting gold value.")]
        public bool modifyGold;

        [ShowIf(nameof(modifyGold))]
        [Tooltip("Gold value to inject when the override is enabled.")]
        public int startingGold;

        [Tooltip("List of equipment identifiers that should be available when the preset is injected.")]
        public List<string> startingEquipments = new();

        /// <summary>
        /// Returns a trimmed name that is always safe to display in the UI and logs.
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(presetName) ? name : presetName.Trim();

        /// <summary>
        /// Normalizes editable fields so the preset stays deterministic and easy to compare in source control.
        /// </summary>
        public bool Normalize()
        {
            var changed = false;
            var normalizedName = DisplayName;

            if (presetName != normalizedName)
            {
                presetName = normalizedName;
                changed = true;
            }

            if (!modifyGold && startingGold != 0)
            {
                startingGold = 0;
                changed = true;
            }

            if (startingEquipments is null)
            {
                startingEquipments = new List<string>();
                changed = true;
            }

            var cleanedEquipments = new List<string>(startingEquipments.Count);
            var seenEquipments = new HashSet<string>();

            foreach (var equipment in startingEquipments)
            {
                if (string.IsNullOrWhiteSpace(equipment))
                {
                    changed = true;
                    continue;
                }

                var normalizedEquipment = equipment.Trim();
                if (!seenEquipments.Add(normalizedEquipment))
                {
                    changed = true;
                    continue;
                }

                if (normalizedEquipment != equipment)
                {
                    changed = true;
                }

                cleanedEquipments.Add(normalizedEquipment);
            }

            if (!HaveSameValues(startingEquipments, cleanedEquipments))
            {
                startingEquipments = cleanedEquipments;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Validates the preset after normalization so callers can surface useful diagnostics.
        /// </summary>
        public bool IsValid(out string validationMessage)
        {
            Normalize();

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                validationMessage = "Preset name cannot be empty.";
                return false;
            }

            if (startingEquipments is null)
            {
                validationMessage = "Starting equipments list was unexpectedly null.";
                return false;
            }

            validationMessage = "Preset is valid.";
            return true;
        }

        /// <summary>
        /// Produces a compact human-readable summary used by the window and runtime injector logs.
        /// </summary>
        public string BuildSummary()
        {
            Normalize();

            var equipmentSummary = startingEquipments.Count > 0
                ? string.Join(", ", startingEquipments)
                : "None";

            var goldSummary = modifyGold
                ? startingGold.ToString()
                : "Disabled";

            return $"Preset='{DisplayName}', Time={startingTime}, Gold={goldSummary}, Equipments=[{equipmentSummary}]";
        }

        private void OnValidate()
        {
            Normalize();
        }

        private static bool HaveSameValues(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var index = 0; index < left.Count; index++)
            {
                if (left[index] != right[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
