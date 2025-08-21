using RootMotion.FinalIK;
using System;
using UnityEngine;
using UnityMemoryMappedFile;

namespace VMC
{
    public class WristRotationFix : MonoBehaviour
    {

        public VRIK ik;

        // ControlWPFWindowの参照を保持
        [SerializeField]
        private ControlWPFWindow controlWPFWindow;

        private ArmFixItem LeftArmFixItem;
        private ArmFixItem RightArmFixItem;
        // VRIKで元から回ってる分はキャンセルされるのでこの割合の通りに回転される
        public float UpperArmWeight = 0.2f; // 20%
        public float ForearmWeight = 0.57f;  // 57%

        private Guid? eventId = null;

        [Header("Twist Limits")]
        [Tooltip("累積回転がこの角度を超えたら連続性をリセット")]
        [Range(180f, 720f)]
        public float maxAccumulatedTwist = 300f;

        private System.Threading.SynchronizationContext context = null;

        void Start()
        {
            context = System.Threading.SynchronizationContext.Current;
            
            // ControlWPFWindowの参照取得（SerializeFieldで設定されていない場合のみ）
            if (controlWPFWindow == null)
            {
                controlWPFWindow = GameObject.Find("ControlWPFWindow")?.GetComponent<ControlWPFWindow>();
            }
            
            // 設定からパラメータを読み込み
            LoadSettingsValues();
            
            // UIとの通信を設定
            if (controlWPFWindow != null)
            {
                controlWPFWindow.server.ReceivedEvent += Server_Received;
                // AdditionalSettingActionに登録して設定読み込み時に自動実行されるようにする
                controlWPFWindow.AdditionalSettingAction += ApplySettings;
            }
        }

        void OnDestroy()
        {
            if (eventId != null) IKManager.Instance.RemoveOnPostUpdate(eventId.Value);
            
            // 通信ハンドラの登録解除
            if (controlWPFWindow != null)
            {
                controlWPFWindow.server.ReceivedEvent -= Server_Received;
                controlWPFWindow.AdditionalSettingAction -= ApplySettings;
            }
        }

        // 設定読み込み時に呼ばれるメソッド（IKManager.SetHandFreeOffsetと同様）
        private void ApplySettings(GameObject gameObject)
        {
            LoadSettingsValues();
        }

        private void Server_Received(object sender, DataReceivedEventArgs e)
        {
            context.Post(async s =>
            {
                if (e.CommandType == typeof(PipeCommands.GetWristRotationFixSetting))
                {
                    if (controlWPFWindow != null)
                    {
                        await controlWPFWindow.server.SendCommandAsync(new PipeCommands.SetWristRotationFixSetting
                        {
                            UpperArmWeight = Settings.Current.WristRotationFix_UpperArmWeight,
                            ForearmWeight = Settings.Current.WristRotationFix_ForearmWeight,
                            MaxAccumulatedTwist = Settings.Current.WristRotationFix_MaxAccumulatedTwist,
                        }, e.RequestId);
                    }
                }
                else if (e.CommandType == typeof(PipeCommands.SetWristRotationFixSetting))
                {
                    var d = (PipeCommands.SetWristRotationFixSetting)e.Data;
                    
                    // 設定を保存
                    SaveSettingsValues(d);
                }
            }, null);
        }

        private void LoadSettingsValues()
        {
            if (Settings.Current != null)
            {
                UpperArmWeight = Settings.Current.WristRotationFix_UpperArmWeight / 1000f;
                ForearmWeight = Settings.Current.WristRotationFix_ForearmWeight / 1000f;
                maxAccumulatedTwist = Settings.Current.WristRotationFix_MaxAccumulatedTwist;
            }
        }

        private void SaveSettingsValues(PipeCommands.SetWristRotationFixSetting d)
        {
            UpperArmWeight = d.UpperArmWeight / 1000f;
            ForearmWeight = d.ForearmWeight / 1000f;
            maxAccumulatedTwist = d.MaxAccumulatedTwist;
            if (Settings.Current != null)
            {
                Settings.Current.WristRotationFix_UpperArmWeight = d.UpperArmWeight;
                Settings.Current.WristRotationFix_ForearmWeight = d.ForearmWeight;
                Settings.Current.WristRotationFix_MaxAccumulatedTwist = d.MaxAccumulatedTwist;
            }
        }

