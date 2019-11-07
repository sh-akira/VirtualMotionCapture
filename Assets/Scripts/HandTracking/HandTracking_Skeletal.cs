using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Valve.VR;

public class HandTracking_Skeletal : MonoBehaviour
{

    private static uint activeActionSetSize = 0;
    private static uint skeletalActionData_size = 0;
    private static Type enumType = typeof(SteamVR_Input_Sources);
    private static Type descriptionType = typeof(DescriptionAttribute);

    protected InputSkeletalActionData_t skeletalActionData = new InputSkeletalActionData_t();

    public EVRSkeletalMotionRange rangeOfMotion { get; set; }
    public EVRSkeletalTransformSpace skeletalTransformSpace { get; set; }

    protected VRBoneTransform_t[] tempBoneTransforms = new VRBoneTransform_t[SteamVR_Action_Skeleton.numBones];

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
    
    [SerializeField]
    private HandController handController;

    //Indexで手を完全に開いたとき
    private Vector3[] indexHandReferences_Paper = new Vector3[] {
        //左手
        new Vector3( 359.5620000f ,   5.868034000f ,   6.407459f), //小指から
        new Vector3(   0.1860409f ,   0.010366790f , 345.483900f),
        new Vector3( 354.5298000f ,  15.953710000f , 350.730700f),
        new Vector3( 359.9762000f ,   1.456663000f , 355.366200f),
        new Vector3( 359.7434000f ,   0.007270342f , 356.552400f),
        new Vector3( 354.1742000f ,  12.197950000f , 351.004200f),
        new Vector3( 359.4128000f ,   3.201009000f ,   7.462775f),
        new Vector3(   2.0188290f , 358.361300000f , 352.935000f),
        new Vector3( 341.5331000f ,  10.819690000f , 350.100100f),
        new Vector3(   0.1769647f ,   2.613527000f ,   1.541096f),
        new Vector3(   5.2568340f ,  -0.003815474f ,   5.271921f),
        new Vector3(   1.1935620f ,   4.396974000f , 349.946500f),
        new Vector3(   0.1973783f , 357.474800000f ,  25.656840f), //第1関節(指先)
        new Vector3(   9.3738620f , 357.220000000f , 353.428300f), //第2関節
        new Vector3(  10.8035900f , 282.224500000f , 244.613900f), //第3関節
        //右手
        new Vector3(359.3318f,5.937632f,8.409874f),
        new Vector3(0.2176495f,359.9919f,347.779f),
        new Vector3(354.3216f,16.03978f,351.1829f),
        new Vector3(359.9786f,1.456076f,355.2201f),
        new Vector3(359.7449f,0.007630959f,356.2551f),
        new Vector3(354.1842f,12.19416f,350.9579f),
        new Vector3(359.4052f,3.202486f,7.591185f),
        new Vector3(2.02463f,358.3632f,353.1028f),
        new Vector3(341.5198f,10.81796f,350.1397f),
        new Vector3(0.1769647f,2.613527f,1.541096f),
        new Vector3(5.256834f,-0.003815474f,5.271921f),
        new Vector3(1.193562f,4.396974f,349.9465f),
        new Vector3(0.1973783f,357.4748f,25.65684f),
        new Vector3(9.373862f,357.22f,353.4283f),
        new Vector3(349.1964f,257.7755f,64.61391f),
    };
    //Indexで手を完全に閉じたとき
    private Vector3[] indexHandReferences_Rock = new Vector3[] {
        //左手
        new Vector3(359.78060000f , 358.5597000f , 255.5686f), //小指から
        new Vector3(  0.21743000f ,   1.2954080f , 269.2150f),
        new Vector3(359.40910000f ,   6.1556050f , 265.4337f),
        new Vector3(  0.08867738f ,   0.4809364f , 257.6532f),
        new Vector3(359.95290000f , 359.7137000f , 263.2600f),
        new Vector3(354.04300000f ,   7.4427940f , 264.6832f),
        new Vector3(359.92100000f , 359.5338000f , 262.2974f),
        new Vector3(  0.07707720f ,   0.4156884f , 262.4894f),
        new Vector3(347.87730000f ,   3.7890210f , 262.8109f),
        new Vector3(  0.01379834f ,   0.1329391f , 276.3850f),
        new Vector3(359.88360000f , 359.4341000f , 248.4853f),
        new Vector3(  6.07085000f ,  12.3191300f , 269.0260f),
        new Vector3(  0.11365890f , 359.8626000f , 285.6703f), //第1関節(指先)
        new Vector3(  0.43633980f ,   9.6895590f , 298.2396f), //第2関節
        new Vector3(  0.56381050f , 300.1995000f , 216.8110f), //第3関節
        //右手
        new Vector3(0.1080165f,358.717f,255.8838f),
        new Vector3(0.179034f,1.254422f,269.2236f),
        new Vector3(359.6021f,6.070357f,268.5331f),
        new Vector3(0.08867738f,0.4809364f,257.6532f),
        new Vector3(359.9529f,359.7137f,263.26f),
        new Vector3(354.043f,7.442794f,264.6832f),
        new Vector3(359.921f,359.5338f,262.2974f),
        new Vector3(0.0770772f,0.4156884f,262.4894f),
        new Vector3(347.8773f,3.789021f,262.8109f),
        new Vector3(0.01379834f,0.1329391f,276.385f),
        new Vector3(359.8836f,359.4341f,248.4853f),
        new Vector3(6.07085f,12.31913f,269.026f),
        new Vector3(0.1136589f,359.8626f,285.6703f),
        new Vector3(0.4363398f,9.689559f,298.2396f),
        new Vector3(359.4362f,239.8005f,36.81102f),
    };

