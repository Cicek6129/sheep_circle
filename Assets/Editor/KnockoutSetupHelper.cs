using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheepCircle.EditorTools
{
    /// <summary>
    /// Wires the crash knock-out into whatever scene is currently open: the fallen
    /// sprite for each kind, the ring of stars on the animal prefab, and the wool
    /// debris burst.
    ///
    /// This patches in place instead of going through GameSceneBuilder, because
    /// that rebuilds the board from scratch and would throw away the level layout
    /// and audio wiring the scene already carries.
    ///
    /// Sheep Circle > Setup Knockout.
    /// </summary>
    public static class KnockoutSetupHelper
    {
        const string ArtDir = "Assets/Art";
        const string AnimalPrefabPath = "Assets/Prefabs/Animal.prefab";
        const string DebrisPrefabPath = "Assets/Prefabs/Debris.prefab";

        // Sits over the body (10) but under the crash burst (30).
        const int DizzyOrder = 20;

        public static void Setup()
        {
            ImportArt("sheep_ko", "cow_ko", "goat_ko", "chicken_ko", "dizzy_stars", "debris_wool");

            Sprite stars = Load("dizzy_stars");
            Sprite wool = Load("debris_wool");

            if (stars == null || wool == null)
            {
                Debug.LogError("Sheep Circle: dizzy_stars.png or debris_wool.png missing from Assets/Art.");
                return;
            }

            AddDizzyToAnimalPrefab(stars);
            Burst debris = BuildDebrisPrefab(wool);
            WireScene(debris);

            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------- prefab

        /// <summary>Adds the star ring to the animal prefab, switched off. Animal
        /// turns it on in KnockOut.</summary>
        static void AddDizzyToAnimalPrefab(Sprite stars)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(AnimalPrefabPath);
            if (root == null)
            {
                Debug.LogError($"Sheep Circle: {AnimalPrefabPath} not found.");
                return;
            }

            Transform existing = root.transform.Find("Dizzy");
            GameObject dizzy = existing != null ? existing.gameObject : new GameObject("Dizzy");
            dizzy.transform.SetParent(root.transform, false);

            // Offset toward the head, which the sprites all point at +X.
            dizzy.transform.localPosition = new Vector3(0.36f, 0.30f, 0f);
            dizzy.transform.localScale = Vector3.one * 0.55f;

            // Not "?? AddComponent": ?? bypasses Unity's overloaded == , so a
            // missing component comes back as a non-null stand-in and the
            // AddComponent never runs.
            var sr = dizzy.GetComponent<SpriteRenderer>();
            if (sr == null) sr = dizzy.AddComponent<SpriteRenderer>();

            sr.sprite = stars;
            sr.sortingOrder = DizzyOrder;

            if (dizzy.GetComponent<Spin>() == null) dizzy.AddComponent<Spin>();

            dizzy.SetActive(false);

            var animal = root.GetComponent<Animal>();
            var so = new SerializedObject(animal);
            so.FindProperty("dizzy").objectReferenceValue = sr;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, AnimalPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);

            Debug.Log("Sheep Circle: Dizzy stars added to Animal.prefab.");
        }

        static Burst BuildDebrisPrefab(Sprite wool)
        {
            Directory.CreateDirectory("Assets/Prefabs");

            var root = new GameObject("Debris");
            var burst = root.AddComponent<Burst>();

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = wool;
            sr.sortingOrder = 28;

            // Slower and wider than the crash flash: the tufts should still be
            // drifting once the burst itself has gone.
            var so = new SerializedObject(burst);
            so.FindProperty("sprite").objectReferenceValue = sr;
            so.FindProperty("life").floatValue = 0.9f;
            so.FindProperty("startScale").floatValue = 0.35f;
            so.FindProperty("endScale").floatValue = 1.5f;
            so.FindProperty("maxSpin").floatValue = 35f;
            so.FindProperty("hold").floatValue = 0.2f;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, DebrisPrefabPath);
            Object.DestroyImmediate(root);

            return asset.GetComponent<Burst>();
        }

        // -------------------------------------------------------------- scene

        static void WireScene(Burst debris)
        {
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                Debug.LogError("Sheep Circle: no GameManager in the open scene. Open Game.unity first.");
                return;
            }

            var so = new SerializedObject(gm);
            so.FindProperty("debrisPrefab").objectReferenceValue = debris;

            SerializedProperty kinds = so.FindProperty("kinds");
            int matched = 0;

            for (int i = 0; i < kinds.arraySize; i++)
            {
                SerializedProperty el = kinds.GetArrayElementAtIndex(i);
                SerializedProperty bodyProp = el.FindPropertyRelative("bodySprite");
                SerializedProperty koProp = el.FindPropertyRelative("koSprite");

                if (koProp == null) continue;

                // Match on the standing sprite's own name - sheep -> sheep_ko -
                // rather than on displayName, which keeps getting retyped as the
                // Turkish spellings are fixed up.
                var body = bodyProp != null ? bodyProp.objectReferenceValue as Sprite : null;
                if (body == null) continue;

                Sprite ko = Load(body.name + "_ko");
                if (ko == null) continue;

                koProp.objectReferenceValue = ko;
                matched++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gm);
            EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            EditorSceneManager.SaveScene(gm.gameObject.scene);

            Debug.Log($"Sheep Circle: knockout wired. {matched} of {kinds.arraySize} kinds got a fallen sprite.");
        }

        // ------------------------------------------------------------ helpers

        static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");

        /// <summary>Sprite import settings the board relies on: one world unit
        /// across, so Animal.Setup can scale by kind.size.</summary>
        static void ImportArt(params string[] names)
        {
            foreach (string name in names)
            {
                string path = $"{ArtDir}/{name}.png";
                if (!File.Exists(path)) continue;
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;

                if (importer.textureType == TextureImporterType.Sprite
                    && Mathf.Approximately(importer.spritePixelsPerUnit, tex.width)
                    && importer.alphaIsTransparency
                    && !importer.mipmapEnabled)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = tex.width;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
    }
}
