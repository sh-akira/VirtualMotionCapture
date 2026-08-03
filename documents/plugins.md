# 外部デバイスプラグイン

mocopi 連携・VIVE Pro Eye / VIVE Facial Tracker（SRanipal）・Tobii アイトラッキングは、
本体とは別にビルドするプラグインになっています。

入手に手間のかかる SDK への依存を本体から追い出すのが目的で、
**SDK が無くても本体はそのままクローンしてビルドできます**。

## Mod とプラグインの違い

ユーザーが作る Mod とは別系統です。混同しないよう、識別方法から配置場所まで分けてあります。

| | Mod（ユーザー製作） | プラグイン（公式提供） |
| --- | --- | --- |
| 配置 | `Mods/` | `Plugins/` と `ControlPanel/Plugins/` |
| 参照するAPI | `VMCMod.dll` | `VMC.PluginAPI.dll` / `VMC.ControlPanel.PluginAPI.dll` |
| 識別方法 | `[VMCPlugin]` 属性 | `IVMCPlugin` / `IControlPanelPlugin` の実装 |
| 読み込み時期 | コントロールパネル接続後・プレリリース版のみ | 本体初期化時（設定の適用より前）・常時 |
| VRoid Hub 連携 | Mod を読み込むと無効化される | 影響しない |
| コントロールパネルのUI | Mod一覧の Setting ボタンのみ | 設定画面の「外部デバイス」欄にボタンが並ぶ |

Mod の仕組み（`Assets/VMCMOD/`）は今まで通りで、既存の Mod はそのまま動きます。

## 構成

```
Plugins/                          本体側プラグイン（Unity）
  mocopi/
    VMC.Plugin.Mocopi.dll
  ViveSR/
    VMC.Plugin.ViveSR.dll
    native/                       SRanipalのネイティブDLL
  Tobii/
    VMC.Plugin.Tobii.dll
    native/                       TobiiのネイティブDLL
ControlPanel/Plugins/             コントロールパネル側プラグイン（WPF）
  mocopi/VMC.Plugin.Mocopi.UI.dll
  ViveSR/VMC.Plugin.ViveSR.UI.dll
  Tobii/VMC.Plugin.Tobii.UI.dll
```

**ネイティブDLLは必ず `native/` サブフォルダに置きます。** プラグインフォルダ直下は
マネージドDLLだけという決まりにしてあるので、読み込み側はファイルの中身を見て
振り分ける必要がなく、`native/` を `LoadLibrary`、直下を `Assembly.LoadFrom` と
単純に処理できます。

本体側とコントロールパネル側は同じ `Id` で対応付けます。片方しか入っていない場合は
設定画面のボタンが無効になり、ツールチップで理由が表示されます。

| Id | 内容 |
| --- | --- |
| `mocopi` | mocopi（UDP受信） |
| `ViveSR.Eye` | VIVE Pro Eye / Focus 3 / Droolon F1 |
| `ViveSR.Lip` | VIVE Facial Tracker |
| `Tobii` | Tobii Eye Tracker |

## 拡張点

プラグインは本体の `Assembly-CSharp` を参照せず、`VMC.PluginAPI` だけを見ます。

- `IFaceControl` — まばたき・表情の混ぜ込み・視線（`BeforeApply` の中で `SetLookAtPosition`）
- `IMotionSourceFactory` — 外部デバイスのボーン階層をアバターへ適用する
- `IPluginSettings` — 設定の保存（本体の設定プロファイルに含まれるので、プロファイル切り替えに追従します）
- `IPluginIpc` — コントロールパネルとの通信
- `NativeLibraryLoader` — `Plugins/` 配下のネイティブDLLの先読み

## ビルド手順

プラグインのビルドには SDK が必要です。開発者が自分で入手して配置してください。

