using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityMemoryMappedFile;
using VMCMod;

namespace VMC
{
    public class CameraManager : MonoBehaviour
    {
        private static CameraManager current;
        public static CameraManager Current => current;

        [SerializeField]
        private ControlWPFWindow controlWPFWindow;
        private System.Threading.SynchronizationContext context = null;

        public GameObject LeftHandCamera;
        public GameObject RightHandCamera;

        public CameraMouseControl FreeCamera;
        public CameraMouseControl FrontCamera;
        public CameraMouseControl BackCamera;
        public CameraMouseControl PositionFixedCamera;

        public Camera ControlCamera;
        private CameraMouseControl CurrentCameraControl;

        private GameObject CurrentModel;
        private Animator animator;

        private void Awake()
        {
            current = this;
            VMCEvents.OnModelLoaded += ModelLoaded;
            VMCEvents.OnModelUnloading += ModelUnloading;
            controlWPFWindow.AdditionalSettingAction += ApplySettings;
        }

        private void Start()
        {
            context = System.Threading.SynchronizationContext.Current;
            controlWPFWindow.server.ReceivedEvent += Server_Received;

            VMCEvents.OnCameraChanged?.Invoke(ControlCamera);
        }
        private void Server_Received(object sender, DataReceivedEventArgs e)
        {
            context.Post(async s =>
            {
                if (e.CommandType == typeof(PipeCommands.ChangeCamera))
                {
                    var d = (PipeCommands.ChangeCamera)e.Data;
                    ChangeCamera(d.type);
                }
                else if (e.CommandType == typeof(PipeCommands.SetCameraMirror))
                {
                    var d = (PipeCommands.SetCameraMirror)e.Data;
                    SetCameraMirror(d.enable);
                }
                else if (e.CommandType == typeof(PipeCommands.SetCameraFOV))
                {
                    var d = (PipeCommands.SetCameraFOV)e.Data;
                    SetCameraFOV(d.value);
                }
                else if (e.CommandType == typeof(PipeCommands.SetCameraSmooth))
                {
                    var d = (PipeCommands.SetCameraSmooth)e.Data;
                    SetCameraSmooth(d.value);
                }
                else if (e.CommandType == typeof(PipeCommands.StartHandCamera))
                {
                    var d = (PipeCommands.StartHandCamera)e.Data;
                    StartHandCamera(d.IsLeft);
                }
                else if (e.CommandType == typeof(PipeCommands.EndHandCamera))
                {
                    EndHandCamera();
                }
                else if (e.CommandType == typeof(PipeCommands.SetExternalCameraConfig))
                {
                    var d = (PipeCommands.SetExternalCameraConfig)e.Data;
                    StartCoroutine(SetExternalCameraConfig(d));
                }
                else if (e.CommandType == typeof(PipeCommands.GetExternalCameraConfig))
                {
                    var d = (PipeCommands.GetExternalCameraConfig)e.Data;
                    var tracker = controlWPFWindow.handler.GetTrackerTransformByName(d.ControllerName);
                    //InverseTransformPoint  Thanks: えむにわ(@m2wasabi)
                    var rposition = tracker.InverseTransformPoint(ControlCamera.transform.position);
                    var rrotation = (Quaternion.Inverse(tracker.rotation) * ControlCamera.transform.rotation).eulerAngles;
                    await controlWPFWindow.server.SendCommandAsync(new PipeCommands.SetExternalCameraConfig
                    {
                        x = rposition.x,
                        y = rposition.y,
                        z = rposition.z,
                        rx = rrotation.x,
                        ry = rrotation.y,
                        rz = rrotation.z,
                        fov = ControlCamera.fieldOfView,
                        ControllerName = d.ControllerName
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.ImportCameraPlus))
                {
                    var d = (PipeCommands.ImportCameraPlus)e.Data;
                    ImportCameraPlus(d);
                }
                else if (e.CommandType == typeof(PipeCommands.ExportCameraPlus))
                {
                    await controlWPFWindow.server.SendCommandAsync(new PipeCommands.ReturnExportCameraPlus
                    {
                        x = Settings.Current.FreeCameraTransform.localPosition.x,
                        y = Settings.Current.FreeCameraTransform.localPosition.y,
                        z = Settings.Current.FreeCameraTransform.localPosition.z,
                        rx = Settings.Current.FreeCameraTransform.localRotation.eulerAngles.x,
                        ry = Settings.Current.FreeCameraTransform.localRotation.eulerAngles.y,
                        rz = Settings.Current.FreeCameraTransform.localRotation.eulerAngles.z,
                        fov = ControlCamera.fieldOfView
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.EndCalibrate))
                {
                    SetCameraLookTarget();
                }
                else if (e.CommandType == typeof(PipeCommands.GetVirtualWebCamConfig))
                {
                    await controlWPFWindow.server.SendCommandAsync(new PipeCommands.SetVirtualWebCamConfig
                    {
                        Enabled = Settings.Current.WebCamEnabled,
                        Resize = Settings.Current.WebCamResize,
                        Mirroring = Settings.Current.WebCamMirroring,
                        Buffering = Settings.Current.WebCamBuffering,
                    }, e.RequestId);
                }
                else if (e.CommandType == typeof(PipeCommands.SetVirtualWebCamConfig))
                {
                    var d = (PipeCommands.SetVirtualWebCamConfig)e.Data;
                    Settings.Current.WebCamEnabled = d.Enabled;
                    Settings.Current.WebCamResize = d.Resize;
                    Settings.Current.WebCamMirroring = d.Mirroring;
                    Settings.Current.WebCamBuffering = d.Buffering;
                    UpdateWebCamConfig();
                }
                else if (e.CommandType == typeof(PipeCommands.TakePhoto))
                {
                    var d = (PipeCommands.TakePhoto)e.Data;
                    TakePhoto(d.Width, d.TransparentBackground, d.Directory);
                }
            }, null);
        }

        private void ModelLoaded(GameObject currentModel)
        {
            CurrentModel = currentModel;
            animator = currentModel.GetComponent<Animator>();

            if (animator != null)
            {
                //設定用両手のカメラをモデルにアタッチ
                if (LeftHandCamera != null)
                {
                    LeftHandCamera.transform.SetParent(animator.GetBoneTransform(HumanBodyBones.LeftHand));
                    LeftHandCamera.transform.localPosition = new Vector3(-0.07f, -0.13f, 0.14f);
                    LeftHandCamera.transform.localRotation = Quaternion.Euler(-140f, 0f, 90f);
                    LeftHandCamera.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                }
                if (RightHandCamera != null)
                {
                    RightHandCamera.transform.SetParent(animator.GetBoneTransform(HumanBodyBones.RightHand));
                    RightHandCamera.transform.localPosition = new Vector3(0.07f, -0.13f, 0.14f);
                    RightHandCamera.transform.localRotation = Quaternion.Euler(-140f, 0f, -90f);
                    RightHandCamera.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
                }
            }

            SetCameraLookTarget();
        }

        private void ModelUnloading(GameObject currentModel)
        {
            if (LeftHandCamera != null)
            {
                LeftHandCamera.transform.SetParent(null);
            }
            if (RightHandCamera != null)
            {
                RightHandCamera.transform.SetParent(null);
            }
        }


        private Camera saveCurrentCamera = null;

        private void StartHandCamera(bool isLeft)
        {
            RightHandCamera.SetActive(isLeft == false);
            LeftHandCamera.SetActive(isLeft == true);
            saveCurrentCamera = ControlCamera;
            if (saveCurrentCamera != null) saveCurrentCamera.gameObject.SetActive(false);
        }

        private void EndHandCamera()
        {
            RightHandCamera.SetActive(false);
            LeftHandCamera.SetActive(false);
            if (saveCurrentCamera != null) saveCurrentCamera.gameObject.SetActive(true);
            saveCurrentCamera = null;
        }


        public void ChangeCamera(CameraTypes type)
        {
            if (type == CameraTypes.Free)
            {
                SetCameraEnable(FreeCamera);
            }
            else if (type == CameraTypes.Front)
            {
                SetCameraEnable(FrontCamera);
            }
            else if (type == CameraTypes.Back)
            {
                SetCameraEnable(BackCamera);
            }
            else if (type == CameraTypes.PositionFixed)
            {
                SetCameraEnable(PositionFixedCamera);
            }
            Settings.Current.CameraType = type;
        }

        private void SetCameraEnable(CameraMouseControl camera)
        {
            if (camera != null)
            {
                var virtualCam = ControlCamera.GetComponent<VirtualCamera>();
                if (virtualCam != null)
                {
                    virtualCam.enabled = Settings.Current.WebCamEnabled;
                }
                camera.gameObject.SetActive(true);
                if (CurrentCameraControl != null && CurrentCameraControl != camera) CurrentCameraControl.gameObject.SetActive(false);
                camera.GetComponent<CameraMouseControl>().enabled = true;
                CurrentCameraControl = camera;
                SetCameraMirrorEnable(Settings.Current.CameraMirrorEnable);

                VMCEvents.OnCameraChanged?.Invoke(ControlCamera);
            }
        }

        private void SetCameraMirror(bool enable)
        {
            Settings.Current.CameraMirrorEnable = enable;
            SetCameraMirrorEnable(enable);
        }

        private void SetCameraMirrorEnable(bool mirrorEnable)
        {
            var mirror = ControlCamera.GetComponent<CameraMirror>();
            if (mirror != null) mirror.MirrorEnable = mirrorEnable;
        }

        private void SetCameraLookTarget()
        {
            if (animator != null)
            {
                var spineTransform = animator.GetBoneTransform(HumanBodyBones.Spine);
                var calcPosition = Vector3.Lerp(animator.GetBoneTransform(HumanBodyBones.Head).position, spineTransform.position, 0.5f);
                var gameObject = new GameObject("CameraLook");
                gameObject.transform.position = calcPosition;
                gameObject.transform.rotation = spineTransform.rotation;
                gameObject.transform.parent =/* bodyTracker == null ? animator.GetBoneTransform(HumanBodyBones.Spine) :*/ CurrentModel.transform;
                var lookTarget = FrontCamera.GetComponent<CameraMouseControl>();
                if (lookTarget != null)
                {
                    lookTarget.LookTarget = gameObject.transform;
                }
                lookTarget = BackCamera.GetComponent<CameraMouseControl>();
                if (lookTarget != null)
                {
                    lookTarget.LookTarget = gameObject.transform;
                }
                var positionFixedCamera = PositionFixedCamera.GetComponent<CameraMouseControl>();
                if (positionFixedCamera != null)
                {
                    positionFixedCamera.PositionFixedTarget = gameObject.transform;
                }
            }
        }


        private IEnumerator SetExternalCameraConfig(PipeCommands.SetExternalCameraConfig d)
        {
            //フリーカメラに変更
            ChangeCamera(CameraTypes.Free);
            FreeCamera.GetComponent<CameraMouseControl>().enabled = false;
            //externalcamera.cfgは3つ目のコントローラー基準のポジション
            yield return null;
            //指定のコントローラーの子にして座標指定
            CurrentCameraControl.transform.SetParent(TrackingPointManager.Instance.GetTransform(d.ControllerName));
            CurrentCameraControl.transform.localPosition = new Vector3(d.x, d.y, d.z);
            CurrentCameraControl.transform.localRotation = Quaternion.Euler(d.rx, d.ry, d.rz);
            ControlCamera.fieldOfView = d.fov;
            //コントローラーは動くのでカメラ位置の保存はできない
            //if (Settings.Current.FreeCameraTransform == null) Settings.Current.FreeCameraTransform = new StoreTransform(currentCamera.transform);
            //Settings.Current.FreeCameraTransform.SetPosition(currentCamera.transform);
        }

        private async void ImportCameraPlus(PipeCommands.ImportCameraPlus d)
        {
            ChangeCamera(CameraTypes.Free);
            Settings.Current.FreeCameraTransform.localPosition = new Vector3(d.x, d.y, d.z);
            Settings.Current.FreeCameraTransform.localRotation = Quaternion.Euler(d.rx, d.ry, d.rz);
            ControlCamera.fieldOfView = d.fov;
            Settings.Current.FreeCameraTransform.ToLocalTransform(FreeCamera.transform);
            var control = FreeCamera.GetComponent<CameraMouseControl>();
            control.CameraAngle = -FreeCamera.transform.rotation.eulerAngles;
            control.CameraDistance = Vector3.Distance(FreeCamera.transform.localPosition, Vector3.zero);
            control.CameraTarget = FreeCamera.transform.localPosition + FreeCamera.transform.rotation * Vector3.forward * control.CameraDistance;
            await controlWPFWindow.server.SendCommandAsync(new PipeCommands.LoadCameraFOV { fov = d.fov });
        }

        private void SetCameraFOV(float fov)
        {
            Settings.Current.CameraFOV = fov;
            FrontCamera.SetCameraFOV(fov);
            BackCamera.SetCameraFOV(fov);
            FreeCamera.SetCameraFOV(fov);
            PositionFixedCamera.SetCameraFOV(fov);
            ControlCamera.fieldOfView = fov;
        }

        private void SetCameraSmooth(float speed)
        {
            Settings.Current.CameraSmooth = speed;
            ControlCamera.GetComponent<CameraFollower>().Speed = speed;
        }

        private void UpdateWebCamConfig()
        {
            SetCameraEnable(CurrentCameraControl);
            VirtualCamera.Buffering_Global = Settings.Current.WebCamBuffering;
            VirtualCamera.MirrorMode_Global = Settings.Current.WebCamMirroring ? VirtualCamera.EMirrorMode.MirrorHorizontally : VirtualCamera.EMirrorMode.Disabled;
            VirtualCamera.ResizeMode_Global = Settings.Current.WebCamResize ? VirtualCamera.EResizeMode.LinearResize : VirtualCamera.EResizeMode.Disabled;
        }

        private void TakePhoto(int width, bool transparentBackground, string directory = null)
        {
            Debug.Log($"Resolution:{(int)Screen.currentResolution.width}x{(int)Screen.currentResolution.height}");
            var res = new Resolution { width = width, height = (int)((double)width / (double)Screen.currentResolution.width * (double)Screen.currentResolution.height) };
            if (string.IsNullOrWhiteSpace(directory)) directory = Application.dataPath + "/../Photos";
            if (Directory.Exists(directory) == false)
            {
                Directory.CreateDirectory(directory);
            }
            var filename = $"VirtualMotionCapture_{res.width}x{res.height}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss.fff}.png";
            if (transparentBackground) controlWPFWindow.BackgroundRenderer.gameObject.SetActive(false);
            StartCoroutine(Photo.TakePNGPhoto(ControlCamera, res, transparentBackground, bytes =>
            {
                File.WriteAllBytes(Path.Combine(directory, filename), bytes);
                if (transparentBackground) controlWPFWindow.BackgroundRenderer.gameObject.SetActive(true);
            }));
            Debug.Log($"Save Photo: {filename}");
        }

        private void ApplySettings(GameObject gameObject)
        {
            SetCameraFOV(Settings.Current.CameraFOV);
            SetCameraSmooth(Settings.Current.CameraSmooth);
            FreeCamera.GetComponent<CameraMouseControl>()?.CheckUpdate();
            FrontCamera.GetComponent<CameraMouseControl>()?.CheckUpdate();
            BackCamera.GetComponent<CameraMouseControl>()?.CheckUpdate();
            PositionFixedCamera.GetComponent<CameraMouseControl>()?.CheckUpdate();


            if (Settings.Current.FreeCameraTransform != null)
            {
                Settings.Current.FreeCameraTransform.ToLocalTransform(FreeCamera.transform);
                var control = FreeCamera.GetComponent<CameraMouseControl>();
                control.CameraAngle = -FreeCamera.transform.rotation.eulerAngles;
                control.CameraDistance = Vector3.Distance(FreeCamera.transform.position, Vector3.zero);
                control.CameraTarget = FreeCamera.transform.position + FreeCamera.transform.rotation * Vector3.forward * control.CameraDistance;
            }
            if (Settings.Current.FrontCameraLookTargetSettings != null)
            {
                Settings.Current.FrontCameraLookTargetSettings.ApplyTo(FrontCamera);
            }
            if (Settings.Current.BackCameraLookTargetSettings != null)
            {
                Settings.Current.BackCameraLookTargetSettings.ApplyTo(BackCamera);
            }
            if (Settings.Current.PositionFixedCameraTransform != null)
            {
                Settings.Current.PositionFixedCameraTransform.ToLocalTransform(PositionFixedCamera.transform);
                var control = PositionFixedCamera.GetComponent<CameraMouseControl>();
                control.CameraAngle = -PositionFixedCamera.transform.rotation.eulerAngles;
                control.CameraDistance = Vector3.Distance(PositionFixedCamera.transform.position, Vector3.zero);
                control.CameraTarget = PositionFixedCamera.transform.position + PositionFixedCamera.transform.rotation * Vector3.forward * control.CameraDistance;
                control.UpdateRelativePosition();
            }

            if (Settings.Current.CameraType.HasValue)
            {
                ChangeCamera(Settings.Current.CameraType.Value);
            }

            UpdateWebCamConfig();

            SetCameraMirrorEnable(Settings.Current.CameraMirrorEnable);
        }
    }
}