//gpsnmeajp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using uOSC;

public class VMTClient : MonoBehaviour {
    private int vitrualTrackerNo = 50; //トラッカー番号(他と被らなさそうな適当な番号)
    public ControlWPFWindow window;

    public bool sendEnable = false;
    uOscClient client = null;

    void Start () {
        client = GetComponent<uOscClient>();
	}

    public int GetNo() {
        return vitrualTrackerNo;
    }
    public void SetNo(int no) {
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
        if(sendEnable == true && en == false)
        {
            disable();
        }

        sendEnable = en;
    }

    void Update () {
        if (client == null || sendEnable == false)
        {
            return;
        }

        Transform target = window.currentCamera.transform;
        bool enable = true;
        if (target != null)
        {
            //enable=1
            client.Send("/VMT/Room/Unity", (int)vitrualTrackerNo, (int)(enable?1:0), (float)0f,
                (float)target.position.x,
                (float)target.position.y,
                (float)target.position.z,
                (float)target.rotation.x,
                (float)target.rotation.y,
                (float)target.rotation.z,
                (float)target.rotation.w
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
