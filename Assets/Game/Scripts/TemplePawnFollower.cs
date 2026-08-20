using UnityEngine;

namespace TempleCircuit
{
    public sealed class TemplePawnFollower : MonoBehaviour
    {
        public TempleBoardTile tile;
        [Range(0, 3)] public int playerIndex;
        public bool follow = true;

        Vector3 baseLocalScale;

        void Awake()
        {
            baseLocalScale = transform.localScale;
        }

        public void SetJumpStretch(float progress)
        {
            var p = Mathf.Clamp01(progress);
            var airborne = Mathf.Sin(p * Mathf.PI);
            var landing = Mathf.Exp(-Mathf.Pow((p - 0.9f) / 0.075f, 2f));
            var vertical = 1f + airborne * 0.18f - landing * 0.12f;
            var horizontal = 1f - airborne * 0.06f + landing * 0.07f;
            transform.localScale = Vector3.Scale(baseLocalScale, new Vector3(horizontal, vertical, horizontal));
        }

        public void ResetJumpStretch()
        {
            if (baseLocalScale == Vector3.zero)
                baseLocalScale = Vector3.one;
            transform.localScale = baseLocalScale;
        }

        void LateUpdate()
        {
            if (follow && tile != null)
                transform.position = tile.GetStandingPoint(playerIndex);
        }
    }
}
