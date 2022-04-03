//gpsnmeajp
using RootMotion.FinalIK;
using sh_akira;
using System;
using System.Reflection;
using UnityEngine;
using UnityMemoryMappedFile;
using VRM;

namespace VMC
{
    [RequireComponent(typeof(uOSC.uOscClient))]
    public class ExternalSender : MonoBehaviour
    {
        public uOSC.uOscClient uClient = null;
        GameObject CurrentModel = null;
        ControlWPFWindow window = null;
        Animator animator = null;
        VRIK vrik = null;
        VRMBlendShapeProxy blendShapeProxy = null;
        Camera currentCamera = null;
        VRMData vrmdata = null;
        string remoteName = null;
        string remoteJson = null;

        public SteamVR2Input steamVR2Input;
        public MidiCCWrapper midiCCWrapper;
        public ExternalReceiverForVMC externalReceiver;
        public string optionString = "";
        private string optionStringOld = "";

        //フレーム周期
        public int periodStatus = 1;
        public int periodRoot = 1;
        public int periodBone = 1;
        public int periodBlendShape = 1;
        public int periodCamera = 1;
        public int periodDevices = 1;
        public int periodLowRateInfo = 90; //低頻度情報(1秒程度間隔)

        //フレーム数カウント用
        private int frameOfStatus = 1;
        private int frameOfRoot = 1;
        private int frameOfBone = 1;
        private int frameOfBlendShape = 1;
        private int frameOfCamera = 1;
        private int frameOfDevices = 1;
        private int frameOfLowRateInfo = 1;

        //パケット分割数
        const int PACKET_DIV_BONE = 12;

        GameObject handTrackerRoot;

