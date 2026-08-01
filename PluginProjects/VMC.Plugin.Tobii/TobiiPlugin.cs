using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Tobii.Gaming;
using UnityEngine;
using UnityMemoryMappedFile;
using VMC.Plugin.Commands;
using VMC.Plugin;

namespace VMC.Plugin.TobiiEye
{
    /// <summary>
    /// Tobii Eye Tracker のアイトラッキング。
    /// 元は本体の EyeTracking_Tobii.cs。
    ///
    /// 画面上の注視点を、モデルの頭の前方に置いた仮想モニタ上の点として扱い、
    /// そこを見るように目線を向ける。
    /// </summary>
    public class TobiiPlugin : MonoBehaviour, IVMCPlugin
    {
        public string Id => "Tobii";
        public string DisplayName => "Tobii Eye Tracker";
        public string Version => "1.0.0";
        public System.Collections.Generic.IEnumerable<System.Type> CommandTypes => TobiiCommands.Types;

        private IPluginHost host;
        private IPluginSettings settings;

        private GameObject monitorPosition;
        private GameObject lookTarget;
        private Vector3 startPos;

        private float scaleX = 0.5f;
        private float scaleY = 0.2f;
        private float offsetX = 0.0f;
        private float offsetY = 0.0f;
        private float centerX = 0.5f;
        private float centerY = 0.5f;
        private const float Smoothing = 0.7f;

        private Vector3 oldPoint;
        private bool isFirst = true;
        private bool isValidPosition = false;

        private Action faceBeforeApply;

        public void Initialize(IPluginHost host)
        {
            this.host = host;
            settings = host.GetSettings(Id);

            AcceptTobiiEula();

            VMCEvents.OnModelLoaded += OnModelLoaded;
            host.Ipc.Received += OnReceived;
            host.SettingsApplied += ApplySettings;
        }

        private void OnDestroy()
        {
            VMCEvents.OnModelLoaded -= OnModelLoaded;
            if (host != null)
            {
                host.Ipc.Received -= OnReceived;
                host.SettingsApplied -= ApplySettings;
                if (faceBeforeApply != null) host.FaceControl.BeforeApply -= faceBeforeApply;
            }
        }

        /// <summary>
        /// Tobii Unity SDK は EULA 同意マーカーを Resources から読むが、
        /// プラグインDLLからは Resources を提供できないため同意済みフラグを直接立てる。
        /// (本プラグインは Tobii Unity SDK の EULA に同意した上でビルド・配布している)
        /// </summary>
        private void AcceptTobiiEula()
        {
            try
            {
                var type = Type.GetType("Tobii.Gaming.Internal.TobiiEulaFile, " + typeof(TobiiAPI).Assembly.GetName().Name);
                var field = type?.GetField("_eulaAccepted", BindingFlags.NonPublic | BindingFlags.Static);
                field?.SetValue(null, true);
            }
            catch (Exception ex)
            {
                host.LogWarning(Id, $"Tobii SDKのEULA同意フラグを設定できませんでした: {ex.Message}");
            }
        }

        #region 設定

