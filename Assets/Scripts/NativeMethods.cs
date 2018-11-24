using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts
{
    public static class NativeMethods
    {

        #region WindowsAPI

        [StructLayout(LayoutKind.Sequential)]
        public struct DwmMargin
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);
        public static Vector2 GetWindowsMousePosition()
        {
            POINT pos;
            if (GetCursorPos(out pos)) return new Vector2(pos.X, pos.Y);
            return Vector2.zero;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string className, string windowName);
        public static IntPtr GetUnityWindowHandle() => FindWindow(null, Application.productName);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong); /*x uint o int unchecked*/
        [DllImport("user32.dll")]
        public static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SetWindowText(IntPtr hwnd, String lpString);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public static readonly IntPtr HWND_TOP = new IntPtr(0);
        public static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        [Flags()]
        public enum SetWindowPosFlags : uint
        {
            AsynchronousWindowPosition = 0x4000,
            DeferErase = 0x2000,
            DrawFrame = 0x0020,
            FrameChanged = 0x0020,
            HideWindow = 0x0080,
            DoNotActivate = 0x0010,
            DoNotCopyBits = 0x0100,
            IgnoreMove = 0x0002,
            DoNotChangeOwnerZOrder = 0x0200,
            DoNotRedraw = 0x0008,
            DoNotReposition = 0x0200,
            DoNotSendChangingEvent = 0x0400,
            IgnoreResize = 0x0001,
            IgnoreZOrder = 0x0004,
            ShowWindow = 0x0040,
            NoFlag = 0x0000,
            IgnoreMoveAndResize = IgnoreMove | IgnoreResize,
        }
        public static RECT GetUnityWindowPosition() { RECT r; GetWindowRect(GetUnityWindowHandle(), out r); return r; }
        public static void SetUnityWindowPosition(int x, int y) => SetWindowPos(GetUnityWindowHandle(), IntPtr.Zero, x, y, 0, 0, SetWindowPosFlags.IgnoreResize);
        public static void SetUnityWindowSize(int width, int height) => SetWindowPos(GetUnityWindowHandle(), IntPtr.Zero, 0, 0, width, height, SetWindowPosFlags.IgnoreMove);
        public static void SetUnityWindowTopMost(bool enable) => SetWindowPos(GetUnityWindowHandle(), enable ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SetWindowPosFlags.IgnoreMoveAndResize);
        public static void SetUnityWindowTitle(string title) => SetWindowText(GetUnityWindowHandle(), title);

        [DllImport("Dwmapi.dll")]
        public static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref DwmMargin margins);
        public static void SetDwmTransparent(bool enable)
        {
            var margins = new DwmMargin() { cxLeftWidth = enable ? -1 : 0 };
            DwmExtendFrameIntoClientArea(GetUnityWindowHandle(), ref margins);
        }

        public const int GWL_STYLE = -16;
        public const uint WS_POPUP = 0x80000000;
        public const uint WS_VISIBLE = 0x10000000;
        public const int GWL_EXSTYLE = -20;
        public const uint WS_EX_LAYERED = 0x00080000;
        public const uint WS_EX_TRANSPARENT = 0x00000020;
        #endregion
    }
}
