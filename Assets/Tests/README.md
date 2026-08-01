# VMC 自動テストハーネス

実機のVR機器・コントロールパネル(WPF)・ネットワークを使わずに、
**本番のシーンをそのまま動かして**アバターの挙動を検証するためのE2Eテストハーネス。

## 仕組み

| 検証したいもの | 実機の代わりに使うもの |
| --- | --- |
| VR機器(HMD/コントローラ/トラッカー) | VMCProtocolのトラッカーメッセージを注入 |
| VMCProtocolの受信 | `uOscServer.onDataReceived` を直接叩く(UDP不使用) |
| VMCProtocolの送信 | `ExternalSender.SendHook` でバイト列を捕まえ、uOSCのParserで読み戻す |
| コントロールパネル(WPF) | `server.IsConnected = false` にして送信自体を止める |

> **なぜパイプを止めるのか**: `MemoryMappedFileServer` は相手が居なくても `IsConnected = true` になる。
> その状態で `SendCommand` を2回呼ぶと、1回目に立てた完了フラグを誰もクリアしないため
> `while (senderAccessor.ReadByte(0) == 1) Thread.Sleep(1);` で永久に待つ。
> 結果、`await` 側(`ImportVRM` 等)が返らずテストが進まなくなり、
> さらに再生停止時の `OnApplicationQuit` が**同期の** `SendCommand` を呼ぶためメインスレッドごと固まる。
> ハーネスは最初にこれを無効化し、**再生セッション中は元に戻さない**(戻すと停止時に固まるため)。

UDPを介さないため**フレーム単位で決定論的**に再現できる。
さらに `Time.captureDeltaTime` の固定、乱数シードの固定、まばたきの停止、
受信トラッカーのローパスフィルタの無効化により、実行のたびに同じ結果が出るようにしている。

## 検証方法(ゴールデンスナップショット)

各段階で次の情報を1つのJSONに固めて、前回の結果(ゴールデン)と比較する。

- ルートとHumanoid全ボーンのローカル姿勢
- 表情の**最終的な重み**(`Vrm10RuntimeExpression.ActualWeights` = LookAtやOverride適用後)
- 視線の yaw / pitch
- その区間にVMCProtocolとして送信されたOSCメッセージ

比較は許容誤差つき(位置は距離、回転は角度)。
毎回変わる `/VMC/Ext/T` や絶対パスを含む `/VMC/Ext/VRM` `/VMC/Ext/Config` は比較対象外。

### ゴールデンだけでは足りない

ゴールデン比較は「前回と同じか」しか見ないので、**壊れた状態が期待値として保存されると
以後ずっとPASSし続ける**。そこで各シナリオは、ゴールデンに依存しない不変条件も
`result.CheckThat(...)` で検査する。

- 注入したトラッカー姿勢が `TrackingPointManager` に届いているか
- トラッカーを動かしたときアバターのボーンが実際に回転するか
- 受信した表情/視線の値が `ActualWeights` / `LookAt.Yaw` に出ているか
- 送信された `/VMC/Ext/Bone/Pos` が実際のボーン姿勢と一致するか

シナリオを追加するときも、この手の「意味の検査」を必ず1つ以上入れること。

## 準備

1. テスト用のVRMを用意する(ライセンスの都合でリポジトリには含めない)
2. 一度メニューを実行するか `VMC/自動テスト/設定ファイルを開く` で
   `TestData/vmctest.json` の雛形を作り、パスを記入する

```json
{
    "Vrm0Path": "TestData/Models/sample_vrm0.vrm",
    "Vrm10Path": "TestData/Models/sample_vrm10.vrm",
    "GoldenDirectory": "TestData/Golden",
    "OutputDirectory": "TestData/Results",
    "UpdateGolden": false,
    "PositionTolerance": 0.001,
    "RotationToleranceDegrees": 0.2,
    "WeightTolerance": 0.002,
    "Seed": 12345,
    "FixedDeltaTime": 0.016666668
}
```

パスはプロジェクト直下(`VirtualMotionCapture/`)からの相対パスか絶対パス。
VRMが見つからないモデル種別は自動的にSKIPになる。

