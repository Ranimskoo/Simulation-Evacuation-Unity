using System.Collections.Generic;
using UnityEngine;
using IgnisEscape.Core;

namespace IgnisEscape.Objects
{
    /// <summary>
    /// Menggerakkan parameter Animator pintu darurat (Locked -> Unlocking -> Opened)
    /// untuk Pintu Keluar Darurat A maupun B, membuka OcclusionPortal, serta menyediakan
    /// pencarian pintu terdekat dari posisi pemain (Bagian 4, 5, 6, 7D).
    /// </summary>
    public class FireExitDoor : MonoBehaviour
    {
        private static readonly int HashPush = Animator.StringToHash("Push");
        private static readonly int HashIsOpen = Animator.StringToHash("IsOpen");
        private static readonly List<FireExitDoor> ActiveDoors = new List<FireExitDoor>();

        [Header("Identitas & Komponen")]
        [SerializeField] private string doorId = "Exit_A";
        [SerializeField] private Animator doorAnimator;
        [SerializeField] private OcclusionPortal occlusionPortal;
        [SerializeField] private Light crashBarHighlightLight;
        [SerializeField] private float interactionDistance = 2.2f;

        private bool _isOpened;
        private Transform _playerTransform;

        public string DoorId => doorId;
        public bool IsOpened => _isOpened;
        public static IReadOnlyList<FireExitDoor> AllDoors => ActiveDoors;

        public static FireExitDoor FindNearestDoor(Vector3 referencePosition)
        {
            FireExitDoor best = null;
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < ActiveDoors.Count; i++)
            {
                FireExitDoor d = ActiveDoors[i];
                if (d == null) continue;
                float dSq = (d.transform.position - referencePosition).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    best = d;
                }
            }
            return best;
        }

        private void Awake()
        {
            if (doorAnimator == null) doorAnimator = GetComponent<Animator>();
            if (occlusionPortal == null) occlusionPortal = GetComponent<OcclusionPortal>();
            if (occlusionPortal != null) occlusionPortal.open = false;
        }

        private void OnEnable()
        {
            if (!ActiveDoors.Contains(this)) ActiveDoors.Add(this);
        }

        private void OnDisable()
        {
            ActiveDoors.Remove(this);
        }

        private void Start()
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) _playerTransform = playerObj.transform;

            SetCrashBarHighlight(true);
        }

        private void Update()
        {
            if (_isOpened || _playerTransform == null) return;

            float dist = Vector3.Distance(_playerTransform.position, transform.position);
            if (dist <= interactionDistance && InputHelper.WasKeyPressedThisFrame(KeyCode.E))
            {
                TriggerPushOpen();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isOpened) return;
            if (other.CompareTag("Player"))
            {
                TriggerPushOpen();
            }
        }

        public void TriggerPushOpen()
        {
            if (_isOpened) return;
            _isOpened = true;

            if (doorAnimator != null)
            {
                doorAnimator.SetTrigger(HashPush);
                doorAnimator.SetBool(HashIsOpen, true);
            }

            if (occlusionPortal != null)
            {
                occlusionPortal.open = true;
            }

            SetCrashBarHighlight(false);
        }

        public void ResetDoor()
        {
            _isOpened = false;
            if (doorAnimator != null)
            {
                doorAnimator.SetBool(HashIsOpen, false);
                doorAnimator.Play("Locked", 0, 0f);
            }
            if (occlusionPortal != null)
            {
                occlusionPortal.open = false;
            }
            SetCrashBarHighlight(true);
        }

        public void SetCrashBarHighlight(bool active)
        {
            if (crashBarHighlightLight != null)
            {
                crashBarHighlightLight.enabled = active;
            }
        }
    }
}
