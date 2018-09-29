using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRM;

public class VRMLipSync : MonoBehaviour
{
/*×VrmLipSyncTargetObj 〇LipSyncTargetName 名前を変えて存在しなくなってる*/
    //LipSyncTargetName にはLipSyncを適用させるVRMアバターのGameObject名を入れます。
    //公式サンプルのVRMローダーでは VRM という名前でシーンに配置されるので...
    //デフォルト設定は VRM になってます。
    [SerializeField]
    private string LipSyncTargetName = "VRM";

    private VRMBlendShapeProxy VrmProxy;
/*×VRMavater 〇VrmAvatar  他のVrmProxyとかの命名規則に合わせました。後eじゃなくてaのTypoです。*/
    private GameObject VrmAvatar;

    //LipSyncの感度を調整 0.1が鈍感 2.0が敏感
    [SerializeField]
    [Range(0.1f, 2.0f)]
    private float LipSyncSensitivity = 1.0f;
/* 関数一つでしか使ってないのでローカル変数に移行
    private int LipType;
    private float LipValue;
*/
    //trueならLipSynkが有効 falseで無効
    [SerializeField]
    private bool VrmLipSyncIsActive;

/* Update内で何度もGetComponentしないように、Setupの段階で取っておく */
    private OVRLipSyncContextMorphTarget morphTarget;

    void Start()
    {
        //VrmLipSyncIsActiveがtrueなら、LipSyncを実行
/* 開始時は必ず有効にしたい？ */
        VrmLipSyncIsActive = true;
    }

    //クソ雑魚AddComponent
    public void VrmLipSyncSetup()
    {
        //LipSyncTargetNameでVRMを検索、Proxyを取得できなければセットアップの終了
/*VrmProxy取得関数なのに知らぬ間にVrmAvatarも取得されていたので、分離する*/
        VrmAvatar = GetVrmAvatar(LipSyncTargetName);
        VrmProxy = GetVrmProxy(VrmAvatar);
        if (VrmProxy == null) { return; }

        if (VrmAvatar.GetComponent<AudioSource>() == null)
        {
            VrmAvatar.AddComponent<AudioSource>();
        }

        if (VrmAvatar.GetComponent<OVRLipSyncMicInput>() == null)
        {
            VrmAvatar.AddComponent<OVRLipSyncMicInput>();
        }

        if (VrmAvatar.GetComponent<OVRLipSyncContext>() == null)
        {
            VrmAvatar.AddComponent<OVRLipSyncContext>();
        }

        if (VrmAvatar.GetComponent<OVRLipSyncContextMorphTarget>() == null)
        {
            VrmAvatar.AddComponent<OVRLipSyncContextMorphTarget>();
        }
/* Update内で何度もGetComponentしないように、Setupの段階で取っておく */
        morphTarget = VrmAvatar.GetComponent<OVRLipSyncContextMorphTarget>();

        Debug.Log("VRMavaterにLipSyncをセットアップしました。");
    }
    /* VrmAvatarを取得する関数を分ける(一つの関数に複数機能持たせないように) */
    private GameObject GetVrmAvatar(string name)
    {
/*直接グローバルのVrmProxyに入れずに、ローカルで取得したものを返す*/
        GameObject vrmAvatar = null;
        //nameで検索して見つからなければ終了
        vrmAvatar = GameObject.Find(name);
        if (vrmAvatar == null)
        {
            Debug.Log(name + " は見つかりませんでした。VRMのGameObjectを " + name + " に変更してください。");
            return null;
        }
        Debug.Log(name + " の GameObject を取得しました");
        return vrmAvatar;
    }

