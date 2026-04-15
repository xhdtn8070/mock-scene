#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tikim.MockScene.Editor
{
    /// <summary>
    /// Centralizes asset discovery and scene-folder conventions so the editor window stays focused on UI.
    /// </summary>
    internal static class MockSceneEditorAssetUtility
    {
        internal const string ScenesRootFolder = "Assets/MockScene/Scenes";

        internal static IReadOnlyList<MockSceneSceneSettings> FindAllSceneSettings()
        {
            var settings = new List<MockSceneSceneSettings>();
            var guids = AssetDatabase.FindAssets($"t:{nameof(MockSceneSceneSettings)}");

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var sceneSettings = AssetDatabase.LoadAssetAtPath<MockSceneSceneSettings>(assetPath);

                if (sceneSettings is not null)
                {
                    settings.Add(sceneSettings);
                }
            }

            settings.Sort(static (left, right) =>
            {
                var sceneNameCompare = string.Compare(left.sceneName, right.sceneName, StringComparison.OrdinalIgnoreCase);
                if (sceneNameCompare != 0)
                {
                    return sceneNameCompare;
                }

                return string.Compare(
                    AssetDatabase.GetAssetPath(left),
                    AssetDatabase.GetAssetPath(right),
                    StringComparison.OrdinalIgnoreCase);
            });

            return settings;
        }

        internal static MockSceneSceneSettings? FindSceneSettings(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return null;
            }

            foreach (var settings in FindAllSceneSettings())
            {
                if (settings.OwnsScene(scenePath))
                {
                    return settings;
                }
            }

            return null;
        }

        internal static List<MockScenePreset> FindPresetsForScene(Scene scene)
        {
            var result = new List<MockScenePreset>();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                return result;
            }

            var folderPath = GetSceneFolderPath(scene);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                return result;
            }

            var guids = AssetDatabase.FindAssets($"t:{nameof(MockScenePreset)}", new[] { folderPath });
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var preset = AssetDatabase.LoadAssetAtPath<MockScenePreset>(assetPath);

                if (preset is not null)
                {
                    result.Add(preset);
                }
            }

            result.Sort(static (left, right) =>
            {
                var nameCompare = string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
                if (nameCompare != 0)
                {
                    return nameCompare;
                }

                return string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        internal static string GetSceneFolderPath(Scene scene)
        {
            var sceneName = GetSceneName(scene);
            return $"{ScenesRootFolder}/{sceneName}";
        }

        internal static string GetSceneSettingsAssetPath(Scene scene)
        {
            var sceneName = GetSceneName(scene);
            return $"{GetSceneFolderPath(scene)}/{sceneName}.MockSceneSceneSettings.asset";
        }

        internal static bool HasSavedScene(Scene scene)
        {
            return scene.IsValid() && !string.IsNullOrWhiteSpace(scene.path);
        }

        internal static string GetSceneName(Scene scene)
        {
            if (!HasSavedScene(scene))
            {
                return scene.name;
            }

            return Path.GetFileNameWithoutExtension(scene.path);
        }

        internal static MockSceneSceneSettings GetOrCreateSceneSettings(Scene scene)
        {
            if (!HasSavedScene(scene))
            {
                throw new InvalidOperationException("The current scene must be saved before MockScene assets can be created.");
            }

            EnsureSceneFolders(scene);

            var settings = FindSceneSettings(scene.path) ?? AssetDatabase.LoadAssetAtPath<MockSceneSceneSettings>(GetSceneSettingsAssetPath(scene));
            if (settings is null)
            {
                settings = ScriptableObject.CreateInstance<MockSceneSceneSettings>();
                settings.sceneName = GetSceneName(scene);
                settings.scenePath = scene.path;
                AssetDatabase.CreateAsset(settings, GetSceneSettingsAssetPath(scene));
            }

            var discoveredPresets = FindPresetsForScene(scene);
            SaveSettingsIfChanged(settings, scene.path, discoveredPresets);
            return settings;
        }

        internal static MockScenePreset CreatePreset(Scene scene)
        {
            if (!HasSavedScene(scene))
            {
                throw new InvalidOperationException("The current scene must be saved before MockScene presets can be created.");
            }

            EnsureSceneFolders(scene);

            var preset = ScriptableObject.CreateInstance<MockScenePreset>();
            preset.presetName = $"{GetSceneName(scene)} Preset";
            preset.Normalize();

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{GetSceneFolderPath(scene)}/{GetSceneName(scene)}.MockScenePreset.asset");
            AssetDatabase.CreateAsset(preset, assetPath);
            AssetDatabase.SaveAssets();

            var settings = GetOrCreateSceneSettings(scene);
            SaveSettingsIfChanged(settings, scene.path, FindPresetsForScene(scene));

            return preset;
        }

        internal static void SetActivePreset(Scene scene, MockScenePreset preset)
        {
            var settings = GetOrCreateSceneSettings(scene);
            SaveSettingsIfChanged(settings, scene.path, FindPresetsForScene(scene));

            if (settings.activePreset != preset)
            {
                settings.activePreset = preset;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        internal static void SynchronizeSceneSettings(Scene scene, MockSceneSceneSettings settings)
        {
            SaveSettingsIfChanged(settings, scene.path, FindPresetsForScene(scene));
        }

        internal static MockSceneCopyResult CopyFromSource(
            Scene targetScene,
            MockSceneSceneSettings sourceSettings,
            IReadOnlyList<MockScenePreset> presetsToCopy,
            bool copySceneSettings,
            bool allowOverwriteSettings)
        {
            if (!HasSavedScene(targetScene))
            {
                return MockSceneCopyResult.Failure("Save the current scene before copying MockScene assets.");
            }

            if (presetsToCopy.Count == 0 && !copySceneSettings)
            {
                return MockSceneCopyResult.Failure("Select at least one preset or enable scene settings copy before copying.");
            }

            var existingTargetSettings = FindSceneSettings(targetScene.path);
            if (copySceneSettings && existingTargetSettings is not null && !allowOverwriteSettings)
            {
                return MockSceneCopyResult.Failure("Copy aborted because the target scene already has settings. Enable overwrite to replace them.");
            }

            EnsureSceneFolders(targetScene);

            var copiedPresetMap = new Dictionary<MockScenePreset, MockScenePreset>();
            foreach (var sourcePreset in presetsToCopy)
            {
                if (sourcePreset is null)
                {
                    continue;
                }

                var sourceAssetPath = AssetDatabase.GetAssetPath(sourcePreset);
                if (string.IsNullOrWhiteSpace(sourceAssetPath))
                {
                    continue;
                }

                var targetAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{GetSceneFolderPath(targetScene)}/{Path.GetFileName(sourceAssetPath)}");

                if (!AssetDatabase.CopyAsset(sourceAssetPath, targetAssetPath))
                {
                    continue;
                }

                var copiedPreset = AssetDatabase.LoadAssetAtPath<MockScenePreset>(targetAssetPath);
                if (copiedPreset is null)
                {
                    continue;
                }

                copiedPreset.Normalize();
                EditorUtility.SetDirty(copiedPreset);
                copiedPresetMap[sourcePreset] = copiedPreset;
            }

            AssetDatabase.SaveAssets();

            var targetSettings = existingTargetSettings ?? GetOrCreateSceneSettings(targetScene);
            SaveSettingsIfChanged(targetSettings, targetScene.path, FindPresetsForScene(targetScene));

            if (copySceneSettings)
            {
                targetSettings.activePreset = sourceSettings.activePreset is not null
                    && copiedPresetMap.TryGetValue(sourceSettings.activePreset, out var copiedActivePreset)
                    ? copiedActivePreset
                    : null;

                targetSettings.sceneName = GetSceneName(targetScene);
                targetSettings.scenePath = targetScene.path;
                SaveSettingsIfChanged(targetSettings, targetScene.path, FindPresetsForScene(targetScene));
                EditorUtility.SetDirty(targetSettings);
                AssetDatabase.SaveAssets();
            }

            return MockSceneCopyResult.Success(
                copiedPresetMap.Count,
                copySceneSettings,
                copiedPresetMap.Count == 0
                    ? "No presets were copied."
                    : $"Copied {copiedPresetMap.Count} preset(s) into {GetSceneFolderPath(targetScene)}.");
        }

        internal static void PingAsset(UnityEngine.Object asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void SaveSettingsIfChanged(MockSceneSceneSettings settings, string scenePath, IReadOnlyList<MockScenePreset> discoveredPresets)
        {
            if (!settings.Synchronize(scenePath, discoveredPresets))
            {
                return;
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureSceneFolders(Scene scene)
        {
            EnsureFolder("Assets", "MockScene");
            EnsureFolder("Assets/MockScene", "Scenes");
            EnsureFolder(ScenesRootFolder, GetSceneName(scene));
        }

        private static void EnsureFolder(string parentFolder, string childFolderName)
        {
            var targetFolder = $"{parentFolder}/{childFolderName}";
            if (AssetDatabase.IsValidFolder(targetFolder))
            {
                return;
            }

            AssetDatabase.CreateFolder(parentFolder, childFolderName);
        }
    }

    internal readonly struct MockSceneCopyResult
    {
        private MockSceneCopyResult(bool succeeded, int copiedPresetCount, bool copiedSettings, string message)
        {
            Succeeded = succeeded;
            CopiedPresetCount = copiedPresetCount;
            CopiedSettings = copiedSettings;
            Message = message;
        }

        internal bool Succeeded { get; }

        internal int CopiedPresetCount { get; }

        internal bool CopiedSettings { get; }

        internal string Message { get; }

        internal static MockSceneCopyResult Success(int copiedPresetCount, bool copiedSettings, string message)
        {
            return new MockSceneCopyResult(true, copiedPresetCount, copiedSettings, message);
        }

        internal static MockSceneCopyResult Failure(string message)
        {
            return new MockSceneCopyResult(false, 0, false, message);
        }
    }
}
