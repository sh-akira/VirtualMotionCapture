//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;
using VRM;

[RequireComponent(typeof(uOSC.uOscClient))]
public class ExternalSender : MonoBehaviour {
    uOSC.uOscClient uClient = null;
    GameObject CurrentModel = null;
    ControlWPFWindow window = null;
    Animator animator = null;
    VRIK vrik = null;
    VRMBlendShapeProxy blendShapeProxy = null;

    void Start () {
        uClient = GetComponent<uOSC.uOscClient>();
        window = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();

        window.ModelLoadedAction += (GameObject CurrentModel) =>
        {
            this.CurrentModel = CurrentModel;
            animator = CurrentModel.GetComponent<Animator>();
            vrik = CurrentModel.GetComponent<VRIK>();
            blendShapeProxy = CurrentModel.GetComponent<VRMBlendShapeProxy>();
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
                    uClient.Send("/VMC/Ext/Root/Pos",
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
                    uClient.Send("/VMC/Ext/Bone/Pos", 
                        bone.ToString(), 
                        Transform.localPosition.x, Transform.localPosition.y, Transform.localPosition.z, 
                        Transform.localRotation.x, Transform.localRotation.y, Transform.localRotation.z, Transform.localRotation.w);
                }
            }

            //Blendsharp
            if (blendShapeProxy == null)
            {
                blendShapeProxy = CurrentModel.GetComponent<VRMBlendShapeProxy>();
                Debug.Log("ExternalSender: VRMBlendShapeProxy Updated");
            }

            if (blendShapeProxy != null) {
                foreach (var b in blendShapeProxy.GetValues())
                {
                    uClient.Send("/VMC/Ext/Blend/Val",
                        b.Key.ToString(),
                        (float)b.Value
                        );
                }
                uClient.Send("/VMC/Ext/Blend/Apply");
            }

            //Available
            uClient.Send("/VMC/Ext/OK", 1);
        }
        else
        {
            uClient.Send("/VMC/Ext/OK", 0);
        }
        uClient.Send("/VMC/Ext/T", Time.time);
    }
}

