using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using UnityEngine;
using Valve.VR;

public class HandTracking_Index : MonoBehaviour
{

    private static uint activeActionSetSize = 0;
    private static uint skeletalActionData_size = 0;
    private static Type enumType = typeof(SteamVR_Input_Sources);
    private static Type descriptionType = typeof(DescriptionAttribute);

    protected InputSkeletalActionData_t skeletalActionData = new InputSkeletalActionData_t();

    public EVRSkeletalMotionRange rangeOfMotion { get; set; }
    public EVRSkeletalTransformSpace skeletalTransformSpace { get; set; }

    protected VRBoneTransform_t[] tempBoneTransforms = new VRBoneTransform_t[SteamVR_Action_Skeleton.numBones];
    protected VRBoneTransform_t[] tempRightBoneTransforms = new VRBoneTransform_t[SteamVR_Action_Skeleton.numBones];


    public Vector3[] leftBonePositions { get; protected set; }
    public Quaternion[] leftBoneRotations { get; protected set; }
    public Vector3[] rightBonePositions { get; protected set; }
    public Quaternion[] rightBoneRotations { get; protected set; }

    public bool IsDataAvailable { get; set; }

    private bool initialized = false;

    private ulong lefthand_handle = 0;
    private ulong righthand_handle = 0;
    private VRActiveActionSet_t[] rawActiveActionSetArray;

    private string actionSetPath = "/actions/default";
    private string skeletonLeftHandActionPath = "/actions/default/in/SkeletonLeftHand";
    private string skeletonRightHandActionPath = "/actions/default/in/SkeletonRightHand";

    // Use this for initialization
    void Start()
    {
        IsDataAvailable = false;
        leftBonePositions = new Vector3[SteamVR_Action_Skeleton.numBones];
        leftBoneRotations = new Quaternion[SteamVR_Action_Skeleton.numBones];
        rightBonePositions = new Vector3[SteamVR_Action_Skeleton.numBones];
        rightBoneRotations = new Quaternion[SteamVR_Action_Skeleton.numBones];
    }

