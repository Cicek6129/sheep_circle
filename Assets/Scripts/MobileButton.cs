using System.Collections;
using UnityEngine;

namespace SheepCircle
{
    /// <summary>
    /// Universal mobile button component. Provides press/release bounce animation,
    /// optional idle pulse, minimum touch target enforcement, and haptic feedback.
    /// Attach to any UI element with a RectTransform to give it a native mobile feel.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MobileButton : MonoBehaviour
    {
        [Header("Press Animation")]
        [Tooltip("Scale when the button is pressed down.")]
        [SerializeField] float pressedScale = 0.88f;

        [Tooltip("Overshoot scale on release (bounce effect).")]
        [SerializeField] float bounceScale = 1.08f;

        [Tooltip("Duration of the press-down animation in seconds.")]
        [SerializeField] float pressDuration = 0.08f;

        [Tooltip("Duration of the release bounce animation in seconds.")]
        [SerializeField] float releaseDuration = 0.18f;

        [Header("Idle Pulse")]
        [Tooltip("Enable a gentle pulse animation when idle (for primary CTA buttons).")]
        [SerializeField] bool idlePulse;

        [Tooltip("Amplitude of the idle pulse (scale offset from 1.0).")]
        [SerializeField] float pulseAmplitude = 0.035f;

        [Tooltip("Speed of the idle pulse in Hz.")]
        [SerializeField] float pulseSpeed = 2.5f;

        [Header("Touch Target")]
        [Tooltip("Minimum touch target size in pixels (UI coordinates). Set to 0 to skip.")]
        [SerializeField] float minimumTouchSize = 120f;

        [Header("Feedback")]
        [Tooltip("Play a short haptic vibration on press (mobile only).")]
        [SerializeField] bool hapticOnPress;

        [Tooltip("Play tap sound through AudioManager on press.")]
        [SerializeField] bool playSoundOnPress = true;

        [Header("State")]
        [SerializeField] bool interactable = true;

        RectTransform rect;
        Vector3 baseScale;
        Coroutine animCoroutine;
        bool isPressed;
        bool isAnimating;

        /// <summary>Whether this button accepts input.</summary>
        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                if (!interactable && isPressed)
                {
                    isPressed = false;
                    SetScale(baseScale);
                }
            }
        }

        /// <summary>Whether to show idle pulse animation.</summary>
        public bool IdlePulse
        {
            get => idlePulse;
            set => idlePulse = value;
        }

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            baseScale = rect.localScale;
            EnforceMinimumTouchSize();
        }

        void OnEnable()
        {
            isPressed = false;
            isAnimating = false;
        }

        void OnDisable()
        {
            if (animCoroutine != null)
            {
                StopCoroutine(animCoroutine);
                animCoroutine = null;
            }
            isPressed = false;
            isAnimating = false;
            if (rect != null) rect.localScale = baseScale;
        }

        void Update()
        {
            if (!interactable || isPressed || isAnimating) return;
            if (!idlePulse) return;

            float s = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) * pulseAmplitude;
            rect.localScale = baseScale * s;
        }

        // ----------------------------------------------------------- public API

        /// <summary>Call when a pointer press begins over this button.</summary>
        public void Press()
        {
            if (!interactable || isPressed) return;

            isPressed = true;

            if (hapticOnPress)
            {
#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
            }

            if (playSoundOnPress && AudioManager.Instance != null)
                AudioManager.Instance.PlayTap();

            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(AnimatePress());
        }

        /// <summary>Call when the pointer is released.</summary>
        public void Release()
        {
            if (!isPressed) return;

            isPressed = false;

            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(AnimateRelease());
        }

        /// <summary>Convenience: press + immediate release (single tap).</summary>
        public void Tap()
        {
            if (!interactable) return;

            if (hapticOnPress)
            {
#if UNITY_ANDROID || UNITY_IOS
                Handheld.Vibrate();
#endif
            }

            if (playSoundOnPress && AudioManager.Instance != null)
                AudioManager.Instance.PlayTap();

            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(AnimateTap());
        }

        // ----------------------------------------------------------- animations

        IEnumerator AnimatePress()
        {
            isAnimating = true;
            yield return TweenScale(rect.localScale, baseScale * pressedScale, pressDuration);
            isAnimating = false;
        }

        IEnumerator AnimateRelease()
        {
            isAnimating = true;
            // Bounce up past 1.0
            yield return TweenScale(rect.localScale, baseScale * bounceScale, releaseDuration * 0.5f);
            // Settle back to 1.0
            yield return TweenScale(rect.localScale, baseScale, releaseDuration * 0.5f);
            isAnimating = false;
        }

        IEnumerator AnimateTap()
        {
            isAnimating = true;
            // Quick press down
            yield return TweenScale(rect.localScale, baseScale * pressedScale, pressDuration);
            // Bounce up
            yield return TweenScale(rect.localScale, baseScale * bounceScale, releaseDuration * 0.45f);
            // Settle
            yield return TweenScale(rect.localScale, baseScale, releaseDuration * 0.55f);
            isAnimating = false;
        }

        IEnumerator TweenScale(Vector3 from, Vector3 to, float duration)
        {
            if (duration <= 0f)
            {
                SetScale(to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out cubic for snappy feel
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                SetScale(Vector3.LerpUnclamped(from, to, ease));
                yield return null;
            }
            SetScale(to);
        }

        void SetScale(Vector3 s)
        {
            if (rect != null) rect.localScale = s;
        }

        // ----------------------------------------------------------- touch target

        void EnforceMinimumTouchSize()
        {
            if (minimumTouchSize <= 0f || rect == null) return;

            Vector2 size = rect.sizeDelta;
            if (size.x < minimumTouchSize) size.x = minimumTouchSize;
            if (size.y < minimumTouchSize) size.y = minimumTouchSize;
            rect.sizeDelta = size;
        }
    }
}
