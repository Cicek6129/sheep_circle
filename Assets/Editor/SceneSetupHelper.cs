using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace SheepCircle
{
    public static class SceneSetupHelper
    {
        static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        [MenuItem("Sheep Circle/Setup Level UI")]
        public static void SetupLevelUI()
        {
            var playHud = GameObject.Find("HUD/PlayHud");
            if (playHud == null) { Debug.LogError("PlayHud not found"); return; }

            var hudGO = GameObject.Find("HUD");
            if (hudGO == null) { Debug.LogError("HUD not found"); return; }

            // Clean up previously created objects if re-running
            var old1 = playHud.transform.Find("LevelText");
            if (old1 != null) Object.DestroyImmediate(old1.gameObject);
            var old2 = playHud.transform.Find("ProgressText");
            if (old2 != null) Object.DestroyImmediate(old2.gameObject);
            var old3 = hudGO.transform.Find("LevelComplete");
            if (old3 != null) Object.DestroyImmediate(old3.gameObject);

            // Update Hint text
            var hint = GameObject.Find("HUD/PlayHud/Hint");
            if (hint != null)
            {
                var hintTMP = hint.GetComponent<TextMeshProUGUI>();
                if (hintTMP != null) hintTMP.text = "Tikla veya Space -> hayvani gonder";
            }

            // Create LevelText
            var levelGO = CreateUIObject("LevelText", playHud.transform);
            var levelRect = levelGO.GetComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(0.5f, 1f);
            levelRect.anchorMax = new Vector2(0.5f, 1f);
            levelRect.pivot = new Vector2(0.5f, 1f);
            levelRect.anchoredPosition = new Vector2(0f, -10f);
            levelRect.sizeDelta = new Vector2(400f, 60f);
            var levelTMP = levelGO.AddComponent<TextMeshProUGUI>();
            levelTMP.text = "LEVEL 1";
            levelTMP.fontSize = 36;
            levelTMP.alignment = TextAlignmentOptions.Center;
            levelTMP.color = Color.white;

            // Create ProgressText
            var progGO = CreateUIObject("ProgressText", playHud.transform);
            var progRect = progGO.GetComponent<RectTransform>();
            progRect.anchorMin = new Vector2(0.5f, 0f);
            progRect.anchorMax = new Vector2(0.5f, 0f);
            progRect.pivot = new Vector2(0.5f, 0f);
            progRect.anchoredPosition = new Vector2(0f, 40f);
            progRect.sizeDelta = new Vector2(400f, 50f);
            var progTMP = progGO.AddComponent<TextMeshProUGUI>();
            progTMP.text = "0 / 6";
            progTMP.fontSize = 28;
            progTMP.alignment = TextAlignmentOptions.Center;
            progTMP.color = Color.white;

            // Create LevelComplete panel
            var lcGO = CreateUIObject("LevelComplete", hudGO.transform);
            var lcRect = lcGO.GetComponent<RectTransform>();
            lcRect.anchorMin = Vector2.zero;
            lcRect.anchorMax = Vector2.one;
            lcRect.sizeDelta = Vector2.zero;
            var lcImage = lcGO.AddComponent<Image>();
            lcImage.color = new Color(0f, 0f, 0f, 0.65f);
            lcGO.SetActive(false);

            // LevelComplete title
            var lcTitleGO = CreateUIObject("LevelCompleteTitle", lcGO.transform);
            var lcTitleRect = lcTitleGO.GetComponent<RectTransform>();
            lcTitleRect.anchorMin = new Vector2(0.5f, 0.5f);
            lcTitleRect.anchorMax = new Vector2(0.5f, 0.5f);
            lcTitleRect.pivot = new Vector2(0.5f, 0.5f);
            lcTitleRect.anchoredPosition = new Vector2(0f, 30f);
            lcTitleRect.sizeDelta = new Vector2(600f, 120f);
            var lcTitleTMP = lcTitleGO.AddComponent<TextMeshProUGUI>();
            lcTitleTMP.text = "LEVEL TAMAMLANDI!";
            lcTitleTMP.fontSize = 48;
            lcTitleTMP.alignment = TextAlignmentOptions.Center;
            lcTitleTMP.color = new Color(0.2f, 1f, 0.3f, 1f);

            // LevelComplete subtitle
            var lcSubGO = CreateUIObject("LevelCompleteSubtitle", lcGO.transform);
            var lcSubRect = lcSubGO.GetComponent<RectTransform>();
            lcSubRect.anchorMin = new Vector2(0.5f, 0.5f);
            lcSubRect.anchorMax = new Vector2(0.5f, 0.5f);
            lcSubRect.pivot = new Vector2(0.5f, 0.5f);
            lcSubRect.anchoredPosition = new Vector2(0f, -30f);
            lcSubRect.sizeDelta = new Vector2(400f, 50f);
            var lcSubTMP = lcSubGO.AddComponent<TextMeshProUGUI>();
            lcSubTMP.text = "Devam etmek icin tikla";
            lcSubTMP.fontSize = 24;
            lcSubTMP.alignment = TextAlignmentOptions.Center;
            lcSubTMP.color = Color.white;

            // Wire up HUD references
            var hud = hudGO.GetComponent<HUD>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(HUD).GetField("levelText", flags).SetValue(hud, levelTMP);
            typeof(HUD).GetField("progressText", flags).SetValue(hud, progTMP);
            typeof(HUD).GetField("levelCompletePanel", flags).SetValue(hud, lcGO);
            typeof(HUD).GetField("levelCompleteTitle", flags).SetValue(hud, lcTitleTMP);
            EditorUtility.SetDirty(hud);

            // Rename Lane2 to EntryLane, Lane0 to ExitLane for clarity
            var lane2 = GameObject.Find("Board/Lane2");
            if (lane2 != null) lane2.name = "EntryLane";
            var lane0 = GameObject.Find("Board/Lane0");
            if (lane0 != null) lane0.name = "ExitLane";

            // Save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log("Level UI setup complete!");
        }
    }
}
