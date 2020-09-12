//gpsnmeajp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using uOSC;
using Valve.VR;

public class VMTClient : MonoBehaviour
{
    private int vitrualTrackerNo = 50; //トラッカー番号(他と被らなさそうな適当な番号)
    public ControlWPFWindow window;

    public bool sendEnable = false;
    uOscClient client = null;

    void Start()
    {
        client = GetComponent<uOscClient>();
        SendRoomMatrixTemporary(); //とりあえず起動時にぶん投げておく
    }

    public int GetNo()
    {
        return vitrualTrackerNo;
    }
    public void SetNo(int no)
    {
        if (sendEnable == true)
        {
            disable();
        }
        //更新
        vitrualTrackerNo = no;
    }

    public bool GetEnable()
    {
        return sendEnable;
    }
    public void SetEnable(bool en)
    {
        if (sendEnable == true && en == false)
        {
            disable();
        }

        sendEnable = en;
    }

    public void SendRoomMatrixTemporary()
    {
        if (client == null || sendEnable == false)
        {
            return;
        }

        HmdMatrix34_t m = new HmdMatrix34_t();
        OpenVR.ChaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref m);
        client.Send("/VMT/SetRoomMatrix/Temporary",
            m.m0, m.m1, m.m2, m.m3,
            m.m4, m.m5, m.m6, m.m7,
            m.m8, m.m9, m.m10, m.m11);
    }

    void Update()
    {
        if (client == null || sendEnable == false)
        {
            return;
        }

        Transform target = window.currentCamera.transform;
        bool enable = true;
        if (target != null)
        {
            //enable=1
            client.Send("/VMT/Room/Unity", (int)vitrualTrackerNo, (int)(enable ? 1 : 0), (float)0f,
                (float)target.localPosition.x,
                (float)target.localPosition.y,
                (float)target.localPosition.z,
                (float)target.localRotation.x,
                (float)target.localRotation.y,
                (float)target.localRotation.z,
                (float)target.localRotation.w
            );
        }
    }

    private void disable()
    {
        //無効化処理
        client.Send("/VMT/Room/Unity", (int)vitrualTrackerNo, (int)0, (float)0f,
            (float)0f,
            (float)0f,
            (float)0f,
            (float)0f,
            (float)0f,
            (float)0f,
            (float)1f
        );
    }

    private void OnApplicationQuit()
    {
        if (sendEnable == true)
        {
            disable();
        }
    }
}
