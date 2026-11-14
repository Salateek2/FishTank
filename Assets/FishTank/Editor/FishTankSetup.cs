using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FishTank.EditorTools
{
    /// <summary>
    /// FishTank > Build Game: turns the fish models into normalised, animated prefabs,
    /// fills the fish database and (re)builds the playable scene.
    /// Runs once automatically the first time the project sees it.
    /// </summary>
    [InitializeOnLoad]
    public static class FishTankSetup
    {
        const string Root = "Assets/FishTank";
        const string ModelDir = Root + "/Models/Fish";
        const string PrefabDir = Root + "/Prefabs/Fish";
        const string AnimDir = Root + "/Animation";
        const string MaterialDir = Root + "/Materials";
        const string DatabasePath = Root + "/FishDatabase.asset";
        const string ScenePath = "Assets/Scenes/FishTank.unity";
        const string PlayerSpecies = "Clownfish";

        // Raise this whenever the generated scenery changes: the scene is then rebuilt automatically.
        const int SceneVersion = 3;
        const string SceneVersionPath = "UserSettings/FishTankSceneVersion.txt";

        static readonly Color WaterColor = new Color(0.04f, 0.33f, 0.52f);

        // Typical body length of each species in world units. The player starts at 0.8 and wins at 8.
        static readonly Dictionary<string, float> SpeciesSize = new Dictionary<string, float>
        {
            { "Tetra", 0.5f }, { "CardinalFish", 0.6f }, { "RoyalGramma", 0.6f }, { "MandarinFish", 0.65f },
            { "ZebraClownFish", 0.75f }, { "Clownfish", 0.8f }, { "Betta", 0.85f }, { "Goldfish", 0.9f },
            { "BlueGoldfish", 0.95f }, { "YellowTang", 1.0f }, { "BlueTang", 1.1f }, { "Tang", 1.15f },
            { "ButterflyFish", 1.2f }, { "MoorishIdol", 1.3f }, { "Cowfish", 1.35f }, { "Puffer", 1.4f },
            { "Piranha", 1.6f }, { "Lionfish", 1.8f }, { "BlackLionFish", 1.9f }, { "Blobfish", 2.0f },
            { "FlowerHorn", 2.05f }, { "ParrotFish", 2.1f }, { "Koi", 2.2f }, { "RedSnapper", 2.3f },
            { "ArmoredCatfish", 2.4f }, { "Flatfish", 2.45f }, { "Turbot", 2.5f }, { "Anglerfish", 2.6f },
            { "CoralGrouper", 3.0f }, { "Humphead", 3.5f }, { "Tuna", 4.0f }, { "Sunfish", 4.5f },
            { "Swordfish", 5.0f }, { "GoblinShark", 6.0f }, { "Shark", 7.0f },
        };

        static int SavedSceneVersion()
        {
            return File.Exists(SceneVersionPath) && int.TryParse(File.ReadAllText(SceneVersionPath), out int v) ? v : 0;
        }

        static FishTankSetup()
        {
            EditorApplication.delayCall += AutoBuildOnce;
        }

        static void AutoBuildOnce()
        {
            const string key = "FishTank.AutoBuildTried.v2";
            if (SessionState.GetBool(key, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Directory.Exists(ModelDir)) return;

            // The scene is entirely generated, so when the scenery code is newer than the scene on
            // disk the scene is simply built again.
            bool missing = !File.Exists(ScenePath);
            bool outdated = !missing && SavedSceneVersion() != SceneVersion;
            if (!missing && !outdated) return;
            SessionState.SetBool(key, true);

            if (outdated)
            {
                try
                {
                    if (SceneManager.GetActiveScene().isDirty)
                        Debug.Log("FishTank: the world has new scenery. Your open scene has unsaved changes, " +
                                  "so run FishTank > Rebuild Scene Only to see it.");
                    else
                        BuildScene();
                }
                catch (Exception e)
                {
                    Debug.LogError("FishTank: could not rebuild the scene, run FishTank > Rebuild Scene Only.\n" + e);
                }
                return;
            }

            try
            {
                if (SceneManager.GetActiveScene().isDirty)
                {
                    BuildAssets();
                    Debug.Log("FishTank: fish prefabs are ready. Your open scene has unsaved changes, " +
                              "so run FishTank > Build Game to create the game scene.");
                }
                else
                {
                    BuildAssets();
                    BuildScene();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("FishTank: automatic setup failed, run FishTank > Build Game to retry.\n" + e);
            }
        }

        [MenuItem("FishTank/Build Game")]
        public static void BuildGame()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("FishTank: stop Play mode before building the game.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildAssets();
            BuildScene();
        }

        [MenuItem("FishTank/Rebuild Scene Only")]
        public static void RebuildScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("FishTank: stop Play mode before rebuilding the scene.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        [MenuItem("FishTank/Rebuild Fish Prefabs Only")]
        public static void BuildAssets()
        {
            EnsureFolder(PrefabDir);
            EnsureFolder(AnimDir);
            EnsureFolder(MaterialDir);

            var prefabs = new List<Fish>();
            string[] models = Directory.GetFiles(ModelDir, "*.fbx").Select(p => p.Replace('\\', '/')).ToArray();
            try
            {
                for (int i = 0; i < models.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("FishTank", "Preparing " + Path.GetFileName(models[i]),
                        (float)i / models.Length);
                    Fish fish = BuildFishPrefab(models[i]);
                    if (fish != null) prefabs.Add(fish);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var database = AssetDatabase.LoadAssetAtPath<FishDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<FishDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            database.fish = prefabs.OrderBy(f => f.baseSize).ToArray();
            database.playerPrefab = prefabs.FirstOrDefault(f => f.speciesName == PlayerSpecies)
                                    ?? database.fish.FirstOrDefault();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log("FishTank: built " + prefabs.Count + " fish prefabs.");
        }

        // ---------------------------------------------------------------- fish prefabs

        static Fish BuildFishPrefab(string modelPath)
        {
            string species = Path.GetFileNameWithoutExtension(modelPath);
            ConfigureImporter(modelPath);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogWarning("FishTank: could not load " + modelPath);
                return null;
            }

            var root = new GameObject(species);
            try
            {
                var pivot = new GameObject("Pivot").transform;
                pivot.SetParent(root.transform, false);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.transform.SetParent(pivot, false);

                // Measure the model in its rest pose (root and pivot are still at identity).
                var points = new List<Vector3>();
                var eyePoints = new List<Vector3>();
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    CollectPoints(renderer, points, eyePoints);
                    if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                }

                if (points.Count == 0)
                {
                    Debug.LogWarning("FishTank: " + species + " has no mesh, skipped.");
                    return null;
                }

                Vector3 forward = FindForward(instance.transform, points, eyePoints);
                Vector3 up = Mathf.Abs(forward.y) > 0.5f ? Vector3.forward : Vector3.up;
                Quaternion rotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up));

                var bounds = new Bounds(rotation * points[0], Vector3.zero);
                foreach (var p in points) bounds.Encapsulate(rotation * p);
                float scale = 1f / Mathf.Max(bounds.size.z, 1e-5f);

                // Nose along +Z, one unit long, centred on the root.
                pivot.localRotation = rotation;
                pivot.localScale = Vector3.one * scale;
                pivot.localPosition = -bounds.center * scale;

                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.runtimeAnimatorController = GetOrCreateController(species, modelPath);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var fish = root.AddComponent<Fish>();
                fish.speciesName = species;
                fish.baseSize = SpeciesSize.TryGetValue(species, out float size) ? size : 1.5f;
                fish.bodyHalfHeight = Mathf.Clamp(bounds.size.y * scale * 0.5f, 0.1f, 0.6f);
                fish.animator = animator;

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/" + species + ".prefab");
                return prefab.GetComponent<Fish>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void ConfigureImporter(string modelPath)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) return;

            bool changed = false;
            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }

            if (importer.clipAnimations == null || importer.clipAnimations.Length == 0)
            {
                // Clips arrive as "Fish_Armature|Swimming_Normal": shorten the names and loop the swims.
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    int bar = clip.name.LastIndexOf('|');
                    if (bar >= 0) clip.name = clip.name.Substring(bar + 1);
                    clip.loopTime = clip.name.StartsWith("Swimming", StringComparison.OrdinalIgnoreCase);
                }

                if (clips.Length > 0)
                {
                    importer.clipAnimations = clips;
                    changed = true;
                }
            }

            if (changed) importer.SaveAndReimport();
        }

        static RuntimeAnimatorController GetOrCreateController(string species, string modelPath)
        {
            string path = AnimDir + "/" + species + ".controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (existing != null) return existing;

            var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            AnimationClip swim = clips.FirstOrDefault(c => c.name.EndsWith("Swimming_Normal"))
                                 ?? clips.FirstOrDefault(c => c.name.Contains("Swimming"));
            AnimationClip attack = clips.FirstOrDefault(c => c.name.EndsWith("Attack"));
            if (swim == null)
            {
                Debug.LogWarning("FishTank: no swim animation found in " + modelPath);
                return null;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "SwimSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            });

            var machine = controller.layers[0].stateMachine;
            var swimState = machine.AddState("Swim");
            swimState.motion = swim;
            swimState.speedParameterActive = true;
            swimState.speedParameter = "SwimSpeed";
            machine.defaultState = swimState;

            if (attack != null)
            {
                var attackState = machine.AddState("Attack");
                attackState.motion = attack;
                var back = attackState.AddTransition(swimState);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.hasFixedDuration = true;
                back.duration = 0.12f;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void CollectPoints(Renderer renderer, List<Vector3> points, List<Vector3> eyePoints)
        {
            Mesh mesh = null;
            Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;

            if (renderer is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
                // Rest-pose position of a skinned vertex = bone * bindpose * vertex.
                if (mesh != null && skinned.bones.Length > 0 && mesh.bindposes.Length > 0 && skinned.bones[0] != null)
                    toWorld = skinned.bones[0].localToWorldMatrix * mesh.bindposes[0];
            }
            else
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }

            if (mesh == null) return;

            Vector3[] vertices = mesh.vertices;
            var world = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                world[i] = toWorld.MultiplyPoint3x4(vertices[i]);
                points.Add(world[i]);
            }

            Material[] materials = renderer.sharedMaterials;
            for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
            {
                if (materials[sub] == null || !materials[sub].name.StartsWith("Eyes")) continue;
                foreach (int index in mesh.GetTriangles(sub)) eyePoints.Add(world[index]);
            }
        }

        // The spine bones give the body axis; the eyes tell us which end is the head.
        static Vector3 FindForward(Transform model, List<Vector3> points, List<Vector3> eyePoints)
        {
            Transform head = FindDeep(model, "Main1");
            Transform tail = FindDeep(model, "Main6_end") ?? FindDeep(model, "Main6");

            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) bounds.Encapsulate(p);

            Vector3 spine = head != null && tail != null ? head.position - tail.position : Vector3.zero;
            Vector3 axis;
            if (spine.sqrMagnitude > 1e-8f)
            {
                axis = DominantAxis(spine);
            }
            else
            {
                Vector3 s = bounds.size;
                axis = s.x >= s.y && s.x >= s.z ? Vector3.right : (s.z >= s.y ? Vector3.forward : Vector3.up);
            }

            if (eyePoints.Count > 0)
            {
                Vector3 eyes = Vector3.zero;
                foreach (var p in eyePoints) eyes += p;
                eyes /= eyePoints.Count;

                float side = Vector3.Dot(eyes - bounds.center, axis);
                float length = Mathf.Abs(Vector3.Dot(bounds.size, axis));
                if (Mathf.Abs(side) > length * 0.02f) return side > 0f ? axis : -axis;
            }

            return axis;
        }

        static Vector3 DominantAxis(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            if (x >= y && x >= z) return new Vector3(Mathf.Sign(v.x), 0f, 0f);
            if (y >= z) return new Vector3(0f, Mathf.Sign(v.y), 0f);
            return new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }

            return null;
        }

        // ---------------------------------------------------------------- scene

        static void BuildScene()
        {
            var database = AssetDatabase.LoadAssetAtPath<FishDatabase>(DatabasePath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = WaterColor;
            RenderSettings.fogStartDistance = 20f;
            RenderSettings.fogEndDistance = 70f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.62f, 0.72f);

            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = WaterColor;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 400f;
            cameraGo.transform.position = new Vector3(0f, 0f, -18f);
            var rig = cameraGo.AddComponent<CameraRig>();

            var lightGo = new GameObject("Sun", typeof(Light));
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.85f, 0.97f, 1f);
            light.intensity = 1.4f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

            var gameGo = new GameObject("Game");
            var manager = gameGo.AddComponent<GameManager>();
            manager.database = database;
            manager.cameraRig = rig;
            gameGo.AddComponent<FishSpawner>();

            var mood = cameraGo.AddComponent<WaterMood>();
            mood.sun = light;
            mood.sunIntensity = light.intensity;

            // Bloom makes the glowing things in the cave and the abyss actually glow.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = GetPostProfile();

            WorldBuilder.Build();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            File.WriteAllText(SceneVersionPath, SceneVersion.ToString());
            Debug.Log("FishTank: scene built at " + ScenePath + ". Press Play!");
        }

        static VolumeProfile GetPostProfile()
        {
            const string path = Root + "/WaterPost.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.5f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.7f);
            AssetDatabase.AddObjectToAsset(bloom, profile);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.5f);
            AssetDatabase.AddObjectToAsset(vignette, profile);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        internal static Transform MakePrimitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
