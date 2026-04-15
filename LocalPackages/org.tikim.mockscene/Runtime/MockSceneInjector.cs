#nullable enable

using System.Text;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Tikim.MockScene
{
    /// <summary>
    /// Scene-level entry point that resolves the active mock preset and emits a diagnostic injection log.
    /// </summary>
    public sealed class MockSceneInjector : MonoBehaviour
    {
        private void Awake()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return;
            }

            var ownerScene = gameObject.scene;
            if (!ownerScene.IsValid() || string.IsNullOrWhiteSpace(ownerScene.path))
            {
                Debug.LogWarning("[MockScene] Injection skipped because the owning scene has not been saved yet.", this);
                return;
            }

            var sceneSettings = FindSceneSettings(ownerScene.path);
            if (sceneSettings is null)
            {
                Debug.LogWarning(
                    $"[MockScene] Injection skipped because no MockSceneSceneSettings asset matched scene '{ownerScene.name}' ({ownerScene.path}).",
                    this);
                return;
            }

            sceneSettings.Normalize();

            if (sceneSettings.activePreset is null)
            {
                Debug.LogWarning(
                    $"[MockScene] Injection skipped because scene '{ownerScene.name}' has no active preset configured.",
                    this);
                return;
            }

            var activePreset = sceneSettings.activePreset;
            if (!activePreset.IsValid(out var validationMessage))
            {
                Debug.LogWarning(
                    $"[MockScene] Injection skipped because preset '{activePreset.name}' is invalid: {validationMessage}",
                    this);
                return;
            }

            Debug.Log(BuildInjectionMessage(ownerScene.name, activePreset), this);
#endif
        }

#if UNITY_EDITOR
        private static MockSceneSceneSettings? FindSceneSettings(string scenePath)
        {
            var settingsGuids = AssetDatabase.FindAssets($"t:{nameof(MockSceneSceneSettings)}");

            foreach (var guid in settingsGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<MockSceneSceneSettings>(assetPath);

                if (settings is not null && settings.OwnsScene(scenePath))
                {
                    return settings;
                }
            }

            return null;
        }
#endif

        private static string BuildInjectionMessage(string sceneName, MockScenePreset preset)
        {
            var goldSummary = preset.modifyGold
                ? preset.startingGold.ToString()
                : "Disabled";

            var equipmentsSummary = preset.startingEquipments.Count > 0
                ? string.Join(", ", preset.startingEquipments)
                : "None";

            var builder = new StringBuilder();
            builder.AppendLine("<b><color=#88F0D0>MockScene injection complete</color></b>");
            builder.AppendLine($"Scene: <b>{sceneName}</b>");
            builder.AppendLine($"Preset: <b>{preset.DisplayName}</b>");
            builder.AppendLine($"Starting Time: {preset.startingTime}");
            builder.AppendLine($"Gold Override: {goldSummary}");
            builder.Append($"Equipments: {equipmentsSummary}");
            return builder.ToString();
        }
    }
}
