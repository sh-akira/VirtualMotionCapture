using DVRSDK.Auth.Hena.Models;
using DVRSDK.Auth.Okami.Models;
using DVRSDK.Serializer;
using DVRSDK.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;

namespace DVRSDK.Auth
{
    public class DVRAuthController2D : MonoBehaviour
    {
        [SerializeField]
        private GUIStyle textCenterLabelStyle;

        private string HelloReturn = "";

        private CurrentUserModel currentUser = null;
        private Texture2D thumnbnailTexture = null;

        private List<AvatarModel> currentAvatars;

        private float currentVRMDownloadTime = 0.0f;

        private string verificationUri;

        private Dictionary<ApiRequestErrors, string> apiRequestErrorMessages = new Dictionary<ApiRequestErrors, string> {
            { ApiRequestErrors.Unknown,"Unknown request error" },
            { ApiRequestErrors.Forbidden, "Request forbidden" },
            { ApiRequestErrors.Unregistered, "User unregistered" },
            { ApiRequestErrors.Unverified, "User email unverified" },
        };

        public void Awake()
        {
            var sdkSettings = Resources.Load<SdkSettings>("SdkSettings");
            var client_id = sdkSettings.client_id;
            var config = new DVRAuthConfiguration(client_id, new UnitySettingStore(), new UniWebRequest(), new NewtonsoftJsonSerializer());
            Authentication.Instance.Init(config);
        }

        public void OnGUI()
        {
            var height = Screen.height - 10;

            if (thumnbnailTexture != null)
            {
                GUI.DrawTexture(new Rect(10, 10, 256, 256), thumnbnailTexture);
            }

            GUI.BeginGroup(new Rect(Screen.width / 2 - 320, Screen.height / 2 - height / 2, 640, height));
            GUI.Box(new Rect(0, 0, 640, height), "DVRSDK : Connect Login Sample");

            GUI.Label(new Rect(320 - 50, 20, 100, 20), "DVRAuth Examples", textCenterLabelStyle);

            if (GUI.Button(new Rect(320 - 50, 40, 100, 30), "Login"))
            {
                DoLogin();
            }

            GUILayout.BeginArea(new Rect(320 - 160, 80, 320, height - 50));

            GUILayout.Label("Verification Uri", textCenterLabelStyle);
            GUILayout.TextField(verificationUri);

            if (GUILayout.Button("Open Uri"))
            {
                Application.OpenURL(verificationUri);
            }

            if (GUILayout.Button("Refresh"))
            {
                var t = Authentication.Instance.DoRefresh();
            }

            if (GUILayout.Button("Remove Refresh Token"))
            {
                Authentication.Instance.RemoveRefreshToken();
            }

            GUILayout.Label("Test", textCenterLabelStyle);
            GUILayout.TextField(HelloReturn);

            if (GUILayout.Button("Ask Hello"))
            {
                var t = TestHello();
            }

            GUILayout.Label("Current User", textCenterLabelStyle);
            GUILayout.TextField(currentUser != null ? currentUser.name : "");

            if (GUILayout.Button("Get Current User"))
            {
                var t = GetCurrentUser();
            }

            if (GUILayout.Button("Get User Thumbnail"))
            {
                var t = GetUserThumbnail();
            }

            GUILayout.Label("Current Avatar Count", textCenterLabelStyle);
            GUILayout.TextField(currentAvatars != null ? currentAvatars.Count.ToString() : "");

            if (GUILayout.Button("Get Current Avatars"))
            {
                var t = GetAvatars();
            }
            GUILayout.Label("VRM Download Seconds", textCenterLabelStyle);
            GUILayout.TextField(currentVRMDownloadTime.ToString());

            if (GUILayout.Button("Download first VRM Avatar"))
            {
                var t = GetCurrentAvatar();
            }

            if (GUILayout.Button("Clear Cache Data"))
            {
                ClearCacheData();
            }

            GUILayout.EndArea();

            GUI.EndGroup();
        }

        private void DoLogin()
        {
            Authentication.Instance.Authorize(
                openBrowser: url =>
                {
                    verificationUri = url;
                    Application.OpenURL(url);
                },
                onAuthSuccess: isSuccess =>
                {
                    if (isSuccess)
                    {
                        LoginSuccess();
                    }
                    else
                    {
                        Debug.LogError("Login Failed");
                    }
                },
                onAuthError: exception =>
                {
                    Debug.LogError(exception);
                });
        }

        private void LoginSuccess()
        {
            Debug.Log("Login Success!");
        }

        private async Task TestHello()
        {
            HelloReturn = await Authentication.Instance.Okami.AskHello();
        }

        private async Task GetCurrentUser()
        {
            try
            {
                currentUser = await Authentication.Instance.Okami.GetCurrentUserAsync();
            }
            catch (ApiRequestException ex)
            {
                Debug.LogError(apiRequestErrorMessages[ex.ErrorType]);
            }
        }

        private async Task GetUserThumbnail()
        {
            if (currentUser != null)
            {
                var imageBinary = await Authentication.Instance.Okami.GetUserThumbnailAsync(currentUser);
                if (imageBinary != null)
                {
                    thumnbnailTexture = new Texture2D(1, 1);
                    thumnbnailTexture.LoadImage(imageBinary);
                }
            }
        }

        private async Task GetAvatars()
        {
            try
            {
                currentAvatars = await Authentication.Instance.Okami.GetAvatarsAsync();
            }
            catch (ApiRequestException ex)
            {
                Debug.LogError(apiRequestErrorMessages[ex.ErrorType]);
            }
        }

        private async Task GetCurrentAvatar()
        {
            // 自身のアバター一覧からカレントを取得する場合
            //if (currentAvatars == null || currentAvatars.Count == 0)
            //{
            //    currentStatus = "No avatars on your account.";
            //    return;
            //}
            //var currentAvatar = currentAvatars.FirstOrDefault(avatar => avatar.is_current);
            //if (currentAvatar == null) currentAvatar = currentAvatars.First();

            // 自身のユーザーからカレントを取得する場合
            //var currentUser = await Authentication.Instance.Okami.GetCurrentUserAsync();
            //var currentAvatar = currentUser.current_avatar;

            // 自身のユーザーIDからユーザー情報を取得して取得する場合(データ暗号化)
            var currentUser = await Authentication.Instance.Okami.GetCurrentUserAsync();
            var myUser = await Authentication.Instance.Okami.GetUserAsync(currentUser.id);
            var currentAvatar = myUser.current_avatar;

            var startTime = Time.realtimeSinceStartup;
            await Authentication.Instance.Okami.LoadAvatarVRMAsync(currentAvatar, null);
            currentVRMDownloadTime = Time.realtimeSinceStartup - startTime;
        }

        private void ClearCacheData()
        {
            EncryptedDataStorage.ClearValue();
            EncryptedDataStorage.ApplyValue();
        }

        void OnApplicationQuit()
        {
            Authentication.Instance.CancelAuthorize();
        }
    }
}
