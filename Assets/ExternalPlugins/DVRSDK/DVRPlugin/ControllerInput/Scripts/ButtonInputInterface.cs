using System;

namespace DVRSDK.Plugins.Input
{
    public interface ButtonInputInterface
    {
        event EventHandler<KeyEventArgs> KeyDownEvent;
        event EventHandler<KeyEventArgs> KeyUpEvent;
        event EventHandler<AxisEventArgs> AxisChangedEvent;

        void CheckUpdate();
    }
}
