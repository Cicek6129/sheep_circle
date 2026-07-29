using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

namespace SheepCircle
{
    public static class AudioSetupHelper
    {
        [MenuItem("Sheep Circle/Inject Audio System To Scene")]
        public static void InjectAudio()
        {
            // 0. HAFİZADAKİ BOZUK SAHNEYİ SİLİP DOĞRU OLANI ZORLA YÜKLE
            // Tahir'in hatasız sahnesini diskten yükle (Eğer kaydet sorarsa DON'T SAVE diyecek)
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

            // 1. AudioListener'ı kameraya ekle
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<AudioListener>() == null)
            {
                cam.gameObject.AddComponent<AudioListener>();
            }

            // 2. AudioManager'ı ekle
            if (GameObject.Find("AudioManager") == null)
            {
                var am = new GameObject("AudioManager");
                am.AddComponent<AudioManager>();
            }

            // 3. Ses butonunu StartPanel'e ekle
            var startPanel = GameObject.Find("HUD/StartPanel");
            if (startPanel != null && startPanel.transform.Find("SoundButton") == null)
            {
                var soundOn = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/icon_sound_on.png");
                var soundOff = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/icon_sound_off.png");

                var soundBtn = new GameObject("SoundButton", typeof(RectTransform));
                soundBtn.transform.SetParent(startPanel.transform, false);
                var soundImg = soundBtn.AddComponent<Image>();
                if (soundOn != null) soundImg.sprite = soundOn;
                
                var rect = soundBtn.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-70f, -70f);
                rect.sizeDelta = new Vector2(80f, 80f);

                // HUD referanslarını güncelle
                var hud = Object.FindObjectOfType<HUD>();
                if (hud != null)
                {
                    var so = new SerializedObject(hud);
                    so.FindProperty("soundButtonImage").objectReferenceValue = soundImg;
                    so.FindProperty("soundOnSprite").objectReferenceValue = soundOn;
                    so.FindProperty("soundOffSprite").objectReferenceValue = soundOff;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(hud);
                }
            }

            // Ayrıca BASLA kelimesini BAŞLA yapalım (Tahir'in sahnesindeki buton için)
            var startLabel = GameObject.Find("HUD/StartPanel/StartButton/Label");
            if (startLabel != null)
            {
                var tmp = startLabel.GetComponent<TMPro.TextMeshProUGUI>();
                if (tmp != null && tmp.text == "BASLA")
                {
                    tmp.text = "BAŞLA";
                    EditorUtility.SetDirty(tmp);
                }
            }

            // Tahir'in UI eklentisini de çalıştır (Level yazılarını geri getirmek için)
            SceneSetupHelper.SetupLevelUI();

            // Sahneyi kaydet
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            
            Debug.Log("Sahne basariyla tamir edildi! Oynamaya hazir!");
        }
    }
}
