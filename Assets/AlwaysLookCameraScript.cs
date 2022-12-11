//gpsnmeajp
using System.Collections.Generic;
using UnityEngine;
using VMC;
using VRM;

public class AlwaysLookCameraScript : MonoBehaviour
{
    //固定視線制御モード
    public enum FixedGaze
    {
        Off, //視線を正面に戻し、以後制御しない
        Camera, //視線をカメラ目線に固定する
        Ahead, //視線をアバターの前方に固定する(顔仰角に依存しない)
        Front, //視線を顔の正面に固定する(顔仰角に依存する)
    };

    // --- 制御モード入力 ---

    public FixedGaze gaze = FixedGaze.Off;    //固定視線制御モード(手動カメラ目線・正面・前方・オフ)
    public bool fluctuationEnable = false; //目の揺れ制御
    public bool autoLookCameraEnable = false; //自動カメラ目線

    // --- 内部パラメータ ---
    const float limitterAngle = 60f; //角度リミッター
    bool isAppliedLastFrame = false; //前Frameで制御適用中か

    // --- 連携変数 ---
    FaceController faceController = null; //VMC表情制御ミキサー
    Camera currentCamera = null; //現在選択中のカメラ(VMCEventsで更新)

    GameObject currentModel = null; //現在読み込み済みのモデル(VMCEventsで更新)
    VRMLookAtHead vrmLookAtHead = null; //現在のモデルに割り当たっているVRM視線制御器
    Transform headTransform = null; //現在のモデルの頭部位置

    System.Action beforeFaceApply; //VMC表情制御ミキサーに登録する表情制御前処理用Action
    bool isSetFaceApplyAction = false; //VMC表情制御ミキサーにActionを登録しているかどうか

    // --- 制御用GameObject ---
    GameObject lookTargetALC; //視線制御ターゲット(注意: モデルの子にしてはならない)
    GameObject modelHeadBaseALC; //HeadのY軸回転と位置のみを反映する参照用オブジェクト (注意: モデルの子にしてはならない)

    // ----------------

    //ショートカットキーによる固定視線制御
    public void ManualOperation(FixedGaze lookCamera)
    {
        gaze = lookCamera;
        Debug.Log("ManualOperation:" + gaze);
    }

    //グローバル設定の反映
    public void Apply(Settings settings)
    {
        autoLookCameraEnable = settings.AutoLookCameraEnable;
        fluctuationEnable = settings.FluctuationEnable;

        Debug.Log("Apply:" + fluctuationEnable + "/" + autoLookCameraEnable);
    }

    //初期化
    void Start()
    {
        //VMC表情制御ミキサーを取得(必ず取れる)
        faceController = GameObject.Find("AnimationController").GetComponent<FaceController>();

        //視線制御ターゲットを作成
        lookTargetALC = new GameObject();
        lookTargetALC.transform.parent = null;
        lookTargetALC.name = "lookTargetALC";

        //モデル前方参照用オブジェクトを作成(HeadのY軸回転と位置のみを反映する参照用オブジェクト)
        modelHeadBaseALC = new GameObject();
        modelHeadBaseALC.transform.parent = null;
        modelHeadBaseALC.name = "modelHeadBaseALC";

        //モデル更新イベントが発生した
        VMCEvents.OnModelLoaded += (GameObject CurrentModel) =>
        {
            //設定済みの視線制御Actionがある場合、解除する
            unsetFaceApplyAction();

            //頭部位置を無効化
            headTransform = null;

            //モデルを更新(nullの可能性がある)
            currentModel = CurrentModel;

            //有効なモデルが読み込まれていなければ処理しない
            if (currentModel == null) {
                return;
            }

            //VRM視線制御器を取得(nullの可能性がある)
            vrmLookAtHead = currentModel.GetComponent<VRMLookAtHead>();
            
            //有効なボーン情報があれば、頭部位置を取得(nullの可能性がある)
            if (currentModel.TryGetComponent<Animator>(out var animator))
            {
                headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
            }
        };

        //選択中のカメラが変更された
        VMCEvents.OnCameraChanged += (Camera currentCamera) =>
        {
            //現在のカメラを更新
            this.currentCamera = currentCamera;
        };

        //表情制御前処理
        beforeFaceApply = () =>
        {
            if (vrmLookAtHead != null)
            {
                //表情制御前にターゲットを設定し、ワールド座標に視線を向ける
                vrmLookAtHead.Target = lookTargetALC.transform;
                vrmLookAtHead.LookWorldPosition();

                //ターゲット設定を解除する
                vrmLookAtHead.Target = null;
            }
        };
    }

