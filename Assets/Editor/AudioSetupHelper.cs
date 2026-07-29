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

            // 3. Ses butonunu ana HUD'a ekle (oyun oynanırken de görünsün)
            var mainHud = GameObject.Find("HUD");
            if (mainHud != null)
            {
                var soundOn = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/icon_sound_on.png");
                var soundOff = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/icon_sound_off.png");

                // Varsa eski butonları temizle (StartPanel'de veya ana HUD'da olanları)
                var oldBtn1 = GameObject.Find("HUD/StartPanel/SoundButton");
                if (oldBtn1 != null) Object.DestroyImmediate(oldBtn1);
                var oldBtn2 = GameObject.Find("HUD/SoundButton");
                if (oldBtn2 != null) Object.DestroyImmediate(oldBtn2);

                // Sol üste yeni butonu ekle
                var soundBtn = new GameObject("SoundButton", typeof(RectTransform));
                soundBtn.transform.SetParent(mainHud.transform, false);
                var soundImg = soundBtn.AddComponent<Image>();
                if (soundOn != null) soundImg.sprite = soundOn;
                
                var rect = soundBtn.GetComponent<RectTransform>();
                // Sol üst köşe
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(70f, -70f);
                rect.sizeDelta = new Vector2(90f, 90f);
                soundBtn.transform.SetAsLastSibling(); // En üstte görünsün

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
