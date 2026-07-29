using UnityEngine;

namespace SheepCircle
{
    public static class AudioGenerator
    {
        const int SampleRate = 44100;

        public static AudioClip GenerateTapSound()
        {
            float duration = 0.05f;
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];

            float frequency = 600f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float env = 1f - (float)i / sampleCount; // linear decay
                samples[i] = Mathf.Sin(t * frequency * 2 * Mathf.PI) * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("Tap", sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip GenerateScoreSound()
        {
            float duration = 0.3f;
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];

            float frequency1 = 880f;
            float frequency2 = 1320f;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-10f * t); // exponential decay
                float wave = (Mathf.Sin(t * frequency1 * 2 * Mathf.PI) + Mathf.Sin(t * frequency2 * 2 * Mathf.PI)) * 0.5f;
                samples[i] = wave * env * 0.3f;
            }

            AudioClip clip = AudioClip.Create("Score", sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip GenerateCrashSound()
        {
            float duration = 0.4f;
            int sampleCount = (int)(SampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-8f * t);
                // Simple white noise with lowpass-ish feel by just randomizing, 
                // but real lowpass needs state. We'll just use random.
                float noise = Random.Range(-1f, 1f);
                samples[i] = noise * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("Crash", sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
