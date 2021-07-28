using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityMemoryMappedFile;
using static VMC.ControlWPFWindow;


namespace VMC
{
    public class CameraMouseControl : MonoBehaviour
    {
        public static CameraMouseControl Current;

        public Camera TargetCamera;

        public Vector3 cameraSpeed = new Vector3(0.2f, 0.2f, 1.0f);
        public Vector3 CameraTarget = new Vector3(0.0f, 1.0f, 0.0f);
        public Vector3 CameraAngle = new Vector3(-30.0f, -150.0f, 0.0f);
        public float CameraDistance = 1.5f;

        public Transform LookTarget = null;
        public Vector3 LookOffset = new Vector3(0, 0.05f, 0);

        public Transform PositionFixedTarget = null;
        public Vector3 RelativePosition = new Vector3(0, 0, -1f);
        private bool doUpdateRelativePosition = false;

        private Vector3 lastMousePosition;

        private Transform parentTransform;

        private Vector3 currentNoScaledPosition = Vector3.zero;

        private void OnEnable()
        {
            Current = this;
        }

        void Start()
        {
            UpdateCamera();
            if (transform.parent != null)
            {
                parentTransform = transform.parent;
            }
        }

        private bool isTargetRotate = false;

        void Update()
        {
            CheckUpdate();
        }
        public void CheckUpdate()
        {
            var mousePosition = Input.mousePosition;
            bool settingChanged = false;
            if (LookTarget == null)
            {
                // 注視点を中心に回転
                if (Input.GetMouseButtonDown((int)MouseButtons.Left) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
                {
                    isTargetRotate = true;
                }

                if (Input.GetMouseButton((int)MouseButtons.Left) && isTargetRotate)
                {
                    Vector3 dragOffset = mousePosition - lastMousePosition;
                    CameraAngle.x = (CameraAngle.x + dragOffset.y * cameraSpeed.x) % 360.0f;
                    CameraAngle.y = (CameraAngle.y - dragOffset.x * cameraSpeed.y) % 360.0f;
                    settingChanged = true;
                }

                if (Input.GetMouseButtonUp((int)MouseButtons.Left) && isTargetRotate)
                {
                    isTargetRotate = false;
                }

                // カメラ回転
                if (Input.GetMouseButton((int)MouseButtons.Right))
                {
                    Vector3 dragOffset = mousePosition - lastMousePosition;
                    if (Input.GetMouseButtonDown((int)MouseButtons.Right) == false)
                    {
                        CameraAngle.x = (CameraAngle.x + dragOffset.y * cameraSpeed.x * (currentFOV / 60.0f)) % 360.0f;
                        CameraAngle.y = (CameraAngle.y - dragOffset.x * cameraSpeed.y * (currentFOV / 60.0f)) % 360.0f;
                        var setPosition = transform.position;
                        //TODO:元の座標を取っておいて計算しないと計算誤差で微妙にずれる
                        setPosition = new Vector3((setPosition.x - parentTransform.position.x) / parentTransform.localScale.x, (setPosition.y - parentTransform.position.y) / parentTransform.localScale.y, (setPosition.z - parentTransform.position.z) / parentTransform.localScale.z);
                        CameraTarget = setPosition + Quaternion.Euler(-CameraAngle) * Vector3.forward * CameraDistance;
                        if (PositionFixedTarget != null) // 座標追従カメラ
                        {
                            UpdateRelativePosition();
                        }
                        settingChanged = true;
                    }
                }

            }

            // カメラ移動
            if (Input.GetMouseButton((int)MouseButtons.Center))
            {
                Vector3 mousePositionInWorld = TargetCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, CameraDistance));
                Vector3 lastMousePositionInWorld = TargetCamera.ScreenToWorldPoint(new Vector3(lastMousePosition.x, lastMousePosition.y, CameraDistance));
                Vector3 dragOffset = mousePositionInWorld - lastMousePositionInWorld;

                if (Input.GetMouseButtonDown((int)MouseButtons.Center) == false)
                {
                    if (LookTarget != null) // フロント/バックカメラ
                    {
                        dragOffset.Set(0, dragOffset.y, 0);
                        LookOffset -= dragOffset;
                    }
                    else if (PositionFixedTarget != null) // 座標追従カメラ
                    {
                        CameraTarget -= dragOffset;
                        UpdateRelativePosition();
                    }
                    else // フリーカメラ
                    {
                        CameraTarget -= dragOffset;
                    }
                    settingChanged = true;
                }
            }