        private void OnReceived(object sender, DataReceivedEventArgs e)
        {
            host.Ipc.Post(async () =>
            {
                if (e.CommandType == typeof(GetEyeTracking_TobiiOffsets))
                {
                    await host.Ipc.SendCommandAsync(new SetEyeTracking_TobiiOffsets
                    {
                        OffsetHorizontal = offsetX,
                        OffsetVertical = offsetY,
                        ScaleHorizontal = scaleX,
                        ScaleVertical = scaleY,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(SetEyeTracking_TobiiOffsets))
                {
                    var d = (SetEyeTracking_TobiiOffsets)e.Data;
                    settings.Set("OffsetHorizontal", d.OffsetHorizontal);
                    settings.Set("OffsetVertical", d.OffsetVertical);
                    settings.Set("ScaleHorizontal", d.ScaleHorizontal);
                    settings.Set("ScaleVertical", d.ScaleVertical);
                    ApplyOffsets();
                }
                else if (e.CommandType == typeof(EyeTracking_TobiiCalibration))
                {
                    Calibration(host.CurrentModel, fromSetting: false);
                }
            });
        }

        private void ApplySettings() => ApplyOffsets();

        private void ApplyOffsets()
        {
            scaleX = settings.Get("ScaleHorizontal", 0.5f);
            scaleY = settings.Get("ScaleVertical", 0.2f);
            offsetX = settings.Get("OffsetHorizontal", 0.0f);
            offsetY = settings.Get("OffsetVertical", 0.0f);
        }

        #endregion

        private void OnModelLoaded(GameObject model) => Calibration(model, fromSetting: true);

        /// <summary>
        /// 仮想モニタの位置と注視の中心を決める。
        /// fromSetting = true なら保存済みの値を復元し、false ならその場で取り直す。
        /// </summary>
        private void Calibration(GameObject currentModel, bool fromSetting)
        {
            if (currentModel == null) return;
            if (TobiiAPI.IsConnected == false) return;

            var animator = currentModel.GetComponent<Animator>();
            var head = animator.GetBoneTransform(HumanBodyBones.Head);

            if (monitorPosition == null) monitorPosition = new GameObject("Tobii_MonitorPosition");
            monitorPosition.transform.parent = null;

            if (fromSetting)
            {
                var stored = settings.Get<StoredTransform>("MonitorPosition", null);
                stored?.ApplyTo(monitorPosition.transform);
                centerX = settings.Get("CenterX", 0.5f);
                centerY = settings.Get("CenterY", 0.5f);
            }
            else
            {
                //モデルの頭の前方50cm地点にモニターがあることにする
                monitorPosition.transform.position = head.position + head.forward * 0.5f;
                monitorPosition.transform.rotation = head.rotation;

                var gazePoint = GazeViewportToMonitorViewport(TobiiAPI.GetGazePoint().Viewport);
                centerX = gazePoint.x;
                centerY = gazePoint.y;

                settings.Set("MonitorPosition", StoredTransform.From(monitorPosition.transform));
                settings.Set("CenterX", centerX);
                settings.Set("CenterY", centerY);
            }

            if (lookTarget == null) lookTarget = new GameObject("LookTarget");
            lookTarget.transform.parent = monitorPosition.transform;
            lookTarget.transform.localRotation = Quaternion.identity;
            lookTarget.transform.localPosition = Vector3.zero;

            if (faceBeforeApply != null) host.FaceControl.BeforeApply -= faceBeforeApply;
            faceBeforeApply = () =>
            {
                if (lookTarget == null) return;
                if (isValidPosition == false) return;
                //視線が有効な時だけLookTargetの方向を目線に反映する
                host.FaceControl.SetLookAtPosition(lookTarget.transform.position);
            };
            host.FaceControl.BeforeApply += faceBeforeApply;

            startPos = lookTarget.transform.localPosition;
            isFirst = true;
        }

        private void Update()
        {
            if (TobiiAPI.IsConnected == false) return;
            if (lookTarget == null || monitorPosition == null) return;

            var gazePoint = TobiiAPI.GetGazePoint();
            isValidPosition = gazePoint.IsValid;
            if (isValidPosition == false) return;

            var gazePointToMonitor = GazeViewportToMonitorViewport(gazePoint.Viewport);
            var gazePointInWorld = new Vector3(
                startPos.x + ((gazePointToMonitor.x - centerX) * scaleX) + offsetX,
                startPos.y + ((gazePointToMonitor.y - centerY) * scaleY) + offsetY,
                startPos.z);
            lookTarget.transform.localPosition = Smoothify(gazePointInWorld);
        }

        /// <summary>
        /// Tobiiのviewport(ウインドウ左下基準0～1.0)をモニタ全体でのviewportへ変換する
        /// </summary>
        private Vector2 GazeViewportToMonitorViewport(Vector2 viewport)
        {
            var monitorw = Screen.currentResolution.width;
            var monitorh = Screen.currentResolution.height;
            var windowrect = GetUnityWindowPosition();
            var winx = windowrect.left;
            var winbottom = windowrect.bottom;
            var winw = windowrect.right - windowrect.left;
            var winh = windowrect.bottom - windowrect.top;
            var clientw = Screen.width;
            var clienth = Screen.height;
            var borderw = (winw - clientw) / 2;
            var clientx = winx + borderw;
            var clientbottom = (monitorh - winbottom) + borderw;
            var realx = (clientw * viewport.x) + clientx;
            var realy = (clienth * viewport.y) + clientbottom;
            return new Vector2((float)realx / monitorw, (float)realy / monitorh);
        }

        private Vector3 Smoothify(Vector3 point)
        {
            if (isFirst)
            {
                oldPoint = point;
                isFirst = false;
            }

            var smoothedPoint = new Vector3(
                point.x * (1.0f - Smoothing) + oldPoint.x * Smoothing,
                point.y * (1.0f - Smoothing) + oldPoint.y * Smoothing,
                point.z);

            oldPoint = smoothedPoint;
            return smoothedPoint;
        }

        #region Unityウインドウ位置の取得(本体の NativeMethods 相当)

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        private static IntPtr unityWindowHandle = IntPtr.Zero;

        private static RECT GetUnityWindowPosition()
        {
            if (unityWindowHandle == IntPtr.Zero) unityWindowHandle = GetActiveWindow();
            GetWindowRect(unityWindowHandle, out var rect);
            return rect;
        }

        #endregion

        /// <summary>
        /// 仮想モニタの姿勢の保存用。本体の StoreTransform に相当するものを
        /// プラグイン側に持つ(本体の型を参照しなくて済むように)。
        /// </summary>
        public class StoredTransform
        {
            public float px, py, pz;
            public float rx, ry, rz, rw;

            public static StoredTransform From(Transform t) => new StoredTransform
            {
                px = t.position.x,
                py = t.position.y,
                pz = t.position.z,
                rx = t.rotation.x,
                ry = t.rotation.y,
                rz = t.rotation.z,
                rw = t.rotation.w,
            };

            public void ApplyTo(Transform t)
            {
                t.position = new Vector3(px, py, pz);
                t.rotation = new Quaternion(rx, ry, rz, rw);
            }
        }
    }
}
