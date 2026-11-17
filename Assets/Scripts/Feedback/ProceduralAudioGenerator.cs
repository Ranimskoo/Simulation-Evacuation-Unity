using UnityEngine;

namespace IgnisEscape.Feedback
{
    /// <summary>
    /// Menghasilkan AudioClip prosedural dari kode:
    /// - Bunyi benturan frekuensi rendah (Bagian 7B)
    /// - Nada panduan napas (Bagian 4)
    /// - Bunyi sirine alarm kebakaran dua-nada untuk atmosfer gedung (Bagian 6)
    /// </summary>
    public static class ProceduralAudioGenerator
    {
        public static AudioClip CreateLowFrequencyImpactClip(float durationSeconds = 0.25f, float baseFreqHz = 72f)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Exp(-12f * t);
                float freq = Mathf.Lerp(baseFreqHz, 32f, t / durationSeconds);
                float sub = Mathf.Sin(2f * Mathf.PI * freq * t);
                float thud = Mathf.Sin(2f * Mathf.PI * (freq * 0.5f) * t) * 0.5f;
                samples[i] = Mathf.Clamp((sub + thud) * envelope * 0.8f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural_LowFreqImpact", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateCalmBreathCueClip(float durationSeconds = 0.6f, float freqHz = 396f)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float norm = t / durationSeconds;
                float envelope = Mathf.Sin(Mathf.PI * norm) * Mathf.Exp(-2f * norm);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freqHz * t) * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create("Procedural_BreathCue", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip CreateFireAlarmLoopClip(float durationSeconds = 1.6f)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(sampleRate * durationSeconds));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float freq = (t % 0.8f < 0.4f) ? 880f : 660f;
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);
                float harmonic = Mathf.Sin(2f * Mathf.PI * (freq * 2f) * t) * 0.25f;
                samples[i] = (wave + harmonic) * 0.12f;
            }

            AudioClip clip = AudioClip.Create("Procedural_FireAlarmLoop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
