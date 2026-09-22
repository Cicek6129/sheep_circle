using System.Collections;
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

        [Header("Mobile Panels")]
        [Tooltip("CanvasGroup on the game-over panel for fade animation.")]
        [SerializeField] CanvasGroup gameOverCanvasGroup;
        [Tooltip("CanvasGroup on the level-complete panel for fade animation.")]
        [SerializeField] CanvasGroup levelCompleteCanvasGroup;
        [Tooltip("The retry button on the game-over screen.")]
        [SerializeField] RectTransform retryButton;
        [Tooltip("The next-level button on the level-complete screen.")]
        [SerializeField] RectTransform nextLevelButton;

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
            // The start button's idle pulse is now handled by MobileButton.
            // Nothing else needs per-frame work here.
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

            if (levelCompleteCanvasGroup != null && levelCompletePanel != null)
                StartCoroutine(AnimatePanelIn(levelCompleteCanvasGroup, levelCompletePanel.GetComponent<RectTransform>()));
        }

        public void HideLevelComplete()
        {
            if (levelCompleteCanvasGroup != null)
            {
                levelCompleteCanvasGroup.alpha = 0f;
                levelCompleteCanvasGroup.interactable = false;
                levelCompleteCanvasGroup.blocksRaycasts = false;
            }
            if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        }

        // ----------------------------------------------------------- game over

        public void HideGameOver()
        {
            if (gameOverCanvasGroup != null)
            {
                gameOverCanvasGroup.alpha = 0f;
                gameOverCanvasGroup.interactable = false;
                gameOverCanvasGroup.blocksRaycasts = false;
            }
            gameOverPanel.SetActive(false);
        }

        public void ShowGameOver(string reason, int placed)
        {
            gameOverPanel.SetActive(true);
            gameOverTitle.text = reason;
            gameOverBody.text = $"{placed} hayvan yerlesti";

            if (gameOverCanvasGroup != null)
                StartCoroutine(AnimatePanelIn(gameOverCanvasGroup, gameOverPanel.GetComponent<RectTransform>()));
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

        // ----------------------------------------------------------- mobile buttons

        public bool IsPointerOverRetryButton(Vector2 screenPos)
        {
            if (retryButton == null || !gameOverPanel.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(retryButton, screenPos, null);
        }

        public bool IsPointerOverNextLevelButton(Vector2 screenPos)
        {
            if (nextLevelButton == null || levelCompletePanel == null || !levelCompletePanel.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(nextLevelButton, screenPos, null);
        }

        /// <summary>Triggers the MobileButton tap animation on the given RectTransform, if it has one.</summary>
        public void TriggerButtonTap(RectTransform button)
        {
            if (button == null) return;
            var mb = button.GetComponent<MobileButton>();
            if (mb != null) mb.Tap();
        }

        // ----------------------------------------------------------- panel animation

        /// <summary>Fade + scale entrance animation for overlay panels.</summary>
        IEnumerator AnimatePanelIn(CanvasGroup group, RectTransform panelRect)
        {
            if (group == null) yield break;

            const float duration = 0.3f;
            Vector3 targetScale = Vector3.one;

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            if (panelRect != null) panelRect.localScale = Vector3.one * 0.85f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out cubic
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);

                group.alpha = ease;
                if (panelRect != null)
                    panelRect.localScale = Vector3.LerpUnclamped(Vector3.one * 0.85f, targetScale, ease);

                yield return null;
            }

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            if (panelRect != null) panelRect.localScale = targetScale;
        }
    }
}
