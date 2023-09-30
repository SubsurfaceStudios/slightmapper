#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using SubsurfaceStudios.Slightmapper.Assets;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEditor.UIElements;
using System.Linq;
using SubsurfaceStudios.Slightmapper.Global;
using System.Collections.Generic;
using System;

namespace SubsurfaceStudios.Slightmapper.Editor {
    [EditorWindowTitle(title = "SLightmapper", icon = "Lighting")]
    public class SlightmapperWindow : EditorWindow {
        [MenuItem("Window/Rendering/SLightmapper Window")]
        public static void ShowSlightmapperWindow() {
			GetWindow<SlightmapperWindow>();
        }

		private string GetSceneDataFolderPath() {
			Scene current = SceneManager.GetActiveScene();
			return $"{Path.GetDirectoryName(current.path)}/{current.name}";
		}
        private void EnsureSceneDataFolder() {
            Scene current = SceneManager.GetActiveScene();
            string scene_dir = Path.GetDirectoryName(current.path);
            string data_dir_name = current.name;
            if (!AssetDatabase.IsValidFolder($"{scene_dir}/{data_dir_name}"))
                AssetDatabase.CreateFolder(scene_dir, data_dir_name);
        }
		private void GUIHorizontalLine() => EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);

		SlightmapperData editingBakeState;
        string importedStateName;
        int bakeStateIndex;
        SlightmapperRuntimeState editingRuntimeState;

		public void OnGUI() {
            if (editingBakeState != null) {
                MainGUI();
                return;
            }

            string data_path = GetSceneDataFolderPath();
            string[] results = AssetDatabase.FindAssets("t:SlightmapperData", new[] {data_path});
            if (results.Length > 0) {
                string path = AssetDatabase.GUIDToAssetPath(results.First());
                editingBakeState = AssetDatabase.LoadAssetAtPath<SlightmapperData>(path);
                MainGUI();
                return;
            }

            EditorGUILayout.HelpBox("This scene is not configured to use the Slightmapper.", MessageType.Info);
            if (GUILayout.Button("Set it up for me!")) {
                EnsureSceneDataFolder();
                SlightmapperData new_asset = CreateInstance<SlightmapperData>();
                AssetDatabase.CreateAsset(new_asset, $"{GetSceneDataFolderPath()}/SlightmapperData.asset");
                editingBakeState = new_asset;
            }
        }

        private void MainGUI() {
            GUILayout.Label("Name of imported bake configuration and runtime state asset:");
            importedStateName = GUILayout.TextField(importedStateName);
            EditorGUILayout.HelpBox(
                "This will import the scene's current lighting state "+
                "(lightmaps, reflection probes, light probes, etc) as "+
                "a Runtime State (loadable at runtime) and a Bake State "+
                "(loaded before rebaking the Runtime State).",
                MessageType.Info
            );
            if (GUILayout.Button("Import my current scene lighting configuration.")) {
                ImportLightConfigAsBakeState();
                ImportRuntimeState();
            }

            GUIHorizontalLine();

            List<SlightmapperBakeState> states = editingBakeState.BaketimeStates;
            int states_count = states.Count;

            if (states_count > 0) {
                GUILayout.Label("Currently editing baking configuration:");

                string[] state_names = new string[states_count];
                int[] state_indices = new int[states_count];

                for (int i = 0; i < states_count; i++) {
                    state_names[i] = states[i].Name;
                    state_indices[i] = i;
                }

                Rect r = EditorGUILayout.GetControlRect(true);
                bakeStateIndex = EditorGUI.IntPopup(
                    r,
                    bakeStateIndex,
                    state_names,
                    state_indices
                );

                GUILayout.Space(EditorGUIUtility.singleLineHeight);

                EditorGUILayout.HelpBox(
                    "This will overwrite the currently selected baking configuration "+
                    "with the current setup used in your scene.",
                    MessageType.Warning
                );
                if (GUILayout.Button("Overwrite light baking configuration")) {
                    if (
                        EditorUtility.DisplayDialog(
                            "Confirm Overwrite",
                            "Are you sure you want to overwrite your bake configuration "+
                            $"\"{state_names[bakeStateIndex]}\" with the current scene "+
                            "lighting configuration?",
                            "Yes",
                            "No"
                        )
                    ) {
                        OverwriteBakeState(bakeStateIndex);
                    }
                }

                EditorGUILayout.HelpBox(
                    "This will overwrite the configuration of all lights in your scene with "+
                    "the configuration stored in the selected baking configuration. This could "+
                    "result in a loss of data, if you're not careful.",
                    MessageType.Warning
                );
                if (GUILayout.Button("Load light baking configuration")) {
                    if (
                        EditorUtility.DisplayDialog(
                            "Confirm Load",
                            "Are you sure you want to overwrite the scene lighting "+
                            $"configuration with the one saved as \"{state_names[bakeStateIndex]}\"? "+
                            "This will result in the loss of any unsaved changes to your "+
                            "scene lighting.",
                            "Yes",
                            "No"
                        )
                    ) {
                        LoadBakeState(states[bakeStateIndex]);
                    }
                }
            } else {
                EditorGUILayout.HelpBox(
                    "In order to edit a state, you need to have at least one already present.\n"+
                    "To create one, set up your scene lighting how you want it, and then press the "+
                    "\"Import\" button above.",
                    MessageType.Warning
                );
            }

            GUIHorizontalLine();

            GUILayout.Label("Currently editing runtime state: ");

            editingRuntimeState = EditorGUILayout.ObjectField(
                editingRuntimeState,
                typeof(SlightmapperRuntimeState),
                false
            ) as SlightmapperRuntimeState;

            if (editingRuntimeState != null) {
                GUILayout.Space(EditorGUIUtility.singleLineHeight);

                EditorGUILayout.HelpBox(
                    "This will overwrite your runtime state asset with the lighting data "+
                    "currently in use in the scene.",
                    MessageType.Warning
                );
                if (GUILayout.Button("Overwrite runtime state")) {
                    if (
                        EditorUtility.DisplayDialog(
                            "Confirm Overwrite",
                            "Are you sure you want to overwrite the specified runtime state asset? "+
                            "This will replace all the lightmaps, light probes, and reflection probes "+
                            "saved in the asset with those currently found in the scene, and may result "+
                            "in a loss of lightmap data.",
                            "Yes",
                            "No"
                        )
                    ) {
                        OverwriteRuntimeState(editingRuntimeState);
                    }
                }

                EditorGUILayout.HelpBox(
                    "This will replace the lightmaps and other data currently in use "+
                    "in the scene with what is stored in the asset. This will be automatically "+
                    "reverted by Unity when you reload the scene.",
                    MessageType.Info
                );
                if (GUILayout.Button("Load runtime state")) {
                    if (
                        EditorUtility.DisplayDialog(
                            "Confirm Load",
                            "Are you sure you want to load this runtime state asset?",
                            "Yes",
                            "No"
                        )
                    ) {
                        LoadRuntimeState(editingRuntimeState);
                    }
                }
            }
        }

