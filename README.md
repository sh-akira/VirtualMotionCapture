# バーチャルモーションキャプチャー (OVRCharacterController)  
VRゲーム中にモデルをコントロール  
  
VRMのモデルファイルを読み込んで、フルトラッキングで操作するアプリです。  
追加のモーションキャプチャーがなくても、動いている姿を見せることができます。

  
# VRゲーム中にも同時起動可能  
  
通常のUnityアプリと違い、VRゲームと同時起動ができます。  
HMDと両手コントローラーのみ、  
HMDと両手コントローラーと腰トラッカー(現在問題あり)、  
HMDと両手コントローラーと両足トラッカー、  
HMDと両手コントローラーと腰と両足トラッカー  
それぞれのキャリブレーションに対応しています。  
  
OVRCharacterControllerを最初に起動し、その後VRゲームを起動してください。  
  
# 基本の操作方法  
起動するとコントロールパネルが表示されます。  
VRMモデル読み込みを押して、任意のVRMモデルを読み込んでください。  
モデルのライセンスが表示されますので、問題なければ同意しますを押してください。  
その後、モデル読み込みウインドウを閉じて、キャリブレーションボタンを押します。  
画面の指示通りに進めてください。  
  
キャラクタが表示されている画面をマウスで操作するとカメラがコントロールできます。  
ホイール：ズーム  
右クリック＋ドラッグ：カメラの移動  
  
# ダウンロード
リリースページ：[https://github.com/sh-akira/OVRCharacterController/releases]  
ダウンロードはリリースページからOVRCharacterController.zipをダウンロードしてください。  
解凍後Build.exeを実行で開始します。  
  
  
テスト環境：  
OS: Windows 10 (1803)  
CPU:Core i7 8700k  
GPU: Geforce GTX1080Ti  
Mem: 16GB  
VR: Vive  
  
(GTX1080 での動作も確認済み)  
  
  
まだテスト版です。テストが不十分の可能性が大いにあります。  
何か問題があった際は、Twitterでお問い合わせください。  
動作環境スペック報告もお待ちしています。  
[@sh_akira](https://twitter.com/sh_akira)  
下記の問題は把握しています。  
  
  
●見つかった問題  
・腰のトラッカーのみでトラッキングした際に、地に足付かない。  
・設定読み込み時にチェックボックスが反映されない  
・複数起動時、ドラッグの挙動が怪しい  
  
●実装予定  
・アプリ名をバーチャルモーションキャプチャー(命名:[ねこます](https://twitter.com/kemomimi_oukoku)さん)に変える  
・リップシンク  
・カメラ設定時のグリッド  
・キャリブレーションの保存(複数起動を楽にしたい)  
  
●将来の実装予定  
・自身の状態をVR内にオーバーレイ表示  
  
  
  
# ビルド手順  
ビルド環境：Unity 2018.1.6f1 / Visual Studio 2017 (Windowsデスクトップ開発パッケージ)  
  
  
・このリポジトリをクローンかダウンロードしてUnityで開いてください。  
・Unity 2018.1.6f1で開いてください。  
・Assets直下にExternalPluginsフォルダを作って、その下に  
　・OVRTracking (OVRTrackingライブラリ - 入れてあります)  
　・RootMotion ([Final IK](https://assetstore.unity.com/packages/tools/animation/final-ik-14290))  
　・SteamVR ([SteamVR Plugin](https://assetstore.unity.com/packages/templates/systems/steamvr-plugin-32647))  
　・VRM ([UniVRM-0.38.unitypackage](https://github.com/dwango/UniVRM/releases))  
　・VRM.Samples ([UniVRM-RuntimeLoaderSample-0.38.unitypackage](https://github.com/dwango/UniVRM/releases))  
以上のようなフォルダになるように各アセットをインポートしてください。  
・ControlWindow/ControlWindow.slnをVisual Studio 2017で開きます。  
・そのままビルドをするとdllが作成され自動でAsset/Plugins/ControlWindow.dllにコピーされます  
・UnityのPlayer Settingsを開き、Other SettingsのScripting Runtime Version を .NET 4.x Equivalent にして再起動  
・実行  
  
  
# FAQ  
Q.アプリを使うのに表記はいりますか？  
A.特にいりませんが、あると嬉しいです。その場合はTwitterをお願いします。
