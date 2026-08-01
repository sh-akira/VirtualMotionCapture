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
        private readonly ControlWPFWindow controlWPFWindow;
        private readonly FaceControlAdapter faceControl;
        private readonly MotionSourceFactory motionSource;
        private readonly PluginIpc ipc;

        private GameObject currentModel;

        public PluginHost(ControlWPFWindow controlWPFWindow, FaceController faceController)
        {
            this.controlWPFWindow = controlWPFWindow;
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

        public void Log(string pluginId, string message) => Debug.Log($"[{pluginId}] {message}");
        public void LogWarning(string pluginId, string message) => Debug.LogWarning($"[{pluginId}] {message}");
        public void LogError(string pluginId, string message) => Debug.LogError($"[{pluginId}] {message}");
    }

    /// <summary>FaceController を IFaceControl として公開するアダプタ</summary>
    internal class FaceControlAdapter : IFaceControl
    {
        private readonly FaceController faceController;
        private Vrm10Instance vrm10Instance;

        public FaceControlAdapter(FaceController faceController)
        {
            this.faceController = faceController;
            VMCEvents.OnCurrentModelChanged += OnCurrentModelChanged;
            VMCEvents.OnModelUnloading += _ => vrm10Instance = null;
        }

        private void OnCurrentModelChanged(GameObject model)
        {
            vrm10Instance = model != null ? model.GetComponent<Vrm10Instance>() : null;
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
            //(LookAtTarget未使用時のみ有効。ボーン/Expressionどちらの目線タイプもRuntimeが処理する)
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

    /// <summary>VirtualAvatar / MotionManager を IMotionSource として公開するアダプタ</summary>
    internal class MotionSourceFactory : IMotionSourceFactory
    {
        public IMotionSourceAvatar Create(Transform boneParentTransform)
        {
            var virtualAvatar = new VirtualAvatar(boneParentTransform, global::VMC.MotionSource.ExternalDevice);
            virtualAvatar.Enable = false;
            MotionManager.Instance.AddVirtualAvatar(virtualAvatar);
            return new MotionSourceAvatarAdapter(virtualAvatar);
        }
    }

    internal class MotionSourceAvatarAdapter : IMotionSourceAvatar
    {
        private readonly VirtualAvatar virtualAvatar;

        public MotionSourceAvatarAdapter(VirtualAvatar virtualAvatar) => this.virtualAvatar = virtualAvatar;

        public bool Enable { get => virtualAvatar.Enable; set => virtualAvatar.Enable = value; }
        public bool ApplyRootPosition { get => virtualAvatar.ApplyRootPosition; set => virtualAvatar.ApplyRootPosition = value; }
        public bool ApplyRootRotation { get => virtualAvatar.ApplyRootRotation; set => virtualAvatar.ApplyRootRotation = value; }
        public bool ApplySpine { get => virtualAvatar.ApplySpine; set => virtualAvatar.ApplySpine = value; }
        public bool ApplyChest { get => virtualAvatar.ApplyChest; set => virtualAvatar.ApplyChest = value; }
        public bool ApplyHead { get => virtualAvatar.ApplyHead; set => virtualAvatar.ApplyHead = value; }
        public bool ApplyLeftArm { get => virtualAvatar.ApplyLeftArm; set => virtualAvatar.ApplyLeftArm = value; }
        public bool ApplyRightArm { get => virtualAvatar.ApplyRightArm; set => virtualAvatar.ApplyRightArm = value; }
        public bool ApplyLeftHand { get => virtualAvatar.ApplyLeftHand; set => virtualAvatar.ApplyLeftHand = value; }
        public bool ApplyRightHand { get => virtualAvatar.ApplyRightHand; set => virtualAvatar.ApplyRightHand = value; }
        public bool ApplyLeftLeg { get => virtualAvatar.ApplyLeftLeg; set => virtualAvatar.ApplyLeftLeg = value; }
        public bool ApplyRightLeg { get => virtualAvatar.ApplyRightLeg; set => virtualAvatar.ApplyRightLeg = value; }
        public bool ApplyLeftFoot { get => virtualAvatar.ApplyLeftFoot; set => virtualAvatar.ApplyLeftFoot = value; }
        public bool ApplyRightFoot { get => virtualAvatar.ApplyRightFoot; set => virtualAvatar.ApplyRightFoot = value; }
        public bool CorrectHipBone { get => virtualAvatar.CorrectHipBone; set => virtualAvatar.CorrectHipBone = value; }

        public void Recenter() => virtualAvatar.Recenter();

        public void Remove()
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
