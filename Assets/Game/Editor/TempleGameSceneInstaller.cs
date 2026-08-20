using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TempleCircuit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace TempleCircuitEditor
{
    public static class TempleGameSceneInstaller
    {
        const string RuntimeRootName = "TempleGame_Runtime";
        const string AssetRoot = "Assets/模型资产/手绘低多边形中国佛寺幻想微缩沙盘风";
        static readonly string[] TileFolders = { "/41/", "/42/", "/43/", "/44/" };
        static readonly string[] PawnFolders = { "/32/", "/33/", "/34/", "/35/" };
        // Keep the ownership flag order identical to PlayerColors/PawnFolders:
        // yellow, green, red, purple. The source asset folders are not ordered
        // by player color, so the red flag (30) belongs to player three.
        static readonly string[] FlagFolders = { "/38/", "/39/", "/30/", "/40/" };

        [MenuItem("Tools/香火争渡/安装完整游戏到当前场景")]
        public static void Install()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                throw new InvalidOperationException("No active scene is available.");

            var tileRoots = FindPrefabRoots(path => TileFolders.Any(path.Contains));
            if (tileRoots.Count != 32)
                throw new InvalidOperationException($"Expected 32 colored path tiles from folders 41-44, but found {tileRoots.Count}.");

            var orderedTiles = SortPathTiles(tileRoots);
            var boardCenter = orderedTiles.Aggregate(Vector3.zero, (sum, tile) => sum + tile.transform.position) / orderedTiles.Count;
            var tileSpan = EstimateTileSpan(orderedTiles);

            var oldRoot = GameObject.Find(RuntimeRootName);
            if (oldRoot != null)
                Undo.DestroyObjectImmediate(oldRoot);

            var runtimeRoot = new GameObject(RuntimeRootName);
            Undo.RegisterCreatedObjectUndo(runtimeRoot, "Install Temple Circuit Game");
            runtimeRoot.transform.position = boardCenter;
            var game = runtimeRoot.AddComponent<TempleCircuitGame>();
            var hud = runtimeRoot.AddComponent<TempleGameHUD>();
            var audio = runtimeRoot.AddComponent<TempleAudioManager>();
            game.hud = hud;
            game.audioManager = audio;
            ConfigureAudio(audio);

            var tileComponents = new TempleBoardTile[orderedTiles.Count];
            for (var i = 0; i < orderedTiles.Count; i++)
            {
                var root = orderedTiles[i];
                var marker = root.GetComponent<TempleBoardTile>() ?? Undo.AddComponent<TempleBoardTile>(root);
                marker.index = i;
                marker.category = CategoryForPath(GetPrefabAssetPath(root));
                tileComponents[i] = marker;
            }
            game.pathTiles = tileComponents;

            ConfigurePathMotion(orderedTiles, tileSpan);
            ConfigurePeripheralMotion(orderedTiles, boardCenter, tileSpan);
            game.pawnFollowers = CreatePlayers(runtimeRoot.transform, tileComponents[0], tileSpan);
            ConfigureOwnershipFlagSetup(game);

            var diceRoot = FindPrefabRoots(path => path.Contains("骰子/")).OrderBy(root => Vector3.Distance(root.transform.position, boardCenter)).FirstOrDefault();
            if (diceRoot == null)
                throw new InvalidOperationException("The dice model from the dice folder is not present in the scene.");
            EnsureBoxCollider(diceRoot);
            var dice = diceRoot.GetComponent<TempleDiceInteractor>() ?? Undo.AddComponent<TempleDiceInteractor>(diceRoot);
            dice.game = game;
            dice.SnapToSupportSurface();
            dice.CaptureRestPose();
            game.dice = dice;

            RemoveExistingXrRigs();
            var xrRoot = new GameObject("PICO_XR_TabletopRig");
            Undo.RegisterCreatedObjectUndo(xrRoot, "Create PICO tabletop rig");
            var xrRig = xrRoot.AddComponent<TempleXRTabletopRig>();
            xrRig.game = game;
            xrRig.dice = dice;

            var startScreen = runtimeRoot.AddComponent<TempleStartScreen>();
            startScreen.game = game;
            startScreen.rig = xrRig;
            startScreen.coverTexture = ImportStartCoverTexture();
            xrRig.startScreen = startScreen;

            EnsureEventSystem();
            hud.panelSprite = ImportGeneratedPanelSprite();
            hud.topHudSprite = ImportGeneratedTopHudSprite();
            hud.uiFont = ImportBundledUiFont();
            CreateWaterSurface(runtimeRoot.transform, boardCenter, tileSpan);

            EditorUtility.SetDirty(game);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(dice);
            EditorUtility.SetDirty(xrRig);
            EditorUtility.SetDirty(startScreen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = runtimeRoot;
            Debug.Log($"Temple Circuit installed: {tileComponents.Length} path tiles, 4 pawns, ownership flags, central fixed-scale dice, PICO XR tabletop rig and Meadow Forest water.");
        }

        [MenuItem("Tools/香火争渡/修复 UI、骰子与观看位置")]
        public static void RepairPresentation()
        {
            var scene = SceneManager.GetActiveScene();
            var game = UnityEngine.Object.FindAnyObjectByType<TempleCircuitGame>();
            var dice = UnityEngine.Object.FindAnyObjectByType<TempleDiceInteractor>();
            var rig = RemoveDuplicateXrRigs();
            var hud = UnityEngine.Object.FindAnyObjectByType<TempleGameHUD>();
            if (game == null || dice == null || rig == null || hud == null)
                throw new InvalidOperationException("The installed Temple Circuit runtime objects are incomplete.");

            var audio = game.GetComponent<TempleAudioManager>() ?? Undo.AddComponent<TempleAudioManager>(game.gameObject);
            var startScreen = game.GetComponent<TempleStartScreen>() ?? Undo.AddComponent<TempleStartScreen>(game.gameObject);

            Undo.RecordObjects(new UnityEngine.Object[] { game, dice, rig, hud, dice.transform }, "Repair Temple presentation");
            game.dice = dice;
            game.hud = hud;
            game.audioManager = audio;
            ConfigureAudio(audio);
            dice.game = game;
            dice.SnapToSupportSurface();
            dice.CaptureRestPose();
            rig.game = game;
            rig.dice = dice;
            rig.startScreen = startScreen;
            startScreen.game = game;
            startScreen.rig = rig;
            startScreen.coverTexture = ImportStartCoverTexture();
            hud.panelSprite = ImportGeneratedPanelSprite();
            hud.topHudSprite = ImportGeneratedTopHudSprite();
            hud.uiFont = ImportBundledUiFont();
            ConfigureOwnershipFlagSetup(game);
            var tileRoots = game.pathTiles.Where(tile => tile != null).Select(tile => tile.gameObject).ToList();
            if (tileRoots.Count > 1)
            {
                var boardCenter = tileRoots.Aggregate(Vector3.zero, (sum, root) => sum + root.transform.position) / tileRoots.Count;
                var tileSpan = EstimateTileSpan(tileRoots);
                ConfigurePathMotion(tileRoots, tileSpan);
                ConfigurePeripheralMotion(tileRoots, boardCenter, tileSpan);
            }

            EditorUtility.SetDirty(game);
            EditorUtility.SetDirty(dice);
            EditorUtility.SetDirty(rig);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(audio);
            EditorUtility.SetDirty(startScreen);
            EditorUtility.SetDirty(dice.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Temple presentation repaired: stable camera reference, head-locked player HUD, dice spatial UI and dice surface placement.");
        }

        [MenuItem("Tools/香火争渡/更新 16:9 启动封面")]
        public static void RefreshStartScreenInSavedScene()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var game = UnityEngine.Object.FindAnyObjectByType<TempleCircuitGame>();
            var rig = UnityEngine.Object.FindAnyObjectByType<TempleXRTabletopRig>();
            if (game == null || rig == null)
                throw new InvalidOperationException("The saved game scene is missing its Temple Circuit runtime or PICO tabletop rig.");

            var startScreen = game.GetComponent<TempleStartScreen>() ?? Undo.AddComponent<TempleStartScreen>(game.gameObject);
            startScreen.game = game;
            startScreen.rig = rig;
            startScreen.coverTexture = ImportStartCoverTexture();
            rig.startScreen = startScreen;

            EditorUtility.SetDirty(game);
            EditorUtility.SetDirty(rig);
            EditorUtility.SetDirty(startScreen);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = startScreen.gameObject;
            Debug.Log("Temple 16:9 start cover is assigned to the saved scene.");
        }

        static void ConfigureAudio(TempleAudioManager audio)
        {
            if (audio == null)
                return;

            audio.backgroundMusic = LoadAudioClip("Assets/Game/Audio/Temple_BGM_Chinese_Quiet.mp3");
            audio.diceRoll = LoadAudioClip("Assets/Game/Audio/Temple_SFX_Dice_Roll.mp3");
            audio.diceLand = LoadAudioClip("Assets/Game/Audio/Temple_SFX_Dice_Land.mp3");
            audio.claimChime = LoadAudioClip("Assets/Game/Audio/Temple_SFX_Claim_Chime.mp3");
            audio.pawnStep = LoadAudioClip("Assets/Game/Audio/Temple_SFX_Stone_Click.mp3");
            audio.uiClick = audio.pawnStep;
            audio.musicVolume = 0.32f;
            audio.diceRollVolume = 0.65f;
            audio.diceLandVolume = 0.8f;
            audio.pawnStepVolume = 0.2f;
            audio.claimVolume = 0.7f;
            audio.uiVolume = 0.32f;
        }

        static AudioClip LoadAudioClip(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }

        [MenuItem("Tools/香火争渡/修复 POLYGON 素材粉色材质")]
        public static void RepairPolygonNatureBiomesMaterials()
        {
            const string packagePath = "Assets/PolygonNatureBiomes/URP_ExtractMe/Unity_2022.unitypackage";
            if (!File.Exists(packagePath))
                throw new FileNotFoundException("The official POLYGON Nature Biomes URP compatibility package was not found.", packagePath);

            AssetDatabase.ImportPackage(packagePath, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            var unsupported = AssetDatabase.FindAssets("t:Material", new[] { "Assets/PolygonNatureBiomes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Count(material => material != null && (material.shader == null || !material.shader.isSupported));
            Debug.Log(unsupported == 0
                ? "POLYGON Nature Biomes URP materials imported successfully; no unsupported material shaders remain."
                : $"POLYGON Nature Biomes URP package imported; {unsupported} materials still use unsupported shaders.");
        }

        public static void RepairPolygonNatureBiomesMaterialsBatch()
        {
            RepairPolygonNatureBiomesMaterials();
        }

        [MenuItem("Tools/香火争渡/导入并安装游戏音频")]
        public static void InstallGameAudio()
        {
            var scene = SceneManager.GetActiveScene();
            var game = UnityEngine.Object.FindAnyObjectByType<TempleCircuitGame>();
            if (!scene.IsValid() || game == null)
                throw new InvalidOperationException("The installed Temple Circuit runtime was not found in the current scene.");

            var audio = game.GetComponent<TempleAudioManager>() ?? Undo.AddComponent<TempleAudioManager>(game.gameObject);
            ConfigureAudio(audio);
            game.audioManager = audio;
            EditorUtility.SetDirty(audio);
            EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Temple audio installed: looping BGM, dice roll/land, pawn steps, claim chime and UI click.");
        }

        public static void InstallGameAudioBatch()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            InstallGameAudio();
        }

        [MenuItem("Tools/香火争渡/修复当前场景水面（不改布局）")]
        public static void RepairCurrentSceneWater()
        {
            RepairWaterInScene(SceneManager.GetActiveScene());
        }

        // Kept public so the same non-destructive repair can be run from Unity batch mode.
        public static void RepairSampleSceneWater()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            RepairWaterInScene(scene);
        }

        static void RepairWaterInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open a valid scene before repairing its water surface.");

            var waterMaterial = CreateUrpWaterMaterial();
            if (waterMaterial == null)
                throw new InvalidOperationException("The URP water material could not be created.");

            var repaired = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!IsExistingWaterSurface(renderer))
                        continue;

                    Undo.RecordObject(renderer, "Repair Temple water material");
                    renderer.sharedMaterial = waterMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    EditorUtility.SetDirty(renderer);
                    repaired++;
                }
            }

            if (repaired == 0)
                throw new InvalidOperationException("No existing water surface was found. No new object was created, so the current scene layout is unchanged.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Repaired {repaired} existing water surface renderer(s) with the mobile-safe URP water material. Scene layout was not changed.");
        }

        static bool IsExistingWaterSurface(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null)
                return false;

            var material = renderer.sharedMaterial;
            // Existing Meadow/Enchanted water is identified from its material or shader;
            // terrain, stone tiles, and game props are deliberately left untouched.
            return material.name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0
                   || (material.shader != null && material.shader.name.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static void RepairPresentationBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            RepairPresentation();
        }

        static TempleXRTabletopRig RemoveDuplicateXrRigs()
        {
            var rigs = UnityEngine.Object.FindObjectsByType<TempleXRTabletopRig>(FindObjectsInactive.Include);
            var keeper = rigs.FirstOrDefault();
            for (var i = 1; i < rigs.Length; i++)
                Undo.DestroyObjectImmediate(rigs[i].gameObject);
            return keeper;
        }

        static void RemoveExistingXrRigs()
        {
            foreach (var rig in UnityEngine.Object.FindObjectsByType<TempleXRTabletopRig>(FindObjectsInactive.Include))
                Undo.DestroyObjectImmediate(rig.gameObject);
        }

        static List<GameObject> SortPathTiles(List<GameObject> roots)
        {
            var center = roots.Aggregate(Vector3.zero, (sum, root) => sum + root.transform.position) / roots.Count;
            var sorted = roots.OrderByDescending(root => Mathf.Atan2(root.transform.position.z - center.z, root.transform.position.x - center.x)).ToList();

            var startReferences = FindPrefabRoots(path => path.Contains("/31/"));
            if (startReferences.Count == 0)
                return sorted;
            var startReference = startReferences.OrderBy(root => Vector3.Distance(root.transform.position, center)).First();
            var start = sorted.OrderBy(root => HorizontalDistance(root.transform.position, startReference.transform.position)).First();
            var startIndex = sorted.IndexOf(start);
            return sorted.Skip(startIndex).Concat(sorted.Take(startIndex)).ToList();
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var delta = a - b;
            delta.y = 0f;
            return delta.sqrMagnitude;
        }

        static float EstimateTileSpan(IReadOnlyList<GameObject> tiles)
        {
            var distances = new List<float>();
            for (var i = 0; i < tiles.Count; i++)
            {
                var a = tiles[i].transform.position;
                var b = tiles[(i + 1) % tiles.Count].transform.position;
                distances.Add(Vector3.Distance(new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z)));
            }
            distances.Sort();
            return distances[distances.Count / 2];
        }

        static TempleTileCategory CategoryForPath(string path)
        {
            if (path.Contains("/41/")) return TempleTileCategory.Offering;
            if (path.Contains("/42/")) return TempleTileCategory.Relic;
            if (path.Contains("/43/")) return TempleTileCategory.Incense;
            return TempleTileCategory.Meditation;
        }

        static void ConfigurePathMotion(IReadOnlyList<GameObject> tiles, float tileSpan)
        {
            for (var i = 0; i < tiles.Count; i++)
            {
                var motion = tiles[i].GetComponent<TempleBoardMotion>() ?? Undo.AddComponent<TempleBoardMotion>(tiles[i]);
                var variation = (i % 7) / 6f;
                motion.verticalAmplitude = tileSpan * Mathf.Lerp(0.045f, 0.075f, variation);
                motion.cycleSeconds = 4.05f + (i % 6) * 0.31f;
                motion.phaseDegrees = i * 47f % 360f;
                motion.pitchDegrees = Mathf.Lerp(0.15f, 0.24f, variation);
                motion.rollDegrees = Mathf.Lerp(0.18f, 0.28f, 1f - variation);
            }
        }

        static void ConfigurePeripheralMotion(IReadOnlyList<GameObject> tiles, Vector3 center, float tileSpan)
        {
            var minX = tiles.Min(tile => tile.transform.position.x);
            var maxX = tiles.Max(tile => tile.transform.position.x);
            var minZ = tiles.Min(tile => tile.transform.position.z);
            var maxZ = tiles.Max(tile => tile.transform.position.z);
            var tileSet = new HashSet<GameObject>(tiles);
            var candidates = FindPrefabRoots(path => path.StartsWith(AssetRoot, StringComparison.Ordinal));
            foreach (var root in candidates)
            {
                if (tileSet.Contains(root))
                    continue;
                if (!TryGetRendererBounds(root, out var bounds))
                    continue;
                var point = bounds.center;
                var outsideRing = point.x < minX - tileSpan * 0.55f || point.x > maxX + tileSpan * 0.55f || point.z < minZ - tileSpan * 0.55f || point.z > maxZ + tileSpan * 0.55f;
                var closeEnough = Mathf.Abs(point.x - center.x) < tileSpan * 14f && Mathf.Abs(point.z - center.z) < tileSpan * 14f;
                if (!outsideRing || !closeEnough)
                    continue;

                var horizontalLongest = Mathf.Max(bounds.size.x, bounds.size.z);
                var horizontalShortest = Mathf.Max(0.01f, Mathf.Min(bounds.size.x, bounds.size.z));
                var looksLikeRail = horizontalLongest / horizontalShortest > 3.2f && horizontalLongest > tileSpan * 2.2f;
                var motion = root.GetComponent<TempleBoardMotion>() ?? Undo.AddComponent<TempleBoardMotion>(root);
                var clusterX = Mathf.RoundToInt((point.x - center.x) / (tileSpan * 2.2f));
                var clusterZ = Mathf.RoundToInt((point.z - center.z) / (tileSpan * 2.2f));
                var variation = Mathf.Abs(clusterX * 31 + clusterZ * 17) % 7 / 6f;
                motion.verticalAmplitude = tileSpan * (looksLikeRail ? Mathf.Lerp(0.038f, 0.058f, variation) : Mathf.Lerp(0.06f, 0.095f, variation));
                motion.cycleSeconds = looksLikeRail ? Mathf.Lerp(4.7f, 6.1f, 1f - variation) : Mathf.Lerp(5.0f, 6.8f, variation);
                motion.phaseDegrees = Mathf.Abs(clusterX * 71 + clusterZ * 43) % 360;
                motion.pitchDegrees = looksLikeRail ? Mathf.Lerp(0.18f, 0.3f, variation) : Mathf.Lerp(0.04f, 0.12f, variation);
                motion.rollDegrees = looksLikeRail ? Mathf.Lerp(0.2f, 0.34f, 1f - variation) : Mathf.Lerp(0.05f, 0.14f, 1f - variation);
            }
        }

        static TemplePawnFollower[] CreatePlayers(Transform parent, TempleBoardTile startTile, float tileSpan)
        {
            var container = new GameObject("Players_32-35").transform;
            container.SetParent(parent, false);
            var followers = new TemplePawnFollower[4];
            for (var i = 0; i < 4; i++)
            {
                var group = new GameObject($"Player_{i + 1}");
                group.transform.SetParent(container, false);
                var follower = group.AddComponent<TemplePawnFollower>();
                follower.playerIndex = i;
                follower.tile = startTile;
                followers[i] = follower;

                var pawnAsset = FindAssetPrefab(PawnFolders[i]);
                var pawn = (GameObject)PrefabUtility.InstantiatePrefab(pawnAsset, group.transform);
                pawn.name = $"Pawn_{i + 1}_Model_{32 + i}";
                PrepareModel(pawn, tileSpan * 0.62f, Vector3.zero);
                group.transform.position = startTile.GetStandingPoint(i);
            }
            return followers;
        }

        static void ConfigureOwnershipFlagSetup(TempleCircuitGame game)
        {
            game.ownershipFlagPrefabs = FlagFolders.Select(FindAssetPrefab).ToArray();
            if (game.pawnFollowers == null)
                return;
            foreach (var follower in game.pawnFollowers)
            {
                if (follower == null)
                    continue;
                for (var i = follower.transform.childCount - 1; i >= 0; i--)
                {
                    var child = follower.transform.GetChild(i);
                    if (child.name.StartsWith("Flag_", StringComparison.OrdinalIgnoreCase))
                        Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        static GameObject FindAssetPrefab(string folderFragment)
        {
            var guids = AssetDatabase.FindAssets("t:GameObject", new[] { AssetRoot });
            var path = guids.Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(candidate => candidate.Contains(folderFragment) && candidate.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(path))
                throw new FileNotFoundException($"No FBX was found in {folderFragment}.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void PrepareModel(GameObject model, float targetHeight, Vector3 offset)
        {
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            model.transform.localScale = Vector3.one;
            if (!TryGetRendererBounds(model, out var bounds) || bounds.size.y < 0.0001f)
                return;
            var scale = targetHeight / bounds.size.y;
            model.transform.localScale *= scale;
            if (TryGetRendererBounds(model, out bounds))
                model.transform.position += Vector3.up * (model.transform.parent.position.y - bounds.min.y);
            model.transform.localPosition += offset;
        }

        static void EnsureBoxCollider(GameObject root)
        {
            if (root.GetComponentInChildren<Collider>() != null)
                return;
            if (!TryGetRendererBounds(root, out var worldBounds))
                return;
            var corners = BoundsCorners(worldBounds).Select(root.transform.InverseTransformPoint).ToArray();
            var localBounds = new Bounds(corners[0], Vector3.zero);
            foreach (var corner in corners)
                localBounds.Encapsulate(corner);
            var collider = Undo.AddComponent<BoxCollider>(root);
            collider.center = localBounds.center;
            collider.size = localBounds.size;
        }

        static IEnumerable<Vector3> BoundsCorners(Bounds bounds)
        {
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
                yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));
        }

        static void EnsureEventSystem()
        {
            var eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                eventSystem = go.GetComponent<EventSystem>();
            }
            var legacy = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                Undo.DestroyObjectImmediate(legacy);
            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>() ?? Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
            inputModule.AssignDefaultActions();
        }

        static Sprite ImportGeneratedPanelSprite()
        {
            var assetPath = "Assets/Game/UI/Generated/TempleUI_Panel.png";
            if (!File.Exists(assetPath))
                assetPath = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/UI/Generated" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(assetPath))
                return null;
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        static Texture2D ImportStartCoverTexture()
        {
            const string assetPath = "Assets/Game/UI/Resources/TempleStartCover.png";
            if (!File.Exists(assetPath))
                throw new FileNotFoundException("The exported 16:9 start cover is missing.", assetPath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.compressionQuality = 82;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        static Sprite ImportGeneratedTopHudSprite()
        {
            const string assetPath = "Assets/Game/UI/Generated/TempleHUD_TopFrame.png";
            if (!File.Exists(assetPath))
                return null;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.spriteBorder = new Vector4(105f, 88f, 105f, 88f);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        static Font ImportBundledUiFont()
        {
            return AssetDatabase.LoadAssetAtPath<Font>("Assets/Game/UI/Resources/Fonts/NotoSansSC-Variable.ttf");
        }

        static void CreateWaterSurface(Transform parent, Vector3 boardCenter, float tileSpan)
        {
            var existing = GameObject.Find("TempleWater_Surface_MeadowForest");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var waterMaterial = CreateUrpWaterMaterial();
            var waterPrefabPath = AssetDatabase.FindAssets("SM_Env_Water_Plane_01 t:GameObject", new[] { "Assets/PolygonNatureBiomes" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase));

            GameObject water;
            if (!string.IsNullOrEmpty(waterPrefabPath))
                water = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(waterPrefabPath), parent);
            else
                water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "TempleWater_Surface_MeadowForest";
            water.transform.SetParent(parent, true);
            water.transform.position = boardCenter + Vector3.down * (tileSpan * 2.2f);
            water.transform.rotation = Quaternion.identity;
            if (TryGetRendererBounds(water, out var bounds))
            {
                var desired = tileSpan * 22f;
                var source = Mathf.Max(bounds.size.x, bounds.size.z);
                if (source > 0.001f)
                    water.transform.localScale *= desired / source;
            }
            foreach (var renderer in water.GetComponentsInChildren<Renderer>())
                if (waterMaterial != null)
                    renderer.sharedMaterial = waterMaterial;
            foreach (var collider in water.GetComponentsInChildren<Collider>())
                Undo.DestroyObjectImmediate(collider);
        }

        static Material CreateUrpWaterMaterial()
        {
            const string materialFolder = "Assets/Game/Materials";
            const string materialPath = materialFolder + "/Temple_PNB_Water_URP.mat";
            Directory.CreateDirectory(materialFolder);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                return null;
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Temple PNB Water URP" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.shader = shader;
            // The old PNB refraction map becomes almost black under the current URP
            // setup. Keep the existing normal map for ripples and use a readable teal
            // albedo instead, so the water remains visible on both desktop and PICO.
            material.SetTexture("_BaseMap", null);
            material.SetColor("_BaseColor", new Color(0.055f, 0.43f, 0.56f, 0.86f));
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_Metallic", 0.04f);
            material.SetFloat("_Smoothness", 0.9f);
            material.SetFloat("_BumpScale", 0.55f);
            var normalPath = AssetDatabase.FindAssets("WaterNormals_01 t:Texture2D", new[] { "Assets/PolygonNatureBiomes" })
                .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            if (!string.IsNullOrEmpty(normalPath))
            {
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                material.EnableKeyword("_NORMALMAP");
            }
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        static List<GameObject> FindPrefabRoots(Func<string, bool> predicate)
        {
            var scene = SceneManager.GetActiveScene();
            var roots = new HashSet<GameObject>();
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform.gameObject.scene != scene || EditorUtility.IsPersistent(transform.gameObject))
                    continue;
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject);
                if (root == null)
                    continue;
                var path = GetPrefabAssetPath(root);
                if (predicate(path))
                    roots.Add(root);
            }
            return roots.ToList();
        }

        static string GetPrefabAssetPath(GameObject root)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            return source == null ? string.Empty : AssetDatabase.GetAssetPath(source).Replace('\\', '/');
        }

        static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
    }
}
