using UnityEngine;

namespace SheepCircle
{
    /// <summary>
    /// A one-shot sprite that pops, fades and deletes itself. Used for the crash
    /// burst. It drives its own clock rather than being ticked by GameManager,
    /// because it has to keep animating after the round is already over.
    /// </summary>
    public class Burst : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] float life = 0.7f;
        [SerializeField] float startScale = 0.45f;
        [SerializeField] float endScale = 1.3f;
        [SerializeField] float maxSpin = 22f;

        [Tooltip("Fraction of the life held at full opacity before the fade starts.")]
        [SerializeField] float hold = 0.35f;

        float age;
        float worldSize = 1f;
        float spin;

        public void Play(Vector2 position, float size)
        {
            transform.position = new Vector3(position.x, position.y, 0f);
            worldSize = size;
            age = 0f;
            spin = Random.Range(-maxSpin, maxSpin);
            Apply();
        }

        void Update()
        {
            age += Time.deltaTime;

            if (age >= life)
            {
                Destroy(gameObject);
                return;
            }

            Apply();
        }

        void Apply()
        {
            float t = Mathf.Clamp01(age / life);

            // Snaps out fast then eases, so the hit reads as sudden rather than
            // as something growing.
            float pop = 1f - Mathf.Pow(1f - t, 3f);

            float scale = Mathf.Lerp(startScale, endScale, pop) * worldSize;
            transform.localScale = new Vector3(scale, scale, 1f);
            transform.rotation = Quaternion.Euler(0f, 0f, spin * pop);

            if (sprite == null) return;

            Color c = sprite.color;
            c.a = t <= hold ? 1f : 1f - (t - hold) / (1f - hold);
            sprite.color = c;
        }
    }
}