        private void LoadRuntimeState(SlightmapperRuntimeState state) {
            System.Diagnostics.Stopwatch benchmark_timer = new();
            benchmark_timer.Start();

            state.Load();

            benchmark_timer.Stop();
            
            Debug.Log($"Loaded lightmaps, light probes, and reflection probes in {benchmark_timer.ElapsedMilliseconds}ms.");
        }

        private void OverwriteRuntimeState(SlightmapperRuntimeState state) {
            try {
                EditorUtility.DisplayProgressBar(
                    "Slightmapper Runtime State Importer",
                    "Importing runtime state...",
                    0   
                );

                state.lightmaps       = LightmapSettings.lightmaps.Select(x => new LightmapReference(x)).ToArray();
                state.lightmapsMode   = LightmapSettings.lightmapsMode;
                state.lightProbes     = LightmapSettings.lightProbes;

                string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(state)) + '/';
                for (int i = 0; i < state.lightmaps.Length; i++) {
                    LightmapReference reference = state.lightmaps[i];


                    if (reference.lightmapColor) {
                        string lm_color_path = AssetDatabase.GetAssetPath(reference.lightmapColor);
                        string filename = Path.GetFileName(lm_color_path);
                        AssetDatabase.MoveAsset(lm_color_path, path + filename);
                    }

                    if (reference.lightmapDir) {
                        string lm_dir_path = AssetDatabase.GetAssetPath(reference.lightmapDir);
                        string filename = Path.GetFileName(lm_dir_path);
                        AssetDatabase.MoveAsset(lm_dir_path, path + filename);
                    }

                    if (reference.shadowMask) {
                        string shadow_mask_path = AssetDatabase.GetAssetPath(reference.shadowMask);
                        string filename = Path.GetFileName(shadow_mask_path);
                        AssetDatabase.MoveAsset(shadow_mask_path, path + filename);
                    }
                }

                string lighting_data_path = AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset);
                string lighting_data_filename = Path.GetFileName(lighting_data_path);
                AssetDatabase.MoveAsset(lighting_data_path, path + lighting_data_filename);

                EditorUtility.DisplayProgressBar(
                    "Slightmapper Runtime State Importer",
                    "Finding mesh renderers...",
                    0.1f   
                );

                MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>(true);

                int renderers_len = renderers.Length;
                RendererInfo[] renderer_info = new RendererInfo[renderers_len];

