using Mocopi.Receiver;
using Mocopi.Receiver.Core;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC
{
    public class MocopiConnector : MonoBehaviour
    {
        public int Port = 12351;

        [SerializeField]
        private ControlWPFWindow controlWPFWindow;
        private System.Threading.SynchronizationContext context = null;

        private MocopiUdpReceiver udpReceiver;
        private MocopiAvatar mocopiAvatar;
        private GameObject currentModel;

        private VirtualAvatar virtualAvatar;

        private bool isFirstTime = true;

        private void Awake()
        {
            VMCEvents.OnCurrentModelChanged += OnCurrentModelChanged;
            VMCEvents.OnModelUnloading += OnModelUnloading;
            controlWPFWindow.AdditionalSettingAction += ApplySettings;

            virtualAvatar = new VirtualAvatar(transform);
            virtualAvatar.Enable = false;
            MotionManager.Instance.AddVirtualAvatar(virtualAvatar);
        }

        private void Start()
        {
            context = System.Threading.SynchronizationContext.Current;
            controlWPFWindow.server.ReceivedEvent += Server_Received;

            enabled = false;
        }

        private void OnEnable()
        {
            if (isFirstTime)
            {
                isFirstTime = false;
                return;
            }
            StartUdpReceiver();
        }

        private void OnDisable()
        {
            virtualAvatar.Enable = false;
            StopUdpReceiver();
        }
        private void Server_Received(object sender, DataReceivedEventArgs e)
        {
            context.Post(async s =>
            {
                if (e.CommandType == typeof(PipeCommands.mocopi_GetSetting))
                {
                    await controlWPFWindow.server.SendCommandAsync(new PipeCommands.mocopi_SetSetting
                    {
                        enable = Settings.Current.mocopi_Enable,
                        port = Settings.Current.mocopi_Port,
                        ApplyHead = Settings.Current.mocopi_ApplyHead,
                        ApplyChest = Settings.Current.mocopi_ApplyChest,
                        ApplyRightArm = Settings.Current.mocopi_ApplyRightArm,
                        ApplyLeftArm = Settings.Current.mocopi_ApplyLeftArm,
                        ApplySpine = Settings.Current.mocopi_ApplySpine,
                        ApplyRightHand = Settings.Current.mocopi_ApplyRightHand,
                        ApplyLeftHand = Settings.Current.mocopi_ApplyLeftHand,
                        ApplyRightLeg = Settings.Current.mocopi_ApplyRightLeg,
                        ApplyLeftLeg = Settings.Current.mocopi_ApplyLeftLeg,
                        ApplyRightFoot = Settings.Current.mocopi_ApplyRightFoot,
                        ApplyLeftFoot = Settings.Current.mocopi_ApplyLeftFoot,
                        ApplyRootPosition = Settings.Current.mocopi_ApplyRootPosition,
                        ApplyRootRotation = Settings.Current.mocopi_ApplyRootRotation,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.mocopi_SetSetting))
                {
                    var d = (PipeCommands.mocopi_SetSetting)e.Data;
                    SetSetting(d);
                }
                else if (e.CommandType == typeof(PipeCommands.mocopi_Recenter))
                {
                    virtualAvatar.Recenter();
                }

            }, null);
        }

        private void SetSetting(PipeCommands.mocopi_SetSetting setting)
        {
            Settings.Current.mocopi_ApplyHead = setting.ApplyHead;
            Settings.Current.mocopi_ApplyChest = setting.ApplyChest;
            Settings.Current.mocopi_ApplyRightArm = setting.ApplyRightArm;
            Settings.Current.mocopi_ApplyLeftArm = setting.ApplyLeftArm;
            Settings.Current.mocopi_ApplySpine = setting.ApplySpine;
            Settings.Current.mocopi_ApplyRightHand = setting.ApplyRightHand;
            Settings.Current.mocopi_ApplyLeftHand = setting.ApplyLeftHand;
            Settings.Current.mocopi_ApplyRightLeg = setting.ApplyRightLeg;
            Settings.Current.mocopi_ApplyLeftLeg = setting.ApplyLeftLeg;
            Settings.Current.mocopi_ApplyRightFoot = setting.ApplyRightFoot;
            Settings.Current.mocopi_ApplyLeftFoot = setting.ApplyLeftFoot;
            Settings.Current.mocopi_ApplyRootPosition = setting.ApplyRootPosition;
            Settings.Current.mocopi_ApplyRootRotation = setting.ApplyRootRotation;

            SetVirtualAvatarSetting();

            if (Settings.Current.mocopi_Enable != setting.enable || Settings.Current.mocopi_Port != setting.port)
            {
                Settings.Current.mocopi_Enable = setting.enable;
                Settings.Current.mocopi_Port = setting.port;
                ApplySettings(null);
            }
        }

        private void SetVirtualAvatarSetting()
        {
            virtualAvatar.ApplyHead = Settings.Current.mocopi_ApplyHead;
            virtualAvatar.ApplyChest = Settings.Current.mocopi_ApplyChest;
            virtualAvatar.ApplyRightArm = Settings.Current.mocopi_ApplyRightArm;
            virtualAvatar.ApplyLeftArm = Settings.Current.mocopi_ApplyLeftArm;
            virtualAvatar.ApplySpine = Settings.Current.mocopi_ApplySpine;
            virtualAvatar.ApplyRightHand = Settings.Current.mocopi_ApplyRightHand;
            virtualAvatar.ApplyLeftHand = Settings.Current.mocopi_ApplyLeftHand;
            virtualAvatar.ApplyRightLeg = Settings.Current.mocopi_ApplyRightLeg;
            virtualAvatar.ApplyLeftLeg = Settings.Current.mocopi_ApplyLeftLeg;
            virtualAvatar.ApplyRightFoot = Settings.Current.mocopi_ApplyRightFoot;
            virtualAvatar.ApplyLeftFoot = Settings.Current.mocopi_ApplyLeftFoot;
            virtualAvatar.ApplyRootPosition = Settings.Current.mocopi_ApplyRootPosition;
            virtualAvatar.ApplyRootRotation = Settings.Current.mocopi_ApplyRootRotation;
        }

        private void ApplySettings(GameObject gameObject)
        {
            SetVirtualAvatarSetting();

            if (enabled == false && Settings.Current.mocopi_Enable == true)
            {
                Port = Settings.Current.mocopi_Port;
            }
            enabled = Settings.Current.mocopi_Enable;
            ChangePort(Settings.Current.mocopi_Port);
        }

        private void OnCurrentModelChanged(GameObject model)
        {
            if (model != null)
            {
                if (enabled)
                {
                    StopUdpReceiver();
                }

                currentModel = model;

                mocopiAvatar = gameObject.AddComponent<MocopiAvatar>();
                mocopiAvatar.MotionSmoothness = 0.0f;

                if (enabled)
                {
                    StartUdpReceiver();
                }
            }
        }
        private void OnModelUnloading(GameObject model)
        {
            //前回の生成物の削除
            if (currentModel != null)
            {
                DestroyImmediate(mocopiAvatar);

                currentModel = null;
                virtualAvatar.Enable = false;
            }
        }

        private void StartUdpReceiver()
        {
            if (udpReceiver == null)
            {
                udpReceiver = new MocopiUdpReceiver(Port);
            }

            if (mocopiAvatar != null)
            {
                udpReceiver.OnReceiveFrameData += mocopiAvatar.UpdateSkeleton;
                udpReceiver.OnReceiveSkeletonDefinition += InitializeSkeleton;
            }
            udpReceiver?.UdpStart();
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
        public void InitializeSkeleton(int[] boneIds, int[] parentBoneIds, float[] rotationsX, float[] rotationsY, float[] rotationsZ, float[] rotationsW, float[] positionsX, float[] positionsY, float[] positionsZ)
        {
            if (virtualAvatar.Enable == false)
            {
                mocopiAvatar.InitializeSkeleton(boneIds, parentBoneIds, rotationsX, rotationsY, rotationsZ, rotationsW, positionsX, positionsY, positionsZ);
                virtualAvatar.Enable = true;
            }
        }

        public void ChangePort(int port)
        {
            if (Port != port)
            {
                Port = port;
                if (udpReceiver == null) return;
                StopUdpReceiver();
                StartUdpReceiver();
            }
        }
    }
}