        public void SetVRIK(VRIK setIK)
        {
            if (eventId != null) IKManager.Instance.RemoveOnPostUpdate(eventId.Value);
            ik = setIK;

            LeftArmFixItem = new ArmFixItem(ik.references.leftShoulder, ik.references.leftUpperArm, ik.references.leftForearm, ik.references.leftHand);
            RightArmFixItem = new ArmFixItem(ik.references.rightShoulder, ik.references.rightUpperArm, ik.references.rightForearm, ik.references.rightHand);

            // 設定を再読み込み
            LoadSettingsValues();

            eventId = IKManager.Instance.AddOnPostUpdate(10, OnPostUpdate);
        }

        private void OnPostUpdate()
        {
            if (IKManager.Instance.vrik == null) return;
            if (enabled == false) return;

            ApplyArmTwistFix(LeftArmFixItem);
            ApplyArmTwistFix(RightArmFixItem);
        }
        
        private void ApplyArmTwistFix(ArmFixItem item)
        {
            Quaternion originalHandRotation = item.Hand.rotation;
            Quaternion currentUpperArmRotation = item.UpperArm.rotation;
            Quaternion currentForearmRotation = item.Forearm.rotation;

            // 1. UpperArmとForearmの現在の回転からTwist成分のみを除去し、Swing成分のみを残す
            Quaternion upperArmSwingOnly = RemoveTwistFromRotation(currentUpperArmRotation, item.InitialUpperArmRotation, item.UpperArmTwistAxis);
            Quaternion forearmSwingOnly = RemoveTwistFromRotation(currentForearmRotation, item.InitialForearmRotation, item.ForearmTwistAxis);

            // 2. Swing成分のみの回転を適用（IKの結果を保持）
            item.UpperArm.rotation = upperArmSwingOnly;
            item.Forearm.rotation = forearmSwingOnly;

            // 3. HandのForearmに対するローカル回転からTwist角度を計算
            float twistAngle = CalculateHandTwistAngle(item, forearmSwingOnly, originalHandRotation);

            // 4. 連続性を保つための補正
            float lastAngle = item.LastTwistAngle;
            float delta = Mathf.DeltaAngle(lastAngle, twistAngle);
            
            // 通常の連続性補正
            twistAngle = lastAngle + delta;
            
            // 5. 初期角度からの累積角度を更新
            item.AccumulatedTwistFromInitial += delta;
            
            // 初期角度から±maxAccumulatedTwist度を超えたらリセット
            if (Mathf.Abs(item.AccumulatedTwistFromInitial) > maxAccumulatedTwist)
            {
                // リセット時の処理：360度逆回転させる
                float resetDirection = Mathf.Sign(item.AccumulatedTwistFromInitial);
                float resetAdjustment = -resetDirection * 360f;
                
                // Twist角度と累積角度を調整
                twistAngle += resetAdjustment;
                item.AccumulatedTwistFromInitial += resetAdjustment;
                
                // twistAngleがmaxAccumulatedTwist範囲を超えた場合に正規化
                if (twistAngle > maxAccumulatedTwist)
                {
                    // 正の範囲を超えた場合：360度引く
                    twistAngle -= 360f;
                    item.AccumulatedTwistFromInitial -= 360f;
                }
                else if (twistAngle < -maxAccumulatedTwist)
                {
                    // 負の範囲を超えた場合：360度足す
                    twistAngle += 360f;
                    item.AccumulatedTwistFromInitial += 360f;
                }
                
                // デバッグ用
                Debug.Log($"Twist angle reset ({(item.IsLeftArm ? "Left" : "Right")} Arm): direction = {resetDirection}, adjustment = {resetAdjustment} degrees, normalized twistAngle = {twistAngle}, new accumulated = {item.AccumulatedTwistFromInitial}");
            }

            item.LastTwistAngle = twistAngle;

            // 6. Twist角度をUpperArmとForearmに分配
            float upperArmTwist = twistAngle * UpperArmWeight;
            float forearmTwist = twistAngle * ForearmWeight;

            // 7. Twist軸を使用してTwist回転を適用
            Vector3 upperArmTwistAxis = item.UpperArm.TransformDirection(item.UpperArmTwistAxis);
            Vector3 forearmTwistAxis = item.Forearm.TransformDirection(item.ForearmTwistAxis);

            // 8. Swing成分にTwist回転を加算
            item.UpperArm.rotation = Quaternion.AngleAxis(upperArmTwist, upperArmTwistAxis) * upperArmSwingOnly;
            item.Forearm.rotation = Quaternion.AngleAxis(forearmTwist, forearmTwistAxis) * forearmSwingOnly;
            item.Hand.rotation = originalHandRotation;
        }

