using UnityEngine;
using IgnisEscape.Core;
using IgnisEscape.Objects;

namespace IgnisEscape.Objects
{
    /// <summary>
    /// Trigger di balik Fire Exit Door yang menandai keberhasilan evakuasi saat
    /// pintu sudah Opened (Bagian 5A).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ExitGoalTrigger : MonoBehaviour
    {
        [SerializeField] private FireExitDoor fireExitDoor;

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
            if (fireExitDoor == null)
            {
                fireExitDoor = FindFirstObjectByType<FireExitDoor>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (fireExitDoor != null && !fireExitDoor.IsOpened) return;

            var gm = FindFirstObjectByType<GameManager>();
            if (gm != null)
            {
                gm.NotifyPlayerEscaped();
            }
        }
    }
}

