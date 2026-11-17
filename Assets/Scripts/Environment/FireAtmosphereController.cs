using System.Collections.Generic;
using UnityEngine;
using IgnisEscape.Feedback;

namespace IgnisEscape.Environment
{
    /// <summary>
    /// Mengelola atmosfer kebakaran yang bersifat HANYA VISUAL & AUDIO (tidak mengurangi
    /// health dan tidak menghalangi jalur) (Bagian 6):
    /// - Kepadatan asap koridor (ParticleSystem) naik pelan seiring waktu.
    /// - Lampu darurat dinding berkedip (intensitas berfluktuasi).
    /// - Bunyi alarm kebakaran prosedural looping.
    /// </summary>
    public class FireAtmosphereController : MonoBehaviour
    {
        [Header("Lampu Darurat Berkedip")]
        [SerializeField] private List<Light> flickeringEmergencyLights = new List<Light>();
        [SerializeField] private float minLightIntensity = 0.25f;
        [SerializeField] private float maxLightIntensity = 1.65f;

        [Header("Asap Koridor Bertahap")]
        [SerializeField] private List<ParticleSystem> corridorSmokeSystems = new List<ParticleSystem>();
        [SerializeField] private float initialEmissionRate = 2f;
        [SerializeField] private float maxEmissionRate = 12f;
        [SerializeField] private float smokeRampUpDuration = 90f;

        [Header("Audio Alarm Prosedural")]
        [SerializeField] private AudioSource alarmAudioSource;

        private float _elapsedTime;

        private void Awake()
        {
            if (alarmAudioSource == null)
            {
                alarmAudioSource = gameObject.AddComponent<AudioSource>();
            }
            alarmAudioSource.loop = true;
            alarmAudioSource.spatialBlend = 0.2f;
            alarmAudioSource.volume = 0.22f;
            alarmAudioSource.clip = ProceduralAudioGenerator.CreateFireAlarmLoopClip();
        }

        private void Start()
        {
            if (alarmAudioSource != null && alarmAudioSource.clip != null)
            {
                alarmAudioSource.Play();
            }
        }

        private void Update()
        {
            _elapsedTime += Time.deltaTime;

            // Fluktuasi intensitas lampu darurat dinding
            for (int i = 0; i < flickeringEmergencyLights.Count; i++)
            {
                Light l = flickeringEmergencyLights[i];
                if (l == null) continue;
                float n = Mathf.PerlinNoise(Time.time * 6.5f + i * 3.1f, i * 1.7f);
                l.intensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, n);
            }

            // Kepadatan asap koridor naik pelan seiring waktu
            float smokeT = Mathf.Clamp01(_elapsedTime / Mathf.Max(1f, smokeRampUpDuration));
            float targetRate = Mathf.Lerp(initialEmissionRate, maxEmissionRate, smokeT);
            for (int i = 0; i < corridorSmokeSystems.Count; i++)
            {
                ParticleSystem ps = corridorSmokeSystems[i];
                if (ps == null) continue;
                var emission = ps.emission;
                emission.rateOverTime = targetRate;
            }
        }
    }
}

