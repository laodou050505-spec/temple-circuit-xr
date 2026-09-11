using System.Collections.Generic;
using UnityEngine;

namespace TempleCircuit
{
    // Ownership lives beside the courtyard wall, independently of animated stones.
    public sealed class TempleOwnershipFlagAnchor : MonoBehaviour
    {
        Transform flagModel;
        Renderer[] flagRenderers;
        Renderer[] walls;
        TempleBoardTile tile;
        Vector3 boardCenter;
        float spacing;
        public bool PlacementValid { get; private set; }

        public void Configure(TempleBoardTile targetTile, Vector3 center, float tileSpacing, Renderer[] courtyardWalls)
        {
            tile = targetTile;
            boardCenter = center;
            spacing = tileSpacing;
            walls = courtyardWalls;
            transform.position = targetTile.transform.position;
        }

        public void SetFlagModel(Transform model)
        {
            flagModel = model;
            flagRenderers = model.GetComponentsInChildren<Renderer>();
            PlaceBesideWall();
        }

        public Bounds ModelBounds
        {
            get
            {
                var bounds = flagRenderers[0].bounds;
                for (var i = 1; i < flagRenderers.Length; i++) bounds.Encapsulate(flagRenderers[i].bounds);
                return bounds;
            }
        }

        void PlaceBesideWall()
        {
            if (walls == null || walls.Length == 0 || flagRenderers.Length == 0)
            {
                Debug.LogError("Ownership placement requires courtyard wall references.", this);
                return;
            }
            tile.TryGetSurfaceBounds(out var tileBounds);
            var obstacles = new List<Bounds>();
            var stones = new List<Bounds>();
            foreach (var stone in FindObjectsByType<TempleBoardTile>(FindObjectsInactive.Exclude))
            {
                stone.TryGetSurfaceBounds(out var bounds);
                bounds.Expand(new Vector3(spacing * 0.045f, spacing * 0.2f, spacing * 0.045f));
                stones.Add(bounds);
            }
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!renderer.enabled || renderer.transform.IsChildOf(transform) || renderer is ParticleSystemRenderer || renderer is LineRenderer
                    || renderer.GetComponentInParent<Canvas>() != null || renderer.GetComponentInParent<TemplePawnFollower>() != null
                    || renderer.GetComponentInParent<TempleBoardTile>() != null) continue;
                var bounds = renderer.bounds;
                bounds.Expand(spacing * 0.025f);
                obstacles.Add(bounds);
            }
            var originalScale = flagModel.localScale;
            var found = false;
            var bestScore = float.PositiveInfinity;
            var bestPosition = Vector3.zero;
            var bestRotation = Quaternion.identity;
            var bestScale = originalScale;
            // Try nearby wall frontage and sideways offsets for gates/towers.
            // Scaling is a bounded fallback for narrow gaps, never cumulative.
            for (var scaleStep = 0; scaleStep <= 9; scaleStep++)
            {
                var scale = 1f - scaleStep * 0.075f;
                flagModel.localScale = originalScale * scale;
                foreach (var wall in walls)
                {
                    if (wall == null) continue;
                    var wallBounds = wall.bounds;
                    var alongX = wallBounds.size.x > wallBounds.size.z;
                    var outward = alongX
                        ? new Vector3(0f, 0f, Mathf.Sign(wallBounds.center.z - boardCenter.z))
                        : new Vector3(Mathf.Sign(wallBounds.center.x - boardCenter.x), 0f, 0f);
                    var tangent = alongX ? Vector3.right : Vector3.forward;
                    var approach = tileBounds.center - wallBounds.center;
                    approach.y = 0f;
                    if (Vector3.Dot(approach, outward) < 0f || approach.magnitude > spacing * 1.8f) continue;
                    transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
                    var flagBounds = ModelBounds;
                    var depth = alongX ? flagBounds.extents.z : flagBounds.extents.x;
                    var halfWidth = alongX ? flagBounds.extents.x : flagBounds.extents.z;
                    var wallDepth = alongX ? wallBounds.extents.z : wallBounds.extents.x;
                    var wallLength = alongX ? wallBounds.extents.x : wallBounds.extents.z;
                    var projected = Vector3.Dot(approach, tangent);
                    for (var step = 0; step <= 16; step++)
                    {
                        var shift = step == 0 ? 0f : ((step + 1) / 2) * 0.1f * spacing * (step % 2 == 0 ? -1f : 1f);
                        var along = Mathf.Clamp(projected + shift, -wallLength + halfWidth, wallLength - halfWidth);
                        var candidate = wallBounds.center + tangent * along + outward * (wallDepth + depth + spacing * 0.055f);
                        candidate.y = wallBounds.min.y + spacing * 0.035f + flagBounds.extents.y;
                        var candidateBounds = new Bounds(candidate, flagBounds.size);
                        var clear = true;
                        foreach (var stone in stones)
                        {
                            // Keep the entire footprint off every stone, even
                            // if a high flag would clear that stone vertically.
                            if (candidateBounds.min.x < stone.max.x && candidateBounds.max.x > stone.min.x &&
                                candidateBounds.min.z < stone.max.z && candidateBounds.max.z > stone.min.z)
                            { clear = false; break; }
                        }
                        if (!clear) continue;
                        foreach (var obstacle in obstacles)
                            if (candidateBounds.Intersects(obstacle)) { clear = false; break; }
                        if (!clear) continue;
                        var distance = candidate - tileBounds.center;
                        distance.y = 0f;
                        if (distance.magnitude > spacing * 1.4f) continue;
                        var score = distance.sqrMagnitude / (spacing * spacing) + scaleStep * 0.45f;
                        if (score >= bestScore) continue;
                        bestScore = score;
                        bestPosition = candidate;
                        bestRotation = transform.rotation;
                        bestScale = flagModel.localScale;
                        found = true;
                    }
                }
            }
            PlacementValid = found;
            if (!found)
            {
                Debug.LogError($"No clear wall-side ownership position for tile {tile.index}.", this);
                flagModel.gameObject.SetActive(false);
                return;
            }
            flagModel.localScale = bestScale;
            transform.rotation = bestRotation;
            transform.position += bestPosition - ModelBounds.center;
        }
    }
}
