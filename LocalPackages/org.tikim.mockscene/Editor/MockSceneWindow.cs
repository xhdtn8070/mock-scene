#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tikim.MockScene.Editor
{
    /// <summary>
    /// Main editor entry point for managing scene-scoped mock presets.
    /// </summary>
    public sealed class MockSceneWindow : EditorWindow
    {
        private readonly HashSet<string> _selectedCopyPresetPaths = new();
        private IReadOnlyList<MockSceneSceneSettings> _allSceneSettings = System.Array.Empty<MockSceneSceneSettings>();
        private List<MockScenePreset> _currentScenePresets = new();
        private MockSceneSceneSettings? _currentSceneSettings;
        private GUIStyle? _activePresetStyle;
        private GUIStyle? _activePresetButtonStyle;
        private int _selectedCopySourceIndex;
        private string? _statusMessage;
        private bool _allowOverwriteSettings;
        private bool _copySceneSettings;
        private bool _needsRefresh = true;
        private Scene _currentScene;
        private Vector2 _presetScrollPosition;
        private Vector2 _copyScrollPosition;
        private MessageType _statusType = MessageType.Info;

        [MenuItem("Tools/MockScene Manager")]
        public static void Open()
        {
            var window = GetWindow<MockSceneWindow>();
            window.titleContent = new GUIContent("MockScene");
            window.minSize = new Vector2(720f, 540f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.projectChanged += HandleProjectChanged;
            EditorSceneManager.activeSceneChangedInEditMode += HandleActiveSceneChanged;
            _needsRefresh = true;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= HandleProjectChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= HandleActiveSceneChanged;
        }

        private void OnFocus()
        {
            _needsRefresh = true;
        }

        private void OnGUI()
        {
            EnsureStyles();
            RefreshIfNeeded();

            EditorGUILayout.Space(8f);
            DrawSceneHeader();
            EditorGUILayout.Space(8f);

            if (!MockSceneEditorAssetUtility.HasSavedScene(_currentScene))
            {
                EditorGUILayout.HelpBox("Save the current scene before creating or activating MockScene assets.", MessageType.Warning);
                return;
            }

            DrawStatusMessage();
            DrawToolbar();

            EditorGUILayout.Space(8f);
            DrawCurrentScenePresetList();

            EditorGUILayout.Space(12f);
            DrawCopySection();

            EditorGUILayout.Space(16f);
            DrawPlayButton();
        }

        private void DrawSceneHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Current Scene", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Scene Name", MockSceneEditorAssetUtility.GetSceneName(_currentScene));
                EditorGUILayout.LabelField("Scene Path", string.IsNullOrWhiteSpace(_currentScene.path) ? "(Unsaved Scene)" : _currentScene.path);

                var activePresetName = _currentSceneSettings?.activePreset is null
                    ? "(None)"
                    : _currentSceneSettings.activePreset.DisplayName;

                EditorGUILayout.LabelField("Active Preset", activePresetName);
            }
        }

        private void DrawStatusMessage()
        {
            if (string.IsNullOrWhiteSpace(_statusMessage))
            {
                return;
            }

            EditorGUILayout.HelpBox(_statusMessage, _statusType);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh", GUILayout.Width(120f)))
                {
                    _needsRefresh = true;
                    _statusMessage = null;
                }

                if (GUILayout.Button("Create Scene Settings", GUILayout.Width(180f)))
                {
                    var settings = MockSceneEditorAssetUtility.GetOrCreateSceneSettings(_currentScene);
                    MockSceneEditorAssetUtility.PingAsset(settings);
                    SetStatus("Created or refreshed scene settings asset.", MessageType.Info);
                    _needsRefresh = true;
                }

                if (GUILayout.Button("Create Preset", GUILayout.Width(140f)))
                {
                    var preset = MockSceneEditorAssetUtility.CreatePreset(_currentScene);
                    MockSceneEditorAssetUtility.PingAsset(preset);
                    SetStatus($"Created preset '{preset.DisplayName}'.", MessageType.Info);
                    _needsRefresh = true;
                }

                if (_currentSceneSettings is not null && GUILayout.Button("Sync Settings", GUILayout.Width(140f)))
                {
                    MockSceneEditorAssetUtility.SynchronizeSceneSettings(_currentScene, _currentSceneSettings);
                    SetStatus("Synchronized scene settings with discovered presets.", MessageType.Info);
                    _needsRefresh = true;
                }
            }
        }

        private void DrawCurrentScenePresetList()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Current Scene Presets", EditorStyles.boldLabel);

                if (_currentScenePresets.Count == 0)
                {
                    EditorGUILayout.HelpBox("No MockScenePreset assets were found for this scene yet.", MessageType.Info);
                    return;
                }

                _presetScrollPosition = EditorGUILayout.BeginScrollView(_presetScrollPosition, GUILayout.MinHeight(220f));

                foreach (var preset in _currentScenePresets)
                {
                    var isActive = _currentSceneSettings?.activePreset == preset;
                    using (new EditorGUILayout.VerticalScope(isActive ? _activePresetStyle! : EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            using (new EditorGUILayout.VerticalScope())
                            {
                                EditorGUILayout.LabelField(preset.DisplayName, EditorStyles.boldLabel);
                                EditorGUILayout.LabelField(preset.BuildSummary(), EditorStyles.wordWrappedMiniLabel);
                            }

                            GUILayout.FlexibleSpace();

                            using (new EditorGUI.DisabledScope(isActive))
                            {
                                if (GUILayout.Button("Set Active", isActive ? _activePresetButtonStyle! : GUI.skin.button, GUILayout.Width(100f), GUILayout.Height(28f)))
                                {
                                    MockSceneEditorAssetUtility.SetActivePreset(_currentScene, preset);
                                    SetStatus($"Preset '{preset.DisplayName}' is now active for scene '{_currentScene.name}'.", MessageType.Info);
                                    _needsRefresh = true;
                                }
                            }

                            if (GUILayout.Button("Edit", GUILayout.Width(80f), GUILayout.Height(28f)))
                            {
                                MockSceneEditorAssetUtility.PingAsset(preset);
                            }
                        }
                    }

                    GUILayout.Space(4f);
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCopySection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Copy From Another Scene", EditorStyles.boldLabel);

                var copySources = _allSceneSettings
                    .Where(settings => settings is not null && settings.scenePath != _currentScene.path)
                    .ToList();

                if (copySources.Count == 0)
                {
                    EditorGUILayout.HelpBox("Create MockScene settings for another scene to enable scene-to-scene copy workflows.", MessageType.Info);
                    return;
                }

                _selectedCopySourceIndex = Mathf.Clamp(_selectedCopySourceIndex, 0, copySources.Count - 1);

                var sourceLabels = copySources
                    .Select(settings => $"{settings.sceneName} ({settings.presets.Count} preset(s))")
                    .ToArray();

                var nextSourceIndex = EditorGUILayout.Popup("Source Scene", _selectedCopySourceIndex, sourceLabels);
                if (nextSourceIndex != _selectedCopySourceIndex)
                {
                    _selectedCopySourceIndex = nextSourceIndex;
                    _selectedCopyPresetPaths.Clear();
                }

                var selectedSource = copySources[_selectedCopySourceIndex];
                DrawCopyOptions(selectedSource);
            }
        }

        private void DrawCopyOptions(MockSceneSceneSettings sourceSettings)
        {
            _copySceneSettings = EditorGUILayout.ToggleLeft("Copy Scene Settings", _copySceneSettings);

            using (new EditorGUI.DisabledScope(!_copySceneSettings))
            {
                _allowOverwriteSettings = EditorGUILayout.ToggleLeft("Allow Overwrite Settings", _allowOverwriteSettings);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All Presets", GUILayout.Width(150f)))
                {
                    foreach (var preset in sourceSettings.presets.Where(static preset => preset is not null))
                    {
                        _selectedCopyPresetPaths.Add(AssetDatabase.GetAssetPath(preset!));
                    }
                }

                if (GUILayout.Button("Clear Preset Selection", GUILayout.Width(170f)))
                {
                    _selectedCopyPresetPaths.Clear();
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Source Presets", EditorStyles.miniBoldLabel);
            _copyScrollPosition = EditorGUILayout.BeginScrollView(_copyScrollPosition, GUILayout.MinHeight(160f), GUILayout.MaxHeight(220f));

            if (sourceSettings.presets.Count == 0)
            {
                EditorGUILayout.HelpBox("The selected source scene does not have any presets to copy.", MessageType.Info);
            }
            else
            {
                foreach (var preset in sourceSettings.presets.Where(static preset => preset is not null))
                {
                    var assetPath = AssetDatabase.GetAssetPath(preset!);
                    var isSelected = _selectedCopyPresetPaths.Contains(assetPath);
                    var toggled = EditorGUILayout.ToggleLeft($"{preset!.DisplayName}  |  {preset.BuildSummary()}", isSelected);

                    if (toggled)
                    {
                        _selectedCopyPresetPaths.Add(assetPath);
                    }
                    else
                    {
                        _selectedCopyPresetPaths.Remove(assetPath);
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Copy Selected Assets To Current Scene", GUILayout.Height(34f)))
            {
                var selectedPresets = sourceSettings.presets
                    .Where(static preset => preset is not null)
                    .Where(preset => _selectedCopyPresetPaths.Contains(AssetDatabase.GetAssetPath(preset!)))
                    .Cast<MockScenePreset>()
                    .ToList();

                var result = MockSceneEditorAssetUtility.CopyFromSource(
                    _currentScene,
                    sourceSettings,
                    selectedPresets,
                    _copySceneSettings,
                    _allowOverwriteSettings);

                SetStatus(result.Message, result.Succeeded ? MessageType.Info : MessageType.Warning);
                _needsRefresh = true;
            }
        }

        private void DrawPlayButton()
        {
            var hasActivePreset = _currentSceneSettings?.activePreset is not null;

            if (!hasActivePreset)
            {
                EditorGUILayout.HelpBox("Select an active preset before entering play mode with MockScene.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(!hasActivePreset))
            {
                if (GUILayout.Button("▶ Play with Active Preset", GUILayout.Height(48f)))
                {
                    EditorApplication.isPlaying = true;
                }
            }
        }

        private void EnsureStyles()
        {
            if (_activePresetStyle is not null && _activePresetButtonStyle is not null)
            {
                return;
            }

            var activeRowTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            activeRowTexture.SetPixel(0, 0, new Color(0.17f, 0.37f, 0.30f, 0.45f));
            activeRowTexture.Apply();

            _activePresetStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10),
                margin = new RectOffset(4, 4, 4, 4)
            };
            _activePresetStyle.normal.background = activeRowTexture;

            _activePresetButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };
        }

        private void RefreshIfNeeded()
        {
            if (!_needsRefresh)
            {
                return;
            }

            _currentScene = EditorSceneManager.GetActiveScene();
            _allSceneSettings = MockSceneEditorAssetUtility.FindAllSceneSettings();
            _currentSceneSettings = MockSceneEditorAssetUtility.HasSavedScene(_currentScene)
                ? MockSceneEditorAssetUtility.FindSceneSettings(_currentScene.path)
                : null;
            _currentScenePresets = MockSceneEditorAssetUtility.FindPresetsForScene(_currentScene);

            if (_currentSceneSettings is not null && MockSceneEditorAssetUtility.HasSavedScene(_currentScene))
            {
                MockSceneEditorAssetUtility.SynchronizeSceneSettings(_currentScene, _currentSceneSettings);
                _currentScenePresets = MockSceneEditorAssetUtility.FindPresetsForScene(_currentScene);
            }

            _needsRefresh = false;
        }

        private void SetStatus(string message, MessageType messageType)
        {
            _statusMessage = message;
            _statusType = messageType;
        }

        private void HandleProjectChanged()
        {
            _needsRefresh = true;
            Repaint();
        }

        private void HandleActiveSceneChanged(Scene _, Scene __)
        {
            _needsRefresh = true;
            _selectedCopyPresetPaths.Clear();
            Repaint();
        }
    }
}
