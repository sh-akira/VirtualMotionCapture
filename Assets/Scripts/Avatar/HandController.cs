using System.Collections.Generic;
using UnityEngine;

namespace VMC
{
    public class HandController : MonoBehaviour
    {

        private AnimationController leftAnimationController;
        private AnimationController rightAnimationController;
        private bool doLeftAnimation = false;
        private bool doRightAnimation = false;

        //指のデフォルト角度取得
        public void SetDefaultAngle(Animator animator)
        {
            FingerTransforms.Clear();
            FingerDefaultVectors.Clear();
            foreach (var bone in FingerBones)
            {
                var transform = animator.GetBoneTransform(bone);
                FingerTransforms.Add(transform);
                if (transform == null)
                {
                    FingerDefaultVectors.Add(Vector3.zero);
                }
                else
                {
                    FingerDefaultVectors.Add(new Vector3(transform.rotation.x, transform.rotation.y, transform.rotation.z));
                }
            }
        }


        private List<HumanBodyBones> FingerBones = new List<HumanBodyBones>
    {
        HumanBodyBones.LeftLittleDistal,
        HumanBodyBones.LeftLittleIntermediate,
        HumanBodyBones.LeftLittleProximal,
        HumanBodyBones.LeftRingDistal,
        HumanBodyBones.LeftRingIntermediate,
        HumanBodyBones.LeftRingProximal,
        HumanBodyBones.LeftMiddleDistal,
        HumanBodyBones.LeftMiddleIntermediate,
        HumanBodyBones.LeftMiddleProximal,
        HumanBodyBones.LeftIndexDistal,
        HumanBodyBones.LeftIndexIntermediate,
        HumanBodyBones.LeftIndexProximal,
        HumanBodyBones.LeftThumbDistal,
        HumanBodyBones.LeftThumbIntermediate,
        HumanBodyBones.LeftThumbProximal,
        HumanBodyBones.RightLittleDistal,
        HumanBodyBones.RightLittleIntermediate,
        HumanBodyBones.RightLittleProximal,
        HumanBodyBones.RightRingDistal,
        HumanBodyBones.RightRingIntermediate,
        HumanBodyBones.RightRingProximal,
        HumanBodyBones.RightMiddleDistal,
        HumanBodyBones.RightMiddleIntermediate,
        HumanBodyBones.RightMiddleProximal,
        HumanBodyBones.RightIndexDistal,
        HumanBodyBones.RightIndexIntermediate,
        HumanBodyBones.RightIndexProximal,
        HumanBodyBones.RightThumbDistal,
        HumanBodyBones.RightThumbIntermediate,
        HumanBodyBones.RightThumbProximal,
    };

        private List<Transform> FingerTransforms = new List<Transform>();
        private List<Vector3> FingerDefaultVectors = new List<Vector3>();

        public void SetHandAngle(bool LeftEnable, bool RightEnable, List<int> angles, float animationTime)
        {
            if (leftAnimationController == null) leftAnimationController = new AnimationController();
            if (rightAnimationController == null) rightAnimationController = new AnimationController();

            var startEulers = GetHandEulerAngles();
            if (startEulers == null) return;
            var endEulers = CalcHandEulerAngles(angles);

            if (LeftEnable)
            {
                leftAnimationController.StopAnimations();
                leftAnimationController.ClearAnimations();

                leftAnimationController.AddAnimation(animationTime, 0.0f, 1.0f, v => SetHandEulerAngles(true, false, eulersLerp(startEulers, endEulers, v)));

                doLeftAnimation = true;
            }
            if (RightEnable)
            {
                rightAnimationController.StopAnimations();
                rightAnimationController.ClearAnimations();

                rightAnimationController.AddAnimation(animationTime, 0.0f, 1.0f, v => SetHandEulerAngles(false, true, eulersLerp(startEulers, endEulers, v)));

                doRightAnimation = true;
            }
        }

        /*
        private List<Vector3> eulersLerp(List<Vector3> startEulers, List<Vector3> endEulers, float t)
        {
            var eulers = new List<Vector3>();
            for (int i = 0; i < startEulers.Count; i++)
            {
                eulers.Add(Vector3.Lerp(startEulers[i], endEulers[i], t));
            }
            return eulers;
        }
        */
        private List<Vector3> eulersLerp(List<Vector3> startEulers, List<Vector3> endEulers, float t)
        {
            var eulers = new List<Vector3>();
            for (int i = 0; i < startEulers.Count; i++)
            {
                var calcStart = new Vector3(startEulers[i].x > 180 ? startEulers[i].x - 360 : startEulers[i].x, startEulers[i].y > 180 ? startEulers[i].y - 360 : startEulers[i].y, startEulers[i].z > 180 ? startEulers[i].z - 360 : startEulers[i].z);
                var calcEnd = new Vector3(endEulers[i].x > 180 ? endEulers[i].x - 360 : endEulers[i].x, endEulers[i].y > 180 ? endEulers[i].y - 360 : endEulers[i].y, endEulers[i].z > 180 ? endEulers[i].z - 360 : endEulers[i].z);
                eulers.Add(Vector3.Lerp(calcStart, calcEnd, t));
            }
            return eulers;
        }


        public void SetHandEulerAngles(bool LeftEnable, bool RightEnable, List<Vector3> Eulers)
        {
            if (FingerTransforms.Count == 0) return;
            var handBonesCount = FingerBones.Count / 2;
            if (LeftEnable)
            {
                for (int i = 0; i < handBonesCount; i++)
                {
                    if (FingerTransforms[i] != null) FingerTransforms[i].localRotation = Quaternion.Euler(Eulers[i]);
                }
            }
            if (RightEnable)
            {
                for (int i = 0; i < handBonesCount; i++)
                {
                    if (FingerTransforms[i + handBonesCount] != null) FingerTransforms[i + handBonesCount].localRotation = Quaternion.Euler(Eulers[i + handBonesCount]);
                }
            }
        }

