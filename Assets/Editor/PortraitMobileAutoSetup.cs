using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;
using SheepCircle;

namespace SheepCircle.Editor
{
    [InitializeOnLoad]
    public static class PortraitMobileAutoSetup
    {
        static PortraitMobileAutoSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    RunSetup(false);
                }
            };
        }

        [MenuItem("SheepCircle/Mobil Dikey (Portrait) Ekranı Ayarla")]
        public static void MenuRunSetup()
        {
            RunSetup(true);
            Debug.Log("Mobil Dikey (Portrait) Kamera ve UI ayarları başarıyla sahneye uygulandı!");
        }

        public static void RunSetup(bool log)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) return;

            bool modified = false;

            // 1. Setup Camera.main for Portrait World Width = 13.0
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                var scaler = cam.GetComponent<MobileCameraScaler>();
                if (scaler == null)
                {
                    scaler = cam.gameObject.AddComponent<MobileCameraScaler>();
                    modified = true;
                }
                if (!Mathf.Approximately(scaler.targetWorldWidth, 13.0f) ||
                    !Mathf.Approximately(scaler.targetWorldHeight, 16.0f))
                {
                    scaler.targetWorldWidth = 13.0f;
                    scaler.targetWorldHeight = 16.0f;
                    modified = true;
                }
                scaler.UpdateCameraSize();
                if (log) Debug.Log($"[PortraitMobileAutoSetup] Kamera orthographicSize = {cam.orthographicSize} olarak ayarlandı.");
            }

            // 2. Setup CanvasScaler for 1080x1920
            var canvasScaler = Object.FindFirstObjectByType<CanvasScaler>();
            if (canvasScaler != null)
            {
                if (canvasScaler.referenceResolution != new Vector2(1080, 1920) ||
                    canvasScaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
                    !Mathf.Approximately(canvasScaler.matchWidthOrHeight, 0f))
                {
                    canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    canvasScaler.referenceResolution = new Vector2(1080, 1920);
                    canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    canvasScaler.matchWidthOrHeight = 0f;
                    modified = true;
                    if (log) Debug.Log("[PortraitMobileAutoSetup] CanvasScaler 1080x1920 (Portrait) yapıldı.");
                }
            }

            // 3. Setup PlayHud and UI elements for Portrait layout
            var playHud = GameObject.Find("HUD/PlayHud");
            if (playHud != null)
            {
                var safeArea = playHud.GetComponent<SafeAreaFitter>();
                if (safeArea == null)
                {
                    safeArea = playHud.AddComponent<SafeAreaFitter>();
                    modified = true;
                }

                // LevelText -> top center
                var levelText = playHud.transform.Find("LevelText") as RectTransform;
                if (levelText != null)
                {
                    levelText.anchorMin = new Vector2(0.5f, 1f);
                    levelText.anchorMax = new Vector2(0.5f, 1f);
                    levelText.pivot = new Vector2(0.5f, 1f);
                    levelText.anchoredPosition = new Vector2(0f, -60f);
                    levelText.sizeDelta = new Vector2(500f, 60f);
                    modified = true;
                }

                // ProgressText -> top center under LevelText
                var progText = playHud.transform.Find("ProgressText") as RectTransform;
                if (progText != null)
                {
                    progText.anchorMin = new Vector2(0.5f, 1f);
                    progText.anchorMax = new Vector2(0.5f, 1f);
                    progText.pivot = new Vector2(0.5f, 1f);
                    progText.anchoredPosition = new Vector2(0f, -120f);
                    progText.sizeDelta = new Vector2(500f, 50f);
                    modified = true;
                }

                // Hint -> bottom center
                var hint = playHud.transform.Find("Hint") as RectTransform;
                if (hint != null)
                {
                    hint.anchorMin = new Vector2(0.5f, 0f);
                    hint.anchorMax = new Vector2(0.5f, 0f);
                    hint.pivot = new Vector2(0.5f, 0f);
                    hint.anchoredPosition = new Vector2(0f, 80f);
                    hint.sizeDelta = new Vector2(800f, 60f);
                    var hintTMP = hint.GetComponent<TextMeshProUGUI>();
                    if (hintTMP != null && hintTMP.text != "Göndermek için DOKUN!")
                    {
                        hintTMP.text = "Göndermek için DOKUN!";
                    }
                    modified = true;
                }
            }

            // 4. Fix ALL TextMeshPro components in HUD (GameOverPanel, LevelCompletePanel, etc.)
            var hud = Object.FindFirstObjectByType<HUD>();
            if (hud != null)
            {
                var allTMPs = hud.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var tmp in allTMPs)
                {
                    var rect = tmp.rectTransform;
                    if (rect != null)
                    {
                        // Ensure no text box is wider than 960px (fits 1080 screen with 60px margins)
                        if (rect.sizeDelta.x > 960f)
                        {
                            rect.sizeDelta = new Vector2(960f, rect.sizeDelta.y);
                            modified = true;
                        }
                    }

                    // Enable auto-wrapping and sizing for titles so they never overflow horizontally
                    if (tmp.fontSize > 45f)
                    {
                        tmp.enableWordWrapping = true;
                        tmp.enableAutoSizing = true;
                        tmp.fontSizeMin = 28f;
                        tmp.fontSizeMax = tmp.fontSize;
                        tmp.alignment = TextAlignmentOptions.Center;
                        modified = true;
                    }
                }

                // 5. Fix vertical spacing and auto-wrap on GameOverPanel
                var goPanel = hud.transform.Find("GameOver");
                if (goPanel != null)
                {
                    var goTitle = goPanel.Find("Title") as RectTransform;
                    if (goTitle == null) goTitle = goPanel.Find("GameOverTitle") as RectTransform;
                    if (goTitle != null)
                    {
                        goTitle.anchorMin = new Vector2(0.5f, 0.5f);
                        goTitle.anchorMax = new Vector2(0.5f, 0.5f);
                        goTitle.pivot = new Vector2(0.5f, 0.5f);
                        goTitle.anchoredPosition = new Vector2(0f, 200f);
                        goTitle.sizeDelta = new Vector2(960f, 180f);
                        var tmp = goTitle.GetComponent<TextMeshProUGUI>();
                        if (tmp != null)
                        {
                            tmp.enableWordWrapping = true;
                            tmp.enableAutoSizing = true;
                            tmp.fontSizeMin = 32f;
                            tmp.fontSizeMax = 72f;
                            tmp.alignment = TextAlignmentOptions.Center;
                        }
                        modified = true;
                    }

                    var goBody = goPanel.Find("Body") as RectTransform;
                    if (goBody == null) goBody = goPanel.Find("GameOverBody") as RectTransform;
                    if (goBody != null)
                    {
                        goBody.anchorMin = new Vector2(0.5f, 0.5f);
                        goBody.anchorMax = new Vector2(0.5f, 0.5f);
                        goBody.pivot = new Vector2(0.5f, 0.5f);
                        goBody.anchoredPosition = new Vector2(0f, -40f);
                        goBody.sizeDelta = new Vector2(920f, 160f);
                        var tmp = goBody.GetComponent<TextMeshProUGUI>();
                        if (tmp != null)
                        {
                            tmp.enableWordWrapping = true;
                            tmp.enableAutoSizing = true;
                            tmp.fontSizeMin = 24f;
                            tmp.fontSizeMax = 44f;
                            tmp.alignment = TextAlignmentOptions.Center;
                        }
                        modified = true;
                    }
                }

                // 6. Fix vertical spacing and auto-wrap on LevelCompletePanel
                var lcPanel = hud.transform.Find("LevelComplete");
                if (lcPanel != null)
                {
                    var lcTitle = lcPanel.Find("LevelCompleteTitle") as RectTransform;
                    if (lcTitle != null)
                    {
                        lcTitle.anchorMin = new Vector2(0.5f, 0.5f);
                        lcTitle.anchorMax = new Vector2(0.5f, 0.5f);
                        lcTitle.pivot = new Vector2(0.5f, 0.5f);
                        lcTitle.anchoredPosition = new Vector2(0f, 220f);
                        lcTitle.sizeDelta = new Vector2(960f, 140f);
                        var tmp = lcTitle.GetComponent<TextMeshProUGUI>();
                        if (tmp != null)
                        {
                            tmp.enableWordWrapping = true;
                            tmp.enableAutoSizing = true;
                            tmp.fontSizeMin = 32f;
                            tmp.fontSizeMax = 64f;
                            tmp.alignment = TextAlignmentOptions.Center;
                        }
                        modified = true;
                    }

                    var lcSub = lcPanel.Find("LevelCompleteSubtitle") as RectTransform;
                    if (lcSub != null)
                    {
                        lcSub.anchorMin = new Vector2(0.5f, 0.5f);
                        lcSub.anchorMax = new Vector2(0.5f, 0.5f);
                        lcSub.pivot = new Vector2(0.5f, 0.5f);
                        lcSub.anchoredPosition = new Vector2(0f, -150f);
                        lcSub.sizeDelta = new Vector2(920f, 80f);
                        var tmp = lcSub.GetComponent<TextMeshProUGUI>();
                        if (tmp != null)
                        {
                            tmp.enableWordWrapping = true;
                            tmp.enableAutoSizing = true;
                            tmp.fontSizeMin = 24f;
                            tmp.fontSizeMax = 36f;
                            tmp.alignment = TextAlignmentOptions.Center;
                        }
                        modified = true;
                    }
                }
            }

            if (modified && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }
    }
}