    // Update is called once per frame
    void Update()
    {
        EVRInputError err;

        if (initialized == false)
        {
            if (OpenVR.Input == null)
            {
                return;
            }
            if (activeActionSetSize == 0)
                activeActionSetSize = (uint)(Marshal.SizeOf(typeof(VRActiveActionSet_t)));
            if (skeletalActionData_size == 0)
                skeletalActionData_size = (uint)Marshal.SizeOf(typeof(InputSkeletalActionData_t));

            rangeOfMotion = EVRSkeletalMotionRange.WithoutController;
            skeletalTransformSpace = EVRSkeletalTransformSpace.Parent;

            var path = Application.dataPath + "/../action.json";

            var currentPath = Application.dataPath;
            int lastIndex = currentPath.LastIndexOf('/');
            currentPath = currentPath.Remove(lastIndex, currentPath.Length - lastIndex);

            var fullPath = currentPath + "/actions.json";


            err = OpenVR.Input.SetActionManifestPath(fullPath);
            if (err != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> Error loading action manifest into SteamVR: " + err.ToString());

            err = OpenVR.Input.GetActionHandle(skeletonLeftHandActionPath, ref lefthand_handle);
            if (err != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetActionHandle (" + skeletonLeftHandActionPath + ") error: " + err.ToString());
            err = OpenVR.Input.GetActionHandle(skeletonRightHandActionPath, ref righthand_handle);
            if (err != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetActionHandle (" + skeletonRightHandActionPath + ") error: " + err.ToString());

            initialized = true;

            ulong actionSetHandle = 0;
            err = OpenVR.Input.GetActionSetHandle(actionSetPath, ref actionSetHandle);
            if (err != EVRInputError.None)
                Debug.LogError("<b>[SteamVR]</b> GetActionSetHandle (" + actionSetPath + ") error: " + err.ToString());

            var inputSourceNames = System.Enum.GetNames(typeof(SteamVR_Input_Sources));
            var vrActiveActionSetList = new List<VRActiveActionSet_t>();
            foreach (var inputSourceName in inputSourceNames)
            {
                ulong inputSourceHandle = 0;
                var inputSourcePath = GetPath(inputSourceName); // Any,LeftHand,RightHand,...
                err = OpenVR.Input.GetInputSourceHandle(inputSourcePath, ref inputSourceHandle);
                if (err != EVRInputError.None)
                    Debug.LogError("<b>[SteamVR]</b> GetInputSourceHandle (" + inputSourcePath + ") error: " + err.ToString());
                else
                {
                    vrActiveActionSetList.Add(new VRActiveActionSet_t()
                    {
                        ulActionSet = actionSetHandle,
                        nPriority = 0, //同プライオリティのアクションセットが複数ある場合同時に実行される
                        ulRestrictedToDevice = inputSourceHandle
                    });
                }
            }
            //UpdateSkeleton();
            rawActiveActionSetArray = vrActiveActionSetList.ToArray();

            OpenVR.Compositor.SetTrackingSpace(ETrackingUniverseOrigin.TrackingUniverseStanding);
        }

        bool leftAvailable = false;
        bool rightAvailable = false;

        //すべてのActionSetに対して新しいイベントがないか更新する
        err = OpenVR.Input.UpdateActionState(rawActiveActionSetArray, activeActionSetSize);
        if (err != EVRInputError.None)
            Debug.LogError("<b>[SteamVR]</b> UpdateActionState error: " + err.ToString());

        //左手SkeletalのActionが発生しているか取得する
        err = OpenVR.Input.GetSkeletalActionData(lefthand_handle, ref skeletalActionData, skeletalActionData_size);
        if (err != EVRInputError.None)
            Debug.LogError("<b>[SteamVR]</b> GetSkeletalActionData error (" + "" + "): " + err.ToString() + " handle: " + lefthand_handle.ToString());

        //左手Skeletalのイベントが発生していたら
        if (skeletalActionData.bActive)
            leftAvailable = GetSkeletalBoneData(lefthand_handle, leftBonePositions, leftBoneRotations);

        //右手SkeletalのActionが発生しているか取得する
        err = OpenVR.Input.GetSkeletalActionData(righthand_handle, ref skeletalActionData, skeletalActionData_size);
        if (err != EVRInputError.None)
            Debug.LogError("<b>[SteamVR]</b> GetSkeletalActionData error (" + "" + "): " + err.ToString() + " handle: " + righthand_handle.ToString());

        //右手Skeletalのイベントが発生していたら
        if (skeletalActionData.bActive)
            rightAvailable = GetSkeletalBoneData(righthand_handle, rightBonePositions, rightBoneRotations);

        if (leftAvailable || rightAvailable)
        {
            IsDataAvailable = true;
        }
    }

    private bool GetSkeletalBoneData(ulong handle, Vector3[] bonePositions, Quaternion[] boneRotations)
    {
        //実際にBoneのTransformを取得する
        //rangeOfMotionは実際のコントローラーの形に指を曲げる(WithController)か、完全にグーが出来るようにする(WithoutController)か
        EVRInputError err = OpenVR.Input.GetSkeletalBoneData(handle, skeletalTransformSpace, rangeOfMotion, tempBoneTransforms);
        if (err != EVRInputError.None)
        {
            Debug.LogError("<b>[SteamVR]</b> GetSkeletalBoneData error (" + "" + "): " + err.ToString() + " handle: " + lefthand_handle.ToString());
            return false;
        }

        Debug.Log("<b>Transform</b>(" + handle.ToString() + "):" + tempBoneTransforms[6].orientation.w.ToString() + "," + tempBoneTransforms[6].orientation.x.ToString() + "," + tempBoneTransforms[6].orientation.y.ToString() + "," + tempBoneTransforms[6].orientation.z.ToString());

        for (int boneIndex = 0; boneIndex < tempBoneTransforms.Length; boneIndex++)
        {
            // SteamVR's coordinate system is right handed, and Unity's is left handed.  The FBX data has its
            // X axis flipped when Unity imports it, so here we need to flip the X axis as well
            bonePositions[boneIndex].x = -tempBoneTransforms[boneIndex].position.v0;
            bonePositions[boneIndex].y = tempBoneTransforms[boneIndex].position.v1;
            bonePositions[boneIndex].z = tempBoneTransforms[boneIndex].position.v2;

            boneRotations[boneIndex].x = tempBoneTransforms[boneIndex].orientation.x;
            boneRotations[boneIndex].y = -tempBoneTransforms[boneIndex].orientation.y;
            boneRotations[boneIndex].z = -tempBoneTransforms[boneIndex].orientation.z;
            boneRotations[boneIndex].w = tempBoneTransforms[boneIndex].orientation.w;
        }

        // Now that we're in the same handedness as Unity, rotate the root bone around the Y axis
        // so that forward is facing down +Z

        boneRotations[0] = SteamVR_Action_Skeleton.steamVRFixUpRotation * boneRotations[0];
        return true;
    }

    private static string GetPath(string inputSourceEnumName)
    {
        return ((DescriptionAttribute)enumType.GetMember(inputSourceEnumName)[0].GetCustomAttributes(descriptionType, false)[0]).Description;
    }

}