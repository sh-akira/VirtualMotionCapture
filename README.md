# バーチャルモーションキャプチャー (VirtualMotionCapture)  
VRゲーム中にモデルをコントロール  
  
VRMのモデルファイルを読み込んで、3点～フルトラッキングで操作するアプリです。  
追加のモーションキャプチャーがなくても、動いている姿を見せることができます。  
もちろんVRゲーム中以外でも使用することができます。  
  
  
[公式ホームページはこちらです](https://sh-akira.github.io/VirtualMotionCapture/)
  
  
# VRゲーム中にも同時起動可能  
  
通常のUnityアプリと違い、VRゲームと同時起動ができます。  
HMDと両手コントローラーのみ、  
HMDと両手コントローラーと腰トラッカー、  
HMDと両手コントローラーと両足トラッカー、  
HMDと両手コントローラーと腰と両足トラッカー  
それぞれのキャリブレーションに対応しています。  
  
バーチャルモーションキャプチャーを最初に起動し、その後VRゲームを起動してください。  
  
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
OS: Windows 10 (1803)  
CPU:Core i7 8700k  
GPU: Geforce GTX1080Ti  
Mem: 16GB  
VR: HTC Vive + 3 tracker  
(Oculus Rift+Touchの3点と+Kinectでの6点での動作報告があります)  
(GTX1080 での動作も確認済み)  
(Vive Pro,i7-4790K,GTX980,Mem 8G, Win10 Proで動作確認されました)  
WinMR機器での動作確認もされています(必ず両手のコントローラーが必要です)  
  
  
まだテスト版です。テストが不十分の可能性が大いにあります。  
何か問題があった際は、Twitterでお問い合わせください。  
動作環境スペック報告もお待ちしています。  
[@sh_akira](https://twitter.com/sh_akira)  
  
  
  
# 更新履歴
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
  
  
・このリポジトリをクローンかダウンロードしてUnityで開いてください。  
・Unity 2018.1.6f1で開いてください。  
・Assets直下にExternalPluginsフォルダを作って、その下に  
　・OVRTracking (OVRTrackingライブラリ - 入れてあります)  
　・UnityNamedPipe (名前付きパイプライブラリ - 入れてあります)  
　・RootMotion ([Final IK 1.7](https://assetstore.unity.com/packages/tools/animation/final-ik-14290) - ※現在AssetStoreから入手できるのは`1.8`です)
　・SteamVR ([SteamVR Unity Plugin v1.2.3](https://github.com/ValveSoftware/steamvr_unity_plugin/releases/tag/1.2.3))  
　・VRM ([UniVRM-0.43_4725.unitypackage](https://github.com/dwango/UniVRM/releases))  
　・VRM.Samples ([UniVRM-RuntimeLoaderSample-0.43_4725.unitypackage](https://github.com/dwango/UniVRM/releases))  
　・Oculus ([Oculus Lipsync Unity 1.28.0](https://developer.oculus.com/downloads/package/oculus-lipsync-unity/1.28.0/))  
　・CorrectNormalMapImport ([CorrectNormalMapImport](https://github.com/d30835nm/CorrectNormalMapImport))  
以上のようなフォルダになるように各アセットをインポートしてください。  
・ControlWindowWPF/ControlWindowWPF.slnをVisual Studio 2017で開きます。  
・そのままビルドをするとexeが作成されます  
・UnityのPlayer Settingsを開き、Other SettingsのScripting Runtime Version を .NET 4.x Equivalent にして再起動  
・Unity側の実行  
・先ほどビルドしたexeを実行  
  
  
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
