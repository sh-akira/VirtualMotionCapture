//gpsnmeajp
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Valve.VR;
using VRM;
using UnityMemoryMappedFile;
using System.Linq;

//ここから追加
[DefaultExecutionOrder(15002)]
//ここまで

[RequireComponent(typeof(uOSC.uOscServer))]
public class ExternalReceiverForVMC : MonoBehaviour
{
    public ExternalSender externalSender;
    public MidiCCWrapper MIDICCWrapper;

    //仮想コントローラソート済み辞書
    public SortedDictionary<string, SteamVR_Utils.RigidTransform> virtualController = new SortedDictionary<string, SteamVR_Utils.RigidTransform>();
    public SortedDictionary<string, SteamVR_Utils.RigidTransform> virtualTracker = new SortedDictionary<string, SteamVR_Utils.RigidTransform>();

    public SortedDictionary<string, SteamVR_Utils.RigidTransform> virtualControllerFiltered = new SortedDictionary<string, SteamVR_Utils.RigidTransform>();
    public SortedDictionary<string, SteamVR_Utils.RigidTransform> virtualTrackerFiltered = new SortedDictionary<string, SteamVR_Utils.RigidTransform>();

    public int receivePort = 39540;
    public string statusString = "";
    private string statusStringOld = "";

    //ここから追加
    public bool receiveBonesFlag;
    //ここまで

    static public Action<string> StatusStringUpdated = null;

    ControlWPFWindow window = null;
    GameObject CurrentModel = null;
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

    public int packets = 0;

    //ここから追加
    //ボーン情報取得
    Animator animator = null;
    //VRMのブレンドシェーププロキシ
    VRMBlendShapeProxy blendShapeProxy = null;

    //ボーンENUM情報テーブル
    Dictionary<string, HumanBodyBones> HumanBodyBonesTable = new Dictionary<string, HumanBodyBones>();

    //ボーン情報テーブル
    Dictionary<HumanBodyBones, Vector3> HumanBodyBonesPositionTable = new Dictionary<HumanBodyBones, Vector3>();
    Dictionary<HumanBodyBones, Quaternion> HumanBodyBonesRotationTable = new Dictionary<HumanBodyBones, Quaternion>();

    public bool BonePositionSynchronize = true; //ボーン位置適用(回転は強制)

    //ここまで

    private Dictionary<string, float> blendShapeBuffer = new Dictionary<string, float>();

    void Start()
    {
        var server = GetComponent<uOSC.uOscServer>();
        server.onDataReceived.AddListener(OnDataReceived);

        window = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
        faceController = GameObject.Find("AnimationController").GetComponent<FaceController>();
        window.ModelLoadedAction += (GameObject CurrentModel) =>
        {
            if (CurrentModel != null)
            {
                this.CurrentModel = CurrentModel;
                vrmLookAtHead = CurrentModel.GetComponent<VRMLookAtHead>();
                //var animator = CurrentModel.GetComponent<Animator>();
                //追加
                animator = CurrentModel.GetComponent<Animator>();
                headTransform = null;
                if (animator != null)
                {
                    headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
                }
            }
        };
        window.CameraChangedAction += (Camera currentCamera) =>
        {
            this.currentCamera = currentCamera;
        };

        beforeFaceApply = () =>
        {
            vrmLookAtHead.Target = lookTargetOSC.transform;
            vrmLookAtHead.LookWorldPosition();
            vrmLookAtHead.Target = null;
        };
    }

    private object LockObject = new object();

