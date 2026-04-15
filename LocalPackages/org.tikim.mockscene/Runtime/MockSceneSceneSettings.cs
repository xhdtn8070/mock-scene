#nullable enable

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Tikim.MockScene
{
    /// <summary>
    /// Stores the shared mock configuration that belongs to a single scene.
    /// </summary>
    [CreateAssetMenu(fileName = "MockSceneSceneSettings", menuName = "Tikim/MockScene/Scene Settings")]
    public sealed class MockSceneSceneSettings : ScriptableObject
    {
        [Tooltip("Project-relative path of the scene that owns this settings asset.")]
        public string scenePath = string.Empty;

        [Tooltip("Display name of the owning scene. Derived from Scene Path when possible.")]
        public string sceneName = string.Empty;

        [Tooltip("Preset used by MockSceneInjector when this scene enters play mode.")]
        public MockScenePreset? activePreset;

        [Tooltip("Presets that belong to this scene.")]
        public List<MockScenePreset> presets = new();

        /// <summary>
        /// Synchronizes the owning scene metadata and preset list with the latest discovered assets.
        /// </summary>
        public bool Synchronize(string sourceScenePath, IReadOnlyList<MockScenePreset>? discoveredPresets = null)
        {
            var changed = false;
            var normalizedScenePath = sourceScenePath?.Trim() ?? string.Empty;
            var normalizedSceneName = string.IsNullOrWhiteSpace(normalizedScenePath)
                ? (sceneName?.Trim() ?? string.Empty)
                : Path.GetFileNameWithoutExtension(normalizedScenePath);

            if (presets is null)
            {
                presets = new List<MockScenePreset>();
                changed = true;
            }

            if (scenePath != normalizedScenePath)
            {
                scenePath = normalizedScenePath;
                changed = true;
            }

            if (sceneName != normalizedSceneName)
            {
                sceneName = normalizedSceneName;
                changed = true;
            }

            var normalizedPresets = new List<MockScenePreset>();
            var seenPresets = new HashSet<MockScenePreset>();
            var sourcePresets = discoveredPresets ?? presets;

            foreach (var preset in sourcePresets)
            {
                if (preset is null)
                {
                    changed = true;
                    continue;
                }

                if (!seenPresets.Add(preset))
                {
                    changed = true;
                    continue;
                }

                normalizedPresets.Add(preset);
            }

            if (!HaveSameValues(presets, normalizedPresets))
            {
                presets = normalizedPresets;
                changed = true;
            }

            if (activePreset is not null && !presets.Contains(activePreset))
            {
                activePreset = null;
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Normalizes the current asset contents without replacing the preset list from external discovery.
        /// </summary>
        public bool Normalize()
        {
            return Synchronize(scenePath, presets);
        }

        /// <summary>
        /// Returns whether this settings asset belongs to the provided scene path.
        /// </summary>
        public bool OwnsScene(string candidateScenePath)
        {
            return !string.IsNullOrWhiteSpace(candidateScenePath)
                && scenePath == candidateScenePath.Trim();
        }

        private void OnValidate()
        {
            Normalize();
        }

        private static bool HaveSameValues(IReadOnlyList<MockScenePreset> left, IReadOnlyList<MockScenePreset> right)
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
