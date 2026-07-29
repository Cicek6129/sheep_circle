using UnityEngine;

namespace SheepCircle
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        AudioSource source;
        AudioClip tapClip;
        AudioClip crashClip;
        AudioClip scoreClip;

        public bool IsMuted { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;

            tapClip = AudioGenerator.GenerateTapSound();
            crashClip = AudioGenerator.GenerateCrashSound();
            scoreClip = AudioGenerator.GenerateScoreSound();

            IsMuted = PlayerPrefs.GetInt("SheepCircle.Muted", 0) == 1;
        }

        public void ToggleSound()
        {
            IsMuted = !IsMuted;
            PlayerPrefs.SetInt("SheepCircle.Muted", IsMuted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void PlayTap()
        {
            if (!IsMuted) source.PlayOneShot(tapClip, 0.6f);
        }

        public void PlayCrash()
        {
            if (!IsMuted) source.PlayOneShot(crashClip, 1.0f);
        }

        public void PlayScore()
        {
            if (!IsMuted) source.PlayOneShot(scoreClip, 0.7f);
        }
    }
}
