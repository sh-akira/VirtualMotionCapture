//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EasyDeviceDiscoveryProtocolClient;

public class EasyDeviceDiscoveryProtocolManager : MonoBehaviour
{
    public ExternalReceiverForVMC externalReceiver;
    Requester requester;

    public float time = 0;
    public bool found = false;

    void Start()
    {
        requester = gameObject.AddComponent<Requester>();
        requester.deivceName = "Virtual Motion Capture";
    }

    void Update()
    {
        if (externalReceiver != null)
        {
            time += Time.deltaTime;
            if (time > 5.0)
            {
                if (!found)
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