        void Start()
        {
            uClient = GetComponent<uOSC.uOscClient>();
            window = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
            handTrackerRoot = GameObject.Find("HandTrackerRoot");

            VMCEvents.OnModelLoaded += (GameObject CurrentModel) =>
            {
                if (CurrentModel != null)
                {
                    this.CurrentModel = CurrentModel;
                    animator = CurrentModel.GetComponent<Animator>();
                    vrik = CurrentModel.GetComponent<VRIK>();
                    blendShapeProxy = CurrentModel.GetComponent<VRMBlendShapeProxy>();
                }
            };

            VMCEvents.OnCameraChanged += (Camera currentCamera) =>
            {
                this.currentCamera = currentCamera;
            };

            window.VRMmetaLodedAction += (VRMData vrmdata) =>
            {
                this.vrmdata = vrmdata;
                this.remoteName = null;
                this.remoteJson = null;
                SendPerLowRate(); //即時送信
            };
            window.VRMRemoteLoadedAction += (string path) =>
            {
                this.vrmdata = null;
                if (path.StartsWith("dmmvrconnect://"))
                {
                    var parsed = path.Substring("dmmvrconnect://".Length).Split('/');
                    remoteName = "dmmvrconnect";
                    remoteJson = Json.Serializer.Serialize(new DMMVRConnectRemote { user_id = parsed[0], avatar_id = parsed[1] });
                }
                else if (path.StartsWith("vroidhub://"))
                {
                    var characterModelId = path.Substring("vroidhub://".Length);
                    remoteName = "vroidhub";
                    remoteJson = Json.Serializer.Serialize(new VRoidHubRemote { characterModelId = characterModelId });
                }
                SendPerLowRate(); //即時送信
            };

            VMCEvents.OnLightChanged += () =>
            {
                SendPerLowRate(); //即時送信
            };

            VMCEvents.OnLoadedConfigPathChanged += path =>
            {
                SendPerLowRate(); //即時送信
            };

            steamVR2Input.KeyDownEvent += (object sender, OVRKeyEventArgs e) =>
            {
                if (this.isActiveAndEnabled)
                {
                    //Debug.Log("Ext: ConDown");
                    try
                    {
                        uClient?.Send("/VMC/Ext/Con", 1, e.Name, e.IsLeft ? 1 : 0, e.IsTouch ? 1 : 0, e.IsAxis ? 1 : 0, e.Axis.x, e.Axis.y, e.Axis.z);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            };

            steamVR2Input.KeyUpEvent += (object sender, OVRKeyEventArgs e) =>
            {
                if (this.isActiveAndEnabled)
                {
                    //Debug.Log("Ext: ConUp");
                    try
                    {
                        uClient?.Send("/VMC/Ext/Con", 0, e.Name, e.IsLeft ? 1 : 0, e.IsTouch ? 1 : 0, e.IsAxis ? 1 : 0, e.Axis.x, e.Axis.y, e.Axis.z);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            };

            steamVR2Input.AxisChangedEvent += (object sender, OVRKeyEventArgs e) =>
            {
                if (this.isActiveAndEnabled)
                {
                    //Debug.Log("Ext: ConAxis");
                    try
                    {
                        if (e.IsAxis)
                        {
                            uClient?.Send("/VMC/Ext/Con", 2, e.Name, e.IsLeft ? 1 : 0, e.IsTouch ? 1 : 0, e.IsAxis ? 1 : 0, e.Axis.x, e.Axis.y, e.Axis.z);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            };

            KeyboardAction.KeyDownEvent += (object sender, KeyboardEventArgs e) =>
            {
                if (this.isActiveAndEnabled)
                {
                    //Debug.Log("Ext: KeyDown");
                    try
                    {
                        uClient?.Send("/VMC/Ext/Key", 1, e.KeyName, e.KeyCode);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            };
            KeyboardAction.KeyUpEvent += (object sender, KeyboardEventArgs e) =>
            {
                if (this.isActiveAndEnabled)
                {
                    //Debug.Log("Ext: KeyUp");
                    try
                    {
                        uClient?.Send("/VMC/Ext/Key", 0, e.KeyName, e.KeyCode);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            };

            midiCCWrapper.noteOnDelegateProxy += (MidiJack.MidiChannel channel, int note, float velocity) =>
            {
                if (this.isActiveAndEnabled)
                {
                    //Debug.Log("Ext: KeyDown");
                    try
                    {
                        uClient?.Send("/VMC/Ext/Midi/Note", 1, (int)channel, note, velocity);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            };
            midiCCWrapper.noteOffDelegateProxy += (MidiJack.MidiChannel channel, int note) =>
            {
                if (this.isActiveAndEnabled)
                {
                    //Debug.Log("Ext: KeyDown");
                    try
                    {
                        uClient?.Send("/VMC/Ext/Midi/Note", 0, (int)channel, note, (float)0f);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            };
            midiCCWrapper.knobUpdateFloatDelegate += (int knobNo, float value) =>
            {
                if (this.isActiveAndEnabled)
                {
                    //Debug.Log("Ext: KeyDown");
                    try
                    {
                        uClient?.Send("/VMC/Ext/Midi/CC/Val", knobNo, value);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            };
            midiCCWrapper.knobUpdateBoolDelegate += (int knobNo, bool value) =>
            {
                if (this.isActiveAndEnabled)
                {
                    //Debug.Log("Ext: KeyDown");
                    try
                    {
                        uClient?.Send("/VMC/Ext/Midi/CC/Bit", knobNo, (int)(value ? 1 : 0));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(ex);
                    }
                }
            };

            this.gameObject.SetActive(false);
            uClient.enabled = true;
        }
        // Update is called once per frame
        void Update()
        {
            //基本的に毎フレーム送信するもの
            SendPerFrame();

            //低頻度(1秒以上)で送信する情報もの
            if (frameOfLowRateInfo > periodLowRateInfo && periodLowRateInfo != 0)
            {
                frameOfLowRateInfo = 1;
                SendPerLowRate();
            }
            frameOfLowRateInfo++;

            //変化時送信
            if (optionString != optionStringOld)
            {
                optionStringOld = optionString;
                SendPerLowRate();
            }

        }

        //低頻度(1秒以上)で送信する情報もの。ただし送信要求が来たら即時発信する
        public void SendPerLowRate()
        {
            //status送信が無効な場合はこれらも送信しない
            if (periodStatus != 0)
            {

                uOSC.Bundle infoBundle = new uOSC.Bundle(uOSC.Timestamp.Immediate);
                //受信有効情報(Receive enable)
                //有効可否と、ポート番号の送信
                infoBundle.Add(new uOSC.Message("/VMC/Ext/Rcv", (int)(externalReceiver.isActiveAndEnabled ? 1 : 0), externalReceiver.receivePort));


                //【イベント送信】DirectionalLight位置・色(DirectionalLight transform & color)
                if ((window.MainDirectionalLightTransform != null) && (window.MainDirectionalLight.color != null))
                {
                    infoBundle.Add(new uOSC.Message("/VMC/Ext/Light",
                        "Light",
                        window.MainDirectionalLightTransform.position.x, window.MainDirectionalLightTransform.position.y, window.MainDirectionalLightTransform.position.z,
                        window.MainDirectionalLightTransform.rotation.x, window.MainDirectionalLightTransform.rotation.y, window.MainDirectionalLightTransform.rotation.z, window.MainDirectionalLightTransform.rotation.w,
                        window.MainDirectionalLight.color.r, window.MainDirectionalLight.color.g, window.MainDirectionalLight.color.b, window.MainDirectionalLight.color.a));
                }

                //【イベント送信】現在の設定
                infoBundle.Add(new uOSC.Message("/VMC/Ext/Setting/Color",
                    Settings.Current.BackgroundColor.r,
                    Settings.Current.BackgroundColor.g,
                    Settings.Current.BackgroundColor.b,
                    Settings.Current.BackgroundColor.a
                ));
                infoBundle.Add(new uOSC.Message("/VMC/Ext/Setting/Win",
                    Settings.Current.IsTopMost ? 1 : 0,
                    Settings.Current.IsTransparent ? 1 : 0,
                    Settings.Current.WindowClickThrough ? 1 : 0,
                    Settings.Current.HideBorder ? 1 : 0
                ));

                //送信
                uClient?.Send(infoBundle);

                //【イベント送信】VRM基本情報(VRM information) [独立送信](大きいため単独で送る)
                if (vrmdata != null)
                {
                    //ファイルパス, キャラ名
                    uClient?.Send(new uOSC.Message("/VMC/Ext/VRM", vrmdata.FilePath, vrmdata.Title));
                }
                else if (string.IsNullOrEmpty(remoteName) == false)
                {
                    uClient?.Send(new uOSC.Message("/VMC/Ext/Remote", remoteName, remoteJson));
                }

                //【イベント送信】設定ファイルパス(Loaded config path) [独立送信](大きいため単独で送る)
                if (window != null)
                {
                    //ファイルパス, キャラ名
                    uClient?.Send(new uOSC.Message("/VMC/Ext/Config", window.lastLoadedConfigPath));
                }

                //【イベント送信】Option文字列(Option string) [独立送信](大きいため単独で送る)
                uClient?.Send(new uOSC.Message("/VMC/Ext/Opt", optionString));
            }
        }

        //基本的に毎フレーム送信するもの
        void SendPerFrame()
        {
            uOSC.Bundle rootBundle = new uOSC.Bundle(uOSC.Timestamp.Immediate);

            if (CurrentModel != null && animator != null)
            {
                //Root
                if (vrik == null)
                {
                    vrik = CurrentModel.GetComponent<VRIK>();
                    Debug.Log("ExternalSender: VRIK Updated");
                }

                if (frameOfRoot > periodRoot && periodRoot != 0)
                {
                    frameOfRoot = 1;
                    if (vrik != null)
                    {
                        var RootTransform = vrik.references.root;
                        var offset = handTrackerRoot.transform;
                        if (RootTransform != null && offset != null)
                        {
                            rootBundle.Add(new uOSC.Message("/VMC/Ext/Root/Pos",
                                "root",
                                RootTransform.position.x, RootTransform.position.y, RootTransform.position.z,
                                RootTransform.rotation.x, RootTransform.rotation.y, RootTransform.rotation.z, RootTransform.rotation.w,
                                offset.localScale.x, offset.localScale.y, offset.localScale.z,
                                offset.position.x, offset.position.y, offset.position.z));
                        }
                    }
                }
                frameOfRoot++;

                //Bones
                if (frameOfBone > periodBone && periodBone != 0)
                {
                    frameOfBone = 1;

                    uOSC.Bundle boneBundle = new uOSC.Bundle(uOSC.Timestamp.Immediate);
                    int cnt = 0;//パケット分割カウンタ

                    foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
                    {
                        if (bone == HumanBodyBones.LastBone)
                        { continue; }

                        var Transform = animator.GetBoneTransform(bone);
                        if (Transform != null)
                        {
                            boneBundle.Add(new uOSC.Message("/VMC/Ext/Bone/Pos",
                                bone.ToString(),
                                Transform.localPosition.x, Transform.localPosition.y, Transform.localPosition.z,
                                Transform.localRotation.x, Transform.localRotation.y, Transform.localRotation.z, Transform.localRotation.w));

                            cnt++;
                            //1200バイトを超えない程度に分割する
                            if (cnt > PACKET_DIV_BONE)
                            {
                                uClient?.Send(boneBundle);
                                boneBundle = new uOSC.Bundle(uOSC.Timestamp.Immediate);
                                cnt = 0;

                            }
                        }
                    }
                    //余ったボーンは雑多な情報と共に送る
                    rootBundle.Add(boneBundle);
                }
                frameOfBone++;

                //Blendsharp
                if (blendShapeProxy == null)
                {
                    blendShapeProxy = CurrentModel.GetComponent<VRMBlendShapeProxy>();
                    Debug.Log("ExternalSender: VRMBlendShapeProxy Updated");
                }

                if (frameOfBlendShape > periodBlendShape && periodBlendShape != 0)
                {
                    frameOfBlendShape = 1;

                    uOSC.Bundle blendShapeBundle = new uOSC.Bundle(uOSC.Timestamp.Immediate);
                    if (blendShapeProxy != null)
                    {
                        foreach (var b in blendShapeProxy.GetValues())
                        {
                            blendShapeBundle.Add(new uOSC.Message("/VMC/Ext/Blend/Val",
                                b.Key.ToString(),
                                (float)b.Value
                                ));
                        }
                        blendShapeBundle.Add(new uOSC.Message("/VMC/Ext/Blend/Apply"));
                    }
                    uClient?.Send(blendShapeBundle);
                }
                frameOfBlendShape++;
            }

            //Camera
            if (frameOfCamera > periodCamera && periodCamera != 0)
            {
                frameOfCamera = 1;
                if (currentCamera != null)
                {
                    rootBundle.Add(new uOSC.Message("/VMC/Ext/Cam",
                        "Camera",
                        currentCamera.transform.position.x, currentCamera.transform.position.y, currentCamera.transform.position.z,
                        currentCamera.transform.rotation.x, currentCamera.transform.rotation.y, currentCamera.transform.rotation.z, currentCamera.transform.rotation.w,
                        currentCamera.fieldOfView));
                }
            }
            frameOfCamera++;

            //TrackerSend
            if (frameOfDevices > periodDevices && periodDevices != 0)
            {
                frameOfDevices = 1;
                var hmdTrackingPoint = TrackingPointManager.Instance.GetHmdTrackingPoint();
                if (hmdTrackingPoint != null)
                {
                    rootBundle.Add(new uOSC.Message("/VMC/Ext/Hmd/Pos",
                            hmdTrackingPoint.Name,
                            hmdTrackingPoint.TargetTransform.position.x, hmdTrackingPoint.TargetTransform.position.y, hmdTrackingPoint.TargetTransform.position.z,
                            hmdTrackingPoint.TargetTransform.rotation.x, hmdTrackingPoint.TargetTransform.rotation.y, hmdTrackingPoint.TargetTransform.rotation.z, hmdTrackingPoint.TargetTransform.rotation.w));
                    rootBundle.Add(new uOSC.Message("/VMC/Ext/Hmd/Pos/Local",
                            hmdTrackingPoint.Name,
                            hmdTrackingPoint.TargetTransform.localPosition.x, hmdTrackingPoint.TargetTransform.localPosition.y, hmdTrackingPoint.TargetTransform.localPosition.z,
                            hmdTrackingPoint.TargetTransform.localRotation.x, hmdTrackingPoint.TargetTransform.localRotation.y, hmdTrackingPoint.TargetTransform.localRotation.z, hmdTrackingPoint.TargetTransform.localRotation.w));
                }
                foreach (var c in TrackingPointManager.Instance.GetControllerTrackingPoints())
                {
                    rootBundle.Add(new uOSC.Message("/VMC/Ext/Con/Pos",
                            c.Name,
                            c.TargetTransform.position.x, c.TargetTransform.position.y, c.TargetTransform.position.z,
                            c.TargetTransform.rotation.x, c.TargetTransform.rotation.y, c.TargetTransform.rotation.z, c.TargetTransform.rotation.w));
                    rootBundle.Add(new uOSC.Message("/VMC/Ext/Con/Pos/Local",
                            c.Name,
                            c.TargetTransform.localPosition.x, c.TargetTransform.localPosition.y, c.TargetTransform.localPosition.z,
                            c.TargetTransform.localRotation.x, c.TargetTransform.localRotation.y, c.TargetTransform.localRotation.z, c.TargetTransform.localRotation.w));
                }
                foreach (var c in TrackingPointManager.Instance.GetTrackerTrackingPoints())
                {
                    rootBundle.Add(new uOSC.Message("/VMC/Ext/Tra/Pos",
                            c.Name,
                            c.TargetTransform.position.x, c.TargetTransform.position.y, c.TargetTransform.position.z,
                            c.TargetTransform.rotation.x, c.TargetTransform.rotation.y, c.TargetTransform.rotation.z, c.TargetTransform.rotation.w));
                    rootBundle.Add(new uOSC.Message("/VMC/Ext/Tra/Pos/Local",
                            c.Name,
                            c.TargetTransform.localPosition.x, c.TargetTransform.localPosition.y, c.TargetTransform.localPosition.z,
                            c.TargetTransform.localRotation.x, c.TargetTransform.localRotation.y, c.TargetTransform.localRotation.z, c.TargetTransform.localRotation.w));
                }
            }
            frameOfDevices++;



            //Status
            if (frameOfStatus > periodStatus && periodStatus != 0)
            {
                frameOfStatus = 1;
                int available = 0;
                if (CurrentModel != null && animator != null)
                {
                    //Available
                    available = 1;
                }
                if (window != null)
                {
                    rootBundle.Add(new uOSC.Message("/VMC/Ext/OK", (int)available, (int)window.calibrationState, (int)window.lastCalibrateType));
                }
                rootBundle.Add(new uOSC.Message("/VMC/Ext/T", Time.time));

            }
            frameOfStatus++;

            uClient?.Send(rootBundle);

            //---End of frame---
        }

        public void ChangeOSCAddress(string address, int port)
        {
            if (uClient == null) uClient = GetComponent<uOSC.uOscClient>();
            uClient.enabled = false;
            var type = typeof(uOSC.uOscClient);
            var addressfield = type.GetField("address", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance);
            addressfield.SetValue(uClient, address);
            var portfield = type.GetField("port", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance);
            portfield.SetValue(uClient, port);
            uClient.enabled = true;
        }
    }

    [Serializable]
    public class DMMVRConnectRemote
    {
        public string user_id;
        public string avatar_id;
    }

    [Serializable]
    public class VRoidHubRemote
    {
        public string characterModelId;
    }

}