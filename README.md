# バーチャルモーションキャプチャー (VirtualMotionCapture)  
VRゲーム中にモデルをコントロール  
  
VRMのモデルファイルを読み込んで、3点～10点フルトラッキングで操作するアプリです。  
追加のモーションキャプチャーがなくても、動いている姿を見せることができます。  
もちろんVRゲーム中以外でも使用することができます。  
  
  
[公式ホームページはこちらです](https://sh-akira.github.io/VirtualMotionCapture/)
  
  
# VRゲーム中にも同時起動可能  
  
通常のUnityアプリと違い、VRゲームと同時起動ができます。  
HMDと両手コントローラーのみ、  
HMDと両手コントローラーと腰トラッカー、  
HMDと両手コントローラーと両足トラッカー、  
HMDと両手コントローラーと腰と両足トラッカー  
これらにプラスして両肘トラッカー、両膝トラッカー、
それぞれのキャリブレーションに対応しています。  
全ての部位にHMD、コントローラー、トラッカーを自由に割り当て可能です。  
  
# 基本の操作方法  
起動するとコントロールパネルが表示されます。  
VRMモデル読み込みを押して、任意のVRMモデルを読み込んでください。  
モデルのライセンスが表示されますので、問題なければ同意しますを押してください。  
その後、モデル読み込みウインドウを閉じて、キャリブレーションボタンを押します。  
画面の指示通りに進めてください。  
デフォルトのキー設定はHTC Vive用になっていますので、  
Oculus Touchをお使いの場合はショートカットキー設定を開き、  
プリセットをOculusの物に変更してください。  
VRoid製のモデルを使用する場合、リップシンクが表情と競合しますので  
同様にショートカットキー設定を開き、VRoidのプリセットを使用してください  
  
モデルが表示されている画面をマウスで操作するとカメラがコントロールできます。  
ホイール：ズーム  
右クリック＋ドラッグ：カメラの移動  
左クリック＋ドラッグ：ウィンドウの移動  
  
# ダウンロード
ダウンロードページ：[https://sh-akira.booth.pm/items/999760]  
ダウンロードページかリリースページからVirtualMotionCapture0.xx.zipをダウンロードしてください。  
解凍後VirtualMotionCapture.exeを実行で開始します。  

[avast アバスト無料アンチウイルスにブロックされる場合はこちらをご覧ください](https://github.com/sh-akira/VirtualMotionCapture/blob/master/documents/avast.md)
  
    
    
テスト環境：  
OS: Windows 10 (1809)  
CPU:Core i9 9900k  
GPU: Geforce RTX2080Ti  
Mem: 16GB  
VR: HTC Vive + 11 tracker  
(Oculus Rift+Touchの3点と+Kinectでの6点での動作報告があります)  
(GTX1080 での動作も確認済み)  
(Vive Pro,i7-4790K,GTX980,Mem 8G, Win10 Proで動作確認されました)  
WinMR機器での動作確認もされています(必ず両手のコントローラーが必要です。キャリブレーション時に両手がトラッキングされている必要があります)  
また、WinMRの場合Tポーズが通常の方法では取れませんがカウントダウンの0秒時に一気に腕を開くことで疑似的にTポーズをとる必要があります  
Oculus Rift Sの動作確認済みです  
  
  
まだテスト版です。テストが不十分の可能性が大いにあります。  
何か問題があった際は、Twitterでお問い合わせください。  
動作環境スペック報告もお待ちしています。  
[@sh_akira](https://twitter.com/sh_akira)  
  
  
  
# 更新履歴
Ver 0.34  
・VIVE Pro Eyeサポート  
  
Ver 0.33  
・Tobii Eye Tracker 4Cサポート  
・UniVRM 0.53に更新  
  
Ver 0.32  
・CPU使用率を大幅に下げました  
・VRM読み込み時のファイルを開くダイアログが裏に行くときがある  
・設定読込/保存時のダイアログも裏に行く  
・[VRoid Hub]自分のモデルでも設定次第ではライセンス表記必須のメッセージが出てしまう  
・[VRoid Hub]VRoid Hubで探すボタンのリンク先をアプリページに変える  
・ロケーションによってexternalcamera.cfgのフォーマットに準拠しないファイルが生成される（ドットがカンマになる）  
・トラッカーオフセット設定を左右を連動してバーを動かす  
・設定保存に解像度も含める  
  
Ver 0.31  
・ライトの方向変更機能追加  
・ライトの色変更機能追加  
・しばらく使うとトラッカー選択が白くなる現象の修正  
  
Ver 0.30  
・フロント/バックカメラのブレ修正  
・フロント/バックカメラ移動がZ軸で回っていたのを修正  
  
Ver 0.29  
・フロントカメラの挙動修正(テスト版)  
  
Ver 0.28  
・モデル表示画面とコントロールパネルのアイコンを分けました  
・externalcamera.cfgの出力と設定保存をキャリブレーション前に行った場合に次の起動で設定を読み込むとカメラ位置がずれていた問題を修正  
・キャリブレーション時のモデルの手のひらを正しい方向(正面)に変更  

Ver 0.27  
・アイコン追加  
・ボタンのスタイル変更  
・再キャリブレーション時のコライダーサイズ修正  
・Final IKを1.8に更新  
・OVRLipSyncを1.30.0に更新  
・UniVRMを0.51に更新  
・[VRoid Hub]貼り付けボタン追加  
・[VRoid Hub]プラグイン0.16更新  

Ver 0.26  
・VRMを開く前に読み込みボタンを押すと固まる問題修正  
・2個以上トラッカーがあって足にトラッカー設定しない場合足が動かない問題修正  
・キャリブレーション後に手首の捻じれ補正が効かなかった問題修正  
・2回以上キャリブレーションを同じモデルで行うと手が入れ替わったりずれたりした問題修正  
  
Ver 0.25  
・VRoid Hubからのモデル読み込みに対応  
・キャリブレーション時にモデルをリロードしないように変更  
・UniVRM0.49対応  
・ローカルVRMを1度も読まずにVRoidHubからモデルを開くとキャリブレーションできなかった問題修正  
  
Ver 0.24  
・物理トラッカーへのexternalcamera.cfgの出力修正  
・モデル表示画面とコントロールパネルの通信方法を変更  
・v0.22からのOculusの認識修正  
  
Ver 0.23  
・FOV変更した設定データを起動直後に読み込むとカメラ位置が壊れる  
・設定読み込み直後の読み込み完了前に再度設定を読み込むとモデルが2体表示される  
  
Ver 0.22  
・画面の透過機能修正  
・OBSで複数起動キャプチャ対応のため、ウインドウタイトルに数字を追加  
・SteamVR起動後の後から起動に対応(最初に起動しなくて良くなりました)  
・キー設定でキーボード入力が正しく動くようになりました  
・マウスホイールでのカメラのズームがウインドウ外で発生しないように修正  
・新しいカメラ操作(Alt+左クリックドラッグ)の注視点を中心に回転を追加  
・カメラのFOV変更機能追加  
・externalcamera.cfg出力後にカメラの移動が出来ないように修正  
・写真撮影機能(高解像度/背景透過対応)追加  
・同じ名称のトラッカーが複数表示される問題修正  
  
Ver 0.21  
・トラッカー割り当て設定時のドロップダウン内も動かしたトラッカーが緑色になるように  
・externalcamera.cfgインポートのコントローラーやトラッカーを番号ではなく名前で選択できるように  
・externalcamera.cfgの出力機能追加  
・タイトルバーにバージョン番号の表示を追加  
・キャリブレーション時にLIVと名のついたコントローラーを除外するように  
  
Ver 0.20  
・キャリブレーション時にトラッカーの位置を白い球で表示  
・MR合成用の新しい二つのキャリブレーションを追加  
  
Ver 0.19  
・UniVRM0.45に対応  
・UnityCaptureとVMC_Cameraが競合していたのを修正  
・キャリブレーションをexternalcamera.cfgでずれないように修正  
  
Ver 0.18  
・ひじ/ひざのトラッキング  
・モデルのひざボーンの方向を自動修正  
・仮想Webカメラ機能追加  
・フルトラ時(ひざ無し)のひざ方向修正  
・肩の回転修正  
・過去のVRoid読み込み  
・リップシンクデバイス未選択時タブを警告色  
・メニューの日本語変換ミス修正  
・VRM読み込みUIに長文が入るとレイアウト崩れる問題修正  
  
Ver 0.17  
・起動後の操作でフリーズする問題修正  
・UniVRM 0.44に更新  
・VRoid向けNormalMap修正オプション削除  
  
Ver 0.16  
・膝が内側に曲がる  
・映像左右反転  
・English version  
・キャリブレーション時VRoidテカテカ  
・トラッカー4つ以上  
・頭だけ動かしたときにSpringBoneが動いてない  
  
Ver 0.15  
・NormalMapのテカテカ再度修正(VRoid v0.2.12向け)  
・OVRLipSyncを1.28に更新  
  
Ver 0.14  
・NormalMapのテカテカ修正(主にVRoid向け)  
・スケール変更時のコライダー修正  
  
Ver 0.13  
・手の角度調整が動かなくなっていたので修正  
・デフォルト-90,-90が0,0に変更になっています  
・手のトラッカーオフセット設定が正しく動いていなかったので修正  
  
Ver 0.12  
・頭、両手、腰、両足すべてのトラッカーを自由に選択できるようになりました  
・座標追従カメラを追加しました  
・UniVRMを0.43に更新  
  
Ver 0.11  
・キャリブレーション(頭と腰のスケール)修正  
・手首のねじれが起きないように補正するように  
・コントローラーの手首位置オフセットを少し修正  
・ハンドジェスチャーを設定時間アニメーションするように  
・足のトラッカーがない場合(3点や４点トラッキング)の時の歩幅や開き具合を調整  
・4点トラッキング(腰追加時)のフロント/バックカメラの追尾位置修正  
・ハンドジェスチャー設定時にVRoidモデルの手が画面に入りきらない問題修正  
・表情設定にリップシンク抑制追加(口を開けた表情/VRoid向け)  
・キャリブレーション開始時複数回トリガー押せないように  
・Oculus Touchのキャリブレーション開始トリガー修正  
・UniVRMを0.42に更新  
  
Ver 0.10  
・キャリブレーションの処理を変更しモデルのスケールをコントローラー位置にスケーリングするように  
・Oculus Touchの入力とプリセットを追加  
・キーの同時押しをしながら一部キーを離したときの処理を修正  
  
Ver 0.09  
・Oculus Touch確認のためにログ出力を入れた(テスト版)  
  
Ver 0.08  
・Oculus Touchの入力を暫定で入れた  
  
Ver 0.07  
・externalcamera.cfgの設定先コントローラー変更に対応  
・カメラが近づいたときに顔が非表示になってしまう時がある問題修正  
  
Ver 0.06  
・externalcamera.cfgファイルの読み込みに対応  
  
Ver 0.05  
・ショートカットキー機能  
・ハンドジェスチャー設定  
・表情設定  
・機能キー設定  
・手の角度の補正機能  
・キャリブレーション時にコントローラーのトリガーで開始できるように  
・UIをWinFormsからWPFの別アプリに変更  
・自動まばたきが完全に閉じなかったり開かなかったりする問題修正  
・使用しているVRIKCalibrator.csが直接編集されてしまっていたので修正  
  
Ver 0.04  
・自動まばたき機能追加(まばたきの細かいアニメーション時間設定ができます)  
・デフォルトの表情変更追加  
  
Ver 0.03  
・リップシンクに対応しました(低遅延・動きの設定が可能)  
・カメラのグリッド表示に対応  
・ウインドウのマウス操作の透過(透過ウインドウ時に下のウインドウを操作できるように)  
  
Ver 0.02  
・バーチャルモーションキャプチャー(命名:[ねこます](https://twitter.com/kemomimi_oukoku)さん)にすべて名称変更  
・設定読み込み時にチェックボックスが反映されない問題修正  
・設定保存時に選択中のカメラを保存するように  
・HMDと両手コントローラーと腰トラッカーのみ使用時、地に足付かなかった問題修正  
・ウィンドウをドラッグで移動時に初回の座標が飛ぶ問題修正  
・カメラの移動速度の感度を調整  
  
  
# ビルド手順  
ビルド環境：Unity 2018.1.6f1 / Visual Studio 2017 (Windowsデスクトップ開発パッケージ)  
  
  
・このリポジトリをクローンかダウンロードします。  
・ダウンロードした場合ColorPickerWPFフォルダがありませんので、忘れずにそちらもダウンロードして入れてください  
※以下、UnityはUnity 2018.1.6f1で開いてください。  
※以下のプラグインインポート前に、ProjectSettingsフォルダをコピーしてバックアップしてください！  
※API UpdaterはNo, Thanksで問題ありません。  
・Assets直下にExternalPluginsフォルダを作って、その下に  
　・OVRTracking (OVRTrackingライブラリ - 入れてあります)  
　・UnityMemoryMappedFile (共有メモリライブラリ - 入れてあります)  
　・VMC_Camera (仮想カメラライブラリ - 入れてあります)  
　・RootMotion(Plugins/RootMotionフォルダ) ([Final IK 1.9](https://assetstore.unity.com/packages/tools/animation/final-ik-14290))  
　・SteamVR ([SteamVR.Plugin.unitypackage](https://github.com/ValveSoftware/steamvr_unity_plugin/releases/tag/2.3.2))  
　・VRM ([UniVRM-0.53.0_6b07.unitypackage](https://github.com/vrm-c/UniVRM/releases))  
　・Oculus ([Oculus Lipsync Unity 1.30.0](https://developer.oculus.com/downloads/package/oculus-lipsync-unity/1.30.0/))  
　・uOSC([uOSC-v0.0.2.unitypackage](https://github.com/hecomi/uOSC/releases/tag/v0.0.2))  
以上のようなフォルダになるように各アセットをインポートしてください。  
アイトラッキングが不要な場合Assets\EyeTrackingフォルダを削除します  
アイトラッキング対応する場合は  
　・ViveSR([Vive-SRanipal-Unity-Plugin.unitypackage](https://hub.vive.com/en-US/profile/material-download)) SRanipal_SDK_1.0.1.0_Eye.zip内  
　・Tobii([TobiiUnitySDKForDesktop_4.0.3.unitypackage](https://github.com/Tobii/UnitySDK/releases)) アセットストアからインポート(インポートしたままフォルダは移動しないでください)  
以上の二つをインポート  
※インポートが終わったらUnityをいったん終了し、ProjectSettingsフォルダを削除して、バックアップしておいたProjectSettingsフォルダを戻してください！ 

・ControlWindowWPF/ControlWindowWPF.slnをVisual Studio 2017で開きます。  
・VirtualMotionCaptureControlPanelプロジェクトのプロパティを開きデバッグのコマンドライン引数を/pipeName VMCTestにする。  
・そのままVisualStudioで1回開始します。自動でexeが作成されます。開いたコントロールパネルは閉じて1回終了します。  
・Unityをもう一度起動します  
・UnityのConsoleを見てエラーが出ている個所4か所(ダブルクリックで開く)を  
　UnityEngine.VR.VRDeviceをUnityEngine.XR.XRDeviceに  
　UnityEngine.VR.VRSettingsをUnityEngine.XR.XRSettingsに変更して保存  
・まだエラーが残ってる場合はUnity再起動  
・ScenesフォルダのVirtualMotionCaptureシーンを開いてUnity側の実行  
・VisualStudioでコントロールパネルを開始  
  
  
# FAQ  
Q.アプリを使うのに表記はいりますか？  
A.特にいりませんが、あると嬉しいです。その場合は[Twitter](https://twitter.com/sh_akira)をお願いします。  
  ソースコードを使用する場合はMITライセンスです。  
  
Q.Oculus Homeからゲームを起動しようとすると、SteamVRを閉じてしまう  
A.Homeは使用せずにゲームのインストールフォルダから直接exeを実行で回避できる報告がありました  
  
Q.externalcamera.cfgはどうやって設定するの？  
A.みゅみゅさんの記事[【HTC Vive】コントローラ２本でクロマキー合成をする方法](https://qiita.com/miyumiyu/items/25deb3542e913750f519)を参照してください。  
バーチャルモーションキャプチャーのカメラ標準はfov=60です。  
実際にコントローラーを3つ繋いでいる場合は、3本目のコントローラーをカメラ代わりにすることができます。  
  
Q.externalcamera.cfgの位置がおかしい  
A.コントローラー番号を変更して再度ファイルを開いてみてください。  
  
Q.起動後に操作するとフリーズする  
A.VirtualMotionCaputre.exeと同じフォルダにあるdefault.jsonを別のフォルダに移動し、起動後に設定の読込ボタンからdefault.jsonを開くようにすると回避できる場合があります  
  
Q.支援先を教えてください  
A.[欲しいものリスト](https://t.co/KPJRzn6sVR) ギフト送付先(akira.satoh.sh[アットマーク]gmail.com)  
A.[BOOTH](https://sh-akira.booth.pm/items/999760)  
A.[pixivFANBOX](https://www.pixiv.net/fanbox/creator/10267568)  
