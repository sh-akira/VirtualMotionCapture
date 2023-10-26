//gpsnmeajp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityMemoryMappedFile;
using VRM;

namespace VMC
{
    [DefaultExecutionOrder(15002)]

    [RequireComponent(typeof(uOSC.uOscServer))]
    public class ExternalReceiverForVMC : MonoBehaviour
    {
        public ExternalSender externalSender;
        public MidiCCWrapper MIDICCWrapper;

        //仮想コントローラソート済み辞書
        public SortedDictionary<string, SteamVR_Utils.RigidTransform> virtualHmd = new SortedDictionary<string, SteamVR_Utils.RigidTransform>();
        public SortedDictionary<string, SteamVR_Utils.RigidTransform> virtualController = new SortedDictionary<string, SteamVR_Utils.RigidTransform>();
        public SortedDictionary<string, SteamVR_Utils.RigidTransform> virtualTracker = new SortedDictionary<string, SteamVR_Utils.RigidTransform>();

        public SortedDictionary<string, SteamVR_Utils.RigidTransform> virtualHmdFiltered = new SortedDictionary<string, SteamVR_Utils.RigidTransform>();
        public SortedDictionary<string, SteamVR_Utils.RigidTransform> virtualControllerFiltered = new SortedDictionary<string, SteamVR_Utils.RigidTransform>();
        public SortedDictionary<string, SteamVR_Utils.RigidTransform> virtualTrackerFiltered = new SortedDictionary<string, SteamVR_Utils.RigidTransform>();

        public int receivePort = 39540;
        public string statusString = "";
        private string statusStringOld = "";

        public bool CorrectRotationWhenCalibration = true;
        private int lastCalibrationState = 0;
        private bool doCalibration = false;
        private Quaternion calibrateRotationOffset = Quaternion.identity;

        public EasyDeviceDiscoveryProtocolManager eddp;

        static public Action<string> StatusStringUpdated = null;

        ControlWPFWindow window = null;
        public GameObject CurrentModel = null;
        Camera currentCamera = null;
        FaceController faceController = null;
        VRMLookAtHead vrmLookAtHead = null;
        Transform headTransform = null;

        //仮想視線操作用
        GameObject lookTargetOSC;
        Action beforeFaceApply;
        bool setFaceApplyAction = false;

        //バッファ
        Vector3 pos;
        Quaternion rot;

        private Queue<(float timestamp, uOSC.Message message)> MessageBuffer = new Queue<(float timestamp, uOSC.Message message)>();

        public int packets = 0;

        //ボーン情報取得
        Animator animator = null;
        //VRMのブレンドシェーププロキシ
        VRMBlendShapeProxy blendShapeProxy = null;

        //ボーンENUM情報テーブル
        Dictionary<string, HumanBodyBones> HumanBodyBonesTable = new Dictionary<string, HumanBodyBones>();

        //ボーン情報テーブル
        Dictionary<HumanBodyBones, Vector3> HumanBodyBonesPositionTable = new Dictionary<HumanBodyBones, Vector3>();
        Dictionary<HumanBodyBones, Quaternion> HumanBodyBonesRotationTable = new Dictionary<HumanBodyBones, Quaternion>();
        private VirtualAvatar virtualAvatar;

        private Dictionary<string, float> blendShapeBuffer = new Dictionary<string, float>();

        public bool DisableBlendShapeReception { get; set; }

        private bool enableLocalHandFix = true;
        private float lastBoneReceivedTime = 0;

        private VMCProtocolReceiverSettings receiverSetting;

        private bool ApplyBlendShape;
        private bool ApplyLookAt;
        private bool ApplyTracker;
        private bool ApplyCamera;
        private bool ApplyLight;
        private bool ApplyMidi;
        private bool ApplyStatus;
        private bool ApplyControl;
        private bool ApplySetting;

