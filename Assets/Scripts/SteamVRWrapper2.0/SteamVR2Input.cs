using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Valve.VR;

public class SteamVR2Input : MonoBehaviour
{
    public static SteamVR2Input Instance;

    public EventHandler<OVRKeyEventArgs> KeyDownEvent;
    public EventHandler<OVRKeyEventArgs> KeyUpEvent;
    public EventHandler<OVRKeyEventArgs> AxisChangedEvent;

    private bool initialized = false;

    private static uint activeActionSetSize = 0;
    private static uint skeletalActionData_size = 0;
    private static uint digitalActionData_size = 0;
    private static uint analogActionData_size = 0;

    public EVRSkeletalMotionRange rangeOfMotion;
    public EVRSkeletalTransformSpace skeletalTransformSpace;
    public HandTracking_Skeletal handTracking_Skeletal;

    public bool EnableSkeletal = true;

    private VRActiveActionSet_t[] rawActiveActionSetArray;
    private List<VRActionSet> ActionSetList = new List<VRActionSet>();

    private SteamVRActions CurrentActionData;


    void OnEnable()
    {
        Instance = this;
    }

    [Serializable]
    private class VRActionSet
    {
        public ulong ulActionSet;
        public ulong ulRestrictedToDevice;
        public bool IsLeft;
        public string InputSourcePath;
    }

    [Serializable]
    public class SteamVRAction
    {
        public string name;
        public string type;
        public string skeleton;
        public ulong handle;

        private string shortName = null;
        public string ShortName
        {
            get
            {
                if (shortName == null) shortName = name.Split('/').LastOrDefault();
                return shortName;
            }
        }

        public InputDigitalActionData_t digitalActionData = new InputDigitalActionData_t();
        public InputDigitalActionData_t lastDigitalActionData = new InputDigitalActionData_t();
        public InputAnalogActionData_t analogActionData = new InputAnalogActionData_t();
        public InputAnalogActionData_t lastAnalogActionData = new InputAnalogActionData_t();
    }

    [Serializable]
    public class SteamVRActionSet
    {
        public string name;
        public string usage;
    }

    [Serializable]
    public class SteamVRDefaultBinding
    {
        public string controller_type;
        public string binding_url;
    }

    [Serializable]
    public class SteamVRActions
    {
        public List<SteamVRAction> actions;
        public List<SteamVRActionSet> action_sets;
        public List<SteamVRDefaultBinding> default_bindings;
    }

    private Dictionary<string, Vector3> LastPositions = new Dictionary<string, Vector3>();

    private Vector3 GetLastPosition(string shortName)
    {
        Vector3 axis = Vector3.zero;
        var partname = shortName.Substring("Touch".Length);
        var key = LastPositions.Keys.FirstOrDefault(d => d.Contains(partname));
        if (key != null)
        {
            axis = LastPositions[key];
        }
        return axis;
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
            if (digitalActionData_size == 0)
                digitalActionData_size = (uint)Marshal.SizeOf(typeof(InputDigitalActionData_t));
            if (analogActionData_size == 0)
                analogActionData_size = (uint)Marshal.SizeOf(typeof(InputAnalogActionData_t));

            rangeOfMotion = EVRSkeletalMotionRange.WithoutController;
            skeletalTransformSpace = EVRSkeletalTransformSpace.Parent;

            var path = Application.dataPath + "/../actions.json";

            var currentPath = Application.dataPath;
            int lastIndex = currentPath.LastIndexOf('/');
            currentPath = currentPath.Remove(lastIndex, currentPath.Length - lastIndex);

            var fullPath = currentPath + "/actions.json";


            err = OpenVR.Input.SetActionManifestPath(fullPath);
            if (err != EVRInputError.None)
                Debug.LogError($"<b>[SteamVR]</b> Error loading action manifest into SteamVR: {err}");

            //parse actions.json
            var json = File.ReadAllText(fullPath);
            CurrentActionData = JsonUtility.FromJson<SteamVRActions>(json);

            foreach (var action in CurrentActionData.actions)
            {
                err = OpenVR.Input.GetActionHandle(action.name, ref action.handle);
                if (err != EVRInputError.None)
                    Debug.LogError($"<b>[SteamVR]</b> GetActionHandle error ({action.name}): {err}");
            }

            initialized = true;

            var actionSetPath = CurrentActionData.action_sets.First().name;
            ulong actionSetHandle = 0;
            err = OpenVR.Input.GetActionSetHandle(actionSetPath, ref actionSetHandle);
            if (err != EVRInputError.None)
                Debug.LogError($"<b>[SteamVR]</b> GetActionSetHandle error ({actionSetPath}): {err}");

            var inputSourceNames = System.Enum.GetNames(typeof(SteamVR_Input_Sources));
            foreach (var inputSourceName in inputSourceNames)
            {
                ulong inputSourceHandle = 0;
                var inputSourcePath = GetPath(inputSourceName); // Any,LeftHand,RightHand,...
                err = OpenVR.Input.GetInputSourceHandle(inputSourcePath, ref inputSourceHandle);
                if (err != EVRInputError.None)
                    Debug.LogError($"<b>[SteamVR]</b> GetInputSourceHandle error ({inputSourcePath}): {err}");
                else
                {
                    ActionSetList.Add(new VRActionSet
                    {
                        ulActionSet = actionSetHandle,
                        ulRestrictedToDevice = inputSourceHandle,
                        InputSourcePath = inputSourcePath,
                        IsLeft = inputSourcePath.Contains("left"),
                    });
                }
            }
            //UpdateSkeleton();
            rawActiveActionSetArray = ActionSetList.Select(d => new VRActiveActionSet_t
            {
                ulActionSet = d.ulActionSet,
                nPriority = 0,//同プライオリティのアクションセットが複数ある場合同時に実行される
                ulRestrictedToDevice = d.ulRestrictedToDevice
            }).ToArray();

            OpenVR.Compositor.SetTrackingSpace(ETrackingUniverseOrigin.TrackingUniverseStanding);
        }

