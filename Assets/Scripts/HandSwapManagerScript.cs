//gpsnmeajp
using sh_akira.OVRTracking;
using UnityEngine;

namespace VMC
{
    public class HandSwapManagerScript : MonoBehaviour
    {
        public bool Enable = true;

        [Header("OpenVR (Input)")]
        public string OpenVRLeftHand;
        public string OpenVRRightHand;

        [Header("WPF (Input)")]
        public Transform WPFLeftHandTransform;
        public Transform WPFRightHandTransform;
        public string WPFLeftHand;
        public string WPFRightHand;

        [Header("Output")]
        public bool HandSwap = false;
        public SteamVR2Input steamVR2Input = null;

        void Start()
        {

        }

        void Update()
        {
            if (!Enable)
            {
                return;
            }

            if (WPFLeftHandTransform)
            {
                WPFLeftHand = WPFLeftHandTransform.name;
                if (WPFLeftHand != null)
                {
                    WPFLeftHand = WPFLeftHand.Replace("[Controller]", "");
                }
            }
            if (WPFRightHandTransform)
            {
                WPFRightHand = WPFRightHandTransform.name;
                if (WPFRightHand != null)
                {
                    WPFRightHand = WPFRightHand.Replace("[Controller]", "");
                }
            }

            OpenVRWrapper.Instance.GetControllerSerial(out OpenVRLeftHand, out OpenVRRightHand);

            if (OpenVRLeftHand != null && OpenVRRightHand != null && OpenVRLeftHand == WPFRightHand && OpenVRRightHand == WPFLeftHand)
            {
                HandSwap = true;
                steamVR2Input.handSwap = true;
            }
            else
            {
                HandSwap = false;
                steamVR2Input.handSwap = false;
            }
        }
    }
}