using UnityEngine;
using VMCMod;

[VMCPlugin(
    Name: "ExampleMod",
    Version: "1.0.0",
    Author: "sh_akira",
    Description: "サンプルModです。右手に球体を取り付けます。Settingボタンで色をランダムに変更します。",
    AuthorURL: "https://twitter.com/sh_akira",
    PluginURL: "http://mod.vmc.info/")]
public class ExampleMod : MonoBehaviour
{
    private Transform rightHandTransform;
    private GameObject sphere;
    private Material sphereMaterial;

    private void Awake()
    {
        VMCEvents.OnModelLoaded += OnModelLoaded;
        VMCEvents.OnCameraChanged += OnCameraChanged;
    }

    void Start()
    {
        Debug.Log("Example Mod started.");

        sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        sphereMaterial = sphere.GetComponent<Renderer>().material;
    }

    void Update()
    {
        if (rightHandTransform != null)
        {
            sphere.transform.position = rightHandTransform.position;
        }
    }

    [OnSetting]
    public void OnSetting()
    {
        if (sphereMaterial != null)
        {
            sphereMaterial.color = new Color(Random.value, Random.value, Random.value);
        }
    }

    private void OnModelLoaded(GameObject currentModel)
    {
        if (currentModel == null) return;

        var animator = currentModel.GetComponent<Animator>();
        rightHandTransform = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal); //右手中指のボーン
    }

    private void OnCameraChanged(Camera currentCamera)
    {
        //カメラ切り替え時に現在のカメラを取得できます
    }
}
