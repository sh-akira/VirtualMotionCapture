using System;
using RootMotion.FinalIK;

[Serializable]
public class VRIKSolverData
{
    public VRIKSolverData(IKSolverVR solver)
    {
        IKPositionWeight = solver.IKPositionWeight;
        LOD = solver.LOD;
        plantFeet = solver.plantFeet;
        Spine = new VRIKSolverDataSpine(solver.spine);
        LeftArm = new VRIKSolverDataArm(solver.leftArm);
        RightArm = new VRIKSolverDataArm(solver.rightArm);
        LeftLeg = new VRIKSolverDataLeg(solver.leftLeg);
        RightLeg = new VRIKSolverDataLeg(solver.rightLeg);
        Locomotion = new VRIKSolverDataLocomotion(solver.locomotion);
    }

    public float IKPositionWeight;
    public int LOD;
    public bool plantFeet;

    public VRIKSolverDataSpine Spine;
    public VRIKSolverDataArm LeftArm;
    public VRIKSolverDataArm RightArm;
    public VRIKSolverDataLeg LeftLeg;
    public VRIKSolverDataLeg RightLeg;
    public VRIKSolverDataLocomotion Locomotion;

    public void ApplyTo(IKSolverVR solver)
    {
        solver.IKPositionWeight = IKPositionWeight;
        solver.LOD = LOD;
        solver.plantFeet = plantFeet;
        Spine.ApplyTo(solver.spine);
        LeftArm.ApplyTo(solver.leftArm);
        RightArm.ApplyTo(solver.rightArm);
        LeftLeg.ApplyTo(solver.leftLeg);
        RightLeg.ApplyTo(solver.rightLeg);
        Locomotion.ApplyTo(solver.locomotion);
    }
}

[Serializable]
public class VRIKSolverDataSpine
{
    public VRIKSolverDataSpine(IKSolverVR.Spine spine)
    {
        positionWeight = spine.positionWeight;
        rotationWeight = spine.rotationWeight;
        pelvisPositionWeight = spine.pelvisPositionWeight;
        pelvisRotationWeight = spine.pelvisRotationWeight;
        chestGoalWeight = spine.chestGoalWeight;
        minHeadHeight = spine.minHeadHeight;
        bodyPosStiffness = spine.bodyPosStiffness;
        bodyRotStiffness = spine.bodyRotStiffness;
        neckStiffness = spine.neckStiffness;
        rotateChestByHands = spine.rotateChestByHands;
        chestClampWeight = spine.chestClampWeight;
        headClampWeight = spine.headClampWeight;
        moveBodyBackWhenCrouching = spine.moveBodyBackWhenCrouching;
        maintainPelvisPosition = spine.maintainPelvisPosition;
        maxRootAngle = spine.maxRootAngle;
        rootHeadingOffset = spine.rootHeadingOffset;
    }

    public float positionWeight;
    public float rotationWeight;
    public float pelvisPositionWeight;
    public float pelvisRotationWeight;
    public float chestGoalWeight;
    public float minHeadHeight;
    public float bodyPosStiffness;
    public float bodyRotStiffness;
    public float neckStiffness;
    public float rotateChestByHands;
    public float chestClampWeight;
    public float headClampWeight;
    public float moveBodyBackWhenCrouching;
    public float maintainPelvisPosition;
    public float maxRootAngle;
    public float rootHeadingOffset;

    public void ApplyTo(IKSolverVR.Spine spine)
    {
        spine.positionWeight = positionWeight;
        spine.rotationWeight = rotationWeight;
        spine.pelvisPositionWeight = pelvisPositionWeight;
        spine.pelvisRotationWeight = pelvisRotationWeight;
        spine.chestGoalWeight = chestGoalWeight;
        spine.minHeadHeight = minHeadHeight;
        spine.bodyPosStiffness = bodyPosStiffness;
        spine.bodyRotStiffness = bodyRotStiffness;
        spine.neckStiffness = neckStiffness;
        spine.rotateChestByHands = rotateChestByHands;
        spine.chestClampWeight = chestClampWeight;
        spine.headClampWeight = headClampWeight;
        spine.moveBodyBackWhenCrouching = moveBodyBackWhenCrouching;
        spine.maintainPelvisPosition = maintainPelvisPosition;
        spine.maxRootAngle = maxRootAngle;
        spine.rootHeadingOffset = rootHeadingOffset;
    }
}


