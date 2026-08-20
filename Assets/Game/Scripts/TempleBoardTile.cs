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

        public Vector3 GetStandingPoint(int playerIndex)
        {
            var top = transform.position;
            if (TryGetSurfaceBounds(out var bounds))
                top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);

            var spacing = EstimateSpacing() * 0.13f;
            var offsets = new[]
            {
                new Vector3(-spacing, 0f, -spacing),
                new Vector3(spacing, 0f, -spacing),
                new Vector3(-spacing, 0f, spacing),
                new Vector3(spacing, 0f, spacing)
            };
            return top + offsets[Mathf.Clamp(playerIndex, 0, offsets.Length - 1)] + Vector3.up * (spacing * 0.18f);
        }

        public float EstimateSpacing()
        {
            if (!TryGetSurfaceBounds(out var bounds))
                return 4f;
            return Mathf.Max(0.2f, Mathf.Min(bounds.size.x, bounds.size.z));
        }

        public bool TryGetSurfaceBounds(out Bounds bounds)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var found = false;
            bounds = default;
            foreach (var renderer in renderers)
            {
                if (renderer == null || IsRuntimeDecoration(renderer.transform))
                    continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }
            return found;
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
