using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityMemoryMappedFile
{
    public partial class PipeCommands
    {

        public class VRoidSDK_Error
        {
            public string Message { get; set; }
        }

        //Unity側にVRoid SDKが組み込まれている(VMC_VROIDSDK定義)かを問い合わせる。
        //SDK未同梱のクローンでもビルドが通るよう、WPFはこの応答でVRoid Hubボタンの表示可否を決める。
        public class VRoidSDK_CheckAvailable { }
        public class VRoidSDK_ReturnAvailable
        {
            public bool Available { get; set; }
        }

        public class VRoidSDK_StartAuthenticate { }
        public class VRoidSDK_EndAuthenticate
        {
            public bool IsSuccess { get; set; }
        }

        public class VRoidSDK_NeedLogin { }
        public class VRoidSDK_DoLogin { }

        public class VRoidSDK_RegisterCode
        {
            public string Code { get; set; }
        }

        public class VRoidSDK_RequestAccountCharacterModels { }
        public class VRoidSDK_RequestHearts { }
        public class VRoidSDK_RequestRecommend { }
        //スクロール最下部での続き読み込み(指定フィルタの次ページを取得)
        public class VRoidSDK_RequestMoreModels
        {
            public ModelFilters ModelFilter { get; set; }
        }
        public class VRoidSDK_ReturnModels
        {
            public ModelFilters ModelFilter { get; set; }
            public List<CharacterModel> Models { get; set; }
            //trueの場合は既存リストへ追記(ページネーション)、falseの場合は同フィルタを置き換え
            public bool Append { get; set; }
            //このフィルタにさらに次ページがあるか
            public bool HasNext { get; set; }
        }

        public class VRoidSDK_LoadModel
        {
            public string id { get; set; }
        }
        public class VRoidSDK_ModelDownloadProgress
        {
            public float progress { get; set; }
        }
        public class VRoidSDK_ModelLoadComplete { }

        public enum ModelFilters
        {
            Account,
            Heart,
            Recommend,
        }

        #region VRoidStructures

        /// <summary>
        /// 画像データの情報
        /// </summary>
        public struct WebImage
        {
            /// <summary>
            /// 画像データへのリンク
            /// </summary>
            public string url;

            /// <summary>
            /// 2倍サイズへの画像リンク
            /// </summary>
            /// <remarks>
            /// 2倍サイズがない場合はnullを返す
            /// </remarks>
            public string url2x;

            /// <summary>
            /// 画像の幅
            /// </summary>
            public int width;

            /// <summary>
            /// 画像の高さ
            /// </summary>
            public int height;
        }

        /// <summary>
        /// バストアップ画像
        /// </summary>
        public struct PortraitImage
        {
            /// <summary>
            /// オリジナルの画像
            /// </summary>
            public WebImage original;

            /// <summary>
            /// 幅600の画像
            /// </summary>
            public WebImage w600;

            /// <summary>
            /// 幅300の画像
            /// </summary>
            public WebImage w300;

            /// <summary>
            /// 正方形の一辺が600の画像
            /// </summary>
            public WebImage sq600;

            /// <summary>
            /// 正方形の一辺が300の画像
            /// </summary>
            public WebImage sq300;

            /// <summary>
            /// 正方形の一辺が150の画像
            /// </summary>
            public WebImage sq150;
        }

        /// <summary>
        /// 全身画像
        /// </summary>
        public struct FullBodyImage
        {
            /// <summary>
            /// オリジナル画像
            /// </summary>
            public WebImage original;

            /// <summary>
            /// 幅600に変換された画像
            /// </summary>
            public WebImage w600;

            /// <summary>
            /// 幅300に変換された画像
            /// </summary>
            public WebImage w300;
        }

        /// <summary>
        /// キャラクターの利用条件
        /// </summary>
        public struct CharacterLicense
        {
            /// <summary>
            /// 改変
            /// </summary>
            /// <remarks>
            /// <para>allow: 許可</para>
            /// <para>disallow: 不可</para>
            /// <para>default: 未設定</para>
            /// </remarks>
            public string modification;

            /// <summary>
            /// 再配布
            /// </summary>
            /// <remarks>
            /// <para>allow: 許可</para>
            /// <para>disallow: 不可</para>
            /// <para>default: 未設定</para>
            /// </remarks>
            public string redistribution;

            /// <summary>
            /// クレジット表記
            /// </summary>
            /// <remarks>
            /// <para>necessary: 必須</para>
            /// <para>unnecessary: 不要</para>
            /// <para>default: 未設定</para>
            /// </remarks>
            public string credit;

            /// <summary>
            /// アバターとしての利用
            /// </summary>
            /// <remarks>
            /// <para>everyone: 全員に許可</para>
            /// <para>default: 未設定</para>
            /// <para>author: 作成者のみ</para>
            /// </remarks>
            public string characterization_allowed_user;

            /// <summary>
            /// 性的表現での利用
            /// </summary>
            /// <remarks>
            /// <para>allow: 許可</para>
            /// <para>disallow: 不可</para>
            /// <para>default: 未設定</para>
            /// </remarks>
            public string sexual_expression;

            /// <summary>
            /// 暴力表現での利用
            /// </summary>
            /// <remarks>
            /// <para>allow: 許可</para>
            /// <para>disallow: 不可</para>
            /// <para>default: 未設定</para>
            /// </remarks>
            public string violent_expression;

            /// <summary>
            /// 法人の商用利用
            /// </summary>
            /// <remarks>
            /// <para>allow: 許可</para>
            /// <para>disallow: 不可</para>
            /// <para>default: 未設定</para>
            /// </remarks>
            public string corporate_commercial_use;

            /// <summary>
            /// 営利目的での活動
            /// </summary>
            /// <remarks>
            /// <para>profit: 許可</para>
            /// <para>nonprofit: 非商用利用に限り許可</para>
            /// <para>disallow: 不可</para>
            /// <para>default: 未設定</para>
            /// </remarks>
            public string personal_commercial_use;
        }

        /// <summary>
        /// VRM1.0のキャラクター利用条件
        /// </summary>
        /// <remarks>
        /// 各項目はVRoid SDKの CharacterLicenseVRM10.What*() が返す EnumLicense を文字列化したもの。
        /// 値: ok(許可) / ng(不可) / need(必要) / noneed(不要) / profit(営利可) / nonprofit(非営利のみ) / notset(未設定)
        /// </remarks>
        public struct CharacterLicenseVRM10
        {
            /// <summary>他ユーザーによるアバター利用</summary>
            public string avatar_user;
            /// <summary>暴力表現</summary>
            public string violence;
            /// <summary>性的表現</summary>
            public string sexuality;
            /// <summary>政治・宗教用途</summary>
            public string political_religious;
            /// <summary>反社会的・憎悪表現用途</summary>
            public string antisocial_hate;
            /// <summary>個人の商用利用</summary>
            public string personal_commercial;
            /// <summary>法人の商用利用</summary>
            public string corporate_commercial;
            /// <summary>再配布</summary>
            public string redistribution;
            /// <summary>改変</summary>
            public string modification;
            /// <summary>クレジット表記</summary>
            public string credit;
        }

        /// <summary>
        /// タグの情報
        /// </summary>
        public struct Tag
        {
            /// <summary>
            /// タグ名
            /// </summary>
            public string name;

            /// <summary>
            /// タグのロケール
            /// </summary>
            public string locale;

            /// <summary>
            /// タグの英語表記
            /// </summary>
            public string en_name;

            /// <summary>
            /// タグの日本語表記
            /// </summary>
            public string ja_name;
        }

        /// <summary>
        /// 年齢制限
        /// </summary>
        public struct AgeLimit
        {
            /// <summary>
            /// R18
            /// </summary>
            public bool is_r18;

            /// <summary>
            /// R15
            /// </summary>
            public bool is_r15;

            /// <summary>
            /// 全年齢
            /// </summary>
            public bool is_adult;
        }

        /// <summary>
        /// ユーザのアイコン画像
        /// </summary>
        public struct UserIcon
        {
            /// <summary>
            /// 正方形の一辺が170の画像
            /// </summary>
            public WebImage sq170;

            /// <summary>
            /// 正方形の一辺が50の画像
            /// </summary>
            public WebImage sq50;
        }

        /// <summary>
        /// ユーザの情報
        /// </summary>
        public struct User
        {
            /// <summary>
            /// ユーザID
            /// </summary>
            public string id;

            /// <summary>
            /// ピクシブアカウントのユーザID
            /// </summary>
            public string pixiv_user_id;

            /// <summary>
            /// ユーザ名
            /// </summary>
            public string name;

            /// <summary>
            /// ユーザのアイコン
            /// </summary>
            public UserIcon icon;
        }

        /// <summary>
        /// キャラクター情報
        /// </summary>
        public struct Character
        {
            /// <summary>
            /// キャラクターのID
            /// </summary>
            public string id;

            /// <summary>
            /// キャラクター名
            /// </summary>
            public string name;

            /// <summary>
            /// 非公開かどうか
            /// </summary>
            public bool is_private;

            /// <summary>
            /// 作成日時
            /// </summary>
            public string created_at;

            /// <summary>
            /// 公開した日時
            /// </summary>
            public string published_at;

            /// <summary>
            /// 投稿したユーザ
            /// </summary>
            public User user;

            /// <summary>
            /// キャラクターを作成した日時を取得する
            /// </summary>
            /// <remarks>
            /// created_atがnullか空文字だった場合は、nullを返す
            /// </remarks>
            /// <returns>作成日時</returns>
            public DateTime? CreatedAt()
            {
                if (string.IsNullOrEmpty(created_at))
                {
                    return null;
                }
                return DateTime.Parse(created_at);
            }

            /// <summary>
            /// キャラクターを公開した日時を取得する
            /// </summary>
            /// <remarks>
            /// published_atがnullか空文字だった場合は、nullを返す
            /// </remarks>
            /// <returns>公開日時</returns>
            public DateTime? PublishedAt()
            {
                if (string.IsNullOrEmpty(published_at))
                {
                    return null;
                }
                return DateTime.Parse(published_at);
            }
        }

        /// <summary>
        /// キャラクターのバージョン
        /// </summary>
        public struct CharacterVersion
        {
            /// <summary>
            /// バージョンID
            /// </summary>
            public string id;

            /// <summary>
            /// 作成日時
            /// </summary>
            public string created_at;

            /// <summary>
            /// 作成日時を取得する
            /// </summary>
            /// <returns>作成日時</returns>
            public DateTime? CreatedAt()
            {
                if (string.IsNullOrEmpty(created_at))
                {
                    return null;
                }
                return DateTime.Parse(created_at);
            }
        }

        /// <summary>
        /// キャラクターモデルデータ
        /// </summary>
        public struct CharacterModel
        {
            /// <summary>
            /// キャラクターモデルID
            /// </summary>
            public string id;

            /// <summary>
            /// モデルの名前
            /// </summary>
            public string name;

            /// <summary>
            /// 非公開かどうか
            /// </summary>
            public bool is_private;

            /// <summary>
            /// ダウンロードが可能か
            /// </summary>
            public bool is_downloadable;

            /// <summary>
            /// 自分がこのモデルに対しハートしたか
            /// </summary>
            public bool is_hearted;

            /// <summary>
            /// バストアップ画像
            /// </summary>
            public PortraitImage portrait_image;

            /// <summary>
            /// 全身画像
            /// </summary>
            public FullBodyImage full_body_image;

            /// <summary>
            /// モデルの作成日時
            /// </summary>
            public string created_at;

            /// <summary>
            /// ハートされている数
            /// </summary>
            public long heart_count;

            /// <summary>
            /// ダウンロードされた数
            /// </summary>
            public long download_count;

            /// <summary>
            /// モデル利用のためにダウンロードライセンスを発行した数
            /// </summary>
            public long usage_count;

            /// <summary>
            /// 閲覧数
            /// </summary>
            public long view_count;

            /// <summary>
            /// 公開日時
            /// </summary>
            public string published_at;

            /// <summary>
            /// 利用条件(VRM0.x)
            /// </summary>
            public CharacterLicense license;

            /// <summary>
            /// VRMのバージョン ("0.0" または "1.0")
            /// </summary>
            public string spec_version;

            /// <summary>
            /// 利用条件(VRM1.0)。spec_versionが"1.0"のときに設定される
            /// </summary>
            public CharacterLicenseVRM10 license_vrm10;

            /// <summary>
            /// 設定されているタグ
            /// </summary>
            public List<Tag> tags;

            /// <summary>
            /// 年齢制限
            /// </summary>
            public AgeLimit age_limit;

            /// <summary>
            /// 紐づいているキャラクター
            /// </summary>
            public Character character;

            /// <summary>
            /// モデルのバージョン
            /// </summary>
            public CharacterVersion latest_character_model_version;

            /// <summary>
            /// モデルを作成した日時を取得する
            /// </summary>
            /// <returns>作成日時</returns>
            public DateTime? CreatedAt()
            {
                if (string.IsNullOrEmpty(created_at))
                {
                    return null;
                }
                return DateTime.Parse(created_at);
            }

            /// <summary>
            /// モデルを公開した日時を取得する
            /// </summary>
            /// <remarks>
            /// published_atがnullか空文字だった場合は、nullを返す
            /// </remarks>
            /// <returns>公開日時</returns>
            public DateTime? PublishedAt()
            {
                if (string.IsNullOrEmpty(published_at))
                {
                    return null;
                }
                return DateTime.Parse(published_at);
            }
        }
        #endregion
    }
}