    private Vector3[] vrmHandReferences_Paper = new Vector3[] {
        //左手
        new Vector3(359.5f,8.33795E-10f,358f),      //小指から
        new Vector3(359.5f,-8.33795E-10f,3f),
        new Vector3(359.5f,339f,10f),
        new Vector3(359.5f,0f,5f),
        new Vector3(359.5f,0f,5f),
        new Vector3(359.5f,347f,4f),
        new Vector3(359.5f,-8.33795E-10f,2f),
        new Vector3(359.5f,0f,0f),
        new Vector3(359.5f,356f,359f),
        new Vector3(359.5f,-4.168975E-10f,1f),
        new Vector3(359.5f,-4.168975E-10f,1f),
        new Vector3(359.5f,2f,-8.33795E-10f),
        new Vector3(346.6555f,21.15556f,357.3556f), //第1関節(指先)
        new Vector3(349.7889f,9.71111f,356.1667f),  //第2関節
        new Vector3(359.5f,357f,14f),               //第3関節
        //右手
        new Vector3(359.5f,-8.33795E-10f,2f),
        new Vector3(359.5f,8.33795E-10f,357f),
        new Vector3(359.5f,21f,350f),
        new Vector3(359.5f,0f,355f),
        new Vector3(359.5f,0f,355f),
        new Vector3(359.5f,13f,356f),
        new Vector3(359.5f,8.33795E-10f,358f),
        new Vector3(359.5f,0f,0f),
        new Vector3(359.5f,4f,1f),
        new Vector3(359.5f,4.168975E-10f,359f),
        new Vector3(359.5f,4.168975E-10f,359f),
        new Vector3(359.5f,358f,8.33795E-10f),
        new Vector3(346.6555f,338.8445f,2.644444f),
        new Vector3(349.7889f,350.2889f,3.833333f),
        new Vector3(359.5f,3f,346f),
    };
    private Vector3[] vrmHandReferences_Rock = new Vector3[] {
        //左手
        new Vector3(359.5f,0f,90f),                 //小指から
        new Vector3(359.5f,0f,90f),
        new Vector3(359.5f,0f,90f),
        new Vector3(359.5f,0f,90f),
        new Vector3(359.5f,0f,90f),
        new Vector3(359.5f,0f,90f),
        new Vector3(359.5f,0f,90f),
        new Vector3(359.5f,0f,90f),
        new Vector3(359.5f,0f,86f),
        new Vector3(359.5f,0f,81f),
        new Vector3(359.5f,-2.668144E-08f,102f),
        new Vector3(359.5f,0f,79f),
        new Vector3(44.83333f,285.3333f,9.333335f), //第1関節(指先)
        new Vector3(29.47778f,330.0222f,11.83333f), //第2関節
        new Vector3(359.5f,345f,6f),                //第3関節
        //右手
        new Vector3(359.5f,0f,270f),                  
        new Vector3(359.5f,0f,270f),
        new Vector3(359.5f,0f,270f),
        new Vector3(359.5f,0f,270f),
        new Vector3(359.5f,0f,270f),
        new Vector3(359.5f,0f,270f),
        new Vector3(359.5f,0f,270f),
        new Vector3(359.5f,0f,270f),
        new Vector3(359.5f,0f,274f),
        new Vector3(359.5f,0f,279f),
        new Vector3(359.5f,2.668144E-08f,258f),
        new Vector3(359.5f,0f,281f),
        new Vector3(44.83333f,74.66666f,350.6667f),
        new Vector3(29.47778f,29.97778f,348.1667f),
        new Vector3(359.5f,15f,354f),
    };