1. 各SDKを、使うプラグインのプロジェクト直下の `SDK/` へ置きます
   （`SDK/` の中身は `.gitignore` 済みで、置き方の説明として `SDK/README.md` だけが入っています）。

   ```
   PluginProjects/
     VMC.Plugin.Mocopi/SDK/MocopiReceiver/   mocopi Receiver Plugin for Unity
     VMC.Plugin.ViveSR/SDK/ViveSR/           VIVE SRanipal SDK の ViveSR フォルダ
     VMC.Plugin.Tobii/SDK/Tobii/             Tobii Unity SDK の Tobii フォルダ
   ```

   - [mocopi Receiver Plugin for Unity](https://www.sony.net/Products/mocopi-dev/)
   - [VIVE SRanipal SDK](https://developer.vive.com/resources/vive-sense/eye-and-facial-tracking-sdk/)
   - [Tobii Unity SDK](https://developer.tobii.com/pc-gaming/unity-sdk/)

   SDKはそれを使うプラグインの中だけに閉じているので、
   1つのプラグインだけビルドしたい場合はそのSDKだけ用意すれば済みます。

2. Unity で一度プロジェクトを開きます。プラグインは Unity が生成した
   `Library/ScriptAssemblies/VMC.PluginAPI.dll` を参照するため、これが先に必要です。

3. コントロールパネル（`ControlWindowWPF/ControlWindowWPF.sln`）をビルドします。
   `VMC.ControlPanel.PluginAPI.dll` と `UnityMemoryMappedFile.dll` が生成されます。

4. プラグインをビルドします。

   ```bash
   msbuild PluginProjects/VMCPlugins.sln /t:Build /p:Configuration=Release
   ```

   出力先は自動で `UnityBuild/Plugins/`（本体側）と
   `BuildRootFiles/ControlPanel/Plugins/`（コントロールパネル側）になります。
   どちらも配布物の xcopy 対象なので、この後コントロールパネルをビルドすれば同梱されます。

## エディタ上での動作確認

プラグインはビルド済みのDLLを実行時に読み込む仕組みですが、
コピー先は **プラグインをビルドすれば自動で用意されます**。

| | コピー先 | 誰が入れるか |
| --- | --- | --- |
| 本体側(エディタ実行) | `<リポジトリ直下>/Plugins/` | プラグインのビルド時に自動 |
| 本体側(配布) | `UnityBuild/Plugins/` | プラグインのビルド時に自動 |
| コントロールパネル側 | `ControlWindowWPF/ControlWindowWPF/bin/Debug/ControlPanel/Plugins/` | `BuildRootFiles` からの xcopy で自動 |

エディタでは `Application.dataPath + "/../Plugins/"` がリポジトリ直下を指すため、
`<リポジトリ直下>/Plugins/` に置いておけば再生ボタンだけで読み込まれます。

### 手順

1. プラグインをビルドします。

   ```bash
   msbuild PluginProjects/VMCPlugins.sln /t:Build /p:Configuration=Release
   ```

2. コントロールパネルをビルドします（`ControlWindowWPF/ControlWindowWPF.sln`）。
   デバッグのコマンドライン引数は `/pipeName VMCTest` にしておきます。
3. Unity で `Assets/Scenes/VirtualMotionCapture` を開いて再生します。
   Console に `[Plugin] ... を読み込みました` が出れば読み込み成功です。
4. Visual Studio でコントロールパネルを開始して接続し、
   設定画面の「外部デバイス」欄にボタンが並ぶことを確認します。

プラグインを作り直したら、Unity の再生を止めてからビルドしてください
（再生中はDLLがロックされてコピーに失敗します）。

> `<リポジトリ直下>/Plugins/` は `.gitignore` 済みです。

## 新しいプラグインを追加するには

1. `PluginProjects/` に Unity側とコントロールパネル側の2プロジェクトを作ります
   （既存のものをコピーするのが早いです）。
2. Unity側は `MonoBehaviour` を継承しつつ `IVMCPlugin` を実装します。
   初期化は `Awake` ではなく `Initialize(IPluginHost)` に書いてください
   （`AddComponent` の時点ではまだ `host` を受け取っていないため）。
3. コントロールパネル側は `IControlPanelPlugin` を実装します。
   表示名は `Plugin_<Id>_Title` のリソースキーで、プラグイン自身の
   `Resources/{Japanese,English,Chinese,Korean}.xaml` に持たせます。
   本体のキーと衝突しないよう、プラグイン固有のキーには接頭辞を付けてください。
4. 独自のコマンドが必要な場合は、共有アセンブリ（`UnityMemoryMappedFile`）に
   `PipeCommands_<プラグイン名>.cs` を `partial class PipeCommands` として足します。
   受信側の型解決が `PipeCommands` のネスト型を走査する実装のためです。

## 注意点

- **ネイティブDLL**: `Plugins/` 配下は `DllImport` の探索パスに入りません。
  `NativeLibraryLoader.PreloadFrom` が `native/` 内のDLLを絶対パスで先読みします
  （`PluginManager` が自動で行うので、通常プラグイン側の対応は不要です）。
  一度プロセスへ読み込まれていれば、以降の `DllImport` は同じモジュールを使います。
  ネイティブDLLをフォルダ直下に置いてしまうと `Assembly.LoadFrom` の対象になり、
  Mono が `Could not load image ...` をコンソールへ出すので注意してください。
- **Tobii の EULA**: Tobii Unity SDK は EULA 同意マーカーを `Resources` から読みますが、
  プラグインDLLからは `Resources` を提供できないため、同意済みフラグを直接設定しています。
  Tobii プラグインを自前でビルド・配布する場合は、Tobii Unity SDK の EULA に同意した上で行ってください。
- **設定の移行**: 本体機能だった頃の設定（`Settings.mocopi_*` など）は、
  設定ファイルの読み込み時に一度だけプラグインの設定領域へ移されます（`PluginSettingsMigration`）。
  旧フィールドは古い設定ファイルを読めるように残してありますが、移行後は使われません。
