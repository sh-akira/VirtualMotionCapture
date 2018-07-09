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

        public Action<float, float, float, bool> ChangeBackgroundColor;   // void ChangeBackgroundColor(float r, float g, float b, bool isCustom) { }
        public Action SetBackgroundTransparent;                // void SetBackgroundTransparent() { }
        public Action<bool> SetWindowBorder;    //void SetWindowBorder(bool enable) { }
        public Action<bool> SetWindowTopMost;    //void SetWindowTopMost(bool enable) { }


        public Action SaveSettings;                // void SaveSettings() { }
        public Action LoadSettings;                // void LoadSettings() { }
        public Action<float, float, float> LoadCustomBackgroundColor;   // void LoadCustomBackgroundColor(float r, float g, float b) { } /*Unity to Forms*/

        public Action<CameraTypes> ChangeCamera; //void ChangeCamera(CameraTypes type) { }

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
