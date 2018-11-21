using UnityEngine;

[RequireComponent(typeof(Camera))]
public class VirtualCamera : MonoBehaviour
{
    public enum EResizeMode
    {
        Disabled = 0,
        LinearResize = 1
    }

    public enum EMirrorMode
    {
        Disabled = 0,
        MirrorHorizontally = 1
    }

    public enum ECaptureSendResult
    {
        SUCCESS = 0,
        WARNING_FRAMESKIP = 1,
        WARNING_CAPTUREINACTIVE = 2,
        ERROR_UNSUPPORTEDGRAPHICSDEVICE = 100,
        ERROR_PARAMETER = 101,
        ERROR_TOOLARGERESOLUTION = 102,
        ERROR_TEXTUREFORMAT = 103,
        ERROR_READTEXTURE = 104,
        ERROR_INVALIDCAPTUREINSTANCEPTR = 200
    };

    [SerializeField]
    [Tooltip("Unity側とキャプチャーの解像度が違うときにスケールをするか(重いので非推奨)")]
    public EResizeMode ResizeMode = EResizeMode.Disabled;
    public static EResizeMode ResizeMode_Global = EResizeMode.Disabled;
    [SerializeField]
    [Tooltip("送信が停止しているとみなされるまで、新しいフレームを待つ時間は何ミリ秒か")]
    public int Timeout = 1000;
    [SerializeField]
    [Tooltip("画像をミラーするか")]
    public EMirrorMode MirrorMode = EMirrorMode.Disabled;
    public static EMirrorMode MirrorMode_Global = EMirrorMode.Disabled;
    [SerializeField]
    [Tooltip("バッファリングする枚数(パフォーマンスのために1か2を設定すると良いが遅延が発生する)")]
    public int Buffering = 0;
    public static int Buffering_Global = 0;

    Interface CaptureInterface;

    void Awake()
    {
        if (Application.runInBackground == false)
        {
            Application.runInBackground = true;
        }
    }

    void Start()
    {
        CaptureInterface = new Interface();
    }

    void OnDestroy()
    {
        CaptureInterface.Close();
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(source, destination);
        var buffering = Buffering_Global != 0 ? Buffering_Global : Buffering;
        var resizeMode = ResizeMode_Global != EResizeMode.Disabled ? ResizeMode_Global : ResizeMode;
        var mirrorMode = MirrorMode_Global != EMirrorMode.Disabled ? MirrorMode_Global : MirrorMode;
        switch (CaptureInterface.SendTexture(source, Timeout, buffering, resizeMode, mirrorMode))
        {
            case ECaptureSendResult.SUCCESS: break;
            case ECaptureSendResult.WARNING_FRAMESKIP: Debug.LogWarning("[VirtualCamera] キャプチャデバイスがフレームをスキップしました。キャプチャフレームレートがレンダリングフレームレートと一致しません。"); break;
            case ECaptureSendResult.WARNING_CAPTUREINACTIVE: Debug.LogWarning("[VirtualCamera] キャプチャデバイスが非アクティブです"); break;
            case ECaptureSendResult.ERROR_UNSUPPORTEDGRAPHICSDEVICE: Debug.LogError("[VirtualCamera] 非対応のグラフィックデバイス (D3D11のみ対応しています)"); break;
            case ECaptureSendResult.ERROR_PARAMETER: Debug.LogError("[VirtualCamera] 入力パラメータが不正です"); break;
            case ECaptureSendResult.ERROR_TOOLARGERESOLUTION: Debug.LogError("[VirtualCamera] 解像度が大きすぎます"); break;
            case ECaptureSendResult.ERROR_TEXTUREFORMAT: Debug.LogError("[VirtualCamera] 非対応のテクスチャフォーマット (非HDR(ARGB32)かHDR(FP16/ARGB Half)のみ対応)"); break;
            case ECaptureSendResult.ERROR_READTEXTURE: Debug.LogError("[VirtualCamera] テクスチャの読出しに失敗"); break;
            case ECaptureSendResult.ERROR_INVALIDCAPTUREINSTANCEPTR: Debug.LogError("[VirtualCamera] キャプチャインスタンスのポインタが不正です"); break;
        }
    }

    public class Interface
    {
        [System.Runtime.InteropServices.DllImport("VMC_CameraPlugin")]
        extern static System.IntPtr CaptureCreateInstance(int CapNum);
        [System.Runtime.InteropServices.DllImport("VMC_CameraPlugin")]
        extern static void CaptureDeleteInstance(System.IntPtr instance);
        [System.Runtime.InteropServices.DllImport("VMC_CameraPlugin")]
        extern static ECaptureSendResult CaptureSendTexture(System.IntPtr instance, System.IntPtr nativetexture, int Timeout, int Buffering, EResizeMode ResizeMode, EMirrorMode MirrorMode, bool IsLinearColorSpace);
        System.IntPtr CaptureInstance;

        public Interface()
        {
            CaptureInstance = CaptureCreateInstance(0);
        }

        ~Interface()
        {
            Close();
        }

        public void Close()
        {
            if (CaptureInstance != System.IntPtr.Zero) CaptureDeleteInstance(CaptureInstance);
            CaptureInstance = System.IntPtr.Zero;
        }

        public ECaptureSendResult SendTexture(Texture Source, int Timeout = 1000, int Buffering = 0, EResizeMode ResizeMode = EResizeMode.Disabled, EMirrorMode MirrorMode = EMirrorMode.Disabled)
        {
            if (CaptureInstance == System.IntPtr.Zero) return ECaptureSendResult.ERROR_INVALIDCAPTUREINSTANCEPTR;
            return CaptureSendTexture(CaptureInstance, Source.GetNativeTexturePtr(), Timeout, Buffering, ResizeMode, MirrorMode, QualitySettings.activeColorSpace == ColorSpace.Linear);
        }
    }
}
