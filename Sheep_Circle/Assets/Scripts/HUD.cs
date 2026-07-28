using TMPro;
using UnityEngine;

namespace SheepCircle
{
    /// <summary>Score readout plus the game-over card.</summary>
    public class HUD : MonoBehaviour
    {
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text bestText;
        [SerializeField] GameObject gameOverPanel;
        [SerializeField] TMP_Text gameOverTitle;
        [SerializeField] TMP_Text gameOverBody;

        public void SetScore(int score) => scoreText.text = score.ToString();

        public void SetBest(int best) => bestText.text = $"REKOR  {best}";

        public void HideGameOver() => gameOverPanel.SetActive(false);

        public void ShowGameOver(string reason, int score)
        {
            gameOverPanel.SetActive(true);
            gameOverTitle.text = reason;
            gameOverBody.text = $"{score} hayvan agila girdi\n\nTekrar icin tikla";
        }
    }
}