    //nameでVRMを検索してVRMBlendShapeProxyを取得する。
/* nameからではなく、VrmAvatarを引数に、本当にVrmProxyだけを取得する関数にする */
    private VRMBlendShapeProxy GetVrmProxy(GameObject vrmAvatar)
    {
/*直接グローバルのVrmProxyに入れずに、ローカルで取得したものを返す*/
        VRMBlendShapeProxy vrmProxy = null;
/* GetVrmProxyなのに知らぬ間にVrmAvatarを取得しない。上のGetVrmAvatarに移動しました
        //nameで検索して見つからなければ終了
        if (GameObject.Find(name) == true)
        {
            VrmAvatar = GameObject.Find(name);
        }
        else if (GameObject.Find(name) == false)
        {
            Debug.Log(name + " は見つかりませんでした。VRMのGameObjectを " + name + " に変更してください。");
            return null;
        }
*/ 
/* 上のGetVrmAvatarと同様にGetComponentして、nullだったら存在しない
        //検索したVRMにVRMBlendShapeProxyがアタッチされてなければ終了
        if (VrmAvatar.GetComponent<VRMBlendShapeProxy>() == true)
        {
            VrmProxy = VrmAvatar.GetComponent<VRMBlendShapeProxy>();
        }
        else if (VrmAvatar.GetComponent<VRMBlendShapeProxy>() == false)
        {
            Debug.Log(name + " に VRMBlendShapeProxy がアタッチされていません。再度セットアップしてください");
            return null;
        }
*/
        //VRMからVRMBlendShapeProxyを取得
        vrmProxy = vrmAvatar.GetComponent<VRMBlendShapeProxy>();
/* 存在しない場合のみチェック */
        if (vrmProxy == null)
        {
            Debug.Log("VRM に VRMBlendShapeProxy がアタッチされていません。再度セットアップしてください");
            return null;
        }
        Debug.Log("VRM の VRMBlendShapeProxy を取得しました");
        return vrmProxy;
    }

    //OVRLiySyncの処理を VRM の VRMBlendShapeProxy に対応させる
    private void LipSyncConversion()
    {
/* LipValue,LipTypeはこの関数内でしか使ってないので移動してきた */
        float LipValue = 0.0f;
        int LipType = 0;
        float Value;
        // VRMLipValue[0] は「無音時に1を返す処理」の為、iに0を含めない
        for (int i = 1; i < 15; i++)
        {
/* Update内で何度もGetComponentしないように、Setupの段階で取っておいたmorphTargetをつかう */
            Value = morphTarget.VRMLipValue[i];
            //1番大きい値の時にLipTypeを更新
            if (LipValue < Value)
            {
/* すでにValueに入っているので、2度取得せずValueを使う */
                LipValue = Value;
                LipType = i;
            }
        }

        switch (LipType)
        {
            case 10:
                VrmProxy.SetValue(BlendShapePreset.A, LipValue / LipSyncSensitivity);
/*ValueとLipValueは必ず0ならswitch抜けた後に書けばいいが、そもそもローカル変数なので捨ててよいので削除
                Value = 0.0f;
                LipValue = 0.0f;*/
                break;
            case 12:
                VrmProxy.SetValue(BlendShapePreset.I, LipValue / LipSyncSensitivity);
/*              Value = 0.0f;
                LipValue = 0.0f;*/
                break;
            case 14:
                VrmProxy.SetValue(BlendShapePreset.U, LipValue / LipSyncSensitivity);
/*              Value = 0.0f;
                LipValue = 0.0f;*/
                break;
            case 11:
                VrmProxy.SetValue(BlendShapePreset.E, LipValue / LipSyncSensitivity);
/*              Value = 0.0f;
                LipValue = 0.0f;*/
                break;
            case 13:
                VrmProxy.SetValue(BlendShapePreset.O, LipValue / LipSyncSensitivity);
/*              Value = 0.0f;
                LipValue = 0.0f;*/
                break;
            default:
                VrmProxy.SetValue(BlendShapePreset.A, 0);
                VrmProxy.SetValue(BlendShapePreset.I, 0);
                VrmProxy.SetValue(BlendShapePreset.U, 0);
                VrmProxy.SetValue(BlendShapePreset.E, 0);
                VrmProxy.SetValue(BlendShapePreset.O, 0);
                Value = 0.0f;
                LipValue = 0.0f;
                break;
        }
    }

    void Update()
    {
        //LキーでLipSyncのセットアップ
        if (Input.GetKeyDown(KeyCode.L))
        {
            VrmLipSyncSetup();
        }

        //Proxyが参照できなければLipSyncを開始しない
        if (VrmProxy == null) { return; }

        //VrmLipSyncIsActiveがtrueの時は、LipSyncを実行
        if (VrmLipSyncIsActive == true)
        {
            LipSyncConversion();
        }
    }

    //外部からLipSyncの有効/無効を切り替える
    public void LipSyncActiveSwitch()
    {
        VrmLipSyncIsActive = !VrmLipSyncIsActive;
        if (VrmLipSyncIsActive == true)
        {
            Debug.Log("VRMLipSync：有効");
        }
        else if (VrmLipSyncIsActive == false)
        {
            Debug.Log("VRMLipSync：無効");
        }
    }

}