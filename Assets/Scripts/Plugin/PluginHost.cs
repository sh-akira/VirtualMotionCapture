using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityMemoryMappedFile;
using UniVRM10;
using VMC.Plugin;

namespace VMC
{
    /// <summary>
    /// IPluginHost の本体側実装。
    /// プラグインへ渡す窓口をここに集約し、プラグインが Assembly-CSharp を
    /// 直接参照しなくて済むようにする。
    /// </summary>
    public class PluginHost : IPluginHost
    {
        private readonly FaceControlAdapter faceControl;
        private readonly MotionSourceFactory motionSource;
        private readonly PluginIpc ipc;

        private GameObject currentModel;

        public PluginHost(ControlWPFWindow controlWPFWindow, FaceController faceController)
        {
            faceControl = new FaceControlAdapter(faceController);
            motionSource = new MotionSourceFactory();
            ipc = new PluginIpc(controlWPFWindow);

            VMCEvents.OnCurrentModelChanged += model => currentModel = model;
            VMCEvents.OnModelUnloading += model => currentModel = null;
        }

        public IFaceControl FaceControl => faceControl;
        public IMotionSourceFactory MotionSource => motionSource;
        public IPluginIpc Ipc => ipc;
        public GameObject CurrentModel => currentModel;

        public event Action SettingsApplied;

        /// <summary>本体の設定適用が終わったときに ControlWPFWindow から呼ばれる</summary>
        internal void RaiseSettingsApplied() => SettingsApplied?.Invoke();

        public IPluginSettings GetSettings(string pluginId) => new PluginSettingsStore(pluginId);
    }

    /// <summary>FaceController を IFaceControl として公開するアダプタ</summary>
    internal class FaceControlAdapter : IFaceControl
    {
        private readonly FaceController faceController;
        private Vrm10Instance vrm10Instance;

        public FaceControlAdapter(FaceController faceController)
        {
            this.faceController = faceController;
            VMCEvents.OnCurrentModelChanged += model =>
                vrm10Instance = model != null ? model.GetComponent<Vrm10Instance>() : null;
            VMCEvents.OnModelUnloading += _ => vrm10Instance = null;
        }

        public event Action BeforeApply
        {
            add { faceController.BeforeApply += value; }
            remove { faceController.BeforeApply -= value; }
        }

        public void SetBlink_L(float value) => faceController.SetBlink_L(value);
        public void SetBlink_R(float value) => faceController.SetBlink_R(value);

        public void MixPresets(string presetName, string[] keys, float[] values)
            => faceController.MixPresets(presetName, keys, values);

        public void SetLookAtPosition(Vector3 worldPosition)
        {
            if (vrm10Instance == null) return;
            //LookAtTarget未使用時のみ有効。ボーン/Expressionどちらの目線タイプもRuntimeが処理する
            var lookAt = vrm10Instance.Runtime.LookAt;
            var (yaw, pitch) = lookAt.CalculateYawPitchFromLookAtPosition(worldPosition);
            lookAt.SetYawPitchManually(yaw, pitch);
        }

        public bool ExternalEyelidControlEnabled
        {
            get => faceController.ExternalEyelidControlEnabled;
            set => faceController.ExternalEyelidControlEnabled = value;
        }
    }

    /// <summary>VirtualAvatar の生成と MotionManager への登録を仲介する</summary>
    internal class MotionSourceFactory : IMotionSourceFactory
    {
        public VirtualAvatar Create(Transform boneParentTransform)
        {
            var virtualAvatar = new VirtualAvatar(boneParentTransform, MotionSource.ExternalDevice)
            {
                Enable = false,
            };
            MotionManager.Instance.AddVirtualAvatar(virtualAvatar);
            return virtualAvatar;
        }

        public void Remove(VirtualAvatar virtualAvatar)
        {
            virtualAvatar.Enable = false;
            MotionManager.Instance?.RemoveVirtualAvatar(virtualAvatar);
        }
    }

    /// <summary>コントロールパネルとの通信をプラグインへ中継する</summary>
    internal class PluginIpc : IPluginIpc
    {
        private readonly ControlWPFWindow controlWPFWindow;
        private readonly System.Threading.SynchronizationContext context;

        public PluginIpc(ControlWPFWindow controlWPFWindow)
        {
            this.controlWPFWindow = controlWPFWindow;
            context = System.Threading.SynchronizationContext.Current;
        }

        public event EventHandler<DataReceivedEventArgs> Received
        {
            add { controlWPFWindow.server.ReceivedEvent += value; }
            remove { controlWPFWindow.server.ReceivedEvent -= value; }
        }

        public Task SendCommandAsync(object command, string requestId = null)
            => controlWPFWindow.server.SendCommandAsync(command, requestId);

        public void Post(Action action) => context.Post(_ => action(), null);
    }
}
