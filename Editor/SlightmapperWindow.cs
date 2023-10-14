#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using SubsurfaceStudios.Slightmapper.Assets;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;
using SubsurfaceStudios.Slightmapper.Global;
using System.Collections.Generic;
using System;

namespace SubsurfaceStudios.Slightmapper.Editor {
    [EditorWindowTitle(title = "SLightmapper", icon = "Lighting")]
    public class SlightmapperWindow : EditorWindow {
        [MenuItem("Window/Rendering/SLightmapper Window")]
        public static void ShowSlightmapperWindow() {
			SlightmapperWindow win = GetWindow<SlightmapperWindow>();
			SceneManager.activeSceneChanged += win.OnSceneSwitch;
        }

		private string GetSceneDataFolderPath() {
			Scene current = SceneManager.GetActiveScene();
            return Path.Join(Path.GetDirectoryName(current.path), current.name);
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

        private void OnSceneSwitch(Scene a, Scene b) {
            editingBakeState = null;
        }

        public void OnDestroy() {
            SceneManager.activeSceneChanged -= OnSceneSwitch;
        }

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

            bool emptyName = string.IsNullOrEmpty(importedStateName) || string.IsNullOrWhiteSpace(importedStateName); 
            if(emptyName) {
                EditorGUILayout.HelpBox(
                    "You must specify a name for the imported state.",
                    MessageType.Error
                );
            }

            bool stateExists = editingBakeState.BaketimeStates.Any(x => x.Name == importedStateName);
            if(stateExists) {
                EditorGUILayout.HelpBox(
                    "A bake configuration with this name already exists.",
                    MessageType.Error
                );
            }
            
            EditorGUILayout.HelpBox(
                "This will import the scene's current lighting state "+
                "(lightmaps, reflection probes, light probes, etc) as "+
                "a Runtime State (loadable at runtime) and a Bake State "+
                "(loaded before rebaking the Runtime State).",
                MessageType.Info
            );
            
            EditorGUI.BeginDisabledGroup(emptyName || stateExists);
            if (GUILayout.Button("Import my current scene lighting configuration.")) {
                importedStateName = importedStateName.Trim();
                
                SlightmapperRuntimeState rs = ImportRuntimeState();
                ImportLightConfigAsBakeState(rs);
            }
            EditorGUI.EndDisabledGroup();
            
            GUIHorizontalLine();

            List<SlightmapperBakeState> states = editingBakeState.BaketimeStates;
            int states_count = states.Count;

			if (states_count <= 0) {
				EditorGUILayout.HelpBox(
					"In order to edit a state, you need to have at least one already present.\n" +
					"To create one, set up your scene lighting how you want it, and then press the " +
					"\"Import\" button above.",
					MessageType.Warning
				);
                return;
			}

            GUILayout.Label("Currently editing baking configuration:");

            string[] state_names = new string[states_count];
            int[] state_indices = new int[states_count];

            for (int i = 0; i < states_count; i++)
            {
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

            SlightmapperBakeState bakeState = states[bakeStateIndex];

            GUILayout.Space(EditorGUIUtility.singleLineHeight);

            EditorGUILayout.HelpBox(
                "This will load the lighting configuration and lightmaps stored in this asset.\n"+
                "BE ADVISED - This will overwrite the lighting configuration in your scene! Make "+
                "sure to save your current lighting config before performing this action.",
                MessageType.Warning
            );
            if (GUILayout.Button("Load lighting state"))
            {
                LoadBakeState(bakeState);
                LoadRuntimeState(bakeState.RuntimeState);
                Lightmapping.lightingDataAsset = bakeState.DataAsset;
            }

            EditorGUILayout.HelpBox(
                "This will overwrite the lighting configuration and lightmaps stored in this state.\n"+
                "Be careful!",
                MessageType.Warning
            );
            if (GUILayout.Button("Overwrite lighting state"))
            {
                OverwriteBakeState(bakeStateIndex);
                OverwriteRuntimeState(bakeState.RuntimeState);
            }

            if (GUILayout.Button("Rebake lighting"))
            {
                try {
                    EditorUtility.DisplayProgressBar("Slightmapper Bake Util", "Loading lighting configuration...", 0);
                    LoadBakeState(bakeState);
                    LoadRuntimeState(bakeState.RuntimeState);
                    
                    EditorUtility.DisplayProgressBar("Slightmapper Bake Util", "Prepping for bake...", 0.1f);
                    Lightmapping.lightingDataAsset = null;

                    EditorUtility.DisplayProgressBar("Slightmapper Bake Util", "Baking lights...", 0.5f);

                    void OnBakeFinished() {
                        Lightmapping.bakeCompleted -= OnBakeFinished;

                        EditorUtility.DisplayProgressBar("Slightmapper Bake Util", "Finalizing bake...", 0.75f);
                        OverwriteRuntimeState(bakeState.RuntimeState);

                        if (bakeState.DataAsset != null)
                            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(bakeState.DataAsset));

                        string dataAssetPath = AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset);
                        string dataAssetName = Path.GetFileName(dataAssetPath);
                        string dataAssetDestinationPath = Path.Join(GetSceneDataFolderPath(), bakeState.Name, dataAssetName);

                        AssetDatabase.MoveAsset(dataAssetPath, dataAssetDestinationPath);

                        bakeState.DataAsset = Lightmapping.lightingDataAsset;
                        EditorUtility.ClearProgressBar();
                    }

                    Lightmapping.bakeCompleted += OnBakeFinished;
                    Lightmapping.BakeAsync();
                } catch (Exception ex) {
                    Debug.LogException(ex);
                } finally {
                    EditorUtility.ClearProgressBar();
                }
            }
            
