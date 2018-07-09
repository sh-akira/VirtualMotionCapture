using System;
using Valve.VR;

namespace sh_akira.OVRTracking
{
    public class OVREventArgs : EventArgs
    {
        public VREvent_t pEvent { get; }

        public OVREventArgs(VREvent_t pevent) : base()
        {
            pEvent = pevent;
        }
    }
}