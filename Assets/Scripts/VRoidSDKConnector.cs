// VRoid SDK(Assets/VRoidSDK)が同梱されている場合のみコンパイルする。
// SDK未同梱でクローンした場合はEditorスクリプト(VRoidSDKDefineConfigurator)がVMC_VROIDSDKを未定義にし、
// このファイル全体が除外されてビルドが通る。SDK参照はこの1ファイルに閉じている。
#if VMC_VROIDSDK
using Pixiv.VroidSdk;
using Pixiv.VroidSdk.Api;
using Pixiv.VroidSdk.Api.DataModel;
using Pixiv.VroidSdk.Browser;
using Pixiv.VroidSdk.Cache;
using Pixiv.VroidSdk.Cache.DataModel;
using Pixiv.VroidSdk.Cache.Migrate;
using Pixiv.VroidSdk.IO;
using Pixiv.VroidSdk.Networking.Drivers;
using Pixiv.VroidSdk.Oauth;
using Pixiv.VroidSdk.Unity.Crypt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using UniGLTF;
using UniGLTF.Extensions.VRMC_vrm;
using UnityEngine;
using UnityMemoryMappedFile;
using UniVRM10;
using VMCMod;
using VRoidSDK.Examples.Core.Model;

namespace VMC
{
    public class VRoidSDKConnector : MonoBehaviour
    {
        [SerializeField]
        private ControlWPFWindow controlWPFWindow;
        [SerializeField]
        private ModManager modManager;

        private ApiModel _model;
        private Client _oauthClient;
        private DefaultApi _api;
        private IManualCodeRegistrable _browser;

        private MemoryMappedFileServer server;

        private List<CharacterModel> _characterModels;

        //各フィルタの次ページ取得用リンク(カーソルページネーション)
        private Dictionary<PipeCommands.ModelFilters, ApiLinksFormat> _nextLinks = new Dictionary<PipeCommands.ModelFilters, ApiLinksFormat>();

        private System.Threading.SynchronizationContext context = null;

        void Awake()
        {
            modManager.OnBeforeModLoad += () =>
            {
                if (server != null)
                {
                    server.ReceivedEvent -= Server_Received;
                }
                controlWPFWindow = null;
                server = null;
                DestroyImmediate(gameObject);
            };
        }

        // Use this for initialization
        void Start()
        {
            context = System.Threading.SynchronizationContext.Current;
        }

        // Update is called once per frame
        void Update()
        {
            if (server == null)
            {
                if (controlWPFWindow != null)
                {
                    server = controlWPFWindow.server;
                    if (server != null)
                    {
                        server.ReceivedEvent += Server_Received;
                    }
                }
            }
        }
        private ISdkConfig LoadConfigFromTextAsset()
        {
            var asset = Resources.Load<TextAsset>("credential.json");
            if (asset == null)
            {
                throw new NullReferenceException("You have to place the credential.json.bytes in any of the Resources folders");
            }

            try
            {
                return OauthProvider.CreateSdkConfig(asset.text);
            }
            catch (SerializationException)
            {
                Debug.LogError($"Could not parse textAsset: {asset.text}");
                throw;
            }
        }

