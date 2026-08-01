using Mocopi.Receiver;
using Mocopi.Receiver.Core;
using System;
using UnityEngine;
using UnityMemoryMappedFile;
using VMC.Plugin;

namespace VMC.Plugin.Mocopi
{
    /// <summary>
    /// mocopi連携。UDPで受け取ったスケルトンをアバターへ流し込む。
    ///
    /// 元は本体の MocopiConnector.cs だったものを、mocopi Receiver Plugin for Unity
    /// への依存ごとプラグインへ切り出したもの。
    /// </summary>
    public class MocopiPlugin : MonoBehaviour, IVMCPlugin
    {
        public string Id => "mocopi";
        public string DisplayName => "mocopi";
        public string Version => "1.0.0";

        private IPluginHost host;
        private IPluginSettings settings;
        private IMotionSourceAvatar motionSource;

        private MocopiUdpReceiver udpReceiver;
        private MocopiAvatar mocopiAvatar;
        private GameObject currentModel;

        private int port = 12351;
        private bool enableReceive;

        public void Initialize(IPluginHost host)
        {
            this.host = host;
            settings = host.GetSettings(Id);

            VMCEvents.OnCurrentModelChanged += OnCurrentModelChanged;
            VMCEvents.OnModelUnloading += OnModelUnloading;

            host.Ipc.Received += OnReceived;
            host.SettingsApplied += ApplySettings;

            motionSource = host.MotionSource.Create(transform);
            motionSource.Enable = false;
        }

        private void OnDestroy()
        {
            VMCEvents.OnCurrentModelChanged -= OnCurrentModelChanged;
            VMCEvents.OnModelUnloading -= OnModelUnloading;
            if (host != null)
            {
                host.Ipc.Received -= OnReceived;
                host.SettingsApplied -= ApplySettings;
            }
            StopUdpReceiver();
            motionSource?.Remove();
        }

        #region 設定

