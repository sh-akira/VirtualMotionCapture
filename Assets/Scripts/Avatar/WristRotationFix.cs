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
        // VRIKで元から回ってる分があるので上腕は特に弱くて良い
        public float UpperArmWeight = 0.05f; // 5%
        public float ForearmWeight = 0.45f;  // 45%

        private Guid? eventId = null;

        [Header("Smoothing")]
        [Tooltip("スムージングの強さ。0で無効、値が大きいほど滑らか（遅延も増加）")]
        [Range(0f, 0.2f)]
        public float smoothingTime = 0.05f;

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
                            UpperArmWeight = (int)(UpperArmWeight * 1000),
                            ForearmWeight = (int)(ForearmWeight * 1000),
                            SmoothingTime = (int)(smoothingTime * 1000),
                            MaxAccumulatedTwist = (int)maxAccumulatedTwist,
                        }, e.RequestId);
                    }
                }
                else if (e.CommandType == typeof(PipeCommands.SetWristRotationFixSetting))
                {
                    var d = (PipeCommands.SetWristRotationFixSetting)e.Data;
                    UpperArmWeight = d.UpperArmWeight / 1000f;
                    ForearmWeight = d.ForearmWeight / 1000f;
                    smoothingTime = d.SmoothingTime / 1000f;
                    maxAccumulatedTwist = d.MaxAccumulatedTwist;
                    
                    // 設定を保存
                    SaveSettingsValues();
                }
            }, null);
        }

        private void LoadSettingsValues()
        {
            if (Settings.Current != null)
            {
                UpperArmWeight = Settings.Current.WristRotationFix_UpperArmWeight;
                ForearmWeight = Settings.Current.WristRotationFix_ForearmWeight;
                smoothingTime = Settings.Current.WristRotationFix_SmoothingTime;
                maxAccumulatedTwist = Settings.Current.WristRotationFix_MaxAccumulatedTwist;
            }
        }

        private void SaveSettingsValues()
        {
            if (Settings.Current != null)
            {
                Settings.Current.WristRotationFix_UpperArmWeight = UpperArmWeight;
                Settings.Current.WristRotationFix_ForearmWeight = ForearmWeight;
                Settings.Current.WristRotationFix_SmoothingTime = smoothingTime;
                Settings.Current.WristRotationFix_MaxAccumulatedTwist = maxAccumulatedTwist;
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

            //FixAxis(LeftElbowFixItem);
            //FixAxis(LeftUpperArmFixItem);
            //FixAxis(RightElbowFixItem);
            //FixAxis(RightUpperArmFixItem);
            ApplyArmTwistFix(LeftArmFixItem);
            ApplyArmTwistFix(RightArmFixItem);
        }
        private void ApplyArmTwistFix(ArmFixItem item)
        {
            Quaternion originalHandRotation = item.Hand.rotation;
            Quaternion originalUpperArmRotation = item.UpperArm.rotation;
            Quaternion originalForearmRotation = item.Forearm.rotation;

            // 1. HandのForearmに対するローカル回転からTwist角度を計算
            float twistAngle = CalculateHandTwistAngle(item, originalForearmRotation, originalHandRotation);

            // 2. 連続性を保つための補正
            float lastAngle = item.LastTwistAngle;
            float delta = Mathf.DeltaAngle(lastAngle, twistAngle);
            
            // 通常の連続性補正
            twistAngle = lastAngle + delta;
            
            // 現在のTwist角度が閾値を超えたらリセット
            if (Mathf.Abs(twistAngle) > maxAccumulatedTwist)
            {
                // 360度単位で正規化して連続性を保つ
                float cycles = Mathf.Floor(twistAngle / 360f);
                twistAngle -= cycles * 360f;
                
                // デバッグ用
                Debug.Log($"Twist angle normalized from {lastAngle + delta} to {twistAngle} degrees");
            }

            // 3. スムージング（調整可能）
            if (smoothingTime > 0 && ShouldApplySmoothing(twistAngle, lastAngle))
            {
                twistAngle = SmoothTwistAngle(item.LastTwistAngle, twistAngle, smoothingTime);
            }

            item.LastTwistAngle = twistAngle;

            // 4. Twist角度をUpperArmとForearmに分配
            float upperArmTwist = twistAngle * UpperArmWeight;
            float forearmTwist = twistAngle * ForearmWeight;

            // 5. Twist軸を定義（多くのアバターではX軸）
            Vector3 upperArmTwistAxis = item.UpperArm.right;
            Vector3 forearmTwistAxis = item.Forearm.right;

            // 6. 回転を適用
            item.UpperArm.rotation = Quaternion.AngleAxis(upperArmTwist, upperArmTwistAxis) * originalUpperArmRotation;
            item.Forearm.rotation = Quaternion.AngleAxis(forearmTwist, forearmTwistAxis) * originalForearmRotation;
            item.Hand.rotation = originalHandRotation;
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

        // Twist角度のスムージング（オプション）
        private float SmoothTwistAngle(float currentAngle, float targetAngle, float smoothTime = 0.1f)
        {
            float delta = Mathf.DeltaAngle(currentAngle, targetAngle);
            return currentAngle + delta * Mathf.Min(1f, Time.deltaTime / smoothTime);
        }

        // スムージングを適用すべきか判定
        private bool ShouldApplySmoothing(float currentAngle, float lastAngle)
        {
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(currentAngle, lastAngle));

            // 急激な変化（30度以上）の場合はスムージングを適用
            if (angleDiff > 30f) return true;

            // 0度や180度付近（±10度）の場合もスムージングを適用
            float absAngle = Mathf.Abs(currentAngle % 180f);
            if (absAngle < 10f || absAngle > 170f) return true;

            return false;
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

            // 初期状態のHandの回転（T-Pose時など）
            public Quaternion NeutralHandRotation;

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
            }
        }
    }
}