    //毎フレーム処理
    void Update()
    {
        //カメラが無効, モデルが無効, VRM視線制御器が無効, 頭部位置が無効のいずれかを満たす場合は処理しない
        if (currentCamera == null || currentModel == null || vrmLookAtHead == null ||  headTransform == null)
        {
            return;
        }


        // --- 常に必要な値の計算(単純合成不可) --
        //手動カメラ目線算出(Target: 絶対位置)
        Vector3 manualCameraTarget = getManualCameraTarget();

        //自動カメラ目線算出(Target: 絶対位置, 非制御状態ではZeroを返す)
        Vector3 autoCameraTarget = Vector3.zero;
        if (autoLookCameraEnable)
        {
            autoCameraTarget = calcAutoCameraTarget(manualCameraTarget);
        }

        //顔正面算出(Target: 絶対位置, 絶対にZeroにはならない)
        Vector3 frontTarget = calcFrontTarget();

        //前方算出(Target: 絶対位置, 絶対にZeroにはならない)
        Vector3 aheadTarget = calcAheadTarget();

        //意図的な視線制御合成
        Vector3 controlledEyeTarget = Vector3.zero;
        switch (gaze)
        {
            case FixedGaze.Camera:
                controlledEyeTarget = manualCameraTarget;
                break;
            case FixedGaze.Ahead:
                controlledEyeTarget = aheadTarget;
                break;
            case FixedGaze.Front:
                controlledEyeTarget = frontTarget;
                break;
            case FixedGaze.Off:
                controlledEyeTarget = Vector3.zero; //追従オフ
                break;
            default:
                Debug.LogError("gaze error!");
                break;
        }

        //自動カメラが視線向き状態になっている場合は、絶対位置を上書きする
        if (autoCameraTarget != Vector3.zero)
        {
            //顔とターゲットを結ぶベクトルと、顔の正面ベクトルの間の角度を算出
            float HeadForwardToAutoCameraTargetAngle = Vector3.Angle(headTransform.forward, (autoCameraTarget - headTransform.position).normalized);

            //可視範囲(+-60 deg)の範囲にある場合のみ追従する
            //この判定がないと、後段の安全装置で正面を向いてしまうため、角度オーバー時はそもそもカメラを向かないようにする
            if (HeadForwardToAutoCameraTargetAngle <= 60f)
            {
                controlledEyeTarget = autoCameraTarget;
            }
        }

        //顔とターゲットを結ぶベクトルと、顔の正面ベクトルの間の角度を算出
        float HeadForwardToTargetAngle = Vector3.Angle(headTransform.forward, (controlledEyeTarget - headTransform.position).normalized);

        //可視範囲(+-60 deg)の範囲にある場合のみ追従する(ギョロ目防止安全装置)
        if (HeadForwardToTargetAngle > 60f)
        {
            controlledEyeTarget = Vector3.zero; //追従オフ
        }

        //視線がZeroの場合は、追従不能なので絶対位置を顔正面にする(Zeroのまま流出すると目が異常になるので注意)
        if (controlledEyeTarget == Vector3.zero) {
            controlledEyeTarget = frontTarget;
        }

        //頭からターゲットの距離を計算(これが微動や揺れの大きさに影響する)
        float targetDistance = Vector3.Distance(headTransform.position, controlledEyeTarget);

        // --- 有効無効が分かれる値の計算(単純合成可能) ---
        //固視微動算出(EyeMovements: 相対位置)
        Vector3 fixational = Vector3.zero;
        if (fluctuationEnable)
        {
            fixational = calcFixationalEyeMovements(targetDistance);
        }

        //視線揺れ算出(EyeMovements: 相対位置)
        Vector3 saccade = Vector3.zero;
        if (fluctuationEnable)
        {
            saccade = calcSaccadeEyeMovements(targetDistance);
        }

        //ランダムな視線制御合成(相対位置)
        Vector3 randomEyeMove = fixational + saccade;

        //顔とランダム視線適用済みターゲットを結ぶベクトルと、顔の正面ベクトルの間の角度を算出
        float HeadForwardToTargetWithRandomEyeMoveAngle = Vector3.Angle(headTransform.forward, ((controlledEyeTarget + randomEyeMove) - headTransform.position).normalized);

        //可視範囲(+-60 deg)の範囲にある場合のみ加算する(ギョロ目防止安全装置)
        if (HeadForwardToTargetWithRandomEyeMoveAngle > 60f)
        {
            randomEyeMove = Vector3.zero; //加算オフ
        }

        // --- 視線制御に反映 ---
        //最終的な視線ターゲット(絶対位置)
        Vector3 target = controlledEyeTarget + randomEyeMove; //(絶対位置) + (相対位置)

        //今のフレームで視線制御が行われているかどうかの判定を更新する(正面戻しに使用する)
        if (randomEyeMove != Vector3.zero || gaze != FixedGaze.Off)
        {
            //視線制御は実施中
            isAppliedLastFrame = true;

            //ターゲットオブジェクトに反映
            lookTargetALC.transform.position = target;

            //視線反映設定
            setFaceApplyAction();
        }
        else {
            //視線制御が行われなくなった & 前フレームまで行われていた場合
            if (isAppliedLastFrame)
            {
                //視線を正面に戻すため、1フレームだけ正面を設定する
                target = frontTarget;

                //ターゲットオブジェクトに反映
                lookTargetALC.transform.position = target;

                //視線反映設定
                setFaceApplyAction();

                Debug.Log("Turn off gaze control...");
            }
            else {
                //視線反映解除
                unsetFaceApplyAction();
            }

            //視線制御は実施されていない
            isAppliedLastFrame = false;
        }
    }

