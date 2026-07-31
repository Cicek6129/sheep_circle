using TMPro;
using UnityEngine;

namespace SheepCircle
{
    /// <summary>Title card, score readout, level info and the game-over card.</summary>
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

        [Header("Level")]
        [Tooltip("Displays the current level number (e.g. 'LEVEL 3'). Optional.")]
        [SerializeField] TMP_Text levelText;
        [Tooltip("Displays progress like '2 / 6'. Optional.")]
        [SerializeField] TMP_Text progressText;
        [Tooltip("Panel shown when the player clears a level. Optional.")]
        [SerializeField] GameObject levelCompletePanel;
        [Tooltip("Title on the level-complete panel (e.g. 'LEVEL 3 TAMAMLANDI!'). Optional.")]
        [SerializeField] TMP_Text levelCompleteTitle;
        [Tooltip("Stars shown on the level-complete panel.")]
        [SerializeField] UnityEngine.UI.Image[] stars;

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
            UpdateSoundIcon();
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

        // ----------------------------------------------------------- level info

        public void SetLevel(int level)
        {
            if (levelText != null) levelText.text = $"LEVEL {level}";
        }

        public void SetProgress(int placed, int total)
        {
            if (progressText != null) progressText.text = $"{placed} / {total}";
        }

        public void ShowLevelComplete(int level, int earnedStars)
        {
            if (levelCompletePanel != null) levelCompletePanel.SetActive(true);
            if (levelCompleteTitle != null) levelCompleteTitle.text = $"LEVEL {level} TAMAMLANDI!";
            
            if (stars != null)
            {
                for (int i = 0; i < stars.Length; i++)
                {
                    if (stars[i] != null)
                    {
                        // Gold if earned, dark grey if empty
                        stars[i].color = (i < earnedStars) ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.8f);
                    }
                }
            }
        }

        public void HideLevelComplete()
        {
            if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        }

        // ----------------------------------------------------------- game over

        public void HideGameOver() => gameOverPanel.SetActive(false);

        public void ShowGameOver(string reason, int placed)
        {
            gameOverPanel.SetActive(true);
            gameOverTitle.text = reason;
            gameOverBody.text = $"{placed} hayvan yerleşti\n\nTekrar için dokun";
        }

        public void ToggleSound()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ToggleSound();
                UpdateSoundIcon();
            }
        }

        public bool IsPointerOverSoundButton(Vector2 screenPos)
        {
            if (soundButtonImage == null || !soundButtonImage.gameObject.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(soundButtonImage.rectTransform, screenPos, null);
        }

        public void UpdateSoundIcon()
        {
            if (soundButtonImage != null && AudioManager.Instance != null)
            {
                soundButtonImage.sprite = AudioManager.Instance.IsMuted ? soundOffSprite : soundOnSprite;
            }
        }

        // ----------------------------------------------------------- level select

        [Header("Level Select")]
        [SerializeField] UnityEngine.UI.Image menuButtonImage;
        [SerializeField] GameObject levelSelectPanel;
        [SerializeField] UnityEngine.UI.Image[] levelButtonImages;
        [SerializeField] TMP_Text[] levelButtonTexts;
        [SerializeField] Color unlockedColor = Color.white;
        [SerializeField] Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        public bool IsLevelSelectActive => levelSelectPanel != null && levelSelectPanel.activeInHierarchy;

        public void ShowLevelSelect(int bestLevel)
        {
            if (levelSelectPanel == null) return;
            levelSelectPanel.SetActive(true);
            if (playHud != null) playHud.SetActive(false);
            
            if (levelButtonImages != null && levelButtonTexts != null)
            {
                for (int i = 0; i < levelButtonImages.Length; i++)
                {
                    bool unlocked = i <= bestLevel;
                    if (levelButtonImages[i] != null)
                        levelButtonImages[i].color = unlocked ? unlockedColor : lockedColor;
                    if (levelButtonTexts[i] != null)
                        levelButtonTexts[i].color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.5f);
                }
            }
        }

        public void HideLevelSelect()
        {
            if (levelSelectPanel == null) return;
            levelSelectPanel.SetActive(false);
            if (playHud != null && !startPanel.activeInHierarchy) playHud.SetActive(true);
        }

        public bool IsPointerOverMenuButton(Vector2 screenPos)
        {
            if (menuButtonImage == null || !menuButtonImage.gameObject.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(menuButtonImage.rectTransform, screenPos, null);
        }

        public int GetClickedLevelIndex(Vector2 screenPos)
        {
            if (levelButtonImages == null || !IsLevelSelectActive) return -1;
            
            for (int i = 0; i < levelButtonImages.Length; i++)
            {
                if (levelButtonImages[i] != null && RectTransformUtility.RectangleContainsScreenPoint(levelButtonImages[i].rectTransform, screenPos, null))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
