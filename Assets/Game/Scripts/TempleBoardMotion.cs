using UnityEngine;

namespace TempleCircuit
{
    public sealed class TempleBoardMotion : MonoBehaviour
    {
        [Min(0f)] public float verticalAmplitude = 0.22f;
        [Min(0.05f)] public float cycleSeconds = 4.8f;
        [Range(0f, 360f)] public float phaseDegrees;
        [Range(0f, 1.5f)] public float pitchDegrees = 0.2f;
        [Range(0f, 1.5f)] public float rollDegrees = 0.22f;

        Vector3 baseLocalPosition;
        Quaternion baseLocalRotation;

        void Awake()
        {
            CaptureBasePose();
        }

        public void CaptureBasePose()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
        }

        void LateUpdate()
        {
            var phase = phaseDegrees * Mathf.Deg2Rad;
            var t = Time.time * (Mathf.PI * 2f / Mathf.Max(0.05f, cycleSeconds)) + phase;
            transform.localPosition = baseLocalPosition + Vector3.up * (Mathf.Sin(t) * verticalAmplitude);
            transform.localRotation = baseLocalRotation * Quaternion.Euler(
                Mathf.Sin(t * 0.73f) * pitchDegrees,
                0f,
                Mathf.Cos(t * 0.61f) * rollDegrees);
        }
    }
}
