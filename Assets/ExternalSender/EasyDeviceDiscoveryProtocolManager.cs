//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyDeviceDiscoveryProtocolClient;

public class EasyDeviceDiscoveryProtocolManager : MonoBehaviour
{
    public ControlWPFWindow window;
    public ExternalSender externalSender;
    public ExternalReceiverForVMC externalReceiver;
    Requester requester;
    Responder responder;

    public string myname = "Virtual Motion Capture";

    [Header("Read Only")]

    private float time = 0;
    public bool found = false;

    public bool requesterEnable = true;
    public bool responderEnable = false;

    public int lastPackets = 0;

    void Start()
    {
        requester = gameObject.AddComponent<Requester>();
        requester.deivceName = myname;
        requester.ignoreDeivceName = myname;
        requester.desktopMode = true;

        responder = gameObject.AddComponent<Responder>();
        responder.deivceName = myname;
        responder.ignoreDeivceName = myname;
        responder.desktopMode = true;
        responder.OnRequested = ()=> {
            //見つけた相手のアドレスとポートを自動設定
            window.ChangeExternalMotionSenderAddress(responder.requestIpAddress, responder.requestServicePort);
        };
    }

    void Update()
    {
        //受信ポートを常に反映
        responder.servicePort = externalReceiver.receivePort;
        requester.servicePort = externalReceiver.receivePort;

        //各通信コンポーネントの有効状態と、個別の有効状態の両方(AND)で動く
        requester.enabled = externalReceiver.isActiveAndEnabled && requesterEnable;
        responder.enabled = externalSender.isActiveAndEnabled && responderEnable;

        //3秒間隔でリクエストするかを判断する
        time += Time.deltaTime;
        if (time > 3.0)
        {
            //受信できていなければ、発見状態はリセット
            if (lastPackets == externalReceiver.packets) {
                found = false;
            }
            lastPackets = externalReceiver.packets;

            //発見状態ではなく、探索有効ならビーコン発信を行う
            if (!found && externalReceiver.isActiveAndEnabled)
            {
                requester.StartDiscover(() => {
                    found = true;
                });
            }
            time = 0;
        }
    }
}