            lastMousePosition = mousePosition;

            // ズーム
            if (NativeMethods.IsWindowActive())
            {
                float mouseScrollWheel = Input.GetAxis("Mouse ScrollWheel");
                if (mouseScrollWheel != 0.0f)
                {
                    var mousePos = mousePosition;
                    if (mousePos.x >= 0 && mousePos.y >= 0 && mousePos.x < Screen.safeArea.width && mousePos.y < Screen.safeArea.height)
                    {
                        CameraDistance = Mathf.Max(CameraDistance - mouseScrollWheel * cameraSpeed.z * (60.0f / currentFOV) * ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) ? 0.05f : 1f), 0.1f);
                        settingChanged = true;
                    }
                }
            }

            if (changeFOV)
            {
                changeFOV = false;
                var normalPos = FovToPos(newFOV);
                var currentNormalPos = FovToPos(currentFOV);
                var ratio = CameraDistance / currentNormalPos;
                CameraDistance = normalPos * ratio;
                currentFOV = newFOV;
            }

            UpdateCamera();

            if (settingChanged)
            {
                if (LookTarget != null)
                {
                    SaveLookTarget();
                }
                if (CurrentSettings.CameraType == CameraTypes.Free)
                {
                    CurrentSettings.FreeCameraTransform.SetPosition(currentNoScaledPosition);
                    CurrentSettings.FreeCameraTransform.SetRotation(transform);
                }
                else if (CurrentSettings.CameraType == CameraTypes.PositionFixed)
                {
                    CurrentSettings.PositionFixedCameraTransform.SetPositionAndRotation(transform);
                }
            }
        }

        public void UpdateRelativePosition()
        {
            doUpdateRelativePosition = true;
        }

        void UpdateCamera()
        {
            if (doUpdateRelativePosition && PositionFixedTarget != null)
            {
                doUpdateRelativePosition = false;
                RelativePosition = CameraTarget - PositionFixedTarget.position;
            }
            Vector3 setPosition;
            if (LookTarget != null)
            {

                var lookAt = LookTarget.position + LookOffset;

                // カメラとプレイヤーとの間の距離を調整
                setPosition = lookAt - (LookTarget.transform.forward) * (CurrentSettings.CameraType == CameraTypes.Front ? -CameraDistance : CameraDistance);

                transform.position = setPosition;
                // 注視点の設定
                transform.LookAt(lookAt);
            }
            else if (PositionFixedTarget != null)
            {
                transform.rotation = Quaternion.Euler(-CameraAngle);
                setPosition = PositionFixedTarget.position + transform.rotation * Vector3.back * CameraDistance + RelativePosition;
            }
            else
            {
                transform.rotation = Quaternion.Euler(-CameraAngle);
                setPosition = CameraTarget + transform.rotation * Vector3.back * CameraDistance;
            }
            currentNoScaledPosition = setPosition;
            if (parentTransform != null)
            {
                setPosition = new Vector3(setPosition.x * parentTransform.localScale.x + parentTransform.position.x, setPosition.y * parentTransform.localScale.y + parentTransform.position.y, setPosition.z * parentTransform.localScale.z + parentTransform.position.z);
            }
            transform.position = setPosition;
        }

        private void SaveLookTarget()
        {
            if (CurrentSettings.CameraType == CameraTypes.Front)
            {
                if (CurrentSettings.FrontCameraLookTargetSettings == null)
                {
                    CurrentSettings.FrontCameraLookTargetSettings = LookTargetSettings.Create(this);
                }
                else
                {
                    CurrentSettings.FrontCameraLookTargetSettings.Set(this);
                }
            }
            else if (CurrentSettings.CameraType == CameraTypes.Back)
            {
                if (CurrentSettings.BackCameraLookTargetSettings == null)
                {
                    CurrentSettings.BackCameraLookTargetSettings = LookTargetSettings.Create(this);
                }
                else
                {
                    CurrentSettings.BackCameraLookTargetSettings.Set(this);
                }
            }
        }

        private float FovToPos(float fov)
        {
            return (1 / Mathf.Tan(fov * Mathf.Deg2Rad / 2.0f)) / 1.732051f;
        }

        private float currentFOV = 60.0f;
        private float newFOV = 60.0f;
        private bool changeFOV = false;

        public void SetCameraFOV(float fov)
        {
            newFOV = fov;
            changeFOV = true;
        }
    }
}