        // 回転からTwist成分を除去し、Swing成分のみを残す
        private Quaternion RemoveTwistFromRotation(Quaternion currentRotation, Quaternion initialRotation, Vector3 twistAxis)
        {
            // 初期回転からの相対回転を取得
            Quaternion relativeRotation = currentRotation * Quaternion.Inverse(initialRotation);
            
            // Swing-Twist分解でSwing成分のみを抽出
            Quaternion swing, twist;
            SwingTwistDecomposition(relativeRotation, twistAxis, out swing, out twist);
            
            // Swing成分のみを初期回転に適用
            return swing * initialRotation;
        }

        // HandのTwist角度を計算（Swing-Twist分解を使用）
        private float CalculateHandTwistAngle(ArmFixItem item, Quaternion forearmRotation, Quaternion handRotation)
        {
            // HandのForearmに対するローカル回転
            Quaternion handLocal = Quaternion.Inverse(forearmRotation) * handRotation;

            // 初期状態との差分
            Quaternion deltaRotation = handLocal * Quaternion.Inverse(item.NeutralHandRotation);

            // Swing-Twist分解でTwist成分のみを抽出
            Vector3 twistAxis = Vector3.right; // ForearmのローカルX軸（Twist軸）
            Quaternion swing, twist;
            SwingTwistDecomposition(deltaRotation, twistAxis, out swing, out twist);

            // Twist角度を取得
            float angle;
            Vector3 axis;
            twist.ToAngleAxis(out angle, out axis);

            // 符号を正しく設定
            if (Vector3.Dot(axis, twistAxis) < 0)
                angle = -angle;

            // -180～180度の範囲に正規化
            return Mathf.DeltaAngle(0, angle);
        }

        // Swing-Twist分解（改善版）
        private void SwingTwistDecomposition(Quaternion rotation, Vector3 twistAxis, out Quaternion swing, out Quaternion twist)
        {
            twistAxis.Normalize();

            // 回転軸を取得
            Vector3 r = new Vector3(rotation.x, rotation.y, rotation.z);

            // Twist軸への投影
            float dot = Vector3.Dot(r, twistAxis);
            Vector3 twistPart = dot * twistAxis;

            // Twist成分のクォータニオン
            twist = new Quaternion(twistPart.x, twistPart.y, twistPart.z, rotation.w);

            // 長さが0に近い場合は単位クォータニオンに
            if (twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w < 0.01f)
            {
                twist = Quaternion.identity;
            }
            else
            {
                twist = Quaternion.Normalize(twist);
            }

            // Swing成分
            swing = rotation * Quaternion.Inverse(twist);
        }

        public class ArmFixItem
        {
            public Transform Shoulder;
            public Transform UpperArm;
            public Transform Forearm;
            public Transform Hand;

            // Twist軸（ローカル空間）
            public Vector3 UpperArmTwistAxis;
            public Vector3 ForearmTwistAxis;

            // 前回のTwist角度
            public float LastTwistAngle = 0f;
            
            // 初期角度からの累積Twist角度
            public float AccumulatedTwistFromInitial = 0f;

            // 初期状態のHandの回転（T-Pose時など）
            public Quaternion NeutralHandRotation;
            
            // T-ポーズ時の初期回転を保存
            public Quaternion InitialUpperArmRotation;
            public Quaternion InitialForearmRotation;

            public ArmFixItem(Transform shoulder, Transform upperArm, Transform forearm, Transform hand)
            {
                Shoulder = shoulder;
                UpperArm = upperArm;
                Forearm = forearm;
                Hand = hand;

                // UpperArmのTwist軸（Forearm→UpperArm方向をUpperArmローカル空間で）
                UpperArmTwistAxis = upperArm.InverseTransformDirection(forearm.position - upperArm.position).normalized;
                // ForearmのTwist軸（Hand→Forearm方向をForearmローカル空間で）
                ForearmTwistAxis = forearm.InverseTransformDirection(hand.position - forearm.position).normalized;

                // 初期状態のHandの回転を保存
                NeutralHandRotation = Quaternion.Inverse(forearm.rotation) * hand.rotation;
                
                // T-ポーズ時の初期回転を保存
                InitialUpperArmRotation = upperArm.rotation;
                InitialForearmRotation = forearm.rotation;
            }
        }
    }
}