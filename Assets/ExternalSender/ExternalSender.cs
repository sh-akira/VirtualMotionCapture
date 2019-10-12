//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExternalSender : MonoBehaviour {
    uOSC.uOscClient uClient;
    GameObject CurrentModel;
    ControlWPFWindow window;
   

    // Use this for initialization
    void Start () {
        uClient = GetComponent<uOSC.uOscClient>();
        window = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
    }
	
	// Update is called once per frame
	void Update () {

        Animator animator = null;

        CurrentModel = window.GetCurrentModel();
        if (CurrentModel != null)
        {
            animator = CurrentModel.GetComponent<Animator>();
        }

        if (animator != null)
        {
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                var Transform = animator.GetBoneTransform(bone);
                if (Transform != null)
                {
                    uClient.Send("/VMC/ExternalSender/Bone/Transform", bone.ToString(), Transform.localPosition.x, Transform.localPosition.y, Transform.localPosition.z, Transform.localRotation.x, Transform.localRotation.y, Transform.localRotation.z, Transform.localRotation.w);
                }
            }
            uClient.Send("/VMC/ExternalSender/Available", 1);
            uClient.Send("/VMC/ExternalSender/Time", Time.time);
        }
        else
        {
            uClient.Send("/VMC/ExternalSender/Available", 0);
            uClient.Send("/VMC/ExternalSender/Time", Time.time);
        }
    }
}