        private void OnReceived(object sender, DataReceivedEventArgs e)
        {
            //通信スレッドから来るのでUnityのメインスレッドへ移す
            host.Ipc.Post(async () =>
            {
                if (e.CommandType == typeof(PipeCommands.mocopi_GetSetting))
                {
                    await host.Ipc.SendCommandAsync(new PipeCommands.mocopi_SetSetting
                    {
                        enable = enableReceive,
                        port = port,
                        ApplyHead = settings.Get("ApplyHead", true),
                        ApplyChest = settings.Get("ApplyChest", true),
                        ApplyRightArm = settings.Get("ApplyRightArm", true),
                        ApplyLeftArm = settings.Get("ApplyLeftArm", true),
                        ApplySpine = settings.Get("ApplySpine", true),
                        ApplyRightHand = settings.Get("ApplyRightHand", true),
                        ApplyLeftHand = settings.Get("ApplyLeftHand", true),
                        ApplyRightLeg = settings.Get("ApplyRightLeg", true),
                        ApplyLeftLeg = settings.Get("ApplyLeftLeg", true),
                        ApplyRightFoot = settings.Get("ApplyRightFoot", true),
                        ApplyLeftFoot = settings.Get("ApplyLeftFoot", true),
                        ApplyRootPosition = settings.Get("ApplyRootPosition", true),
                        ApplyRootRotation = settings.Get("ApplyRootRotation", true),
                        CorrectHipBone = settings.Get("CorrectHipBone", false),
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.mocopi_SetSetting))
                {
                    SetSetting((PipeCommands.mocopi_SetSetting)e.Data);
                }
                else if (e.CommandType == typeof(PipeCommands.mocopi_Recenter))
                {
                    motionSource.Recenter();
                }
            });
        }

        private void SetSetting(PipeCommands.mocopi_SetSetting setting)
        {
            settings.Set("ApplyHead", setting.ApplyHead);
            settings.Set("ApplyChest", setting.ApplyChest);
            settings.Set("ApplyRightArm", setting.ApplyRightArm);
            settings.Set("ApplyLeftArm", setting.ApplyLeftArm);
            settings.Set("ApplySpine", setting.ApplySpine);
            settings.Set("ApplyRightHand", setting.ApplyRightHand);
            settings.Set("ApplyLeftHand", setting.ApplyLeftHand);
            settings.Set("ApplyRightLeg", setting.ApplyRightLeg);
            settings.Set("ApplyLeftLeg", setting.ApplyLeftLeg);
            settings.Set("ApplyRightFoot", setting.ApplyRightFoot);
            settings.Set("ApplyLeftFoot", setting.ApplyLeftFoot);
            settings.Set("ApplyRootPosition", setting.ApplyRootPosition);
            settings.Set("ApplyRootRotation", setting.ApplyRootRotation);
            settings.Set("CorrectHipBone", setting.CorrectHipBone);

            settings.Set("Enable", setting.enable);
            settings.Set("Port", setting.port);

            //受信の開始・停止が要るかどうかは ApplySettings 側で判断する
            ApplySettings();
        }

        /// <summary>保存済みの設定を自身へ反映する</summary>
        private void ApplySettings()
        {
            motionSource.ApplyHead = settings.Get("ApplyHead", true);
            motionSource.ApplyChest = settings.Get("ApplyChest", true);
            motionSource.ApplyRightArm = settings.Get("ApplyRightArm", true);
            motionSource.ApplyLeftArm = settings.Get("ApplyLeftArm", true);
            motionSource.ApplySpine = settings.Get("ApplySpine", true);
            motionSource.ApplyRightHand = settings.Get("ApplyRightHand", true);
            motionSource.ApplyLeftHand = settings.Get("ApplyLeftHand", true);
            motionSource.ApplyRightLeg = settings.Get("ApplyRightLeg", true);
            motionSource.ApplyLeftLeg = settings.Get("ApplyLeftLeg", true);
            motionSource.ApplyRightFoot = settings.Get("ApplyRightFoot", true);
            motionSource.ApplyLeftFoot = settings.Get("ApplyLeftFoot", true);
            motionSource.ApplyRootPosition = settings.Get("ApplyRootPosition", true);
            motionSource.ApplyRootRotation = settings.Get("ApplyRootRotation", true);
            motionSource.CorrectHipBone = settings.Get("CorrectHipBone", false);

            var newEnable = settings.Get("Enable", true);
            var newPort = settings.Get("Port", 12351);

            if (enableReceive == newEnable && port == newPort) return;

            StopUdpReceiver();
            enableReceive = newEnable;
            port = newPort;
            if (enableReceive) StartUdpReceiver();
        }

        #endregion

        #region モデル・受信

        private void OnCurrentModelChanged(GameObject model)
        {
            if (model == null) return;

            var wasReceiving = udpReceiver != null;
            if (wasReceiving) StopUdpReceiver();

            currentModel = model;

            //MocopiAvatar はモデルごとに作り直す
            if (mocopiAvatar != null) DestroyImmediate(mocopiAvatar);
            mocopiAvatar = gameObject.AddComponent<MocopiAvatar>();
            mocopiAvatar.MotionSmoothness = 0.0f;

            if (wasReceiving) StartUdpReceiver();
        }

        private void OnModelUnloading(GameObject model)
        {
            if (currentModel == null) return;

            if (mocopiAvatar != null) DestroyImmediate(mocopiAvatar);
            currentModel = null;
            motionSource.Enable = false;
        }

        private void StartUdpReceiver()
        {
            if (udpReceiver == null)
            {
                udpReceiver = new MocopiUdpReceiver(port);
            }

            if (mocopiAvatar != null)
            {
                udpReceiver.OnReceiveFrameData += mocopiAvatar.UpdateSkeleton;
                udpReceiver.OnReceiveSkeletonDefinition += InitializeSkeleton;
            }
            udpReceiver.UdpStart();
        }

        private void StopUdpReceiver()
        {
            if (udpReceiver == null) return;
            udpReceiver.UdpStop();

            if (mocopiAvatar != null)
            {
                udpReceiver.OnReceiveFrameData -= mocopiAvatar.UpdateSkeleton;
                udpReceiver.OnReceiveSkeletonDefinition -= InitializeSkeleton;
            }
            motionSource.Enable = false;
            udpReceiver = null;
        }

        private void InitializeSkeleton(int[] boneIds, int[] parentBoneIds,
            float[] rotationsX, float[] rotationsY, float[] rotationsZ, float[] rotationsW,
            float[] positionsX, float[] positionsY, float[] positionsZ)
        {
            //スケルトン定義が来て初めてボーン階層が出来るので、そこで有効化する
            if (motionSource.Enable) return;

            mocopiAvatar.InitializeSkeleton(boneIds, parentBoneIds,
                rotationsX, rotationsY, rotationsZ, rotationsW,
                positionsX, positionsY, positionsZ);
            motionSource.Enable = true;
        }

        #endregion
    }
}