            GUIStyle bold = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };
            
            if (GUILayout.Button("Delete Configuration", bold))
            {
                if (EditorUtility.DisplayDialog(
                    "Are you sure?",
                    "This will delete the bake configuration and runtime state asset. This action cannot be undone.",
                    "Yes",
                    "No"
                ))
                {
                    int delete_index = bakeStateIndex;
                    SlightmapperBakeState delete_state = editingBakeState.BaketimeStates[delete_index];

                    editingBakeState = null;
                    
                    EditorUtility.DisplayProgressBar("Slightmapper Bake Util", "Deleting bake configuration...", 0);
                    
                    // Delete the configuration folder
                    string data_path = GetSceneDataFolderPath();
                    string config_path = Path.Join(data_path, delete_state.Name);
                    
                    AssetDatabase.DeleteAsset(config_path);
                    
                    // Remove from Slightmapper Data
                    states.RemoveAt(delete_index);
                    
                    EditorUtility.ClearProgressBar();
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

                for (int i = 0; i < state.lightmaps.Length; i++) {
                    LightmapReference reference = state.lightmaps[i];

                    if (reference.lightmapColor != null)
                        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(reference.lightmapColor));

                    if (reference.lightmapDir != null)
                        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(reference.lightmapDir));

                    if (reference.shadowMask != null)
                        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(reference.shadowMask));
                }

                for (int i = 0; i < state.reflectionProbeData.Length; i++) {
                    ReflectionProbeInfo info = state.reflectionProbeData[i];

                    if (info.bakedTexture != null)
                        AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(info.bakedTexture));
                }

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

        private SlightmapperRuntimeState ImportRuntimeState() {
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
                string scene_data_path = Path.Join(Path.GetDirectoryName(scene.path), scene.name);
                
                string asset_dir = Path.Join(scene_data_path, importedStateName);
                if (!AssetDatabase.IsValidFolder(asset_dir))
                    AssetDatabase.CreateFolder(scene_data_path, importedStateName);
                
                string asset_path = Path.Join(scene_data_path, importedStateName, $"{importedStateName}.asset");
                AssetDatabase.CreateAsset(state_asset, asset_path);
                
                string path = Path.Join(Path.GetDirectoryName(AssetDatabase.GetAssetPath(state_asset)), "/");
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
                return state_asset;
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

                    DataAsset = Lightmapping.lightingDataAsset,
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
            
            SlightmapperBakeState old = editingBakeState.BaketimeStates[index];

            if (old.DataAsset != null)
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(old.DataAsset));

            bake_state.Name = old.Name;
            bake_state.RuntimeState = old.RuntimeState;
            editingBakeState.BaketimeStates[index] = bake_state;
        }

        private void ImportLightConfigAsBakeState(SlightmapperRuntimeState rs) {
            try {
                SlightmapperBakeState bake_state = ImportCurrentBakeState();
                bake_state.RuntimeState = rs;

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