    private float[] vrmHandReferenceEuler_Open = new float[] { 2, -3, -10, 21, -5, -5, -4, 13, -2, 0, 1, 4, -1, -1, 0, -2, 34, 23, -14, 3 };
    private float[] vrmHandReferenceEuler_Close = new float[] { -90, -90, -90, 0, -90, -90, -90, 0, -90, -90, -86, 0, -81, -102, -79, 0, -120, -71, -6, 15 };

    private bool leftAvailable = false;
    private bool rightAvailable = false;

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
        
        //OVRControllerAction.Instance.UpdateManual();
        /*
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

            var path = Application.dataPath + "/../actions.json";

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
            */

        if (leftAvailable || rightAvailable)
        {
            IsDataAvailable = true;
            UpdateHandController(leftAvailable, rightAvailable);
            leftAvailable = false;
            rightAvailable = false;
        }
    }

    public void SetSkeltalBoneData(bool isLeft, VRBoneTransform_t[] tempBoneTransforms)
    {
        var bonePositions = isLeft ? leftBonePositions : rightBonePositions;
        var boneRotations = isLeft ? leftBoneRotations : rightBoneRotations;

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
        if (isLeft) leftAvailable = true;
        if (!isLeft) rightAvailable = true;
    }

    /*
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

        //Debug.Log("<b>Transform</b>(" + handle.ToString() + "):" + tempBoneTransforms[6].orientation.w.ToString() + "," + tempBoneTransforms[6].orientation.x.ToString() + "," + tempBoneTransforms[6].orientation.y.ToString() + "," + tempBoneTransforms[6].orientation.z.ToString());

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
    */

    private static string GetPath(string inputSourceEnumName)
    {
        return ((DescriptionAttribute)enumType.GetMember(inputSourceEnumName)[0].GetCustomAttributes(descriptionType, false)[0]).Description;
    }

    private void UpdateHandController(bool leftEnable, bool rightEnable)
    {
        var eulers = new List<Vector3>();
        int index = 0;
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.pinkyDistal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.pinkyMiddle].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.pinkyProximal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.ringDistal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.ringMiddle].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.ringProximal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.middleDistal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.middleMiddle].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.middleProximal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.indexDistal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.indexMiddle].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.indexProximal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.thumbDistal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.thumbMiddle].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, leftBoneRotations[SteamVR_Skeleton_JointIndexes.thumbProximal].eulerAngles));

        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.pinkyDistal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.pinkyMiddle].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.pinkyProximal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.ringDistal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.ringMiddle].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.ringProximal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.middleDistal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.middleMiddle].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.middleProximal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.indexDistal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.indexMiddle].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.indexProximal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.thumbDistal].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.thumbMiddle].eulerAngles));
        eulers.Add(GetVRMAngleFromIndexAngle(index++, rightBoneRotations[SteamVR_Skeleton_JointIndexes.thumbProximal].eulerAngles));

        handController.SetHandEulerAngles(leftEnable, rightEnable, eulers);
    }

    private Vector3 GetVRMAngleFromIndexAngle(int index, Vector3 angle)
    {
        var refmax = indexHandReferences_Paper[index];
        var refmin = indexHandReferences_Rock[index];
        var vrmmax = vrmHandReferences_Paper[index];
        var vrmmin = vrmHandReferences_Rock[index];
        var calcrefmax = new Vector3(refmax.x > 180 ? refmax.x - 360 : refmax.x, refmax.y > 180 ? refmax.y - 360 : refmax.y, refmax.z > 180 ? refmax.z - 360 : refmax.z);
        var calcrefmin = new Vector3(refmin.x > 180 ? refmin.x - 360 : refmin.x, refmin.y > 180 ? refmin.y - 360 : refmin.y, refmin.z > 180 ? refmin.z - 360 : refmin.z);
        var calcvrmmax = new Vector3(vrmmax.x > 180 ? vrmmax.x - 360 : vrmmax.x, vrmmax.y > 180 ? vrmmax.y - 360 : vrmmax.y, vrmmax.z > 180 ? vrmmax.z - 360 : vrmmax.z);
        var calcvrmmin = new Vector3(vrmmin.x > 180 ? vrmmin.x - 360 : vrmmin.x, vrmmin.y > 180 ? vrmmin.y - 360 : vrmmin.y, vrmmin.z > 180 ? vrmmin.z - 360 : vrmmin.z);
        var calcangle = new Vector3(angle.x > 180 ? angle.x - 360 : angle.x, angle.y > 180 ? angle.y - 360 : angle.y, angle.z > 180 ? angle.z - 360 : angle.z);
        var ratio3 = GetRatio3(calcangle, calcrefmin, calcrefmax);
        var value3 = GetValueFromRatio3(ratio3, calcvrmmin, calcvrmmax);
        int onehandCount = indexHandReferences_Paper.Length / 2;
        if (index < 12 || (index >= onehandCount && index < 27)) //小指～人差し指
        {
            return value3;
        }
        else //親指
        {
            var angleRef = new Vector3[] { new Vector3(0.0f, 0.0f, 1.0f), new Vector3(38f, 38f, -15f), new Vector3(34f, 56f, -7f) };
            int vrmeulerangle;
            int vrmeulersideangle;
            var vrmangles = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            int retindex = 0;
            if (index < onehandCount) onehandCount = 0;
            if (index == 12 || index == 12 + onehandCount) //指先
            {
                vrmeulerangle = (int)GetValueFromRatio(ratio3.z, vrmHandReferenceEuler_Close[16], vrmHandReferenceEuler_Open[16]);
                vrmeulersideangle = 0;
                vrmangles[16] = vrmeulerangle;
                retindex = 12 + onehandCount;
            }
            else if (index == 13 || index == 13 + onehandCount)
            {
                vrmeulerangle = (int)GetValueFromRatio(ratio3.z, vrmHandReferenceEuler_Close[17], vrmHandReferenceEuler_Open[17]);
                vrmeulersideangle = 0;
                vrmangles[17] = vrmeulerangle;
                retindex = 13 + onehandCount;
            }
            else if (index == 14 || index == 14 + onehandCount)
            {
                vrmeulerangle = (int)GetValueFromRatio(ratio3.z, vrmHandReferenceEuler_Close[18], vrmHandReferenceEuler_Open[18]);
                vrmeulersideangle = (int)GetValueFromRatio(ratio3.y, vrmHandReferenceEuler_Close[19], vrmHandReferenceEuler_Open[19]);
                vrmangles[18] = vrmeulerangle;
                vrmangles[19] = vrmeulersideangle;
                retindex = 14 + onehandCount;
            }
            var handEulerAngles = handController.CalcHandEulerAngles(vrmangles);
            if (handEulerAngles == null) return Vector3.zero;
            return handEulerAngles[retindex];
        }
    }

    private Vector3 GetRatio3(Vector3 value, Vector3 min, Vector3 max)
    {
        return new Vector3(GetRatio(value.x, min.x, max.x), GetRatio(value.y, min.y, max.y), GetRatio(value.z, min.z, max.z));
    }

    private Vector3 GetValueFromRatio3(Vector3 ratio, Vector3 min, Vector3 max)
    {
        return new Vector3(GetValueFromRatio(ratio.x, min.x, max.x), GetValueFromRatio(ratio.y, min.y, max.y), GetValueFromRatio(ratio.z, min.z, max.z));
    }

    private float GetRatio(float value, float min, float max)
    {
        var div = (max - min);
        if (float.IsNaN(div)) return 0f;
        if (Mathf.Abs(div) < 0.000001f) return 0f;
        return (value - min) / div;
    }

    private float GetValueFromRatio(float ratio, float min, float max)
    {
        return ratio * (max - min) + min;
    }
}