    //手動カメラ目線取得
    Vector3 getManualCameraTarget()
    {
        return currentCamera.transform.position;
    }

    //顔正面算出
    Vector3 calcFrontTarget()
    {
        //顔の正面3m先を視点ターゲットとする
        return headTransform.position + (headTransform.forward * 3f);
    }

    //前方算出
    Vector3 calcAheadTarget()
    {
        //角度ピッチを無視した頭位置を生成
        modelHeadBaseALC.transform.position = headTransform.position;
        modelHeadBaseALC.transform.rotation = Quaternion.Euler(0,headTransform.rotation.eulerAngles.y, 0);

        //そこから3m先の視点をターゲットとする
        return modelHeadBaseALC.transform.position + (modelHeadBaseALC.transform.forward * 3f);
    }

    //固視微動算出
    const float fixationalFactor = 0.003f; //移動の大きさ
    const float fixationalPeriod = 0.02f; //微動の周期

    float fixationalTime = 0; //時間カウンタ
    Vector3 fixationalEyeMovement = Vector3.zero; //位置
    Vector3 calcFixationalEyeMovements(float distance) {
        if (fixationalTime > fixationalPeriod)
        {
            fixationalEyeMovement = new Vector3(UnityEngine.Random.Range(-fixationalFactor, fixationalFactor), UnityEngine.Random.Range(-fixationalFactor, fixationalFactor), UnityEngine.Random.Range(-fixationalFactor, fixationalFactor)) * distance;
            fixationalTime = UnityEngine.Random.Range(-fixationalPeriod, 0f); //最大2倍の時間の間でランダム
        }
        fixationalTime += Time.deltaTime;
        return fixationalEyeMovement;
    }

    //視線揺れ算出
    const float saccadeFactor = 0.04f; //移動の大きさ
    const float saccadePeriod = 1.4f; //微動の周期
    const float saccadeLength = 0.6f; //継続時間

    float saccadeTime = 0; //時間カウンタ
    bool saccadeFlag = false; //移動制御用フラグ
    Vector3 saccadeEyeMovement = Vector3.zero; //位置
    Vector3 calcSaccadeEyeMovements(float distance) {
        
        if (saccadeTime > (saccadePeriod + saccadeLength))
        {
            saccadeTime = UnityEngine.Random.Range(-saccadePeriod, 0f); //最大2倍の時間の間でランダム
            saccadeEyeMovement = Vector3.zero; //視線戻す
            saccadeFlag = false;
        }
        else if (saccadeTime > saccadePeriod && saccadeFlag == false)
        {
            saccadeEyeMovement = new Vector3(UnityEngine.Random.Range(-saccadeFactor, saccadeFactor), UnityEngine.Random.Range(-saccadeFactor, saccadeFactor), UnityEngine.Random.Range(-saccadeFactor, saccadeFactor)) * distance;
            saccadeFlag = true;
        }
        saccadeTime += Time.deltaTime;
        return saccadeEyeMovement;
    }

    //自動カメラ目線算出
    const float autoCameraPeriod = 15.16f; //移動周期
    const float autoCameraLength = 4.3f;//継続時間

    bool autoCameraFlag = false; //移動制御用フラグ
    float autoCameraTime = 0;//時間カウンタ
    Vector3 autoCameraTarget = Vector3.zero;
    Vector3 calcAutoCameraTarget(Vector3 manualCameraTarget)
    {
        if (autoCameraTime > (autoCameraPeriod + autoCameraLength))
        {
            autoCameraTime = UnityEngine.Random.Range(-autoCameraPeriod, 0f); //最大2倍の時間の間でランダム
            autoCameraTarget = Vector3.zero; //視線戻す
            autoCameraFlag = false;
        }
        else if (autoCameraTime > autoCameraPeriod && autoCameraFlag == false)
        {
            autoCameraTarget = manualCameraTarget;
            autoCameraFlag = true;
        }
        autoCameraTime += Time.deltaTime;
        return autoCameraTarget;
    }

    //VMC表情制御ミキサーに視線制御Actionを設定する
    void setFaceApplyAction()
    {
        //設定済みの場合は処理しない
        if (isSetFaceApplyAction == true)
        {
            return;
        }
        faceController.BeforeApply += beforeFaceApply;
        isSetFaceApplyAction = true;
    }

    //VMC表情制御ミキサーに設定済みの視線制御Actionを解除する
    void unsetFaceApplyAction()
    {
        //解除済みの場合は処理しない
        if (isSetFaceApplyAction == false)
        {
            return;
        }
        faceController.BeforeApply -= beforeFaceApply;
        isSetFaceApplyAction = false;
    }
}