                for (int i = 0; i < renderers_len; i++) {
                    EditorUtility.DisplayProgressBar(
                        "Slightmapper Runtime State Importer",
                        "Gathering renderer data...",
                        (float)i / renderers_len
                    );

                    MeshRenderer element = renderers[i];

                    if (!element.TryGetComponent<RendererId>(out _)) {
                        element.gameObject.AddComponent<RendererId>();
                    }

                    renderer_info[i] = new RendererInfo(element);
                }

                ReflectionProbe[] probes = FindObjectsOfType<ReflectionProbe>(true);

                int probe_len = probes.Length;
                ReflectionProbeInfo[] probe_info = new ReflectionProbeInfo[probe_len];

                for (int i = 0; i < probe_len; i++) {
                    EditorUtility.DisplayProgressBar(
                        "Slightmapper Runtime State Importer",
                        "Gathering Reflection Probe data...",
                        (float)i / probe_len
                    );
                    
                    ReflectionProbe element = probes[i];

                    if (!element.TryGetComponent<RendererId>(out _)) {
                        element.gameObject.AddComponent<RendererId>();
                    }

                    ReflectionProbeInfo val = new(element);

                    string baked_texture_path = AssetDatabase.GetAssetPath(val.bakedTexture);
                    string baked_texture_filename = Path.GetFileName(baked_texture_path);
                    AssetDatabase.MoveAsset(baked_texture_path, path + baked_texture_filename);

                    probe_info[i] = val;
                }

                state.reflectionProbeData = probe_info;

                EditorUtility.DisplayProgressBar(
                    "Slightmapper Runtime State Importer",
                    "Writing data...",
                    1
                );

                state.staticRendererData = renderer_info;

