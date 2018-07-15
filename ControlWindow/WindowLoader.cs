using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlWindow
{
    public enum CameraTypes
    {
        Free, Front, Back
    }

    public class WindowLoader
    {
        public static WindowLoader Instance { get; } = new WindowLoader();

        public void ShowWindow()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var windowinstance = assembly.CreateInstance("ControlWindow." + nameof(ControlWindow));
            windowinstance.GetType().GetMethod("ShowWindow", new Type[0]).Invoke(windowinstance, null);
        }

        //Events
        public Func<VRMData> LoadVRM;           // VRMData LoadVRM() { }
        public Action<string, bool> ImportVRM;  // void ImportVRM(string path, bool ImportForCalibration) { }
        public string CurrentVRMFilePath = null;

        public Action Calibrate;                // void Calibrate() { }
        public Action EndCalibrate;                // void EndCalibrate() { }

        public Action<bool> SetLipSyncEnable;       //void LipSyncEnable(bool enable) { }
        public Func<string[]> GetLipSyncDevices;    // string[] GetLipSyncDevices() { }
        public Action<string> SetLipSyncDevice;     //void SetLipSyncDevice(string device) { }
        public Action<float> SetLipSyncGain;        //void SetLipSyncGain(float gain) { }
        public Action<bool> SetLipSyncMaxWeightEnable;        //void SetLipSyncMaxWeightEnable(bool enable) { }
        public Action<float> SetLipSyncWeightThreashold;        //void SetLipSyncWeightThreashold(float threashold) { }
        public Action<bool> SetLipSyncMaxWeightEmphasis;        //void SetLipSyncMaxWeightEmphasis(bool enable) { }

        public Action<float, float, float, bool> ChangeBackgroundColor;   // void ChangeBackgroundColor(float r, float g, float b, bool isCustom) { }
        public Action SetBackgroundTransparent;                // void SetBackgroundTransparent() { }
        public Action<bool> SetWindowBorder;    //void SetWindowBorder(bool enable) { }
        public Action<bool> SetWindowTopMost;    //void SetWindowTopMost(bool enable) { }
        public Action<bool> SetWindowClickThrough;    //void SetWindowClickThrough(bool enable) { }

        public Action<bool> SetAutoBlinkEnable;       //void SetAutoBlinkEnable(bool enable) { }
        public Action<float> SetBlinkTimeMin;         //void SetBlinkTimeMin(float time) { }
        public Action<float> SetBlinkTimeMax;         //void SetBlinkTimeMax(float time) { }
        public Action<float> SetCloseAnimationTime;   //void SetCloseAnimationTime(float time) { }
        public Action<float> SetOpenAnimationTime;    //void SetOpenAnimationTime(float time) { }
        public Action<float> SetClosingTime;          //void SetClosingTime(float time) { }
        public Action<string> SetDefaultFace;         //void SetDefaultFace(string face) { }

        public Action SaveSettings;                // void SaveSettings() { }
        public Action LoadSettings;                // void LoadSettings() { }
        public Action<float, float, float> LoadCustomBackgroundColor;   // void LoadCustomBackgroundColor(float r, float g, float b) { } /*Unity to Forms*/
        public Action<bool> LoadHideBorder;     //void LoadHideBorder(bool enable) { } /*Unity to Forms*/
        public Action<bool> LoadIsTopMost;      //void LoadIsTopMost(bool enable) { } /*Unity to Forms*/
        public Action<bool> LoadSetWindowClickThrough;      //void LoadShowCameraGrid(bool enable) { } /*Unity to Forms*/
        public Action<bool> LoadShowCameraGrid;      //void LoadShowCameraGrid(bool enable) { } /*Unity to Forms*/
        public Action<bool> LoadLipSyncEnable;      //void LoadLipSyncEnable(bool enable) { } /*Unity to Forms*/
        public Action<string> LoadLipSyncDevice;    //void LoadLipSyncDevice(string device) { } /*Unity to Forms*/
        public Action<float> LoadLipSyncGain;        //void LoadLipSyncGain(float gain) { } /*Unity to Forms*/
        public Action<bool> LoadLipSyncMaxWeightEnable;      //void LoadLipSyncMaxWeightEnable(bool enable) { } /*Unity to Forms*/
        public Action<float> LoadLipSyncWeightThreashold;      //void LoadLipSyncWeightThreashold(float threashold) { } /*Unity to Forms*/
        public Action<bool> LoadLipSyncMaxWeightEmphasis;      //void LoadLipSyncMaxWeightEmphasis(bool enable) { } /*Unity to Forms*/
        public Action<bool> LoadAutoBlinkEnable;       //void LoadAutoBlinkEnable(bool enable) { } /*Unity to Forms*/
        public Action<float> LoadBlinkTimeMin;         //void LoadBlinkTimeMin(float time) { } /*Unity to Forms*/
        public Action<float> LoadBlinkTimeMax;         //void LoadBlinkTimeMax(float time) { } /*Unity to Forms*/
        public Action<float> LoadCloseAnimationTime;   //void LoadCloseAnimationTime(float time) { } /*Unity to Forms*/
        public Action<float> LoadOpenAnimationTime;    //void LoadOpenAnimationTime(float time) { } /*Unity to Forms*/
        public Action<float> LoadClosingTime;          //void LoadClosingTime(float time) { } /*Unity to Forms*/
        public Action<string> LoadDefaultFace;         //void LoadDefaultFace(string face) { } /*Unity to Forms*/

        public Action<CameraTypes> ChangeCamera; //void ChangeCamera(CameraTypes type) { }
        public Action<bool> SetGridVisible;    //void SetGridVisible(bool enable) { }

        public Action<int, Action> RunAfterMs; // void RunAfterMs(int ms, Action action) { }
        public Action<Action> RunOnUnity;       // void RunOnUnity(Action action) { }

        public Action<object> TestEvent;        // void TestEvent(object obj) { }
    }

    public class VRMData
    {
        public string FilePath { get; set; }

        public string ExporterVersion { get; set; }

        // Info
        public string Title { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public string ContactInformation { get; set; }
        public string Reference { get; set; }
        public byte[] ThumbnailPNGBytes { get; set; }

        // Permission
        public AllowedUser AllowedUser { get; set; }
        public UssageLicense ViolentUssage { get; set; }
        public UssageLicense SexualUssage { get; set; }
        public UssageLicense CommercialUssage { get; set; }
        public string OtherPermissionUrl { get; set; }

        // Distribution License
        public LicenseType LicenseType { get; set; }
        public string OtherLicenseUrl { get; set; }
    }

    public enum AllowedUser
    {
        OnlyAuthor,
        ExplicitlyLicensedPerson,
        Everyone,
    }

    public enum LicenseType
    {
        Redistribution_Prohibited,
        CC0,
        CC_BY,
        CC_BY_NC,
        CC_BY_SA,
        CC_BY_NC_SA,
        CC_BY_ND,
        CC_BY_NC_ND,
        Other
    }

    public enum UssageLicense
    {
        Disallow,
        Allow,
    }

}
