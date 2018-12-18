using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityNamedPipe;
using static ControlWPFWindow;

public class CameraMouseControl : MonoBehaviour
{
    /*

    private Vector3 cameraMouseOldPos; // マウスの位置を保存する変数


    // マウス関係のイベント
    private void CameraMouseEvent()
    {
        var mousePos = Input.mousePosition;
        //Debug.Log(mousePos.ToString() + " " + Screen.safeArea.ToString());
        //SetUnityWindowTitle(mousePos.ToString() + " " + Screen.safeArea.ToString());
        if (mousePos.x >= 0 && mousePos.y >= 0 && mousePos.x < Screen.safeArea.width && mousePos.y < Screen.safeArea.height)
        {
            float delta = Input.GetAxis("Mouse ScrollWheel");
            if (delta != 0.0f)
            {
                if (CurrentSettings.CameraType == CameraTypes.Free) //フリーカメラ
                {
                    transform.position += transform.forward * delta;
                    if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(transform);
                    CurrentSettings.FreeCameraTransform.SetPosition(transform);
                }
                else if (CurrentSettings.CameraType == CameraTypes.PositionFixed)
                {
                    transform.position += transform.forward * delta;
                    if (CurrentSettings.PositionFixedCameraTransform == null) CurrentSettings.PositionFixedCameraTransform = new StoreTransform(transform);
                    CurrentSettings.PositionFixedCameraTransform.SetPosition(transform);
                    positionFixedCamera.UpdatePosition();

                }
                else //固定カメラ
                {
                    if (currentCameraLookTarget != null)
                    {
                        currentCameraLookTarget.Distance += delta;
                        SaveLookTarget(currentCamera);
                    }
                }
            }
        }

        // 押されたとき
        if (Input.GetMouseButtonDown((int)MouseButtons.Right) || Input.GetMouseButtonDown((int)MouseButtons.Center))
            cameraMouseOldPos = mousePos;

        Vector3 diff = mousePos - cameraMouseOldPos;
        if (CurrentSettings.CameraMirrorEnable)
        {
            diff.x *= -1;
        }

        // 差分の長さが極小数より小さかったら、ドラッグしていないと判断する
        if (diff.magnitude >= Vector3.kEpsilon)
        {

            if (Input.GetMouseButton((int)MouseButtons.Center))
            { // 注視点
                if (CurrentSettings.CameraType == CameraTypes.Free) //フリーカメラ
                {
                    transform.Translate(-diff * Time.deltaTime * 1.1f);
                    if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(transform);
                    CurrentSettings.FreeCameraTransform.SetPosition(transform);
                }
                else if (CurrentSettings.CameraType == CameraTypes.PositionFixed)
                {
                    transform.Translate(-diff * Time.deltaTime * 1.1f);
                    if (CurrentSettings.PositionFixedCameraTransform == null) CurrentSettings.PositionFixedCameraTransform = new StoreTransform(transform);
                    CurrentSettings.PositionFixedCameraTransform.SetPosition(transform);
                    positionFixedCamera.UpdatePosition();
                }
                else //固定カメラ
                {
                    currentCameraLookTarget.Offset += new Vector3(0, -diff.y, 0) * Time.deltaTime * 1.1f;
                    SaveLookTarget(currentCamera);
                }
            }
            else if (Input.GetMouseButton((int)MouseButtons.Right))
            { // 回転
                if (CurrentSettings.CameraType == CameraTypes.Free)
                {
                    transform.RotateAround(transform.position, transform.right, -diff.y * Time.deltaTime * 30.0f);
                    transform.RotateAround(transform.position, Vector3.up, diff.x * Time.deltaTime * 30.0f);
                    if (CurrentSettings.FreeCameraTransform == null) CurrentSettings.FreeCameraTransform = new StoreTransform(transform);
                    CurrentSettings.FreeCameraTransform.SetRotation(transform);
                }
                else if (CurrentSettings.CameraType == CameraTypes.PositionFixed)
                {
                    transform.RotateAround(transform.position, transform.right, -diff.y * Time.deltaTime * 30.0f);
                    transform.RotateAround(transform.position, Vector3.up, diff.x * Time.deltaTime * 30.0f);
                    if (CurrentSettings.PositionFixedCameraTransform == null) CurrentSettings.PositionFixedCameraTransform = new StoreTransform(transform);
                    CurrentSettings.PositionFixedCameraTransform.SetRotation(transform);
                    positionFixedCamera.UpdatePosition();
                }
            }

            this.cameraMouseOldPos = mousePos;
        }
        return;
    }
    */
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

