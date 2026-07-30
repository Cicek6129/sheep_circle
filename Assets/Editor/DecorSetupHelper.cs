using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheepCircle.EditorTools
{
    /// <summary>
    /// Scatters farm scenery over the empty grass and the middle island of
    /// whatever scene is open. Purely cosmetic - nothing here is tapped, and
    /// nothing collides.
    ///
    /// Placement rule: everything stays clear of all four road corridors, not
    /// just the two the board currently uses, so bringing the north and south
    /// lanes back cannot leave a barn sitting in the road. That is why the large
    /// pieces sit on the diagonals rather than straight above and below the ring.
    ///
    /// Runs as its own menu item instead of going through GameSceneBuilder,
    /// which would rebuild the board and discard the level and audio wiring.
    ///
    /// Sheep Circle > Setup Decor.
    /// </summary>
    public static class DecorSetupHelper
    {
        const string ArtDir = "Assets/Art";
        const string RootName = "Decor";

        // Matches GameSceneBuilder: ring outer edge and half the road width.
        const float RingOuter = 3.35f;
        const float RoadHalfWidth = 1.30f;
        const float IslandRadius = 1.90f;

        // Under the animals and their shadows, over the ground.
        const int GroundOrder = -5;   // pond, field: flat on the grass
        const int PropOrder = -4;     // everything that stands up

        struct Piece
        {
            public string sprite;
            public float x, y, size;
            public int order;

            public Piece(string sprite, float x, float y, float size, int order = PropOrder)
            {
                this.sprite = sprite; this.x = x; this.y = y; this.size = size; this.order = order;
            }
        }

        static readonly Piece[] Outer =
        {
            new Piece("pond",       -5.9f, -3.6f, 2.9f, GroundOrder),
            new Piece("crop_field",  5.7f, -3.7f, 2.9f, GroundOrder),
            new Piece("barn",       -5.7f,  3.6f, 2.7f),
            new Piece("windmill",    5.8f,  3.8f, 2.5f),
            new Piece("fence",       5.7f, -2.1f, 2.9f),   // top edge of the field
            new Piece("trough",     -3.6f,  3.0f, 0.95f),
            new Piece("signpost",    3.5f,  3.2f, 0.90f),
            new Piece("scarecrow",   3.7f, -3.2f, 1.00f),
            new Piece("rocks",      -3.5f, -3.0f, 0.90f),
        };

        static readonly Piece[] Island =
        {
            new Piece("tree",         -0.70f,  0.50f, 1.05f),
            new Piece("haybale",       0.75f,  0.60f, 0.80f),
            new Piece("well",          0.72f, -0.62f, 0.75f),
            new Piece("bush",         -0.75f, -0.55f, 0.65f),
            new Piece("flower_patch",  0.02f, -1.05f, 0.55f),
        };

        [MenuItem("Sheep Circle/Setup Decor")]
        public static void Setup()
        {
            ImportArt();

            // Rebuild the whole group each run so the menu item is repeatable.
            GameObject old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(RootName);

            int placed = 0;
            placed += Place(Outer, root.transform, "Outer");
            placed += Place(Island, root.transform, "Island");

            if (placed == 0)
            {
                Debug.LogError("Sheep Circle: no decor sprites found in Assets/Art.");
                return;
            }

            Warn();

            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            Debug.Log($"Sheep Circle: decor placed. {placed} pieces.");
        }

        static int Place(Piece[] pieces, Transform parent, string groupName)
        {
            var group = new GameObject(groupName);
            group.transform.SetParent(parent, false);

            int placed = 0;
            foreach (Piece p in pieces)
            {
                Sprite sprite = Load(p.sprite);
                if (sprite == null)
                {
                    Debug.LogWarning($"Sheep Circle: {ArtDir}/{p.sprite}.png missing, skipped.");
                    continue;
                }

                var go = new GameObject(p.sprite);
                go.transform.SetParent(group.transform, false);
                go.transform.position = new Vector3(p.x, p.y, 0f);
                go.transform.localScale = Vector3.one * p.size;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = p.order;

                placed++;
            }

            return placed;
        }

        /// <summary>Flags anything that would sit on a road or on the ring, in
        /// case the numbers above are edited later.</summary>
        static void Warn()
        {
            foreach (Piece p in Outer)
            {
                if (!Extents(p, out float halfW, out float halfH)) continue;

                if ((Mathf.Abs(p.y) - halfH) < RoadHalfWidth || (Mathf.Abs(p.x) - halfW) < RoadHalfWidth)
                    Debug.LogWarning($"Sheep Circle: {p.sprite} overlaps a road corridor.");

                if (new Vector2(p.x, p.y).magnitude - Mathf.Max(halfW, halfH) < RingOuter)
                    Debug.LogWarning($"Sheep Circle: {p.sprite} overlaps the ring.");
            }

            foreach (Piece p in Island)
            {
                if (!Extents(p, out float halfW, out float halfH)) continue;

                float reach = new Vector2(p.x, p.y).magnitude + Mathf.Max(halfW, halfH);
                if (reach > IslandRadius)
                    Debug.LogWarning($"Sheep Circle: {p.sprite} spills off the island ({reach:0.00} > {IslandRadius}).");
            }
        }

        /// <summary>Half-extents from the sprite's own aspect. The importer gives
        /// every piece a one-unit width, so size is the world width and the height
        /// follows the texture. Measuring a wide strip like the fence as if it
        /// were square reports it blocking a road it clears easily.</summary>
        static bool Extents(Piece p, out float halfW, out float halfH)
        {
            halfW = halfH = 0f;

            Sprite sprite = Load(p.sprite);
            if (sprite == null) return false;

            halfW = p.size * 0.5f;
            halfH = p.size * (sprite.rect.height / sprite.rect.width) * 0.5f;
            return true;
        }

        static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/{name}.png");

        /// <summary>One world unit across, so the scale above reads directly as
        /// the piece's size on the board.</summary>
        static void ImportArt()
        {
            string[] names =
            {
                "barn", "windmill", "pond", "crop_field", "well", "scarecrow",
                "flower_patch", "rocks", "signpost", "fence_corner",
                "tree", "bush", "haybale", "trough", "fence",
            };

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
