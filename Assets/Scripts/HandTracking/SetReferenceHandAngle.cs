using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetReferenceHandAngle : MonoBehaviour
{

    public Transform SteamVRHandSkeletonRoot = null;
    private Transform[] SteamVRHandBones = null;

    public HandController handController;

    private Vector3[] HandAngles_rock = new Vector3[]
    {
        new Vector3(-7.01671E-15f,180f,-7.01671E-15f),
        new Vector3(44.97409f,176.607f,8.359565f),
        new Vector3(359.4362f,239.8005f,36.81101f),
        new Vector3(0.4363385f,9.689557f,298.2396f),
        new Vector3(0.1136601f,359.8626f,285.6703f),
        new Vector3(-1.590278E-15f,-1.590277E-15f,-6.361109E-15f),
        new Vector3(288.7646f,121.0024f,152.0427f),
        new Vector3(6.070851f,12.31913f,269.0261f),
        new Vector3(359.8836f,359.4341f,248.4853f),
        new Vector3(0.01379834f,0.1329391f,276.385f),
        new Vector3(1.192708E-15f,2.226388E-14f,-3.975693E-16f),
        new Vector3(270.7873f,283.7241f,350.5438f),
        new Vector3(347.8773f,3.789021f,262.8109f),
        new Vector3(0.0770772f,0.4156884f,262.4894f),
        new Vector3(359.921f,359.5338f,262.2974f),
        new Vector3(1.188867E-15f,-9.563924E-17f,355.4007f),
        new Vector3(275.3454f,15.01525f,253.954f),
        new Vector3(354.043f,7.442795f,264.6832f),
        new Vector3(359.9529f,359.7137f,263.26f),
        new Vector3(0.08867738f,0.4809363f,257.6532f),
        new Vector3(7.951387E-16f,0f,0f),
        new Vector3(287.7268f,36.1883f,227.1799f),
        new Vector3(359.4091f,6.155605f,265.4337f),
        new Vector3(0.21743f,1.295408f,269.215f),
        new Vector3(359.7806f,358.5597f,255.5686f),
        new Vector3(8.276074E-32f,1.590277E-15f,-2.98177E-15f),
        new Vector3(345.8224f,290.752f,145.3749f),
        new Vector3(317.9687f,179.644f,17.36886f),
        new Vector3(318.3672f,179.7948f,19.96865f),
        new Vector3(327.232f,183.9954f,23.51695f),
        new Vector3(336.3155f,182.8792f,26.07514f),
    };
    private Vector3[] HandAngles_paper = new Vector3[]
    {
        new Vector3(-7.01671E-15f,180f,-7.01671E-15f),
        new Vector3(44.97409f,176.607f,8.359565f),
        new Vector3(349.1964f,257.7755f,64.61391f),
        new Vector3(9.373862f,357.22f,353.4283f),
        new Vector3(0.1973783f,357.4748f,25.65684f),
        new Vector3(-3.180555E-15f,1.765563E-31f,-3.180555E-15f),
        new Vector3(288.6569f,125.6641f,153.9797f),
        new Vector3(1.193562f,4.396974f,349.9465f),
        new Vector3(5.256834f,-0.003815474f,5.271921f),
        new Vector3(0.1769647f,2.613527f,1.541096f),
        new Vector3(1.192708E-15f,2.226388E-14f,-3.975693E-16f),
        new Vector3(271.5416f,213.6646f,66.93576f),
        new Vector3(341.5198f,10.81796f,350.1397f),
        new Vector3(2.02463f,358.3632f,353.1028f),
        new Vector3(359.4052f,3.202486f,7.591185f),
        new Vector3(1.075081E-15f,-6.468146E-15f,355.4007f),
        new Vector3(275.9737f,22.06263f,256.0568f),
        new Vector3(354.1742f,12.19795f,351.0042f),
        new Vector3(359.7434f,0.007270342f,356.5524f),
        new Vector3(359.9762f,1.456663f,355.3662f),
        new Vector3(7.951387E-16f,0f,0f),
        new Vector3(290.9654f,49.74557f,228.4196f),
        new Vector3(354.3328f,16.03521f,351.1587f),
        new Vector3(0.2158919f,359.9929f,347.6531f),
        new Vector3(359.3444f,5.934065f,8.301458f),
        new Vector3(-1.655215E-32f,1.590277E-15f,5.96354E-16f),
        new Vector3(0.1417727f,282.2093f,322.4996f),
        new Vector3(311.2339f,181.6644f,85.66109f),
        new Vector3(322.8444f,171.8007f,94.36884f),
        new Vector3(333.0991f,181.3022f,98.19785f),
        new Vector3(350.3429f,169.0767f,100.92f),
    };
    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        var sdown = Input.GetKeyDown(KeyCode.S);
        var ddown = Input.GetKeyDown(KeyCode.D);
        if (sdown || ddown)
        {

            if (SteamVRHandBones == null)
            {
                SteamVRHandBones = SteamVRHandSkeletonRoot.GetComponentsInChildren<Transform>();
            }

            var rots = new List<Vector3>();
            var rot = SteamVRHandSkeletonRoot.localRotation.eulerAngles;
            rots.Add(rot);

            for (var i = 2; i < SteamVRHandBones.Length; i++)
            {
                if (SteamVRHandBones[i] == null) continue;
                SteamVRHandBones[i].localRotation = Quaternion.Euler(sdown ? HandAngles_paper[i] : HandAngles_rock[i]);
            }
        }
        var fdown = Input.GetKeyDown(KeyCode.F);
        if (fdown)
        {
            var eulers = handController.GetHandEulerAngles();

            var sb = new System.Text.StringBuilder();
            foreach (var srot in eulers)
            {
                sb.AppendLine("new Vector3(" + RotToString(srot) + "),");
            }
            Debug.LogWarning(sb.ToString());
        }
    }
    private string RotToString(Vector3 rot)
    {
        return rot.x.ToString() + "f," + rot.y.ToString() + "f," + rot.z.ToString() + "f";
    }
}