    private Camera currentCamera;

    void Start()
    {
        currentCamera = GetComponent<Camera>();
        UpdateCamera();
    }

    private bool isTargetRotate = false;

    void Update()
    {
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
                Vector3 dragOffset = Input.mousePosition - lastMousePosition;
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
                Vector3 dragOffset = Input.mousePosition - lastMousePosition;
                if (Input.GetMouseButtonDown((int)MouseButtons.Right) == false)
                {
                    CameraAngle.x = (CameraAngle.x + dragOffset.y * cameraSpeed.x * (currentCamera.fieldOfView / 60.0f)) % 360.0f;
                    CameraAngle.y = (CameraAngle.y - dragOffset.x * cameraSpeed.y * (currentCamera.fieldOfView / 60.0f)) % 360.0f;
                    CameraTarget = transform.position + Quaternion.Euler(-CameraAngle) * Vector3.forward * CameraDistance;
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
            Camera camera = GetComponent<Camera>();
            Vector3 mousePositionInWorld = camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, CameraDistance));
            Vector3 lastMousePositionInWorld = camera.ScreenToWorldPoint(new Vector3(lastMousePosition.x, lastMousePosition.y, CameraDistance));
            Vector3 dragOffset = mousePositionInWorld - lastMousePositionInWorld;

            if (Input.GetMouseButtonDown((int)MouseButtons.Center) == false)
            {
                if (LookTarget != null) // フロント/バックカメラ
                {
                    dragOffset.Set(0, dragOffset.y, dragOffset.z);
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


        lastMousePosition = Input.mousePosition;

        // ズーム
        float mouseScrollWheel = Input.GetAxis("Mouse ScrollWheel");
        if (mouseScrollWheel != 0.0f)
        {
            var mousePos = Input.mousePosition;
            if (mousePos.x >= 0 && mousePos.y >= 0 && mousePos.x < Screen.safeArea.width && mousePos.y < Screen.safeArea.height)
            {
                CameraDistance = Mathf.Max(CameraDistance - mouseScrollWheel * cameraSpeed.z * (60.0f / currentCamera.fieldOfView), 0.1f);
                settingChanged = true;
            }
        }

        if (changeFOV)
        {
            changeFOV = false;
            if (currentCamera == null) currentCamera = GetComponent<Camera>();
            var normalPos = FovToPos(currentFOV);
            var currentNormalPos = FovToPos(currentCamera.fieldOfView);
            var ratio = CameraDistance / currentNormalPos;
            CameraDistance = normalPos * ratio;
            currentCamera.fieldOfView = currentFOV;
        }

        UpdateCamera();

        if (settingChanged)
        {
            if (LookTarget != null)
            {
                SaveLookTarget(currentCamera);
            }
            if (CurrentSettings.CameraType == CameraTypes.Free)
            {
                CurrentSettings.FreeCameraTransform.SetPositionAndRotation(transform);
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
        if (LookTarget != null)
        {

            var lookAt = LookTarget.position + LookOffset;

            // カメラとプレイヤーとの間の距離を調整
            transform.position = lookAt - (LookTarget.transform.forward) * (CurrentSettings.CameraType == CameraTypes.Front ? -CameraDistance : CameraDistance);

            // 注視点の設定
            transform.LookAt(lookAt);
        }
        else if (PositionFixedTarget != null)
        {
            transform.rotation = Quaternion.Euler(-CameraAngle);
            transform.position = PositionFixedTarget.position + transform.rotation * Vector3.back * CameraDistance + RelativePosition;
        }
        else
        {
            transform.rotation = Quaternion.Euler(-CameraAngle);
            transform.position = CameraTarget + transform.rotation * Vector3.back * CameraDistance;
        }
    }

    private void SaveLookTarget(Camera camera)
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
    private bool changeFOV = false;

    public void SetCameraFOV(float fov)
    {
        currentFOV = fov;
        changeFOV = true;
    }
}