                EditorUtility.SetDirty(state);
                AssetDatabase.SaveAssetIfDirty(state);
                AssetDatabase.Refresh();
            } catch (Exception ex) {
                EditorUtility.DisplayDialog("An error occurred!", ex.Message, "ok");
                throw;
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }

        private void ImportRuntimeState() {
            try {
                EditorUtility.DisplayProgressBar(
                    "Slightmapper Runtime State Importer",
                    "Importing runtime state...",
                    0   
                );
                SlightmapperRuntimeState state_asset = CreateInstance<SlightmapperRuntimeState>();

                state_asset.lightmaps       = LightmapSettings.lightmaps.Select(x => new LightmapReference(x)).ToArray();
                state_asset.lightmapsMode   = LightmapSettings.lightmapsMode;
                state_asset.lightProbes     = LightmapSettings.lightProbes;

                EditorUtility.DisplayProgressBar(
                    "Slightmapper Runtime State Importer",
                    "Finding mesh renderers...",
                    0.1f   
                );

                MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>(true);

                int renderers_len = renderers.Length;
                RendererInfo[] renderer_info = new RendererInfo[renderers_len];

                for (int i = 0; i < renderers_len; i++) {
                    EditorUtility.DisplayProgressBar(
                        "Slightmapper Runtime State Importer",
                        "Gathering renderer data...",
                        (float)i / renderers_len
                    );

                    MeshRenderer element = renderers[i];

                    if (!element.TryGetComponent<RendererId>(out _)) {
                        element.gameObject.AddComponent<RendererId>();
                    }

                    renderer_info[i] = new RendererInfo(element);
                }

                ReflectionProbe[] probes = FindObjectsOfType<ReflectionProbe>(true);

                int probe_len = probes.Length;
                ReflectionProbeInfo[] probe_info = new ReflectionProbeInfo[probe_len];

                for (int i = 0; i < probe_len; i++) {
                    EditorUtility.DisplayProgressBar(
                        "Slightmapper Runtime State Importer",
                        "Gathering Reflection Probe data...",
                        (float)i / probe_len
                    );
                    
                    ReflectionProbe element = probes[i];

                    if (!element.TryGetComponent<RendererId>(out _)) {
                        element.gameObject.AddComponent<RendererId>();
                    }

					probe_info[i] = new ReflectionProbeInfo(element);
                }

                state_asset.reflectionProbeData = probe_info;

                EditorUtility.DisplayProgressBar(
                    "Slightmapper Runtime State Importer",
                    "Writing data...",
                    1
                );

                state_asset.staticRendererData = renderer_info;
                
                Scene scene = SceneManager.GetActiveScene();
                string scene_data_path = $"{Path.GetDirectoryName(scene.path)}/{scene.name}";

                if (!AssetDatabase.IsValidFolder($"{scene_data_path}/{importedStateName}"))
                    AssetDatabase.CreateFolder(scene_data_path, importedStateName);

                AssetDatabase.CreateAsset(state_asset, $"{scene_data_path}/{importedStateName}/{importedStateName}.asset");

                string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(state_asset)) + '/';
                for (int i = 0; i < state_asset.lightmaps.Length; i++) {
                    LightmapReference reference = state_asset.lightmaps[i];


                    if (reference.lightmapColor) {
                        string lm_color_path = AssetDatabase.GetAssetPath(reference.lightmapColor);
                        string filename = Path.GetFileName(lm_color_path);
                        AssetDatabase.MoveAsset(lm_color_path, path + filename);
                    }

                    if (reference.lightmapDir) {
                        string lm_dir_path = AssetDatabase.GetAssetPath(reference.lightmapDir);
                        string filename = Path.GetFileName(lm_dir_path);
                        AssetDatabase.MoveAsset(lm_dir_path, path + filename);
                    }

                    if (reference.shadowMask) {
                        string shadow_mask_path = AssetDatabase.GetAssetPath(reference.shadowMask);
                        string filename = Path.GetFileName(shadow_mask_path);
                        AssetDatabase.MoveAsset(shadow_mask_path, path + filename);
                    }
                }

                for (int i = 0; i < probe_info.Length; i++) {
                    string baked_texture_path = AssetDatabase.GetAssetPath(probe_info[i].bakedTexture);
                    string baked_texture_filename = Path.GetFileName(baked_texture_path);
                    AssetDatabase.MoveAsset(baked_texture_path, path + baked_texture_filename);
                }

                string lighting_data_path = AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset);
                string lighting_data_filename = Path.GetFileName(lighting_data_path);
                AssetDatabase.MoveAsset(lighting_data_path, path + lighting_data_filename);

                AssetDatabase.Refresh();
            } catch (Exception ex) {
                EditorUtility.DisplayDialog("An error occurred!", ex.Message, "ok");
                Debug.LogException(ex);
                throw;
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }

        private void LoadBakeState(SlightmapperBakeState state) {
            try {
                int light_loop_len = state.LightSettings.Length;
                for (int i = 0; i < light_loop_len; i++) {
                    EditorUtility.DisplayProgressBar(
                        "Slightmapper Bake Config Applier",
                        "Loading bake config...",
                        (float)i / light_loop_len
                    );

                    ComptimeLightInfo info = state.LightSettings[i];

                    Debug.Assert(GlobalObjectId.TryParse(info.ObjectId, out GlobalObjectId id));

                    if (GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is not Light L) continue;

                    L.gameObject.SetActive(info.Enabled);

                    L.color = info.Color;
                    L.range = info.Range;
                    L.intensity = info.Intensity;
                    L.bounceIntensity = info.IndirectMultiplier;
                }
            } catch (Exception ex) {
                EditorUtility.DisplayDialog("An error occurred!", ex.Message, "ok");
                Debug.LogException(ex);
                throw;
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }

        private SlightmapperBakeState ImportCurrentBakeState() {
            try {
                EditorUtility.DisplayProgressBar(
                    "Slightmapper Bake Config Importer",
                    "Fetching light states...",
                    1/3
                );

                ComptimeLightInfo[] light_cfg = FindObjectsOfType<Light>(true)
                    .Where(l => l.bakingOutput.isBaked || l.bakingOutput.lightmapBakeType != LightmapBakeType.Realtime)
                    .Select(l => new ComptimeLightInfo() {
                        ObjectId = GlobalObjectId.GetGlobalObjectIdSlow(l).ToString(),
                        Enabled = l.gameObject.activeSelf && l.enabled,

                        Color = l.color,

                        Range = l.range,
                        Intensity = l.intensity,
                        IndirectMultiplier = l.bounceIntensity
                    })
                    .ToArray();
                
                EditorUtility.DisplayProgressBar(
                    "Slightmapper Bake Config Importer",
                    "Writing bake state to config object...",
                    2/3
                );

                SlightmapperBakeState bake_state = new() {
                    Name = importedStateName,

                    LightSettings = light_cfg,
                };

                return bake_state;
            } catch (Exception ex) {
                EditorUtility.DisplayDialog("An error occurred!", ex.Message, "ok");
                Debug.LogException(ex);
                throw;
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }

        private void OverwriteBakeState(int index) {
            SlightmapperBakeState bake_state = ImportCurrentBakeState();

            Debug.Assert(editingBakeState);
            editingBakeState.BaketimeStates[index] = bake_state;
        }

        private void ImportLightConfigAsBakeState() {
            try {
                SlightmapperBakeState bake_state = ImportCurrentBakeState();

                Debug.Assert(editingBakeState);
                editingBakeState.BaketimeStates ??= new List<SlightmapperBakeState>();
                editingBakeState.BaketimeStates.Add(bake_state);
                EditorUtility.SetDirty(editingBakeState);

                AssetDatabase.SaveAssets();
            } catch (Exception ex) {
                EditorUtility.DisplayDialog("An error occurred!", ex.Message, "ok");
                Debug.LogException(ex);
                throw;
            } finally {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}

#endif
