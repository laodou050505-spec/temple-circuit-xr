using UnityEngine;

namespace TempleCircuit
{
    [DisallowMultipleComponent]
    public sealed class TempleAudioManager : MonoBehaviour
    {
        [Header("Music")]
        public AudioClip backgroundMusic;
        [Range(0f, 1f)] public float musicVolume = 0.32f;

        [Header("Game feedback")]
        public AudioClip diceRoll;
        public AudioClip diceLand;
        public AudioClip pawnStep;
        public AudioClip claimChime;
        public AudioClip uiClick;
        [Range(0f, 1f)] public float diceRollVolume = 0.65f;
        [Range(0f, 1f)] public float diceLandVolume = 0.8f;
        [Range(0f, 1f)] public float pawnStepVolume = 0.23f;
        [Range(0f, 1f)] public float claimVolume = 0.7f;
        [Range(0f, 1f)] public float uiVolume = 0.35f;

        AudioSource musicSource;
        AudioSource sfxSource;

        void Awake()
        {
            var sources = GetComponents<AudioSource>();
            musicSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
            sfxSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();

            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.volume = musicVolume;

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0.45f;
            sfxSource.minDistance = 2.5f;
            sfxSource.maxDistance = 32f;
            sfxSource.rolloffMode = AudioRolloffMode.Linear;
        }

        void Start()
        {
            if (backgroundMusic == null || musicSource == null)
                return;
            musicSource.clip = backgroundMusic;
            musicSource.volume = musicVolume;
            musicSource.Play();
            Debug.Log($"Temple audio active: BGM '{backgroundMusic.name}' is playing; dice, pawn, claim and UI clips are ready.", this);
        }

        public void PlayDiceRoll()
        {
            PlayOneShot(diceRoll, diceRollVolume, 0.96f, 1.04f);
        }

        public void PlayDiceLand()
        {
            PlayOneShot(diceLand, diceLandVolume, 0.98f, 1.02f);
        }

        public void PlayPawnStep()
        {
            PlayOneShot(pawnStep, pawnStepVolume, 0.97f, 1.05f);
        }

        public void PlayClaim()
        {
            PlayOneShot(claimChime, claimVolume, 0.98f, 1.02f);
        }

        public void PlayUiClick()
        {
            PlayOneShot(uiClick, uiVolume, 0.98f, 1.02f);
        }

        void PlayOneShot(AudioClip clip, float volume, float minPitch, float maxPitch)
        {
            if (clip == null || sfxSource == null || volume <= 0f)
                return;
            sfxSource.pitch = Random.Range(minPitch, maxPitch);
            sfxSource.PlayOneShot(clip, volume);
        }
    }
}