    void OnDataReceived(uOSC.Message message)
    {
        //有効なとき以外処理しない
        if (this.isActiveAndEnabled)
        {
            //生存チェックのためのパケットカウンタ
            packets++;
            if (packets > int.MaxValue / 2)
            {
                packets = 0;
            }

            //仮想コントローラー V2.3
            if (message.address == "/VMC/Ext/Con/Pos"
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
            else if ((message.address == "/VMC/Ext/Hmd/Pos"
                || message.address == "/VMC/Ext/Tra/Pos")
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
            else if (message.address == "/VMC/Ext/Set/Period"
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
            else if (message.address == "/VMC/Ext/Midi/CC/Val"
                && (message.values[0] is int)
                && (message.values[1] is float)
            )
            {
                MIDICCWrapper.KnobUpdated(0, (int)message.values[0], (float)message.values[1]);
            }
            //Camera Control V2.3
            else if (message.address == "/VMC/Ext/Cam"
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
                if (ControlWPFWindow.CurrentSettings.CameraType != UnityMemoryMappedFile.CameraTypes.Free)
                {
                    window.ChangeCamera(UnityMemoryMappedFile.CameraTypes.Free);
                }

                //カメラ制御を切る
                window.FreeCamera.GetComponent<CameraMouseControl>().enabled = false;

                //座標とFOVを適用
                window.FreeCamera.transform.position = pos;
                window.FreeCamera.transform.rotation = rot;
                window.ControlCamera.fieldOfView = fov;
            } //ブレンドシェープ同期
            else if (message.address == "/VMC/Ext/Blend/Val"
                && (message.values[0] is string)
                && (message.values[1] is float)
                )
            {
                blendShapeBuffer[(string)message.values[0]] = (float)message.values[1];
            }
            //ブレンドシェープ適用
            else if (message.address == "/VMC/Ext/Blend/Apply")
            {
                faceController.MixPresets(nameof(ExternalReceiverForVMC), blendShapeBuffer.Keys.ToArray(), blendShapeBuffer.Values.ToArray());
                blendShapeBuffer.Clear();

            }//外部アイトラ V2.3
            else if (message.address == "/VMC/Ext/Set/Eye"
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
            else if (message.address == "/VMC/Ext/Set/Req")
            {
                if (externalSender.isActiveAndEnabled && externalSender.uClient != null)
                {
                    externalSender.SendPerLowRate(); //即時送信
                }
            }
            //情報表示 V2.4
            else if (message.address == "/VMC/Ext/Set/Res" && (message.values[0] is string))
            {
                statusString = (string)message.values[0];
            }
            //キャリブレーション準備 V2.5
            else if (message.address == "/VMC/Ext/Set/Calib/Ready")
            {
                if (File.Exists(ControlWPFWindow.CurrentSettings.VRMPath))
                {
                    window.ImportVRM(ControlWPFWindow.CurrentSettings.VRMPath, true, true, true);
                }
            }
            //キャリブレーション実行 V2.5
            else if (message.address == "/VMC/Ext/Set/Calib/Exec" && (message.values[0] is int))
            {
                PipeCommands.CalibrateType calibrateType = PipeCommands.CalibrateType.Default;

                switch ((int)message.values[0])
                {
                    case 0:
                        calibrateType = PipeCommands.CalibrateType.Default;
                        break;
                    case 1:
                        calibrateType = PipeCommands.CalibrateType.FixedHand;
                        break;
                    case 2:
                        calibrateType = PipeCommands.CalibrateType.FixedHandWithGround;
                        break;
                    default: return; //無視
                }
                StartCoroutine(window.Calibrate(calibrateType));
                Invoke("EndCalibrate", 2f);
            }
            //設定読み込み V2.5
            else if (message.address == "/VMC/Ext/Set/Config" && (message.values[0] is string))
            {
                string path = (string)message.values[0];
                if (File.Exists(path))
                {
                    //なぜか時間がかかる
                    window.LoadSettings(path);
                }
            }
            //スルー情報 V2.6
            else if (message.address != null && message.address.StartsWith("/VMC/Thru/"))
            {
                //転送する
                if (externalSender.isActiveAndEnabled && externalSender.uClient != null)
                {
                    externalSender.uClient.Send(message.address, message.values);
                }
            }
            //Directional Light V2.9
            else if (message.address == "/VMC/Ext/Light"
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

            //ここから追加
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
                }
                //受信と更新のタイミングは切り離した
            }
            //ここまで
        }
    }

    void EndCalibrate()
    {
        window.EndCalibrate();
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

    //修正（LateUpdateに変更）
    private void LateUpdate()
    {
        lock (LockObject)
        {
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
        //ここから追加
        if(receiveBonesFlag)
        {
            BoneSynchronizeByTable();
            //Debug.Log(receiveBonesFlag);
        }
        //ここまで
    }

    public void ChangeOSCPort(int port)
    {
        receivePort = port;
        var uServer = GetComponent<uOSC.uOscServer>();
        uServer.enabled = false;
        var type = typeof(uOSC.uOscServer);
        var portfield = type.GetField("port", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance);
        portfield.SetValue(uServer, port);
        uServer.enabled = true;
    }

    //ここから追加
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
        if (animator != null && bone != HumanBodyBones.LastBone)
        {
            //ローカル座標系の回転打ち消し用にHipsのボーンを押さえる
            Transform hipBone = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform targetTransform;
            Transform tempTransform;
            //ボーンによって操作を分ける
            Transform t = animator.GetBoneTransform(bone);
            if (t != null)
            {
                //左手ボーン
                if (bone == HumanBodyBones.LeftHand)
                {
                    //ローカル座標系の回転打ち消し
                    Quaternion allLocalRotationLeft = Quaternion.identity;
                    targetTransform = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    tempTransform = targetTransform;
                    while (tempTransform != hipBone)
                    {
                        tempTransform = tempTransform.parent;
                        //後から逆回転をかけて打ち消し
                        allLocalRotationLeft = allLocalRotationLeft * Quaternion.Inverse(tempTransform.localRotation);
                    }
                    Quaternion receivedRotationLeft = allLocalRotationLeft * rot;
                    //外部からのボーンへの反映
                    BoneSynchronizeSingle(t, ref bone, ref pos, ref receivedRotationLeft);
                }
                //右手ボーン
                else if (bone == HumanBodyBones.RightHand)
                {
                    //ローカル座標系の回転打ち消し
                    Quaternion allLocalRotationRight = Quaternion.identity;
                    targetTransform = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    tempTransform = targetTransform;
                    while (tempTransform != hipBone)
                    {
                        tempTransform = tempTransform.parent;
                        //逆回転にかけて打ち消し
                        allLocalRotationRight = allLocalRotationRight * Quaternion.Inverse(tempTransform.localRotation);
                    }
                    Quaternion receivedRotationRight = allLocalRotationRight * rot;
                    //外部からのボーンへの反映
                    BoneSynchronizeSingle(t, ref bone, ref pos, ref receivedRotationRight);
                }
                //指ボーン
                else if (bone == HumanBodyBones.LeftIndexDistal ||
                        bone == HumanBodyBones.LeftIndexIntermediate ||
                        bone == HumanBodyBones.LeftIndexProximal ||
                        bone == HumanBodyBones.LeftLittleDistal ||
                        bone == HumanBodyBones.LeftLittleIntermediate ||
                        bone == HumanBodyBones.LeftLittleProximal ||
                        bone == HumanBodyBones.LeftMiddleDistal ||
                        bone == HumanBodyBones.LeftMiddleIntermediate ||
                        bone == HumanBodyBones.LeftMiddleProximal ||
                        bone == HumanBodyBones.LeftRingDistal ||
                        bone == HumanBodyBones.LeftRingIntermediate ||
                        bone == HumanBodyBones.LeftRingProximal ||
                        bone == HumanBodyBones.LeftThumbDistal ||
                        bone == HumanBodyBones.LeftThumbIntermediate ||
                        bone == HumanBodyBones.LeftThumbProximal ||

                        bone == HumanBodyBones.RightIndexDistal ||
                        bone == HumanBodyBones.RightIndexIntermediate ||
                        bone == HumanBodyBones.RightIndexProximal ||
                        bone == HumanBodyBones.RightLittleDistal ||
                        bone == HumanBodyBones.RightLittleIntermediate ||
                        bone == HumanBodyBones.RightLittleProximal ||
                        bone == HumanBodyBones.RightMiddleDistal ||
                        bone == HumanBodyBones.RightMiddleIntermediate ||
                        bone == HumanBodyBones.RightMiddleProximal ||
                        bone == HumanBodyBones.RightRingDistal ||
                        bone == HumanBodyBones.RightRingIntermediate ||
                        bone == HumanBodyBones.RightRingProximal ||
                        bone == HumanBodyBones.RightThumbDistal ||
                        bone == HumanBodyBones.RightThumbIntermediate ||
                        bone == HumanBodyBones.RightThumbProximal)
                {
                    BoneSynchronizeSingle(t, ref bone, ref pos, ref rot);
                }
            }
        }
    }
    //1本のボーンの同期
    private void BoneSynchronizeSingle(Transform t, ref HumanBodyBones bone, ref Vector3 pos, ref Quaternion rot)
    {
        t.localPosition = pos;
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
    //ここまで
}