        public void SetSetting(VMCProtocolReceiverSettings setting)
        {
            receiverSetting = setting;

            if (virtualAvatar != null)
            {
                virtualAvatar.Enable = setting.Enable;
                virtualAvatar.ApplyRootRotation = setting.ApplyRootRotation;
                virtualAvatar.ApplyRootPosition = setting.ApplyRootPosition;
                virtualAvatar.ApplySpine = setting.ApplySpine;
                virtualAvatar.ApplyChest = setting.ApplyChest;
                virtualAvatar.ApplyHead = setting.ApplyHead;
                virtualAvatar.ApplyLeftArm = setting.ApplyLeftArm;
                virtualAvatar.ApplyRightArm = setting.ApplyRightArm;
                virtualAvatar.ApplyLeftHand = setting.ApplyLeftHand;
                virtualAvatar.ApplyRightHand = setting.ApplyRightHand;
                virtualAvatar.ApplyLeftLeg = setting.ApplyLeftLeg;
                virtualAvatar.ApplyRightLeg = setting.ApplyRightLeg;
                virtualAvatar.ApplyLeftFoot = setting.ApplyLeftFoot;
                virtualAvatar.ApplyRightFoot = setting.ApplyRightFoot;
                virtualAvatar.ApplyEye = setting.ApplyEye;
                virtualAvatar.ApplyLeftFinger = setting.ApplyLeftFinger;
                virtualAvatar.ApplyRightFinger = setting.ApplyRightFinger;
                virtualAvatar.CorrectHipBone = setting.CorrectHipBone;
            }

            ApplyBlendShape = setting.ApplyBlendShape;
            ApplyLookAt = setting.ApplyLookAt;
            ApplyTracker = setting.ApplyTracker;
            ApplyCamera = setting.ApplyCamera;
            ApplyLight = setting.ApplyLight;
            ApplyMidi = setting.ApplyMidi;
            ApplyStatus = setting.ApplyStatus;
            ApplyControl = setting.ApplyControl;
            ApplySetting = setting.ApplySetting;
        }

        public void Recenter()
        {
            virtualAvatar.Recenter();
        }

        public void Initialize()
        {
            var server = GetComponent<uOSC.uOscServer>();
            server.onDataReceived.AddListener(OnDataReceived);

            window = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
            faceController = GameObject.Find("AnimationController").GetComponent<FaceController>();
            VMCEvents.OnModelLoaded += (GameObject CurrentModel) =>
            {
                this.CurrentModel = CurrentModel;
                OnModelChanged();
            };
            VMCEvents.OnCameraChanged += (Camera currentCamera) =>
            {
                this.currentCamera = currentCamera;
            };

            beforeFaceApply = () =>
            {
                if (vrmLookAtHead == null || lookTargetOSC == null) return;
                vrmLookAtHead.Target = lookTargetOSC.transform;
                vrmLookAtHead.LookWorldPosition();
                vrmLookAtHead.Target = null;
            };

            var modelRoot = new GameObject("ModelRoot").transform;
            modelRoot.SetParent(transform, false);
            virtualAvatar = new VirtualAvatar(modelRoot, MotionSource.VMCProtocol);
            virtualAvatar.Enable = false;
            MotionManager.Instance.AddVirtualAvatar(virtualAvatar);
            if (receiverSetting != null)
            {
                SetSetting(receiverSetting);
            }

            OnModelChanged();

            this.gameObject.SetActive(false);
            server.enabled = true;
        }

        private void OnDestroy()
        {
            if (virtualAvatar != null)
            {
                MotionManager.Instance.RemoveVirtualAvatar(virtualAvatar);
            }
        }

        private void OnModelChanged()
        {
            if (CurrentModel != null)
            {
                vrmLookAtHead = CurrentModel.GetComponent<VRMLookAtHead>();
                animator = CurrentModel.GetComponent<Animator>();
                headTransform = null;
                if (animator != null)
                {
                    headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
                }
            }
        }

        private object LockObject = new object();

