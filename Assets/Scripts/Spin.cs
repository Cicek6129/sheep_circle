using UnityEngine;

namespace SheepCircle
{
    /// <summary>
    /// Turns its transform at a fixed rate. Used for the ring of stars over a
    /// knocked-out animal: it has to keep spinning after the crash, and by then
    /// the round is over and GameManager has stopped ticking anything.
    /// </summary>
    public class Spin : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 130f;

        void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
    }
}
