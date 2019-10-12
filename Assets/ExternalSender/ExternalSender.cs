//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;

public class ExternalSender : MonoBehaviour {
    uOSC.uOscClient uClient = null;
    GameObject CurrentModel = null;
    ControlWPFWindow window = null;
    Animator animator = null;
    VRIK vrik = null;

    void Start () {
        uClient = GetComponent<uOSC.uOscClient>();
        window = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();

        window.ModelLoadedAction += (GameObject CurrentModel) =>
        {
            this.CurrentModel = CurrentModel;
            animator = CurrentModel.GetComponent<Animator>();
            vrik = CurrentModel.GetComponent<VRIK>();
        };
    }

	// Update is called once per frame
	void Update () {
        if (CurrentModel != null && animator != null && uClient != null)
        {
            //Root
            if (vrik == null)
            {
                vrik = CurrentModel.GetComponent<VRIK>();
                Debug.Log("ExternalSender: VRIK Updated");
            }

            if (vrik != null)
            {
                var RootTransform = vrik.references.root;
                if (RootTransform != null)
                {
                    uClient.Send("/VMC/ExternalSender/Root/Transform",
                        "root",
                        RootTransform.position.x, RootTransform.position.y, RootTransform.position.z,
                        RootTransform.rotation.x, RootTransform.rotation.y, RootTransform.rotation.z, RootTransform.rotation.w);
                }
            }

            //Bones
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                var Transform = animator.GetBoneTransform(bone);
                if (Transform != null)
                {
                    uClient.Send("/VMC/ExternalSender/Bone/Transform", 
                        bone.ToString(), 
                        Transform.localPosition.x, Transform.localPosition.y, Transform.localPosition.z, 
                        Transform.localRotation.x, Transform.localRotation.y, Transform.localRotation.z, Transform.localRotation.w);
                }
            }
            uClient.Send("/VMC/ExternalSender/Available", 1);
        }
        else
        {
            uClient.Send("/VMC/ExternalSender/Available", 0);
        }
        uClient.Send("/VMC/ExternalSender/Time", Time.time);
    }
}