        void OnDataReceived(uOSC.Message message)
        {
            //有効なとき以外処理しない
            if (this.isActiveAndEnabled && receiverSetting != null)
            {
                //生存チェックのためのパケットカウンタ
                packets++;
                if (packets > int.MaxValue / 2)
                {
                    packets = 0;
                }

                if (receiverSetting.DelayMs == 0)
                {
                    ProcessMessage(message);
                }
                else
                {
                    MessageBuffer.Enqueue((Time.realtimeSinceStartup, message));
                }
            }
        }
        void ProcessMessage(uOSC.Message message)
        {
            //有効なとき以外処理しない
            if (this.isActiveAndEnabled)
            {

                //仮想Hmd V2.3
                if (message.address == "/VMC/Ext/Hmd/Pos" && ApplyTracker
                    && (message.values[0] is string)
                    && (message.values[1] is float)
                    && (message.values[2] is float)
                    && (message.values[3] is float)
                    && (message.values[4] is float)
                    && (message.values[5] is float)
                    && (message.values[6] is float)
                    && (message.values[7] is float)
                )
                {
                    string serial = (string)message.values[0];
                    var rigidTransform = SetTransform(ref pos, ref rot, ref message);

                    lock (LockObject)
                    {
                        if (virtualHmd.ContainsKey(serial))
                        {
                            virtualHmd[serial] = rigidTransform;
                        }
                        else
                        {
                            virtualHmd.Add(serial, rigidTransform);
                            virtualHmdFiltered.Add(serial, rigidTransform);
                        }
                    }
                }
                //仮想コントローラー V2.3
                else if (message.address == "/VMC/Ext/Con/Pos" && ApplyTracker
                    && (message.values[0] is string)
                    && (message.values[1] is float)
                    && (message.values[2] is float)
                    && (message.values[3] is float)
                    && (message.values[4] is float)
                    && (message.values[5] is float)
                    && (message.values[6] is float)
                    && (message.values[7] is float)
                )
                {
                    string serial = (string)message.values[0];
                    var rigidTransform = SetTransform(ref pos, ref rot, ref message);

                    lock (LockObject)
                    {
                        if (virtualController.ContainsKey(serial))
                        {
                            virtualController[serial] = rigidTransform;
                        }
                        else
                        {
                            virtualController.Add(serial, rigidTransform);
                            virtualControllerFiltered.Add(serial, rigidTransform);
                        }
                    }
                }
                //仮想トラッカー V2.3
                else if (message.address == "/VMC/Ext/Tra/Pos" && ApplyTracker
                    && (message.values[0] is string)
                    && (message.values[1] is float)
                    && (message.values[2] is float)
                    && (message.values[3] is float)
                    && (message.values[4] is float)
                    && (message.values[5] is float)
                    && (message.values[6] is float)
                    && (message.values[7] is float)
                )
                {
                    string serial = (string)message.values[0];
                    var rigidTransform = SetTransform(ref pos, ref rot, ref message);

                    lock (LockObject)
                    {
                        if (virtualTracker.ContainsKey(serial))
                        {
                            virtualTracker[serial] = rigidTransform;
                        }
                        else
                        {
                            virtualTracker.Add(serial, rigidTransform);
                            virtualTrackerFiltered.Add(serial, rigidTransform);
                        }
                    }
                }
                //フレーム設定 V2.3
                else if (message.address == "/VMC/Ext/Set/Period" && ApplySetting
                    && (message.values[0] is int)
                    && (message.values[1] is int)
                    && (message.values[2] is int)
                    && (message.values[3] is int)
                    && (message.values[4] is int)
                    && (message.values[5] is int)
                )
                {
                    externalSender.periodStatus = (int)message.values[0];
                    externalSender.periodRoot = (int)message.values[1];
                    externalSender.periodBone = (int)message.values[2];
                    externalSender.periodBlendShape = (int)message.values[3];
                    externalSender.periodCamera = (int)message.values[4];
                    externalSender.periodDevices = (int)message.values[5];
                }
                //Virtual MIDI CC V2.3
                else if (message.address == "/VMC/Ext/Midi/CC/Val" && ApplyMidi
                    && (message.values[0] is int)
                    && (message.values[1] is float)
                )
                {
                    MIDICCWrapper.KnobUpdated(0, (int)message.values[0], (float)message.values[1]);
                }
                //Camera Control V2.3
                else if (message.address == "/VMC/Ext/Cam" && ApplyCamera
                    && (message.values[0] is string)
                    && (message.values[1] is float)
                    && (message.values[2] is float)
                    && (message.values[3] is float)
                    && (message.values[4] is float)
                    && (message.values[5] is float)
                    && (message.values[6] is float)
                    && (message.values[7] is float)
                    && (message.values[8] is float)
                )
                {
                    pos.x = (float)message.values[1];
                    pos.y = (float)message.values[2];
                    pos.z = (float)message.values[3];
                    rot.x = (float)message.values[4];
                    rot.y = (float)message.values[5];
                    rot.z = (float)message.values[6];
                    rot.w = (float)message.values[7];
                    float fov = (float)message.values[8];

                    //FreeCameraじゃなかったらFreeCameraにする
                    if (Settings.Current.CameraType != UnityMemoryMappedFile.CameraTypes.Free)
                    {
                        CameraManager.Current.ChangeCamera(UnityMemoryMappedFile.CameraTypes.Free);
                    }

                    //カメラ制御を切る
                    CameraManager.Current.FreeCamera.GetComponent<CameraMouseControl>().enabled = false;

                    //座標とFOVを適用
                    CameraManager.Current.FreeCamera.transform.localPosition = pos;
                    CameraManager.Current.FreeCamera.transform.localRotation = rot;
                    CameraManager.Current.ControlCamera.fieldOfView = fov;
                } //ブレンドシェープ同期
                else if (message.address == "/VMC/Ext/Blend/Val" && ApplyBlendShape
                    && (message.values[0] is string)
                    && (message.values[1] is float)
                    )
                {
                    blendShapeBuffer[(string)message.values[0]] = (float)message.values[1];
                }
                //ブレンドシェープ適用
                else if (message.address == "/VMC/Ext/Blend/Apply" && ApplyBlendShape)
                {
                    if (DisableBlendShapeReception == true)
                    {
                        blendShapeBuffer.Clear();
                    }

                    faceController.MixPresets(nameof(ExternalReceiverForVMC), blendShapeBuffer.Keys.ToArray(), blendShapeBuffer.Values.ToArray());
                    blendShapeBuffer.Clear();

                }//外部アイトラ V2.3
                else if (message.address == "/VMC/Ext/Set/Eye" && ApplyLookAt
                    && (message.values[0] is int)
                    && (message.values[1] is float)
                    && (message.values[2] is float)
                    && (message.values[3] is float)
                )
                {
                    bool enable = ((int)message.values[0]) != 0;
                    pos.x = (float)message.values[1];
                    pos.y = (float)message.values[2];
                    pos.z = (float)message.values[3];

                    if (enable)
                    {
                        //ターゲットが存在しなければ作る
                        if (lookTargetOSC == null)
                        {
                            lookTargetOSC = new GameObject();
                            lookTargetOSC.name = "lookTargetOSC";
                        }
                        //位置を書き込む
                        if (lookTargetOSC.transform != null)
                        {
                            lookTargetOSC.transform.parent = headTransform;
                            lookTargetOSC.transform.localPosition = pos;
                        }

                        //視線に書き込む
                        if (vrmLookAtHead != null && setFaceApplyAction == false)
                        {
                            faceController.BeforeApply += beforeFaceApply;
                            setFaceApplyAction = true;
                        }
                    }
                    else
                    {
                        //視線を止める
                        if (vrmLookAtHead != null && setFaceApplyAction == true)
                        {
                            faceController.BeforeApply -= beforeFaceApply;
                            setFaceApplyAction = false;
                        }
                    }
                }
                //情報要求 V2.4
                else if (message.address == "/VMC/Ext/Set/Req" && ApplyControl)
                {
                    if (externalSender.isActiveAndEnabled)
                    {
                        externalSender.SendPerLowRate(); //即時送信
                    }
                }
                //情報表示 V2.4
                else if (message.address == "/VMC/Ext/Set/Res" && (message.values[0] is string) && ApplyStatus)
                {
                    statusString = (string)message.values[0];
                }
                //キャリブレーション準備 V2.5
                else if (message.address == "/VMC/Ext/Set/Calib/Ready" && ApplyControl)
                {
                    if (File.Exists(Settings.Current.VRMPath))
                    {
                        IKManager.Instance.ModelCalibrationInitialize();
                    }
                }
                //キャリブレーション実行 V2.5
                else if (message.address == "/VMC/Ext/Set/Calib/Exec" && (message.values[0] is int) && ApplyControl)
                {
                    PipeCommands.CalibrateType calibrateType = PipeCommands.CalibrateType.Ipose;

                    switch ((int)message.values[0])
                    {
                        case 0:
                            calibrateType = PipeCommands.CalibrateType.Ipose;
                            break;
                        case 1:
                            calibrateType = PipeCommands.CalibrateType.Tpose;
                            break;
                        case 2:
                            calibrateType = PipeCommands.CalibrateType.FixedHandWithGround;
                            break;
                        case 3:
                            calibrateType = PipeCommands.CalibrateType.FixedHand;
                            break;
                        default: return; //無視
                    }
                    StartCoroutine(IKManager.Instance.Calibrate(calibrateType));
                    Invoke("EndCalibrate", 2f);
                }
                //設定読み込み V2.5
                else if (message.address == "/VMC/Ext/Set/Config" && (message.values[0] is string && ApplySetting))
                {
                    string path = (string)message.values[0];
                    if (File.Exists(path))
                    {
                        //なぜか時間がかかる
                        window.LoadSettings(path);
                    }
                }
                //スルー情報 V2.6
                else if (message.address != null && message.address.StartsWith("/VMC/Thru/") && ApplyControl)
                {
                    //転送する
                    if (externalSender.isActiveAndEnabled)
                    {
                        externalSender.Send(message.address, message.values);
                    }
                }
                //Directional Light V2.9
                else if (message.address == "/VMC/Ext/Light" && ApplyLight
                    && (message.values[0] is string)
                    && (message.values[1] is float)
                    && (message.values[2] is float)
                    && (message.values[3] is float)
                    && (message.values[4] is float)
                    && (message.values[5] is float)
                    && (message.values[6] is float)
                    && (message.values[7] is float)
                    && (message.values[8] is float)
                    && (message.values[9] is float)
                    && (message.values[10] is float)
                    && (message.values[11] is float)
                )
                {
                    pos.x = (float)message.values[1];
                    pos.y = (float)message.values[2];
                    pos.z = (float)message.values[3];
                    rot.x = (float)message.values[4];
                    rot.y = (float)message.values[5];
                    rot.z = (float)message.values[6];
                    rot.w = (float)message.values[7];
                    float r = (float)message.values[8];
                    float g = (float)message.values[9];
                    float b = (float)message.values[10];
                    float a = (float)message.values[11];

                    window.MainDirectionalLight.color = new Color(r, g, b, a);
                    window.MainDirectionalLightTransform.position = pos;
                    window.MainDirectionalLightTransform.rotation = rot;
                }

                //ボーン姿勢
                else if (message.address == "/VMC/Ext/Bone/Pos"
                    && (message.values[0] is string)
                    && (message.values[1] is float)
                    && (message.values[2] is float)
                    && (message.values[3] is float)
                    && (message.values[4] is float)
                    && (message.values[5] is float)
                    && (message.values[6] is float)
                    && (message.values[7] is float)
                    )
                {
                    string boneName = (string)message.values[0];
                    pos.x = (float)message.values[1];
                    pos.y = (float)message.values[2];
                    pos.z = (float)message.values[3];
                    rot.x = (float)message.values[4];
                    rot.y = (float)message.values[5];
                    rot.z = (float)message.values[6];
                    rot.w = (float)message.values[7];

                    //Humanoidボーンに該当するボーンがあるか調べる
                    HumanBodyBones bone;
                    if (HumanBodyBonesTryParse(ref boneName, out bone))
                    {
                        //あれば位置と回転をキャッシュする
                        if (HumanBodyBonesPositionTable.ContainsKey(bone))
                        {
                            HumanBodyBonesPositionTable[bone] = pos;
                        }
                        else
                        {
                            HumanBodyBonesPositionTable.Add(bone, pos);
                        }

                        if (HumanBodyBonesRotationTable.ContainsKey(bone))
                        {
                            HumanBodyBonesRotationTable[bone] = rot;
                        }
                        else
                        {
                            HumanBodyBonesRotationTable.Add(bone, rot);
                        }

                        // 手以外を受信したとき
                        if (!(bone == HumanBodyBones.LeftHand ||
                              bone == HumanBodyBones.RightHand ||
                              (bone >= HumanBodyBones.LeftThumbProximal &&
                               bone <= HumanBodyBones.RightLittleDistal)))
                        {
                            enableLocalHandFix = false;
                            lastBoneReceivedTime = Time.realtimeSinceStartup;
                        }
                    }

                    //受信と更新のタイミングは切り離した
                }

                //ボーン姿勢
                else if (message.address == "/VMC/Ext/OK"
                    && (message.values[0] is int)
                    )
                {
                    int loaded = (int)message.values[0];
                    if (message.values.Length > 2)
                    {
                        int calibrationState = (int)message.values[1];
                        int calibrationMode = (int)message.values[2];

                        if (calibrationState != lastCalibrationState && calibrationState == 3)
                        {
                            doCalibration = true;
                        }
                        lastCalibrationState = calibrationState;
                    }

                }
            }
        }