        private void Server_Received(object sender, DataReceivedEventArgs e)
        {
            context.Post(s =>
            {
                if (e.CommandType == typeof(PipeCommands.VRoidSDK_StartAuthenticate))
                {
                    StartAuthenticate();
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_RegisterCode))
                {
                    var d = (PipeCommands.VRoidSDK_RegisterCode)e.Data;
                    RegisterCode(d.Code);
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_DoLogin))
                {
                    DoLogin();
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_RequestAccountCharacterModels))
                {
                    RequestAccountCharacterModels();
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_RequestHearts))
                {
                    RequestHearts();
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_RequestRecommend))
                {
                    RequestRecommend();
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_RequestMoreModels))
                {
                    var d = (PipeCommands.VRoidSDK_RequestMoreModels)e.Data;
                    RequestMoreModels(d.ModelFilter);
                }
                else if (e.CommandType == typeof(PipeCommands.VRoidSDK_LoadModel))
                {
                    var d = (PipeCommands.VRoidSDK_LoadModel)e.Data;
                    LoadModel(d.id);
                }
            }, null);
        }

        public void RequestAccountCharacterModels()
        {
            // VRoid Hubにて、自身が制作し登録したキャラクターモデルの一覧を取得(次ページ用リンク付き)
            _api.GetAccountCharacterModels(
                10, // 最初の10件を取得
                (models, link) => GetModels_OnSuccess(models, link, PipeCommands.ModelFilters.Account, append: false),
                GetModels_OnError
            );
        }

        public void RequestHearts()
        {
            // VRoid Hubにて、ハートしたキャラクターモデルの一覧を取得 （※ 利用条件次第では含まれないものもある）
            _api.GetHearts(
                10,
                (models, link) => GetModels_OnSuccess(models, link, PipeCommands.ModelFilters.Heart, append: false),
                GetModels_OnError
            );
        }

        public void RequestRecommend()
        {
            // スタッフピック(おすすめ)の一覧を取得。旧実装のID固定バッチから正式なページ対応APIへ変更
            _api.GetStaffPicks(
                10,
                (staffPicks, link) => GetModels_OnSuccess(staffPicks.Select(x => x.character_model).ToList(), link, PipeCommands.ModelFilters.Recommend, append: false),
                GetModels_OnError
            );
        }

        /// <summary>
        /// スクロール最下部で、指定フィルタ(通常は一番下のグループ)の次ページを取得して末尾へ追記する
        /// </summary>
        public void RequestMoreModels(PipeCommands.ModelFilters filter)
        {
            if (_nextLinks.TryGetValue(filter, out var link) == false) return;
            if (link?.next == null) return;

            if (filter == PipeCommands.ModelFilters.Recommend)
            {
                link.next.RequestLink<List<StaffPicksCharacterModel>>(
                    (staffPicks, nextLink) => GetModels_OnSuccess(staffPicks.Select(x => x.character_model).ToList(), nextLink, filter, append: true),
                    GetModels_OnError);
            }
            else
            {
                link.next.RequestLink<List<CharacterModel>>(
                    (models, nextLink) => GetModels_OnSuccess(models, nextLink, filter, append: true),
                    GetModels_OnError);
            }
        }

        /* 正常にキャラクターの情報が取得できた時の処理。VRM1.0モデルも含めて全て返す */
        private async void GetModels_OnSuccess(List<CharacterModel> characterModels, ApiLinksFormat link, PipeCommands.ModelFilters modelFilter, bool append)
        {
            _characterModels.AddRange(characterModels);
            _nextLinks[modelFilter] = link;
            await server.SendCommandAsync(new PipeCommands.VRoidSDK_ReturnModels
            {
                ModelFilter = modelFilter,
                Models = ConvertCharacterModelListToPipe(characterModels),
                Append = append,
                HasNext = link != null && link.next != null,
            });
        }

        /* 通信エラーなどのエラーが発生した時の処理 */
        private async void GetModels_OnError(ApiErrorFormat errorFormat)
        {
            await server.SendCommandAsync(new PipeCommands.VRoidSDK_Error { Message = $"Code:{errorFormat.code} Message:{errorFormat.message}" });
        }

        public async void StartAuthenticate()
        {
            // 認証処理用インスタンスの初期化
            if (_oauthClient == null)
            {
                var config = LoadConfigFromTextAsset();
                var driver = new HttpClientDriver(context);
                _oauthClient = OauthProvider.CreateOauthClient(config, driver);
                _browser = BrowserProvider.Create(_oauthClient, config);
                _api = new DefaultApi(_oauthClient);
                _model = new ApiModel(_oauthClient.IsAccountFileExist());
                _characterModels = new List<CharacterModel>();
                _nextLinks = new Dictionary<PipeCommands.ModelFilters, ApiLinksFormat>();
                //VRM1.0のダウンロード/バージョン処理に正しく対応したSDK組み込みのModelLoaderを使用する
                ModelLoader.Initialize(config, _api, Application.productName);
            }

            if (_model.IsAuthorized())
            {
                GetAccountInfo((account) =>
                {
                    _model.CurrentUser = account;
                    _model.Active = false;
                    AfterAuthentication(true);
                }, async (error) =>
                {
                    // Get this error code if you could not get access token.
                    // It will open browser to re-authorize.
                    if (error.code == "AUTHORIZED_ERROR")
                    {
                        _model.ClearUserInfo();
                        _oauthClient.ReleaseAuthorizedAccount();
                    }
                    else
                    {
                        _model.ApiError = error;
                        await server.SendCommandAsync(new PipeCommands.VRoidSDK_Error { Message = error.ToString() });
                    }
                });
                return;
            }

            if (!_oauthClient.IsAccountFileExist())
            {
                // open login modal.
                _model.Active = true;
                _model.AuthorizationState = ApiModel.State.AUTHORIZATION_CODE_REQUESTED;
                await server.SendCommandAsync(new PipeCommands.VRoidSDK_NeedLogin { });
            }
        }

        public void DoLogin()
        {
            // このアプリケーションでは初めての認証である
            // ブラウザを開き、VRoid Hubサイト上にてアプリケーション連携の許可を得てから認証処理する
            // Open a browser and enter the code if it is not authorized.
            _oauthClient.Login(_browser, (_) =>
            {
                // Close login modal.
                _model.AuthorizationState = ApiModel.State.AUTHORIZED;
                GetAccountInfo((account) =>
                {
                    _model.CurrentUser = account;
                    AfterAuthentication(true);
                }, (error) =>
                {
                    _model.ApiError = error;
                    AfterAuthentication(false);
                });
            }, (e) =>
            {
                _model.AuthorizationState = ApiModel.State.CONNECTION_FAILED;
                AfterAuthentication(false);
            });
            //コードを入力(Register)されたらAfterAuthenticfation
        }
        private void GetAccountInfo(Action<Account> onGetAccount, Action<ApiErrorFormat> onFailed)
        {
            if (_model.CurrentUser != null)
            {
                onGetAccount(_model.CurrentUser);
                return;
            }

            _api.GetAccount(onGetAccount, onFailed);
        }

        public void RegisterCode(string code)
        {
            _browser?.OnRegisterCode(code);
        }


        // 認証完了後の処理
        private async void AfterAuthentication(bool isSuccess)
        {
            //isSuccessがtrueの時はログイン完了、falseの時はRegisterCodeを呼ばないといけない
            await server.SendCommandAsync(new PipeCommands.VRoidSDK_EndAuthenticate { IsSuccess = isSuccess });
        }

        public async void LoadModel(string id)
        {
            float lowprogress = 0.0f;
            try
            {
                var characterObj = await ModelLoader.LoadVrmAsync(
                    characterModel: _characterModels.First(d => d.id == id), // CharacterModel#id を渡す
                    onProgress: async (float progress) =>
                    {
                        progress = (int)(progress * 10) / 10f;
                        if (lowprogress != progress)
                        {
                            lowprogress = progress;
                            // VRMファイルがキャッシュされておらずダウンロードが必要な場合に、進捗状況が0.0〜1.0の間で通知される
                            await server.SendCommandAsync(new PipeCommands.VRoidSDK_ModelDownloadProgress { progress = progress });
                        }
                    }
                );

                // UniVRMでデシリアライズされたVRMファイルのGameObjectが返される
                await server.SendCommandAsync(new PipeCommands.VRoidSDK_ModelLoadComplete { });
                controlWPFWindow.LoadNewModel(characterObj);
            }
            catch (ModelLoadFailException error)
            {
                // 実行中にエラーが発生した場合、呼び出される
                await server.SendCommandAsync(new PipeCommands.VRoidSDK_Error { Message = error.Message });
            }
        }

        #region PipeConverters

        private List<PipeCommands.CharacterModel> ConvertCharacterModelListToPipe(List<CharacterModel> characterModels)
        {
            var list = new List<PipeCommands.CharacterModel>();
            foreach (var model in characterModels)
            {
                list.Add(ConvertCharacterModelToPipe(model));
            }
            return list;
        }

        private PipeCommands.WebImage ConvertWebImageToPipe(WebImage source)
        {
            return new PipeCommands.WebImage
            {
                height = source.height,
                url = source.url,
                url2x = source.url2x,
                width = source.width,
            };
        }

        private PipeCommands.PortraitImage ConvertPortraitImageToPipe(PortraitImage source)
        {
            return new PipeCommands.PortraitImage
            {
                original = ConvertWebImageToPipe(source.original),
                sq150 = ConvertWebImageToPipe(source.sq150),
                sq300 = ConvertWebImageToPipe(source.sq300),
                sq600 = ConvertWebImageToPipe(source.sq600),
                w300 = ConvertWebImageToPipe(source.w300),
                w600 = ConvertWebImageToPipe(source.w600),
            };
        }

        private PipeCommands.FullBodyImage ConvertFullBodyImageToPipe(FullBodyImage source)
        {
            return new PipeCommands.FullBodyImage
            {
                original = ConvertWebImageToPipe(source.original),
                w300 = ConvertWebImageToPipe(source.w300),
                w600 = ConvertWebImageToPipe(source.w600),
            };
        }

        private PipeCommands.CharacterLicense ConvertCharacterLicenseToPipe(CharacterLicense source)
        {
            if (source == null) return default(PipeCommands.CharacterLicense);
            return new PipeCommands.CharacterLicense
            {
                characterization_allowed_user = source.characterization_allowed_user,
                corporate_commercial_use = source.corporate_commercial_use,
                credit = source.credit,
                modification = source.modification,
                personal_commercial_use = source.personal_commercial_use,
                redistribution = source.redistribution,
                sexual_expression = source.sexual_expression,
                violent_expression = source.violent_expression,
            };
        }

        private PipeCommands.Tag ConvertTagToPipe(Tag source)
        {
            return new PipeCommands.Tag
            {
                en_name = source.en_name,
                ja_name = source.ja_name,
                locale = source.locale,
                name = source.name,
            };
        }

        private List<PipeCommands.Tag> ConvertTagListToPipe(List<Tag> source)
        {
            var list = new List<PipeCommands.Tag>();
            foreach (var t in source)
            {
                list.Add(ConvertTagToPipe(t));
            }
            return list;
        }

        private PipeCommands.AgeLimit ConvertAgeLimitToPipe(AgeLimit source)
        {
            return new PipeCommands.AgeLimit
            {
                is_adult = source.is_adult,
                is_r15 = source.is_r15,
                is_r18 = source.is_r18,
            };
        }

        private PipeCommands.UserIcon ConvertUserIconToPipe(UserIcon source)
        {
            return new PipeCommands.UserIcon
            {
                sq170 = ConvertWebImageToPipe(source.sq170),
                sq50 = ConvertWebImageToPipe(source.sq50),
            };
        }

        private PipeCommands.User ConvertUserToPipe(User source)
        {
            return new PipeCommands.User
            {
                icon = ConvertUserIconToPipe(source.icon),
                id = source.id,
                name = source.name,
                pixiv_user_id = source.pixiv_user_id,
            };
        }

        private PipeCommands.Character ConvertCharacterToPipe(Character source)
        {
            return new PipeCommands.Character
            {
                created_at = source.created_at,
                id = source.id,
                is_private = source.is_private,
                name = source.name,
                published_at = source.published_at,
                user = ConvertUserToPipe(source.user),
            };
        }

        private PipeCommands.CharacterVersion ConvertCharacterVersionToPipe(CharacterModelVersion source)
        {
            return new PipeCommands.CharacterVersion
            {
                created_at = source.created_at,
                id = source.id,
            };
        }

        private PipeCommands.CharacterModel ConvertCharacterModelToPipe(CharacterModel source)
        {
            var specVersion = source.getVRMVersion();
            return new PipeCommands.CharacterModel
            {
                age_limit = ConvertAgeLimitToPipe(source.age_limit),
                character = ConvertCharacterToPipe(source.character),
                created_at = source.created_at,
                download_count = source.download_count,
                full_body_image = ConvertFullBodyImageToPipe(source.full_body_image),
                heart_count = source.heart_count,
                id = source.id,
                is_downloadable = source.is_downloadable,
                is_hearted = source.is_hearted,
                is_private = source.is_private,
                latest_character_model_version = ConvertCharacterVersionToPipe(source.latest_character_model_version),
                license = ConvertCharacterLicenseToPipe(source.license),
                spec_version = specVersion,
                license_vrm10 = ConvertCharacterLicenseVRM10ToPipe(source, specVersion),
                name = source.name,
                portrait_image = ConvertPortraitImageToPipe(source.portrait_image),
                published_at = source.published_at,
                tags = ConvertTagListToPipe(source.tags),
                usage_count = source.usage_count,
                view_count = source.view_count,
            };
        }

        /// <summary>
        /// VRM1.0モデルのライセンスを正規化(SDKのWhat*()→EnumLicenseの文字列)してPipe構造体へ変換する
        /// </summary>
        private PipeCommands.CharacterLicenseVRM10 ConvertCharacterLicenseVRM10ToPipe(CharacterModel source, string specVersion)
        {
            if (specVersion != "1.0") return default(PipeCommands.CharacterLicenseVRM10);
            var vrmMeta = source.latest_character_model_version.vrm_meta;
            if (vrmMeta == null) return default(PipeCommands.CharacterLicenseVRM10);

            var l = new CharacterLicenseVRM10(vrmMeta);
            return new PipeCommands.CharacterLicenseVRM10
            {
                avatar_user = l.WhatCanUseAvatarByOtherUser().ToString(),
                violence = l.WhatCanUseViolence().ToString(),
                sexuality = l.WhatCanUseSexuality().ToString(),
                political_religious = l.WhatCanUseReligionOrPolitical().ToString(),
                antisocial_hate = l.WhatCanUseAntisocialOrHatred().ToString(),
                personal_commercial = l.WhatCanUseCommercial().ToString(),
                corporate_commercial = l.WhatCanUseCorporate().ToString(),
                redistribution = l.WhatRedistribution().ToString(),
                modification = l.WhatModification().ToString(),
                credit = l.WhatShowCredit().ToString(),
            };
        }

        #endregion
    }
}
#endif