        //すべてのActionSetに対して新しいイベントがないか更新する
        err = OpenVR.Input.UpdateActionState(rawActiveActionSetArray, activeActionSetSize);
        if (err != EVRInputError.None)
            Debug.LogError($"<b>[SteamVR]</b> UpdateActionState error: {err}");

        foreach (var actionset in ActionSetList)
        {
            foreach (var action in CurrentActionData.actions)
            {
                if (action.type == "boolean")
                {
                    //オンオフ系のデータ取得(クリックやタッチ)
                    action.lastDigitalActionData = action.digitalActionData;
                    err = OpenVR.Input.GetDigitalActionData(action.handle, ref action.digitalActionData, digitalActionData_size, actionset.ulRestrictedToDevice);
                    if (err != EVRInputError.None)
                    {
                        Debug.LogWarning($"<b>[SteamVR]</b> GetDigitalActionData error ({action.name}): {err} handle: {action.handle}");
                        continue;
                    }
                    if (IsKeyDown(action.digitalActionData))
                    {
                        Debug.Log($"<b>[SteamVR]</b> GetDigitalActionData IsKeyDown ({action.name}): {err} handle: {action.handle}");

                        bool isTouch = action.ShortName.StartsWith("Touch") && action.ShortName.Contains("Trigger") == false;
                        Vector3 axis = isTouch ? GetLastPosition(action.ShortName) : Vector3.zero;
                        KeyDownEvent?.Invoke(this, new OVRKeyEventArgs(action.ShortName, axis, actionset.IsLeft, axis != Vector3.zero, isTouch));
                    }
                    if (IsKeyUp(action.digitalActionData))
                    {
                        Debug.Log($"<b>[SteamVR]</b> GetDigitalActionData IsKeyUp ({action.name}): {err} handle: {action.handle}");

                        bool isTouch = action.ShortName.StartsWith("Touch") && action.ShortName.Contains("Trigger") == false;
                        Vector3 axis = isTouch ? GetLastPosition(action.ShortName) : Vector3.zero;
                        KeyUpEvent?.Invoke(this, new OVRKeyEventArgs(action.ShortName, axis, actionset.IsLeft, axis != Vector3.zero, isTouch));
                    }
                }
                else if (action.type == "vector1" || action.type == "vector2" || action.type == "vector3")
                {
                    //アナログ入力のデータ取得(スティックやタッチパッド)
                    action.lastAnalogActionData = action.analogActionData;
                    err = OpenVR.Input.GetAnalogActionData(action.handle, ref action.analogActionData, analogActionData_size, actionset.ulRestrictedToDevice);
                    if (err != EVRInputError.None)
                    {
                        Debug.LogWarning($"<b>[SteamVR]</b> GetAnalogActionData error ({action.name}): {err} handle: {action.handle}");
                        continue;
                    }
                    //Debug.Log($"<b>[SteamVR]</b> GetAnalogActionData Position:{action.analogActionData.x},{action.analogActionData.y} ({action.name}): {err} handle: {action.handle}");
                    var axis = new Vector3(action.analogActionData.x, action.analogActionData.y, action.analogActionData.z);
                    if (axis != Vector3.zero)
                    {
                        LastPositions[action.name] = axis;
                        AxisChangedEvent?.Invoke(this, new OVRKeyEventArgs(action.ShortName, axis, actionset.IsLeft, true, false));
                    }
                }
                else if (action.type == "skeleton")
                {
                    if (EnableSkeletal)
                    {
                        //実際にBoneのTransformを取得する
                        //rangeOfMotionは実際のコントローラーの形に指を曲げる(WithController)か、完全にグーが出来るようにする(WithoutController)か
                        var tempBoneTransforms = new VRBoneTransform_t[SteamVR_Action_Skeleton.numBones];
                        err = OpenVR.Input.GetSkeletalBoneData(action.handle, skeletalTransformSpace, rangeOfMotion, tempBoneTransforms);
                        if (err != EVRInputError.None)
                        {
                            //特定の条件においてものすごい勢いでログが出る
                            //Debug.LogWarning($"<b>[SteamVR]</b> GetDigitalActionData error ({action.name}): {err} handle: {action.handle}");
                            continue;
                        }
                        handTracking_Skeletal.SetSkeltalBoneData(action.name.Contains("Left"), tempBoneTransforms);
                    }
                }
            }
        }
    }

    private bool IsKeyDown(InputDigitalActionData_t actionData)
    {
        return actionData.bState && actionData.bChanged;
    }

    private bool IsKeyUp(InputDigitalActionData_t actionData)
    {
        return actionData.bState == false && actionData.bChanged;
    }

    private static Type enumType = typeof(SteamVR_Input_Sources);
    private static Type descriptionType = typeof(DescriptionAttribute);
    private static string GetPath(string inputSourceEnumName)
    {
        return ((DescriptionAttribute)enumType.GetMember(inputSourceEnumName)[0].GetCustomAttributes(descriptionType, false)[0]).Description;
    }
}

public class OVRKeyEventArgs : EventArgs
{
    public string Name { get; }
    public Vector3 Axis { get; }
    public bool IsLeft { get; }
    public bool IsAxis { get; }
    public bool IsTouch { get; }

    public OVRKeyEventArgs(string name, Vector3 axis, bool isLeft, bool isAxis, bool isTouch) : base()
    {
        Name = name; Axis = axis; IsLeft = isLeft; IsAxis = isAxis; IsTouch = isTouch;
    }
}
