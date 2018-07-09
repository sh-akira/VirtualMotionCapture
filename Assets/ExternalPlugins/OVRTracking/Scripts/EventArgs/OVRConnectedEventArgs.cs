using System;
namespace sh_akira.OVRTracking
{
    public class OVRConnectedEventArgs : EventArgs
    {
        public bool Connected { get; }

        public OVRConnectedEventArgs(bool connected) : base()
        {
            Connected = connected;
        }
    }
}