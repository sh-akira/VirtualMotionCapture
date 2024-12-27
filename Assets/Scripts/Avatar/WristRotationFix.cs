using RootMotion.FinalIK;
using System;
using UnityEngine;

namespace VMC
{
    public class WristRotationFix : MonoBehaviour
    {

        public VRIK ik;

        private FixItem LeftElbowFixItem;
        private FixItem LeftUpperArmFixItem;
        private FixItem RightElbowFixItem;
        private FixItem RightUpperArmFixItem;

        public float ElbowFixWeight = 0.5f;
        public float UpperArmFixWeight = 0.2f; //0.5では強すぎて肩がねじれる場合がある
        private Guid? eventId = null;

        public void SetVRIK(VRIK setIK)
        {
            if (eventId != null) IKManager.Instance.RemoveOnPostUpdate(eventId.Value);
            ik = setIK;

            LeftElbowFixItem = new FixItem(ik.references.leftUpperArm, ik.references.leftForearm, ik.references.leftHand, () => ElbowFixWeight);
            LeftUpperArmFixItem = new FixItem(ik.references.leftShoulder, ik.references.leftUpperArm, ik.references.leftForearm, () => UpperArmFixWeight);
            RightElbowFixItem = new FixItem(ik.references.rightUpperArm, ik.references.rightForearm, ik.references.rightHand, () => ElbowFixWeight);
            RightUpperArmFixItem = new FixItem(ik.references.rightShoulder, ik.references.rightUpperArm, ik.references.rightForearm, () => UpperArmFixWeight);

            eventId = IKManager.Instance.AddOnPostUpdate(10, OnPostUpdate);
        }

        void OnDestroy()
        {
            if (eventId != null) IKManager.Instance.RemoveOnPostUpdate(eventId.Value);
        }

        private void OnPostUpdate()
        {
            if (IKManager.Instance.vrik == null) return;

            FixAxis(LeftElbowFixItem);
            FixAxis(LeftUpperArmFixItem);
            FixAxis(RightElbowFixItem);
            FixAxis(RightUpperArmFixItem);
        }

        private void FixAxis(FixItem fix)
        {
            //Quaternion.AngleAxis:軸を決めて回転させる
            //Quaternion * Vector3: 指定方向に回転させたVector3が返ってくる
            Quaternion targetRotation = fix.Target.rotation;
            Quaternion twistOffset = Quaternion.AngleAxis(0, targetRotation * fix.TwistAxis);
            targetRotation = twistOffset * targetRotation;

            // 親(肩)と子(手首)のワールド座標の緩和軸を求める
            Vector3 relaxedAxisParent = twistOffset * fix.Parent.rotation * fix.AxisRelativeToParentDefault;
            Vector3 relaxedAxisChild = twistOffset * fix.Child.rotation * fix.AxisRelativeToChildDefault;

            // 親(肩)と子(手首)の中間の回転角度を計算する
            Vector3 relaxedAxis = Vector3.Slerp(relaxedAxisParent, relaxedAxisChild, fix.GetFixWeight());

            // relaxedAxisを（axis、twistAxis）空間で変換して、ねじれ角を計算できます
            Quaternion r = Quaternion.LookRotation(targetRotation * fix.Axis, targetRotation * fix.TwistAxis);
            relaxedAxis = Quaternion.Inverse(r) * relaxedAxis;

            // ねじれ軸を中心にこのTransformを回転させるために必要な角度を計算します
            float angle = Mathf.Atan2(relaxedAxis.x, relaxedAxis.z) * Mathf.Rad2Deg;
            //Debug.Log($"Angle{angle}");

            // 子(手首)の回転を取っておいて、対象(ひじ)を回転させた後戻せるようにしておく
            Quaternion childRotation = fix.Child.rotation;

            // 対象(ひじ)を回転させる
            fix.Target.rotation = Quaternion.AngleAxis(angle, targetRotation * fix.TwistAxis) * targetRotation;

            // 対象(ひじ)で動いてしまった子(手首)の回転を元に戻す
            fix.Child.rotation = childRotation;
        }

        private class FixItem
        {
            public Vector3 TwistAxis = Vector3.right;
            public Vector3 Axis = Vector3.forward;
            public Vector3 AxisRelativeToParentDefault;
            public Vector3 AxisRelativeToChildDefault;

            public Transform Parent;
            public Transform Target;
            public Transform Child;

            public Func<float> GetFixWeight;

            public FixItem(Transform parent, Transform target, Transform child, Func<float> getFixWeight)
            {
                Parent = parent;
                Target = target;
                Child = child;
                GetFixWeight = getFixWeight;

                //InverseTransformDirection:特定のワールド座標が自身のローカル座標だといくつになるか
                TwistAxis = target.InverseTransformDirection(child.position - target.position);
                Axis = new Vector3(TwistAxis.y, TwistAxis.z, TwistAxis.x);

                // ワールド座標での軸
                Vector3 elbowAxisWorld = target.rotation * Axis;

                //　肩と手首のワールド座標での軸
                AxisRelativeToParentDefault = Quaternion.Inverse(parent.rotation) * elbowAxisWorld;
                AxisRelativeToChildDefault = Quaternion.Inverse(child.rotation) * elbowAxisWorld;
            }
        }
    }
}