        SteamVR_Utils.RigidTransform SetTransform(ref Vector3 pos, ref Quaternion rot, ref uOSC.Message message)
        {
            pos.x = (float)message.values[1];
            pos.y = (float)message.values[2];
            pos.z = (float)message.values[3];
            rot.x = (float)message.values[4];
            rot.y = (float)message.values[5];
            rot.z = (float)message.values[6];
            rot.w = (float)message.values[7];
            return new SteamVR_Utils.RigidTransform(pos, rot);
        }

        public static float filterStrength = 10.0f;

        private void Update()
        {
            if (receiverSetting == null) return;

            while (MessageBuffer.Count > 0 && MessageBuffer.Peek().timestamp + (float)receiverSetting.DelayMs / 1000f < Time.realtimeSinceStartup)
            {
                ProcessMessage(MessageBuffer.Dequeue().message);
            }

            lock (LockObject)
            {
                foreach (var pair in virtualHmd)
                {
                    var newpos = Vector3.Lerp(virtualHmdFiltered[pair.Key].pos, pair.Value.pos, filterStrength * Time.deltaTime);
                    var newrot = Quaternion.Lerp(virtualHmdFiltered[pair.Key].rot, pair.Value.rot, filterStrength * Time.deltaTime);
                    virtualHmdFiltered[pair.Key] = new SteamVR_Utils.RigidTransform(newpos, newrot);
                }
                foreach (var pair in virtualController)
                {
                    var newpos = Vector3.Lerp(virtualControllerFiltered[pair.Key].pos, pair.Value.pos, filterStrength * Time.deltaTime);
                    var newrot = Quaternion.Lerp(virtualControllerFiltered[pair.Key].rot, pair.Value.rot, filterStrength * Time.deltaTime);
                    virtualControllerFiltered[pair.Key] = new SteamVR_Utils.RigidTransform(newpos, newrot);
                }
                foreach (var pair in virtualTracker)
                {
                    var newpos = Vector3.Lerp(virtualTrackerFiltered[pair.Key].pos, pair.Value.pos, filterStrength * Time.deltaTime);
                    var newrot = Quaternion.Lerp(virtualTrackerFiltered[pair.Key].rot, pair.Value.rot, filterStrength * Time.deltaTime);
                    virtualTrackerFiltered[pair.Key] = new SteamVR_Utils.RigidTransform(newpos, newrot);
                }
            }

            //更新を検出(あまりに高速な変化に追従しないように)
            if (statusString != statusStringOld)
            {
                statusStringOld = statusString;

                if (StatusStringUpdated != null)
                {
                    StatusStringUpdated.Invoke(statusString);
                }
            }
        }

