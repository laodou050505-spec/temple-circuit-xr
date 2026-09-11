using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TempleCircuitEditor
{
    public static class TemplePlacementInspection
    {
        public static void VerifyFlagsAndBuildMac()
        {
            VerifyFlags();
            TemplePicoProjectSetup.BuildMac();
        }

        public static void VerifyFlags()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            VerifyFlagsInLoadedScene();
        }

        public static void VerifyBothScalesAndBuildPico()
        {
            VerifyFlags();
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            var rig = Object.FindAnyObjectByType<TempleCircuit.TempleXRTabletopRig>();
            typeof(TempleCircuit.TempleXRTabletopRig).GetMethod("ScaleSceneForTabletop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(rig, null);
            VerifyFlagsInLoadedScene();
            Debug.Log("FLAG_CHECK_BOTH_SCALES_PASSED: 192 maximum-level color/property/order/scale cases");
            TemplePicoProjectSetup.BuildPicoApk();
        }

        static void VerifyFlagsInLoadedScene()
        {
            var game = Object.FindAnyObjectByType<TempleCircuit.TempleCircuitGame>();
            var centre = game.pathTiles.Aggregate(Vector3.zero, (sum, tile) => sum + tile.transform.position) / game.pathTiles.Length;
            var indices = new[] { 1, 2, 3, 4, 6, 8, 9, 11, 14, 16, 17, 19 };
            var failures = 0;
            for (var pass = 0; pass < 8; pass++)
            {
                var player = pass % 4;
                var testRoot = new GameObject("PlacementVerification");
                foreach (var index in pass < 4 ? indices : indices.Reverse().ToArray())
                {
                    var tile = game.pathTiles.First(tile => tile.index == index);
                    tile.TryGetSurfaceBounds(out var footprint);
                    var spacing = Mathf.Max(0.001f, Mathf.Min(footprint.size.x, footprint.size.z));
                    var marker = new GameObject($"FlagCheck_{player}_{index}");
                    marker.transform.SetParent(testRoot.transform);
                    var anchor = marker.AddComponent<TempleCircuit.TempleOwnershipFlagAnchor>();
                    anchor.Configure(tile, centre, spacing, game.ownershipWalls);
                    var flag = Object.Instantiate(game.ownershipFlagPrefabs[player], marker.transform);
                    flag.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    var renderers = flag.GetComponentsInChildren<Renderer>();
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    flag.transform.localScale *= spacing * 0.63f / bounds.size.y;
                    anchor.SetFlagModel(flag.transform);
                    Debug.Log($"FLAG_CHECK: player={player} tile={index} valid={anchor.PlacementValid} bounds={anchor.ModelBounds}");
                    if (!anchor.PlacementValid) failures++;
                    else
                    {
                        var placed = anchor.ModelBounds;
                        foreach (var stone in game.pathTiles)
                        {
                            stone.TryGetSurfaceBounds(out var stoneBounds);
                            if (placed.min.x < stoneBounds.max.x && placed.max.x > stoneBounds.min.x &&
                                placed.min.z < stoneBounds.max.z && placed.max.z > stoneBounds.min.z)
                                throw new System.Exception($"Flag {player}/{index} still overlaps stone {stone.index} after placement.");
                        }
                        foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                        {
                            if (!renderer.enabled || renderer.transform.IsChildOf(marker.transform) || renderer is ParticleSystemRenderer || renderer is LineRenderer
                                || renderer.GetComponentInParent<Canvas>() != null || renderer.GetComponentInParent<TempleCircuit.TemplePawnFollower>() != null) continue;
                            if (placed.Intersects(renderer.bounds))
                                throw new System.Exception($"Flag {player}/{index} intersects {PathOf(renderer.transform)} after placement.");
                        }
                    }
                }
                Object.DestroyImmediate(testRoot);
            }
            if (failures > 0) throw new System.Exception($"Flag placement failed for {failures} of 96 maximum-level cases.");
            Debug.Log("FLAG_CHECK_ALL_PASSED: 96 maximum-level color/property/order cases");
        }

        public static void ApplyStartAndWalls()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            var game = Object.FindAnyObjectByType<TempleCircuit.TempleCircuitGame>();
            game.ownershipWalls = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(renderer => renderer.name.StartsWith("tripo_convert_1d1ce0ab-4c21-4f4d-a75a-e654f5d73c7f"))
                .OrderBy(renderer => PathOf(renderer.transform)).ToArray();
            if (game.ownershipWalls.Length < 4) throw new System.Exception("Courtyard wall references are missing.");
            var rig = Object.FindAnyObjectByType<TempleCircuit.TempleXRTabletopRig>();
            rig.alignGameplayStartToStartTile = true;
            rig.AlignGameplayStartWithStartTile();
            EditorUtility.SetDirty(game);
            EditorUtility.SetDirty(rig);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            Debug.Log($"PLACEMENT_APPLIED: {game.ownershipWalls.Length} walls, eye {rig.designEyeOffsetFromTabletop}, look {rig.designLookAtOffsetFromTabletop}, yaw {rig.designFacingYawDegrees}");
        }

        static string PathOf(Transform transform) => transform.parent == null ? transform.name : PathOf(transform.parent) + "/" + transform.name;
        static string Numbers(Bounds bounds) => string.Join("\t", new[] { bounds.center.x, bounds.center.y, bounds.center.z, bounds.size.x, bounds.size.y, bounds.size.z }.Select(value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

        public static void Inspect()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
            var game = Object.FindAnyObjectByType<TempleCircuit.TempleCircuitGame>();
            Directory.CreateDirectory("VisualQA/Placement-20260908");
            using (var file = new StreamWriter("VisualQA/Placement-20260908/geometry.tsv"))
            {
                foreach (var tile in game.pathTiles.OrderBy(tile => tile.index))
                    if (tile.TryGetSurfaceBounds(out var bounds)) file.WriteLine($"TILE\t{tile.index}\t{PathOf(tile.transform)}\t{Numbers(bounds)}");
                foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                    file.WriteLine($"RENDERER\t{renderer.name}\t{PathOf(renderer.transform)}\t{Numbers(renderer.bounds)}");
            }
            Debug.Log("PLACEMENT_INSPECTION: VisualQA/Placement-20260908/geometry.tsv");
        }
    }
}
