using System;
using UnityEngine;

namespace DVRSDK.Plugins.Input
{
    public class AxisEventArgs : EventArgs
    {
        public bool IsLeft { get; set; }
        public KeyNames KeyName { get; set; }
        public Vector2 Value { get; set; }

        public AxisEventArgs(KeyNames keyName, bool isLeft, Vector2 value)
        {
            KeyName = keyName;
            IsLeft = isLeft;
            Value = value;
        }
    }
}