        // VRIKのボーン情報を取得するためにLateUpdateを使う
        private void LateUpdate()
        {
            if (CorrectRotationWhenCalibration && doCalibration)
            {
                // 現在のアバターの正面方向回転オフセットを取得
                if (animator != null)
                {
                    var hipBone = animator.GetBoneTransform(HumanBodyBones.Hips);
                    calibrateRotationOffset = Quaternion.Euler(0, hipBone.rotation.eulerAngles.y, 0);
                }
            }
            doCalibration = false;

            BoneSynchronizeByTable();
            if (lastBoneReceivedTime + 5f < Time.realtimeSinceStartup)
            {
                enableLocalHandFix = true;
            }
        }


        private bool internalActive = false;

        public void SetObjectActive(bool enable)
        {
            internalActive = enable;
            if (enable)
            {
                var uServer = GetComponent<uOSC.uOscServer>();
                if (uServer.enabled == true) uServer.enabled = false;
                if (isPortFree(receivePort))
                {
                    uServer.enabled = true;
                    gameObject.SetActive(enable);
                }
                else
                {
                    Debug.LogError("受信ポートが他のアプリと被っています。変更してください");
                }
            }
            else
            {
                var uServer = GetComponent<uOSC.uOscServer>();
                uServer.enabled = false;
                gameObject.SetActive(enable);
            }
        }

