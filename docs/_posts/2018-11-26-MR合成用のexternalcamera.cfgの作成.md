---
date: 2018-11-26
title: MR合成用のexternalcamera.cfgの作成
categories:
  - manual
author_staff_member: sh_akira
---

MR合成用のexternalcamera.cfgの作成  

## externalcamera.cfgとは

ゲーム内に入ったような撮影(MR合成)を行うには、ゲーム側とバーチャルモーションキャプチャーのカメラの向きを合わせるためのファイル、externalcamera.cfgファイルが必要になります。  
externalcamera.cfgファイルには"3本目"のコントローラーからカメラまでのオフセット位置が入っています。本来この機能は、ビデオカメラでVRゲームをしている人をグリーンバックでゲーム内に合成させるために用意された機能です。  
ビデオカメラにコントローラーを取り付けて、現実世界で撮影した、VRをプレイしている人とゲームの位置を合わせて合成することが出来るというものです。  
バーチャルモーションキャプチャーではこの機能を利用して、実際のビデオカメラの代わりに、3Dモデルを同じ位置に表示してゲーム内合成を行うための機能が搭載されています。  
  
SteamVRの一部のゲームや、LIVに対応したゲームでこのファイルを使って合成処理を行うことが出来ます。  
  
[基本のキャリブレーション](https://sh-akira.github.io/VirtualMotionCapture/manual/%E5%9F%BA%E6%9C%AC%E3%81%AE%E6%93%8D%E4%BD%9C%E6%96%B9%E6%B3%95.html)を完了して、バーチャルモーションキャプチャー単体でモデルの動きを確認出来た状態から説明をします。  
  
externalcamera.cfgを出力するためには基準となる3本目のコントローラーもしくは、仮想コントローラーを先に用意する必要があります。仮想コントローラーの作成方法は次のページをご覧ください。  
・LIVの初期設定(執筆中)  
・MixedRealityTwoControllerのインストール(執筆中)  
  

## MR合成用のキャリブレーションを行う

コントロールパネルの設定タブからキャリブレーションを押します。  

![設定タブ](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/master/docs/images/manual/2-1.png)

キャリブレーションはMR合成モードの二つのうちどちらかを選択します。

![キャリブレーション画面](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/master/docs/images/manual/2-2.png)

・MR合成モード(手の位置をなるべく合わせて足は真っ直ぐ延ばす)を選択すると、足が長いモデルの場合地面に足が刺さります。  
　BeatSaberのように地面が透明な場合は特に問題ありません  
  
・MR合成モード(手の位置をなるべく合わせて足は地面に合わせて曲げる)を選択すると、足が長いモデルの場合膝を曲げて地面に立つようにします。  
　H3VRのように地面があるゲームの場合、足が貫通するのを防ぎます。  
  
※通常モード(体の動きをなるべく再現する)を選択すると、プレイエリアの中心にいる場合は問題ありませんが、移動すると手からコントローラーがずれていくため、基本的には上二つのMR合成モードを使用してください。

## カメラの向きを決める

コントロールパネルのカメラタブからフリーカメラを選択します。

![カメラタブ](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/master/docs/images/manual/2-3.png)

バーチャルモーションキャプチャーのメイン画面(モデルが表示されている画面)上でマウス操作でカメラを動かします。  
マウスのホイールクリックでドラッグすることでカメラの移動  
マウスの右クリックでドラッグすることでカメラの回転  
モデルを180度後ろに回転させたい場合は、同じ方向にホイールドラッグと右クリックドラッグを交互に行うことで回転します。  

![カメラの回転](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/master/docs/images/manual/2-4.png)

## externalcamera.cfgを出力する

コントロールパネルの設定タブから詳細設定を開きます

![詳細設定](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/master/docs/images/manual/2-5.png)

externalcamera.cfg(フリーカメラ座標を設定)のコントローラー番号でカメラに使用したい3本目のコントローラーを設定します。  
LIVを使用する場合はコントローラー(LIV Virtual Camera (Controller))を  
MixedRealityTwoControllerを使用する場合はコントローラー(Virtual Controller)を選択してください。  

![コントローラー番号設定](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/master/docs/images/manual/2-6.png)

コントローラーを選択したらexternalcamera.cfgをファイルに出力します。  
externalcamera.cfgを出力ボタンを押してください。

![externalcamera.cfgを出力ボタン](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/master/docs/images/manual/2-7.png)

ファイルを保存する画面が表示されるのでファイル名を変えずに(externalcamera.cfgのまま)お好きなフォルダに保存してください。  
このファイルをこの後合成時に使用します。  

![Export externalcamera.cfg画面](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/master/docs/images/manual/2-8.png)

## 実際にゲームと合成する

・LIVを使用してMR合成する(執筆中)  
・externalcamera.cfgを直接ゲームフォルダに置いてMR合成する(執筆中)  
