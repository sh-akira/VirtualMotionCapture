using System;

namespace DVRSDK.Plugins.Input
{
    public class KeyEventArgs : EventArgs
    {
        public bool IsLeft { get; set; }
        public KeyNames KeyName { get; set; }

        public KeyEventArgs(KeyNames keyName, bool isLeft)
        {
            KeyName = keyName;
            IsLeft = isLeft;
        }
    }
}
