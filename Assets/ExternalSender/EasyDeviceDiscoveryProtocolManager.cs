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

        responder = gameObject.AddComponent<Responder>();
        responder.deivceName = myname;
        responder.OnRequested = ()=> {
            if (responder.requestDeviceName != myname) {
                if(externalSender != null)
                {
                    externalSender.ChangeOSCAddress(responder.requestIpAddress, responder.requestServicePort);
                }
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
                if (!found)
                {
                    requester.servicePort = externalReceiver.receivePort;
                    requester.StartDiscover(() => {
                        if (requester.deivceName != myname) {
                            found = true;
                        }
                    });
                }
                time = 0;
            }
        }
    }
}
