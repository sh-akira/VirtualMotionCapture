//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyDeviceDiscoveryProtocolClient;

public class EasyDeviceDiscoveryProtocolManager : MonoBehaviour
{
    public ExternalSender externalSender;
    public ExternalReceiverForVMC externalReceiver;
    Requester requester;
    Responder responder;

    string myname = "Virtual Motion Capture";

    public float time = 0;
    public bool found = false;

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
            if (externalSender != null)
            {
                externalSender.ChangeOSCAddress(responder.requestIpAddress, responder.requestServicePort);
            }
        };
    }

    void Update()
    {
        if (externalSender != null)
        {
            responder.servicePort = externalReceiver.receivePort;
        }

        if (externalReceiver != null)
        {
            time += Time.deltaTime;
            if (time > 5.0)
            {
                if (!found && externalReceiver.isActiveAndEnabled)
                {
                    requester.servicePort = externalReceiver.receivePort;
                    requester.StartDiscover(() => {
                        found = true;
                    });
                }
                time = 0;
            }
        }
    }
}