        private bool isPortFree(int port)
        {
            var ipGlobalProp = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            var usedPorts = ipGlobalProp.GetActiveUdpListeners();
            return usedPorts.Any(d => d.Port == port) == false;
        }

        public void ChangeOSCPort(int port)
        {
            receivePort = port;
            if (eddp != null) eddp.found = false;

            var uServer = GetComponent<uOSC.uOscServer>();
            uServer.enabled = false;
            var type = typeof(uOSC.uOscServer);
            var portfield = type.GetField("port", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance);
            portfield.SetValue(uServer, port);
            if (internalActive == true)
            {
                SetObjectActive(true);
            }
        }

        //ボーン位置をキャッシュテーブルに基づいて更新
        private void BoneSynchronizeByTable()
        {
            //キャッシュテーブルを参照
            foreach (var bone in HumanBodyBonesTable)
            {
                //キャッシュされた位置・回転を適用
                if (HumanBodyBonesPositionTable.ContainsKey(bone.Value) && HumanBodyBonesRotationTable.ContainsKey(bone.Value))
                {
                    BoneSynchronize(bone.Value, HumanBodyBonesPositionTable[bone.Value], HumanBodyBonesRotationTable[bone.Value]);
                }
            }
        }

        //ボーン位置同期
        private void BoneSynchronize(HumanBodyBones bone, Vector3 pos, Quaternion rot)
        {
            //操作可能な状態かチェック
            if (virtualAvatar != null && animator != null && bone != HumanBodyBones.LastBone)
            {
                Transform targetTransform = virtualAvatar.GetCloneBoneTransform(bone);
                Transform tempTransform;
                //ボーンによって操作を分ける
                if (targetTransform != null)
                {
                    //手首ボーンから先のみ受信した際は
                    if (receiverSetting.FixHandBone && enableLocalHandFix == true && (bone == HumanBodyBones.LeftHand || bone == HumanBodyBones.RightHand))
                    {
                        //ローカル座標系の回転打ち消し
                        Quaternion allLocalRotation = Quaternion.identity;
                        var setRotation = rot;
                        tempTransform = animator.GetBoneTransform(bone);
                        var rootTransform = animator.transform;
                        while (tempTransform != rootTransform)
                        {
                            tempTransform = tempTransform.parent;
                            //後から逆回転をかけて打ち消し
                            allLocalRotation = allLocalRotation * Quaternion.Inverse(tempTransform.localRotation);
                        }
                        allLocalRotation = allLocalRotation * calibrateRotationOffset;
                        Quaternion receivedRotation = allLocalRotation * rot;
                        //外部からのボーンへの反映
                        BoneSynchronizeSingle(targetTransform, ref bone, ref pos, ref receivedRotation);
                    }
                    else
                    {
                        BoneSynchronizeSingle(targetTransform, ref bone, ref pos, ref rot);
                    }                    
                }
            }
        }
        //1本のボーンの同期
        private void BoneSynchronizeSingle(Transform t, ref HumanBodyBones bone, ref Vector3 pos, ref Quaternion rot)
        {
            if (receiverSetting.UseBonePosition) t.localPosition = pos;
            t.localRotation = rot;
        }
        //ボーンENUM情報をキャッシュして高速化
        private bool HumanBodyBonesTryParse(ref string boneName, out HumanBodyBones bone)
        {
            //ボーンキャッシュテーブルに存在するなら
            if (HumanBodyBonesTable.ContainsKey(boneName))
            {
                //キャッシュテーブルから返す
                bone = HumanBodyBonesTable[boneName];
                //ただしLastBoneは発見しなかったことにする(無効値として扱う)
                if (bone == HumanBodyBones.LastBone)
                {
                    return false;
                }
                return true;
            }
            else
            {
                //キャッシュテーブルにない場合、検索する
                var res = EnumTryParse<HumanBodyBones>(boneName, out bone);
                if (!res)
                {
                    //見つからなかった場合はLastBoneとして登録する(無効値として扱う)ことにより次回から検索しない
                    bone = HumanBodyBones.LastBone;
                }
                //キャシュテーブルに登録する
                HumanBodyBonesTable.Add(boneName, bone);
                return res;
            }
        }
        //互換性を持ったTryParse
        private static bool EnumTryParse<T>(string value, out T result) where T : struct
        {
#if NET_4_6
            return Enum.TryParse(value, out result);
#else
        try
        {
            result = (T)Enum.Parse(typeof(T), value, true);
            return true;
        }
        catch
        {
            result = default(T);
            return false;
        }
#endif
        }
    }
}