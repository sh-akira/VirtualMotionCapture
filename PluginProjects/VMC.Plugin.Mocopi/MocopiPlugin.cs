using Mocopi.Receiver;
using Mocopi.Receiver.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;
using VMC.Plugin.Commands;

namespace VMC.Plugin.Mocopi
{
    /// <summary>
    /// mocopi連携。UDPで受け取ったスケルトンをアバターへ流し込む。
    /// </summary>
    public class MocopiPlugin : MonoBehaviour, IVMCPlugin
    {
        public string Id => "mocopi";
        public string DisplayName => "mocopi";
        public string Version => "1.0.0";
        public IEnumerable<Type> CommandTypes => MocopiCommands.Types;

        /// <summary>設定はコマンドの形そのままで1つのキーへ保存する</summary>
        private const string SettingKey = "Setting";

        private IPluginHost host;
        private IPluginSettings settings;
        private VirtualAvatar virtualAvatar;

        private MocopiUdpReceiver udpReceiver;
        private MocopiAvatar mocopiAvatar;
        private GameObject currentModel;

        //既定値は mocopi_SetSetting のコンストラクタが入れる
        private mocopi_SetSetting current = new mocopi_SetSetting();

        public void Initialize(IPluginHost host)
        {
            this.host = host;
            settings = host.GetSettings(Id);

            VMCEvents.OnCurrentModelChanged += OnCurrentModelChanged;
            VMCEvents.OnModelUnloading += OnModelUnloading;

            host.Ipc.Received += OnReceived;
            host.SettingsApplied += ApplySettings;

            virtualAvatar = host.MotionSource.Create(transform);
        }

        private void OnDestroy()
        {
            VMCEvents.OnCurrentModelChanged -= OnCurrentModelChanged;
            VMCEvents.OnModelUnloading -= OnModelUnloading;
            if (host != null)
            {
                host.Ipc.Received -= OnReceived;
                host.SettingsApplied -= ApplySettings;
                if (virtualAvatar != null) host.MotionSource.Remove(virtualAvatar);
            }
            StopUdpReceiver();
        }

        #region 設定

        private void OnReceived(object sender, DataReceivedEventArgs e)
        {
            //通信スレッドから来るのでUnityのメインスレッドへ移す
            host.Ipc.Post(async () =>
            {
                if (e.CommandType == typeof(mocopi_GetSetting))
                {
                    await host.Ipc.SendCommandAsync(current, e.RequestId);
                }
                else if (e.CommandType == typeof(mocopi_SetSetting))
                {
                    settings.Set(SettingKey, (mocopi_SetSetting)e.Data);
                    ApplySettings();
                }
                else if (e.CommandType == typeof(mocopi_Recenter))
                {
                    virtualAvatar.Recenter();
                }
            });
        }

        /// <summary>保存済みの設定を自身へ反映する</summary>
        private void ApplySettings()
        {
            var previous = current;
            current = settings.Get(SettingKey, new mocopi_SetSetting()) ?? new mocopi_SetSetting();

            virtualAvatar.ApplyRootPosition = current.ApplyRootPosition;
            virtualAvatar.ApplyRootRotation = current.ApplyRootRotation;
            virtualAvatar.ApplyChest = current.ApplyChest;
            virtualAvatar.ApplySpine = current.ApplySpine;
            virtualAvatar.ApplyHead = current.ApplyHead;
            virtualAvatar.ApplyLeftArm = current.ApplyLeftArm;
            virtualAvatar.ApplyRightArm = current.ApplyRightArm;
            virtualAvatar.ApplyLeftHand = current.ApplyLeftHand;
            virtualAvatar.ApplyRightHand = current.ApplyRightHand;
            virtualAvatar.ApplyLeftLeg = current.ApplyLeftLeg;
            virtualAvatar.ApplyRightLeg = current.ApplyRightLeg;
            virtualAvatar.ApplyLeftFoot = current.ApplyLeftFoot;
            virtualAvatar.ApplyRightFoot = current.ApplyRightFoot;
            virtualAvatar.CorrectHipBone = current.CorrectHipBone;

            //受信中に enable / port が変わった時だけ作り直す
            if (udpReceiver != null && previous.enable == current.enable && previous.port == current.port) return;

            StopUdpReceiver();
            if (current.enable) StartUdpReceiver();
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
            virtualAvatar.Enable = false;
        }

        private void StartUdpReceiver()
        {
            if (udpReceiver == null) udpReceiver = new MocopiUdpReceiver(current.port);

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
            virtualAvatar.Enable = false;
            udpReceiver = null;
        }

        private void InitializeSkeleton(int[] boneIds, int[] parentBoneIds,
            float[] rotationsX, float[] rotationsY, float[] rotationsZ, float[] rotationsW,
            float[] positionsX, float[] positionsY, float[] positionsZ)
        {
            //スケルトン定義が来て初めてボーン階層が出来るので、そこで有効化する
            if (virtualAvatar.Enable) return;

            mocopiAvatar.InitializeSkeleton(boneIds, parentBoneIds,
                rotationsX, rotationsY, rotationsZ, rotationsW,
                positionsX, positionsY, positionsZ);
            virtualAvatar.Enable = true;
        }

        #endregion
    }
}