> **パス区切りに注意**: JSONなので `\` は `\\` にエスケープが必要
> (`"C:\\Users\\me\\model.vrm"`)。`/` で書けばエスケープ不要
> (`"C:/Users/me/model.vrm"`)。`\` を1つで書くとJSONのパースに失敗し、
> 設定が既定値に戻って全シナリオがSKIPになる。

## 実行

### Unity Editor

メニュー `VMC/自動テスト/` から実行する。再生モードに入り、
コンソールに結果が出て `TestData/Results/report.txt` が書き出される。

- `全シナリオを実行` … ゴールデンと比較する
- `全シナリオを実行(ゴールデンを更新)` … 現在の結果でゴールデンを上書きする
- `VRM0.xのみ実行` / `VRM1.0のみ実行`

**初回はゴールデンが無いので、その時の結果がそのままゴールデンとして保存される。**
保存された内容が妥当かどうかは一度目視で確認すること。

### ビルド済みexe / CI

```bash
VirtualMotionCapture.exe -vmctest -vmctest-config TestData/vmctest.json
```

| 引数 | 意味 |
| --- | --- |
| `-vmctest` | テストを実行する(これが無いと通常起動) |
| `-vmctest-scenarios A,B` | 実行するシナリオ名(省略時は全部) |
| `-vmctest-models vrm0,vrm10` | 対象モデル(省略時は全部) |
| `-vmctest-updategolden` | 比較せずゴールデンを更新する |
| `-vmctest-config <path>` | 設定ファイルのパス |
| `-vmctest-noquit` | 終了後にアプリを終了しない |

失敗があると終了コード1で終了する。

## コンパイルだけ確認したいとき

Unity Editor を開いたままだと `-batchmode` は起動できない
(`HandleProjectAlreadyOpenInAnotherInstance` でクラッシュする)。
Unityが生成する `Assembly-CSharp.csproj` を MSBuild でビルドすれば、
Editorを閉じずにコンパイルエラーだけ確認できる。

```bash
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" Assembly-CSharp.csproj /t:Build /v:minimal
```

## ファイル構成

| ファイル | 役割 |
| --- | --- |
| `VMCTestConfig.cs` | 設定(VRMのパス、許容誤差、ゴールデンの場所) |
| `VMCTestSnapshot.cs` | スナップショットの形式・保存・比較 |
| `VMCTestOsc.cs` | VMCProtocolメッセージの組み立て・注入・送信キャプチャ |
| `VMCTestContext.cs` | アプリ操作ヘルパー(モデル読込/受信機作成/キャリブレーション/フレーム進行) |
| `VMCTestScenario.cs` | シナリオ基底とレポート生成 |
| `VMCTestRunner.cs` | ランナー(コマンドライン引数 / Editorメニューの要求で起動) |
| `Scenarios/` | 各シナリオ(下記) |
| `Editor/VMCTestEditorMenu.cs` | Editorメニュー |

## シナリオ一覧

すべて VRM0.x と VRM1.0 の両方で実行される。

| シナリオ | 内容 | モデル |
| --- | --- | --- |
| `BasicVMCProtocol` | VRM読込 → トラッカー受信 → キャリブレーション → 追従 → 表情/LookAt受信 → 送信 | 両方 |
| `VMCProtocolBoneRoundTrip` | 既知のボーン姿勢を受信し、同じ値が送信されて出るか(VMCの数珠つなぎ) | 両方 |
| `VMCProtocolSendCoverage` | 送信されうる全アドレスが実際に出ているか | 両方 |
| `VMCProtocolControlMessages` | カメラ画角/ライト/送信周期/スルー/入力/リモートキャリブの受信 | 両方 |
| `MotionVrmaRoundTrip` | 記録 → VRMA書き出し → 読み込みでボーン・表情・視線が復元されるか | 両方 |
| `BvhExport` | 記録 → BVH書き出し → 読み込みで見た目の姿勢が保たれるか | VRM0.x |
| `SettingsSaveLoad` | 設定の保存 → 再読み込みで全項目が往復し、アバターとキャリブレーションが復元されるか | 両方 |
| `SettingsMigration` | 旧バージョン(v0.55)の設定ファイルからの移行と二重移行の防止 | VRM0.x |
| `PipeCommandsSerialization` | コントロールパネルとの通信コマンド全型のシリアライズ往復 | 不要 |
| `ModelSwitch` | 別アバター(VRM0.x⇔VRM1.0)に差し替えたときの自動再キャリブレーションと追従・表情・視線の引き継ぎ | 両方 |
| `MultipleReceivers` | 受信機を2つ使ったときの担当範囲の分離と独立性 | VRM0.x |
| `FaceMixing` | ベース/加算/上書きの合成順序、クランプ、VRM0.x名での指定 | 両方 |
| `BlinkFrameDrop` | 処理落ちでまばたきを飛び越しても目が開いた状態に戻るか | 両方 |
| `KeyActions` | ショートカットキーによる表情・機能の実行と同時押しの優先 | VRM0.x |
| `FaceHardwareInputs` | リップシンク(viseme)・リップトラッキング・アイトラッキングの反映 | 両方 |
| `MocopiReceive` | mocopiのスケルトン受信とアバターへの適用 | VRM0.x |
| `VMTSend` | Virtual Motion Trackerへの送信内容 | VRM0.x |
| `DeviceInfoTracking` | トラッキングの飛び検出・復帰補間・一時停止 | 不要 |
| `Robustness` | 壊れたOSC・壊れたVRM・存在しない設定ファイルで落ちないか | VRM0.x |
| `RenderingAndStability` | 写真撮影・スプリングボーン・モデル入れ替えのリーク・処理時間 | VRM0.x |

`ModelSwitch` は `ModelKey` が読み込み元、もう一方が切り替え先になるので、
VRM0.x→VRM1.0 と VRM1.0→VRM0.x の両方向が検証される。

## VMCProtocol の網羅状況

`ExternalSender` / `ExternalReceiverForVMC` のソースから抽出した全アドレス。

### 送信 (ExternalSender)

| アドレス | テスト |
| --- | --- |
| `/VMC/Ext/OK` `/VMC/Ext/T` `/VMC/Ext/Root/Pos` `/VMC/Ext/Bone/Pos` | SendCoverage(有無) + BasicVMCProtocol / BoneRoundTrip(値) |
| `/VMC/Ext/Blend/Val` `/VMC/Ext/Blend/Apply` | SendCoverage + BasicVMCProtocol(値) |
| `/VMC/Ext/Cam` | SendCoverage + ControlMessages(画角の値・座標系・往復) |
| `/VMC/Ext/Hmd/Pos` `/Local` `/VMC/Ext/Con/Pos` `/Local` `/VMC/Ext/Tra/Pos` `/Local` | SendCoverage + BasicVMCProtocol(値) |
| `/VMC/Ext/Rcv` `/VMC/Ext/Light` `/VMC/Ext/Setting/Color` `/VMC/Ext/Setting/Win` `/VMC/Ext/Config` `/VMC/Ext/Opt` `/VMC/Ext/VRM` | SendCoverage(有無) |
| `/VMC/Ext/Con` `/VMC/Ext/Key` `/VMC/Ext/Midi/Note` `/VMC/Ext/Midi/CC/Val` `/VMC/Ext/Midi/CC/Bit` | SendCoverage(有無) |
| `/VMC/Thru/*` の転送 | ControlMessages |
| `/VMC/Ext/Remote` | **未カバー**(VRoid Hub から読み込んだ時だけ送られるため、SDKとログインが要る) |

### 受信 (ExternalReceiverForVMC)

| アドレス | テスト |
| --- | --- |
| `/VMC/Ext/Hmd/Pos` `/VMC/Ext/Con/Pos` `/VMC/Ext/Tra/Pos` | BasicVMCProtocol |
| `/VMC/Ext/Root/Pos` `/VMC/Ext/Bone/Pos` | VMCProtocolBoneRoundTrip |
| `/VMC/Ext/Blend/Val` `/VMC/Ext/Blend/Apply` `/VMC/Ext/Set/Eye` | BasicVMCProtocol |
| `/VMC/Ext/Cam` `/VMC/Ext/Light` `/VMC/Ext/Set/Period` `/VMC/Ext/Set/Req` `/VMC/Ext/Set/Res` | ControlMessages |
| `/VMC/Ext/Con` `/VMC/Ext/Key` `/VMC/Ext/Midi/CC/Val` `/VMC/Thru/*` | ControlMessages |
| `/VMC/Ext/Set/Calib/Ready` `/VMC/Ext/Set/Calib/Exec` | ControlMessages |
| `/VMC/Ext/Set/Config` | **間接的**(SettingsSaveLoad が同じ `LoadSettings` を直接呼んでいる) |
| `/VMC/Ext/OK` | **未カバー**(受信側の内部状態=キャリブ完了検出にしか使われず、外から観測しづらい) |

### カメラについて分かっていること

- 受信した画角は `ControlCamera.fieldOfView` にだけ入り、`Settings.Current.CameraFOV` や
  各カメラリグの `currentFOV` は更新されない。送信側が毎フレーム送るので実害は無いが、
  **受信側のコントロールパネルに表示される画角は自分の値のまま**になる
- `Camera.fieldOfView` は**垂直**画角。送受信のウインドウ解像度(アスペクト比)が違うと
  同じ垂直画角でも水平方向の写る範囲が変わる。これはプロトコルの仕様上の制約で、コードの不具合ではない
- `/VMC/Ext/Cam` の座標は `IKManager.HandTrackerRoot` から見た**ローカル座標**。
  この親はキャリブレーションで身長比のスケールとオフセットを持つため、ローカルで受け取ることで
  送られてきた座標が**受信側アバターのスケールへ写像される**(2021-03 の `58efc0a` で受信側をこの形にした)。
  送信側も同じ座標系で送らないと、VMC同士でスケールが二重に掛かってカメラ距離がずれる。
  ControlMessages シナリオは `HandTrackerRoot` にあえて非単位のスケール・オフセットを入れて
  この食い違いを検出する(ワールドとローカルが同値だと検出できないので、前提自体もチェックしている)

## シナリオの追加方法

`VMCTestScenario` を継承して `Scenarios/` に置き、
`VMCTestRunner.AllScenarios` に追加する。

```csharp
public sealed class Scenario_Something : VMCTestScenario
{
    public override string Name => "Something";

    public override IEnumerator Run(VMCTestContext context, VMCTestResult result)
    {
        context.ResetSettings();
        yield return context.LoadModel(context.Config.GetModelPath(context.ModelKey));
        // ...
        result.CheckSnapshot(context, context.Capture("01_something"));
    }
}
```

シナリオ内で例外を投げるとその実行は失敗として記録される
(入れ子のコルーチンの例外も `VMCTestRunner.Drive` が拾う)。

## 本番コードに入れたテスト用の穴

最小限だけ。いずれも通常動作には影響しない。

- `ExternalSender.SendHook` … 送信内容のキャプチャ用の static event。通常は誰も購読していない
- `ControlWPFWindow.Test_*` … `internal` のアクセサ(CurrentModel / 受信機の追加 / 設定の保存 / MotionPlayer / MotionRecorder)
- `MotionRecorder.Test_*` … `internal` のアクセサ(記録状態 / フレーム数 / 書き出し)
- `MotionPlayer.ApplyPoseByPathAsync` … 既存の `ApplyPoseByPath`(async void)を待機可能にした版。
  `ApplyPoseByPath` はこれを呼ぶだけになっており、動作は変わらない
- `CameraManager.Test_SetCameraFOV` … `internal` のアクセサ
- `DynamicOVRLipSync.ApplyVisemes` / `Test_ApplyVisemes` … visemeの加工処理を `Update` から切り出したもの。
  マイクが無くても同じ経路を通せる(`Update` はこれを呼ぶだけになっており、動作は変わらない)
- `LipTracking_Vive.Test_ApplyLipWeights` … シェイプ名と重みを直接与える `internal` メソッド
- `EyeTracking_ViveProEye.Test_ApplyEyeState` … まぶたの開き具合と視線方向を直接与える `internal` メソッド
- `MocopiConnector.UpdateSkeletonForTest` … UDPの代わりにフレームデータを流し込む `internal` メソッド
- `VMTClient.SendHook` … 送信内容のキャプチャ用の static event。通常は誰も購読していない
- `AnimationController.TestTimeProvider` … 時計の差し替え用の static デリゲート。
  通常は null で `Time.realtimeSinceStartup` が使われる。まばたきは全体で0.19秒しかなく、
  処理落ちを実時間で再現できないため、`BlinkFrameDrop` はこれで疑似時計を与えて
  「1フレームで0.333秒進んだ」状況を決定論的に作る

### ハードウェアが無くても検査できる範囲

境界(SDKから値が返る地点)に注入しているので、実機が要るのは
**「デバイスが繋がるか」「ドライバが応答するか」だけ**になっている。

| 対象 | 注入する地点 | 実機でしか確認できないこと |
| --- | --- | --- |
| VR機器 | VMCProtocolのトラッカーメッセージ | SteamVRとの接続 |
| mocopi | `MocopiConnector.InitializeSkeleton` / `UpdateSkeletonForTest` | センサーとの接続 |
| マイク(リップシンク) | `DynamicOVRLipSync.Test_ApplyVisemes` | マイク入力とOVRLipSyncの解析 |
| Viveリップトラッキング | `LipTracking_Vive.Test_ApplyLipWeights` | SRanipalとの接続 |
| Vive Pro Eye | `EyeTracking_ViveProEye.Test_ApplyEyeState` | SRanipalとの接続 |
| MIDI | `MidiCCWrapper` のデリゲート | MIDIデバイスとの接続 |
| VMTドライバ | `VMTClient.SendHook` | ドライバ側の受信 |
| コントロールパネル | `PipeCommands` のシリアライズ往復 | WPF側のUI動作 |

なお、ハーネスはテスト中に `Settings/common.json`(起動時に読み込む設定ファイルのパス)を
退避して終了時に戻す。`SaveSettings` / `LoadSettings` がここを書き換えるため、
テストで作った設定ファイルが次回のVMC起動時に読まれてしまうのを防いでいる。

## 既知の制限

- `Time.realtimeSinceStartup` に依存する箇所(受信遅延バッファ、`enableLocalHandFix` の5秒判定)は
  実時間に依存する。前者は `DelayMs = 0` で回避しているが、後者は長いシナリオでは影響しうる
- `Application.targetFrameRate` は**必ず正の値**にすること。`DeviceInfo.updateOkTime()` が
  `okTime = validFrames / Application.targetFrameRate` を計算しているため、`-1`(制限なし)にすると
  `okTime` が負になり、トラッキング復帰の補間係数が常に0になって
  **トラッカーの姿勢が最初の値で永久に固定される**(見た目上は何のエラーも出ない)
- トラッカーは認識から1秒(`DeviceInfo.LEAP_SECONDS`)の間、飛び対策で過去値から補間される。
  注入した姿勢をそのまま使いたい場合は `context.WaitTrackingWarmup()` で待つこと
- スプリングボーンや実際の描画結果は検証していない(ボーン・表情・視線・送信データのみ)
- **Humanoidのリターゲットは腕のツイストを上腕と手の間で配分し直す**。
  VRMのアバターとVRMAから作ったアバターで twist 設定が違うため、
  末端(頭・手・足)の向きが完全に一致していてもボーン単位では数度ずれる(実測で最大5度)。
  そのため `MotionVrmaRoundTrip` は「末端の向き」(厳しく1度)と「ボーン単位」(緩く8度)を分けて検査する。
  ボーン単位だけを見ると、見た目が同じでも落ちてしまう
- 同様に、Unity Humanoidのマッスル空間は可動範囲が限られており、
  腕を下ろした姿勢の肩・上腕と、指(特に親指)は元の回転をそのまま表現できない。
  記録→再生の総合誤差は `MotionRetargetToleranceDegrees` / `MotionFingerToleranceDegrees` で別枠にしている
- BVH書き出しは未検証(VRMAのみ)。スプリングボーンと実際の描画結果も対象外
