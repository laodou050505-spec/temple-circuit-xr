using UnityEngine;

namespace TempleCircuit
{
    public enum TempleTileCategory
    {
        Offering,
        Meditation,
        Incense,
        Relic
    }

    public sealed class TempleBoardTile : MonoBehaviour
    {
        [Min(0)] public int index;
        public TempleTileCategory category;

        Renderer[] surfaceRenderers = System.Array.Empty<Renderer>();
        Bounds cachedSurfaceBounds;
        int cachedSurfaceBoundsFrame = -1;
        bool surfaceRenderersCached;
        bool cachedSurfaceBoundsValid;

        public Vector3 GetStandingPoint(int playerIndex)
        {
            var top = transform.position;
            var spacing = 4f;
            if (TryGetSurfaceBounds(out var bounds))
            {
                top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
                spacing = Mathf.Max(0.2f, Mathf.Min(bounds.size.x, bounds.size.z));
            }

            spacing *= 0.13f;
            var clampedIndex = Mathf.Clamp(playerIndex, 0, 3);
            var offset = new Vector3(
                (clampedIndex & 1) == 0 ? -spacing : spacing,
                spacing * 0.18f,
                (clampedIndex & 2) == 0 ? -spacing : spacing);
            return top + offset;
        }

        public float EstimateSpacing()
        {
            if (!TryGetSurfaceBounds(out var bounds))
                return 4f;
            return Mathf.Max(0.2f, Mathf.Min(bounds.size.x, bounds.size.z));
        }

        public bool TryGetSurfaceBounds(out Bounds bounds)
        {
            // Multiple pawns and the tile information card query the same tile
            // during a frame. Reuse both the renderer list and that frame's
            // merged bounds instead of repeatedly walking the hierarchy and
            // allocating temporary component arrays.
            if (Application.isPlaying && cachedSurfaceBoundsFrame == Time.frameCount)
            {
                bounds = cachedSurfaceBounds;
                return cachedSurfaceBoundsValid;
            }

            EnsureSurfaceRenderers();
            var found = false;
            bounds = default;
            foreach (var renderer in surfaceRenderers)
            {
                if (renderer == null)
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }

            if (Application.isPlaying)
            {
                cachedSurfaceBounds = bounds;
                cachedSurfaceBoundsValid = found;
                cachedSurfaceBoundsFrame = Time.frameCount;
            }
            return found;
        }

        void EnsureSurfaceRenderers()
        {
            if (surfaceRenderersCached)
                return;

            var allRenderers = GetComponentsInChildren<Renderer>();
            var count = 0;
            foreach (var renderer in allRenderers)
            {
                if (renderer != null && !IsRuntimeDecoration(renderer.transform))
                    count++;
            }

            surfaceRenderers = new Renderer[count];
            var index = 0;
            foreach (var renderer in allRenderers)
            {
                if (renderer != null && !IsRuntimeDecoration(renderer.transform))
                    surfaceRenderers[index++] = renderer;
            }
            surfaceRenderersCached = true;
        }

        void OnTransformChildrenChanged()
        {
            surfaceRenderersCached = false;
            InvalidateSurfaceBounds();
        }

        public void InvalidateSurfaceBounds()
        {
            cachedSurfaceBoundsFrame = -1;
        }

        bool IsRuntimeDecoration(Transform candidate)
        {
            for (var current = candidate; current != null && current != transform; current = current.parent)
            {
                if (current.name.StartsWith("OwnerMarker", System.StringComparison.OrdinalIgnoreCase) ||
                    current.name.StartsWith("TempleTileInfo", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