        public List<Vector3> GetHandEulerAngles()
        {
            var handBonesCount = FingerBones.Count;
            if (FingerTransforms.Count != handBonesCount) return null;
            var eulers = new List<Vector3>();
            for (int i = 0; i < handBonesCount; i++)
            {
                if (FingerTransforms[i] != null)
                {
                    eulers.Add(FingerTransforms[i].localRotation.eulerAngles);
                }
                else
                {
                    eulers.Add(Vector3.zero);
                }
            }
            return eulers;
        }

        public List<Vector3> CalcHandEulerAngles(List<int> angles)
        {
            if (FingerDefaultVectors == null || FingerDefaultVectors.Count == 0) return null;
            var handBonesCount = FingerBones.Count / 2;
            var eulers = new Vector3[FingerBones.Count];
            for (int i = 0; i < handBonesCount; i += 3)
            {
                if (i >= 12)
                { //親指
                    var vector = FingerDefaultVectors[i + 2]; //第三関節
                    var angle = (-angles[(i / 3) * 4 + 2]) / 90.0f; //-90が1.0 -45は0.5 -180は2.0
                    var sideangle = angles[(i / 3) * 4 + 3];
                    var ax = angle * 0.0f;
                    var ay = angle * 0.0f + sideangle;
                    var az = (float)angles[(i / 3) * 4 + 2];
                    eulers[i + 2] = new Vector3(vector.x + ax, vector.y - ay, vector.z - az);

                    vector = FingerDefaultVectors[i + 1]; //第二関節
                    angle = (-angles[(i / 3) * 4 + 1]) / 90.0f;
                    ax = angle * 38f;
                    ay = angle * 38f;
                    az = angle * -15f;
                    eulers[i + 1] = new Vector3(vector.x + ax, vector.y - ay, vector.z - az);

                    vector = FingerDefaultVectors[i]; //第一関節
                    angle = (-angles[(i / 3) * 4]) / 90.0f;
                    ax = angle * 34f;
                    ay = angle * 56f;
                    az = angle * -7f;
                    eulers[i] = new Vector3(vector.x + ax, vector.y - ay, vector.z - az);
                }
                else
                {
                    var vector = FingerDefaultVectors[i + 2]; //第三関節
                    var angle = angles[(i / 3) * 4 + 2];
                    var sideangle = angles[(i / 3) * 4 + 3];
                    eulers[i + 2] = new Vector3(vector.x, vector.y - sideangle, vector.z - angle);

                    vector = FingerDefaultVectors[i + 1]; //第二関節
                    angle = angles[(i / 3) * 4 + 1];
                    eulers[i + 1] = new Vector3(vector.x, vector.y/* - sideangle*/, vector.z - angle);

                    vector = FingerDefaultVectors[i]; //第一関節
                    angle = angles[(i / 3) * 4];
                    eulers[i] = new Vector3(vector.x, vector.y/* - sideangle*/, vector.z - angle);
                }
            }
            for (int i = 0; i < handBonesCount; i += 3)
            {
                if (i >= 12)
                { //親指
                    var vector = FingerDefaultVectors[i + 2]; //第三関節
                    var angle = (-angles[(i / 3) * 4 + 2]) / 90.0f; //-90が1.0 -45は0.5 -180は2.0
                    var sideangle = angles[(i / 3) * 4 + 3];
                    var ax = angle * 0.0f;
                    var ay = angle * 0.0f + sideangle;
                    var az = (float)angles[(i / 3) * 4 + 2];
                    eulers[i + handBonesCount + 2] = new Vector3(vector.x + ax, vector.y + ay, vector.z + az);

                    vector = FingerDefaultVectors[i + 1]; //第二関節
                    angle = (-angles[(i / 3) * 4 + 1]) / 90.0f;
                    ax = angle * 38f;
                    ay = angle * 38f;
                    az = angle * -15f;
                    eulers[i + handBonesCount + 1] = new Vector3(vector.x + ax, vector.y + ay, vector.z + az);

                    vector = FingerDefaultVectors[i]; //第一関節
                    angle = (-angles[(i / 3) * 4]) / 90.0f;
                    ax = angle * 34f;
                    ay = angle * 56f;
                    az = angle * -7f;
                    eulers[i + handBonesCount] = new Vector3(vector.x + ax, vector.y + ay, vector.z + az);
                }
                else
                {
                    var vector = FingerDefaultVectors[i + 2]; //第三関節
                    var angle = angles[(i / 3) * 4 + 2];
                    var sideangle = angles[(i / 3) * 4 + 3];
                    eulers[i + handBonesCount + 2] = new Vector3(vector.x, vector.y + sideangle, vector.z + angle);

                    vector = FingerDefaultVectors[i + 1]; //第二関節
                    angle = angles[(i / 3) * 4 + 1];
                    eulers[i + handBonesCount + 1] = new Vector3(vector.x, vector.y/* + sideangle*/, vector.z + angle);

                    vector = FingerDefaultVectors[i]; //第一関節
                    angle = angles[(i / 3) * 4];
                    eulers[i + handBonesCount] = new Vector3(vector.x, vector.y/* + sideangle*/, vector.z + angle);
                }
            }
            return new List<Vector3>(eulers);
        }

        // Update is called once per frame
        void Update()
        {
            if (doLeftAnimation && leftAnimationController?.Next() == false)
            {
                doLeftAnimation = false;
            }
            if (doRightAnimation && rightAnimationController?.Next() == false)
            {
                doRightAnimation = false;
            }
        }
    }
}