using System;
using UnityEngine;

namespace IgnisEscape.Player
{
    /// <summary>
    /// Mengelola ketahanan (health) pemain dan event dampak benturan/puing
    /// untuk memicu knockback serta umpan balik pseudo-haptic (Bagian 7B & 7C).
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float HealthNormalized => maxHealth > 0f ? currentHealth / maxHealth : 0f;

        public event Action<float, Vector3> OnDamaged;
        public event Action OnPlayerDepleted;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        public void ApplyDamage(float amount, Vector3 knockbackImpulse)
        {
            if (amount <= 0f) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnDamaged?.Invoke(amount, knockbackImpulse);

            if (currentHealth <= 0f)
            {
                OnPlayerDepleted?.Invoke();
            }
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
        }
    }
}