[Serializable]
public class VRIKSolverDataArm
{
    public VRIKSolverDataArm(IKSolverVR.Arm arm)
    {
        positionWeight = arm.positionWeight;
        rotationWeight = arm.rotationWeight;
        shoulderRotationWeight = arm.shoulderRotationWeight;
        shoulderTwistWeight = arm.shoulderTwistWeight;
        bendGoalWeight = arm.bendGoalWeight;
        swivelOffset = arm.swivelOffset;
        armLengthMlp = arm.armLengthMlp;
    }

    public float positionWeight;
    public float rotationWeight;
    public float shoulderRotationWeight;
    public float shoulderTwistWeight;
    public float bendGoalWeight;
    public float swivelOffset;
    public float armLengthMlp;

    public void ApplyTo(IKSolverVR.Arm arm)
    {
        arm.positionWeight = positionWeight;
        arm.rotationWeight = rotationWeight;
        arm.shoulderRotationWeight = shoulderRotationWeight;
        arm.shoulderTwistWeight = shoulderTwistWeight;
        arm.bendGoalWeight = bendGoalWeight;
        arm.swivelOffset = swivelOffset;
        arm.armLengthMlp = armLengthMlp;
    }
}

[Serializable]
public class VRIKSolverDataLeg
{
    public VRIKSolverDataLeg(IKSolverVR.Leg leg)
    {
        positionWeight = leg.positionWeight;
        rotationWeight = leg.rotationWeight;
        bendGoalWeight = leg.bendGoalWeight;
        swivelOffset = leg.swivelOffset;
        bendToTargetWeight = leg.bendToTargetWeight;
        legLengthMlp = leg.legLengthMlp;
    }

    public float positionWeight;
    public float rotationWeight;
    public float bendGoalWeight;
    public float swivelOffset;
    public float bendToTargetWeight;
    public float legLengthMlp;

    public void ApplyTo(IKSolverVR.Leg leg)
    {
        leg.positionWeight = positionWeight;
        leg.rotationWeight = rotationWeight;
        leg.bendGoalWeight = bendGoalWeight;
        leg.swivelOffset = swivelOffset;
        leg.bendToTargetWeight = bendToTargetWeight;
        leg.legLengthMlp = legLengthMlp;
    }
}

[Serializable]
public class VRIKSolverDataLocomotion
{
    public VRIKSolverDataLocomotion(IKSolverVR.Locomotion locomotion)
    {
        weight = locomotion.weight;
        footDistance = locomotion.footDistance;
        stepThreshold = locomotion.stepThreshold;
        angleThreshold = locomotion.angleThreshold;
        comAngleMlp = locomotion.comAngleMlp;
        maxVelocity = locomotion.maxVelocity;
        velocityFactor = locomotion.velocityFactor;
        maxLegStretch = locomotion.maxLegStretch;
        rootSpeed = locomotion.rootSpeed;
        stepSpeed = locomotion.stepSpeed;
        relaxLegTwistMinAngle = locomotion.relaxLegTwistMinAngle;
        relaxLegTwistSpeed = locomotion.relaxLegTwistSpeed;
    }

    public float weight;
    public float footDistance;
    public float stepThreshold;
    public float angleThreshold;
    public float comAngleMlp;
    public float maxVelocity;
    public float velocityFactor;
    public float maxLegStretch;
    public float rootSpeed;
    public float stepSpeed;
    public float relaxLegTwistMinAngle;
    public float relaxLegTwistSpeed;


    public void ApplyTo(IKSolverVR.Locomotion locomotion)
    {
        locomotion.weight = weight;
        locomotion.footDistance = footDistance;
        locomotion.stepThreshold = stepThreshold;
        locomotion.angleThreshold = angleThreshold;
        locomotion.comAngleMlp = comAngleMlp;
        locomotion.maxVelocity = maxVelocity;
        locomotion.velocityFactor = velocityFactor;
        locomotion.maxLegStretch = maxLegStretch;
        locomotion.rootSpeed = rootSpeed;
        locomotion.stepSpeed = stepSpeed;
        locomotion.relaxLegTwistMinAngle = relaxLegTwistMinAngle;
        locomotion.relaxLegTwistSpeed = relaxLegTwistSpeed;
    }
}