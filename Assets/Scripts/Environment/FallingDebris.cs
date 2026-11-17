using UnityEngine;
using IgnisEscape.Player;

namespace IgnisEscape.Environment
{
    /// <summary>
    /// Puing plafon gipsum atau balok jatuh (Rigidbody + BoxCollider) yang mengurangi
    /// ketahanan pemain, memberi knockback, lalu tetap di lantai sebagai penghalang
    /// fisik (Bagian 7C).
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class FallingDebris : MonoBehaviour
    {
        [SerializeField] private float damageAmount = 15f;
        [SerializeField] private float knockbackForce = 4.5f;

        private bool _hasHitPlayer;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hasHitPlayer) return;

            if (collision.gameObject.CompareTag("Player"))
            {
                _hasHitPlayer = true;
                var health = collision.gameObject.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    Vector3 pushDir = (collision.transform.position - transform.position);
                    pushDir.y = 0f;
                    if (pushDir.sqrMagnitude < 0.001f) pushDir = Vector3.back;
                    health.ApplyDamage(damageAmount, pushDir.normalized * knockbackForce);
                }
            }
        }
    }
}

