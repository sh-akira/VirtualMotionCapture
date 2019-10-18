//gpsnmeajp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;
using VRM;
using System.Reflection;

[RequireComponent(typeof(uOSC.uOscClient))]
public class ExternalSender : MonoBehaviour
{
    uOSC.uOscClient uClient = null;
    GameObject CurrentModel = null;
    ControlWPFWindow window = null;
    Animator animator = null;
    VRIK vrik = null;
    VRMBlendShapeProxy blendShapeProxy = null;
    Camera currentCamera = null;

    public SteamVR2Input steamVR2Input;

    GameObject handTrackerRoot;

    void Start()
    {
        uClient = GetComponent<uOSC.uOscClient>();
        window = GameObject.Find("ControlWPFWindow").GetComponent<ControlWPFWindow>();
        handTrackerRoot = GameObject.Find("HandTrackerRoot");

        window.ModelLoadedAction += (GameObject CurrentModel) =>
        {
            if (CurrentModel != null)
            {
                this.CurrentModel = CurrentModel;
                animator = CurrentModel.GetComponent<Animator>();
                vrik = CurrentModel.GetComponent<VRIK>();
                blendShapeProxy = CurrentModel.GetComponent<VRMBlendShapeProxy>();
            }
        };

        window.CameraChangedAction += (Camera currentCamera) =>
        {
            this.currentCamera = currentCamera;
        };

        steamVR2Input.KeyDownEvent += (object sender, OVRKeyEventArgs e) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: ConDown");
                try
                {
                    uClient?.Send("/VMC/Ext/Con", 1, e.Name, e.IsLeft ? 1 : 0, e.IsTouch ? 1 : 0, e.IsAxis ? 1 : 0, e.Axis.x, e.Axis.y, e.Axis.z);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };

        steamVR2Input.KeyUpEvent += (object sender, OVRKeyEventArgs e) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: ConUp");
                try
                {
                    uClient?.Send("/VMC/Ext/Con", 0, e.Name, e.IsLeft ? 1 : 0, e.IsTouch ? 1 : 0, e.IsAxis ? 1 : 0, e.Axis.x, e.Axis.y, e.Axis.z);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };

        steamVR2Input.AxisChangedEvent += (object sender, OVRKeyEventArgs e) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: ConAxis");
                try
                {
                    if (e.IsAxis)
                    {
                        uClient?.Send("/VMC/Ext/Con", 2, e.Name, e.IsLeft ? 1 : 0, e.IsTouch ? 1 : 0, e.IsAxis ? 1 : 0, e.Axis.x, e.Axis.y, e.Axis.z);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };

        KeyboardAction.KeyDownEvent += (object sender, KeyboardEventArgs e) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: KeyDown");
                try
                {
                    uClient?.Send("/VMC/Ext/Key", 1, e.KeyName, e.KeyCode);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };
        KeyboardAction.KeyUpEvent += (object sender, KeyboardEventArgs e) =>
        {
            if (this.isActiveAndEnabled)
            {
                //Debug.Log("Ext: KeyUp");
                try
                {
                    uClient?.Send("/VMC/Ext/Key", 0, e.KeyName, e.KeyCode);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }
        };
    }

    // Update is called once per frame
    void Update()
    {
        if (CurrentModel != null && animator != null)
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
                var offset = handTrackerRoot.transform;
                if (RootTransform != null && offset != null)
                {
                    uClient?.Send("/VMC/Ext/Root/Pos",
                        "root",
                        RootTransform.position.x, RootTransform.position.y, RootTransform.position.z,
                        RootTransform.rotation.x, RootTransform.rotation.y, RootTransform.rotation.z, RootTransform.rotation.w,
                        offset.localScale.x, offset.localScale.y, offset.localScale.z,
                        offset.position.x, offset.position.y, offset.position.z);
                }
            }

            //Bones
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                var Transform = animator.GetBoneTransform(bone);
                if (Transform != null)
                {
                    uClient?.Send("/VMC/Ext/Bone/Pos",
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

            if (blendShapeProxy != null)
            {
                foreach (var b in blendShapeProxy.GetValues())
                {
                    uClient?.Send("/VMC/Ext/Blend/Val",
                        b.Key.ToString(),
                        (float)b.Value
                        );
                }
                uClient?.Send("/VMC/Ext/Blend/Apply");
            }

            //Available
            uClient?.Send("/VMC/Ext/OK", 1);
        }
        else
        {
            uClient?.Send("/VMC/Ext/OK", 0);
        }

        //Camera
        if (currentCamera != null)
        {
            uClient?.Send("/VMC/Ext/Cam",
                "Camera",
                currentCamera.transform.position.x, currentCamera.transform.position.y, currentCamera.transform.position.z,
                currentCamera.transform.rotation.x, currentCamera.transform.rotation.y, currentCamera.transform.rotation.z, currentCamera.transform.rotation.w,
                currentCamera.fieldOfView);
        }

        uClient?.Send("/VMC/Ext/T", Time.time);
    }

    public void ChangeOSCAddress(string address, int port)
    {
        if (uClient == null) uClient = GetComponent<uOSC.uOscClient>();
        uClient.enabled = false;
        var type = typeof(uOSC.uOscClient);
        var addressfield = type.GetField("address", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance);
        addressfield.SetValue(uClient, address);
        var portfield = type.GetField("port", BindingFlags.SetField | BindingFlags.NonPublic | BindingFlags.Instance);
        portfield.SetValue(uClient, port);
        uClient.enabled = true;
    }
}

