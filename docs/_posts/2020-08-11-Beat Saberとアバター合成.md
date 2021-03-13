---
weight: 100
date: 2021-03-13
title: Beat Saberとアバター合成
categories:
  - manual
author_staff_member: sh_akira
---

Beat Saberと合成するための3つの方法  

# 3つの方法

現在バーチャルモーションキャプチャーを用いてBeat Saberとアバター合成を行う方法は**簡単な順**に以下の3つの方法があります。

- VMCAvatarとCamera2
- CameraPlusとOBSで合成
- LIV

順番に説明します。

***

# VMCAvatarとCamera2を使用する方法

VMCAvatarはバーチャルモーションキャプチャーのVMCProtocol送信機能を利用して、Beat Saber内に直接VRMのアバターを表示するMODです。  
<span style="color: red;">**VMCAvatarの作者は[長月ゆきな(@ngtkd)](https://twitter.com/ngtkd)さんです** </span>   

  
**2020/08/11現在 バーチャルモーションキャプチャーのVMCProtocol送信機能は[pixivFANBOX](https://akira.fanbox.cc/),[Patreon](https://www.patreon.com/sh_akira)の支援者限定の先行配信版のみの機能です**  
  
VMCAvatarは直接ゲーム内にアバターを表示するため、後述するLIVやCameraPlus合成における合成処理や位置合わせが一切不要でキャリブレーションするだけですぐに使用することが出来ます。  

## 1. Beat SaberでMODを使用可能にする
まだBeat SaberでMODを導入していない場合は、[ModAssistant](https://github.com/Assistant/ModAssistant/releases)をダウンロードして、デフォルトでチェックされているものとCamera2にチェックを入れInstallしてください。  
その後Beat Saberを起動してBeat Saberのゲーム内にてCameraの設定をしてください。  

## 2. VMCAvatarを導入する
<span style="color: red;">**VMCAvatarの作者は[長月ゆきな(@ngtkd)](https://twitter.com/ngtkd)さんです** </span>   
長月ゆきなさんが公開している[VMCAvatarのページ](https://github.com/nagatsuki/VMCAvatar-BS)のダウンロードからリリース版バイナリをダウンロードします。  
<span style="color: red;">**このページには困ったときのよくある質問が全て書いてありますので1度目を通してください**</span>  
ダウンロードして解凍したら中にあるVMCAvatar.dllをBeat SaberのインストールフォルダのPluginsフォルダ  
(通常はC:\Program Files (x86)\Steam\steamapps\common\Beat Saber\Plugins)にコピーします。  

## 3. バーチャルモーションキャプチャーの設定
詳細設定のモーション送信にあるOSCでモーション送信を有効にするのチェックを入れます。  
![詳細設定画面](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/9f3205d0b5c69ad9e18044fbeca54b9535f45566/docs/images/manual/BeatSaber/01_vmcsetting.png)  
  
VRMモデルを読み込み、MR合成モードでキャリブレーションをします。
![キャリブレーション](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/9f3205d0b5c69ad9e18044fbeca54b9535f45566/docs/images/manual/BeatSaber/02_vmccalibration.png)  
  
以上で完了です。Beat Saber内に自分のアバターが表示されているはずです。  
アバターが白くなったりする場合はVMCAvatarのページを再度確認してください。  
または**[Reiya(@Reiya__)](https://twitter.com/Reiya__)さんが公開している[VMCAvatarMaterialChange](https://github.com/Reiya1013/VMCAvatarMaterialChange)**もお試しください。  
  
Camera2は自由に複数のカメラを設置したり、ゲーム内でカメラを移動できるので好きな画面構成を作ることが出来ます。  
好みの画面が完成したら後はOBSで録画や配信をしましょう！  
VMCAvatarをご使用の際は是非ハッシュタグ #VMCAvatar と #バーチャルモーションキャプチャー を付けて投稿してください。

***

# CameraPlusとOBSで合成
<span style="color: red;">**CameraPlusはCamera2に置き換わったため非推奨になりました。上記VMCAvatarによる方法をお試しください。** </span>

バーチャルモーションキャプチャーはCameraPlusのcfgファイル読み込みに対応しているため、OBSを使用してぴったりBeat Saberとバーチャルモーションキャプチャーの画面を重ね合わせることが出来ます。  

## 1. Beat SaberでMODを使用可能にする
上記VMCAvatarの手順と同じようにCameraPlusを導入してください。

## 2. Beat Saberとバーチャルモーションキャプチャーの解像度を合わせる
Beat Saberはゲーム内SETTINGSのGRAPHICSのNonVR Resolutionから  
バーチャルモーションキャプチャーは詳細設定の解像度設定から  
それぞれ同じ解像度(1280x720や1920x1080等)に合わせてください。

## 3. バーチャルモーションキャプチャーの設定
まずは通常通りVRMモデルを読み込み、MR合成モードでキャリブレーションをします。  
![キャリブレーション](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/9f3205d0b5c69ad9e18044fbeca54b9535f45566/docs/images/manual/BeatSaber/02_vmccalibration.png)  
  
詳細設定のCameraPlus内のImportボタンからcameraplus.cfgを読み込みます。  
(通常はC:\Program Files (x86)\Steam\steamapps\common\Beat Saber\UserData\CameraPlusにあります)  
![CameraPlus設定](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/9f3205d0b5c69ad9e18044fbeca54b9535f45566/docs/images/manual/BeatSaber/03_vmccameraplus.png)  
読み込みが完了するとBeat Saberとバーチャルモーションキャプチャーのカメラの向きが全く同じになります。  
  
バーチャルモーションキャプチャーの背景色設定で透過を選択してください。  
![透過設定](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/9f3205d0b5c69ad9e18044fbeca54b9535f45566/docs/images/manual/BeatSaber/04_vmctransparent.png)  

## 4. OBSの設定
まずはBeat Saberの画面をゲームキャプチャの特定のウインドウをキャプチャで取得します。
表示されたらバーチャルモーションキャプチャーの画面も同じようにゲームキャプチャで取得します。
その際に透過を許可にチェックを入れてください。
![OBS設定](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/9f3205d0b5c69ad9e18044fbeca54b9535f45566/docs/images/manual/BeatSaber/05_obsgamecapture.png)  
  
あとは録画や配信をすれば完了です。  
OBSで録画する際は設定の出力のエンコーダをNVENC等のハードウェアエンコーダにすることで動作を軽くすることが出来るので確認してください。  
また、Beat Saber内のRoomAdjustの設定を変更すると合成位置がずれてしまうため、位置の変更が必要な場合はRoomAdjustは0に戻してSteamVRのルームスケール設定で部屋の位置を変更することをお勧めします。

***

# LIVを使用する
<span style="color: red;">**現在Beat SaberにおいてLIVの使用は非推奨です。上記VMCAvatarもしくはCameraPlusとOBS合成を先にお試しください。** </span>
<span style="color: red;">**・動作にかなりのスペックを要求する** </span>  
<span style="color: red;">**・上記2つの方法より軽くなることはありません** </span>  
<span style="color: red;">**・正しく設定しても正常に動作しないことが多いです** </span>  
<span style="color: red;">**・動作しない場合LIV側の問題の為こちらでの対応が不可能です** </span>  
<span style="color: red;">**・検索して出てくる古い手順はすべて動作しません** </span>  
  
LIVはBeat Saberに限らず対応しているゲームでMR合成(リアルの映像を合成)を行うためのツールです。  
バーチャルモーションキャプチャーはリアルの映像の代わりに仮想カメラでアバターの映像を出力してゲーム内にアバターを合成させることが出来ます  
ただし映像合成の負荷が高くPCのスペックがかなり必要です。また、複数のゲームに対応するため基本的にカメラ位置の変更には手順を毎回踏む必要があります。そのため上記方法で実現できないような撮影を行いたい場合にのみ使うことをお勧めします  
設定方法については別ページ[LIVとVMTでVRゲーム内合成](https://vmc.info/manual/LIV%E3%81%A8VMT%E3%81%A7VR%E3%82%B2%E3%83%BC%E3%83%A0%E5%86%85%E5%90%88%E6%88%90.html)をご覧ください。

***

### 注記事項

* 本ドキュメントはBeat Saber v1.10.0(2020/08/11)現在の仕様で確認したものです。
* Beat Saber側の仕様変更にMODが対応するまで使用できない場合があります。
* ソフトウェアの仕様・見た目はアップデート等により、予告なく変更されることがあります
