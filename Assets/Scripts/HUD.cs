using TMPro;
using UnityEngine;

namespace SheepCircle
{
    /// <summary>Title card, score readout and the game-over card.</summary>
    public class HUD : MonoBehaviour
    {
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text bestText;
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] TMP_Text gameOverTitle;
        [SerializeField] TMP_Text gameOverBody;

        [Header("Title")]
        [Tooltip("Score, best and the control hint. Hidden while the title card " +
                 "is up, or they read through it and collide with the logo.")]
        [SerializeField] GameObject playHud;
        [SerializeField] GameObject startPanel;
        [SerializeField] RectTransform startButton;
        [SerializeField] TMP_Text startBestText;

        [Header("Sound")]
        [SerializeField] UnityEngine.UI.Image soundButtonImage;
        [SerializeField] Sprite soundOnSprite;
        [SerializeField] Sprite soundOffSprite;
        public RectTransform SoundButtonRect => soundButtonImage != null ? soundButtonImage.rectTransform : null;

        void Start()
        {
            UpdateSoundIcon();
        }

        void Update()
        {
            // Nothing on the title card is clickable - a tap anywhere starts the
            // round - so the button breathes to show it is waiting on the player.
            if (startButton == null || startPanel == null || !startPanel.activeSelf) return;

            float s = 1f + Mathf.Sin(Time.unscaledTime * 3.1f) * 0.035f;
            startButton.localScale = new Vector3(s, s, 1f);
        }

        public void ShowStart(int best)
        {
            if (startPanel == null) return;

            startPanel.SetActive(true);
            if (playHud != null) playHud.SetActive(false);
            if (startBestText != null) startBestText.text = $"REKOR  {best}";
        }

        public void HideStart()
        {
            if (playHud != null) playHud.SetActive(true);
            if (startPanel == null) return;

            startPanel.SetActive(false);
            if (startButton != null) startButton.localScale = Vector3.one;
        }

        public void SetScore(int score) => scoreText.text = score.ToString();

        public void SetBest(int best) => bestText.text = $"REKOR  {best}";

        public void HideGameOver() => gameOverPanel.SetActive(false);

        public void ShowGameOver(string reason, int score)
        {
            gameOverPanel.SetActive(true);
            gameOverTitle.text = reason;
            gameOverBody.text = $"{score} hayvan ağıla girdi\n\nTekrar için tıkla";
        }

        public void ToggleSound()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ToggleSound();
                UpdateSoundIcon();
            }
        }

        public void UpdateSoundIcon()
        {
            if (soundButtonImage != null && AudioManager.Instance != null)
            {
                soundButtonImage.sprite = AudioManager.Instance.IsMuted ? soundOffSprite : soundOnSprite;
            }
        }
    }
}
