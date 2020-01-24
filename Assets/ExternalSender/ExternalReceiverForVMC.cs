//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Valve.VR;
using VRM;

[RequireComponent(typeof(uOSC.uOscServer))]
public class ExternalReceiverForVMC : MonoBehaviour {
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

    static public Action<string> StatusStringUpdated = null;

    ControlWPFWindow window = null;
    GameObject CurrentModel = null;
    Camera currentCamera = null;
    VRMBlendShapeProxy blendShapeProxy = null;
    VRMLookAtHead vrmLookAtHead = null;

    //仮想視線操作用
    GameObject lookTargetOSC;

    //バッファ
    Vector3 pos;
    Quaternion rot;

    void Start () {
        var server = GetComponent<uOSC.uOscServer>();
        server.onDataReceived.AddListener(OnDataReceived);

        window = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
        window.ModelLoadedAction += (GameObject CurrentModel) =>
        {
            if (CurrentModel != null)
            {
                this.CurrentModel = CurrentModel;
                blendShapeProxy = CurrentModel.GetComponent<VRMBlendShapeProxy>();
                vrmLookAtHead = CurrentModel.GetComponent<VRMLookAtHead>();
            }
        };
        window.CameraChangedAction += (Camera currentCamera) =>
        {
            this.currentCamera = currentCamera;
        };
    }

    private object LockObject = new object();

    void OnDataReceived(uOSC.Message message)
    {
        //有効なとき以外処理しない
        if (this.isActiveAndEnabled)
        {
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
                window.FreeCamera.fieldOfView = fov;
            } //ブレンドシェープ同期
            else if (message.address == "/VMC/Ext/Blend/Val"
                && (message.values[0] is string)
                && (message.values[1] is float)
                )
            {
                if (blendShapeProxy != null)
                {
                    blendShapeProxy.AccumulateValue((string)message.values[0], (float)message.values[1]);
                }
            }
            //ブレンドシェープ適用
            else if (message.address == "/VMC/Ext/Blend/Apply")
            {
                if (blendShapeProxy != null)
                {
                    blendShapeProxy.Apply();
                }
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
                        lookTargetOSC.transform.parent = null;
                        lookTargetOSC.name = "lookTargetOSC";
                    }
                    //位置を書き込む
                    if (lookTargetOSC.transform != null)
                    {
                        lookTargetOSC.transform.position = pos;
                    }

                    //視線に書き込む
                    if (vrmLookAtHead != null)
                    {
                        vrmLookAtHead.Target = lookTargetOSC.transform;
                    }
                }
                else
                {
                    //視線を止める
                    if (vrmLookAtHead != null)
                    {
                        vrmLookAtHead.Target = null;
                    }
                }
            }
            //情報要求 V2.4
            else if (message.address == "/VMC/Ext/Set/Req")
            {
                externalSender.SendPerLowRate(); //即時送信
            }
            //情報表示 V2.4
            else if (message.address == "/VMC/Ext/Set/Res" && (message.values[0] is string))
            {
                statusString = (string)message.values[0];
            }

        }
    }
    SteamVR_Utils.RigidTransform SetTransform(ref Vector3 pos, ref Quaternion rot,ref uOSC.Message message) {
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
        if (statusString != statusStringOld) {
            statusStringOld = statusString;

            if (StatusStringUpdated != null) {
                StatusStringUpdated.Invoke(statusString);
            }
        }
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
}
