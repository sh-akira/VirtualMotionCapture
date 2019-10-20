---
date: 2018-11-26
title: MR합성용externalcamera.cfg 작성
categories:
  - manual
author_staff_member: sh_akira
---

MR합성용externalcamera.cfg작성  

## externalcamera.cfg란?

게임에 들어간 듯한 촬영(MR합성)을 하려면, 게임측과 버츄얼모션캡쳐의 카메라의 방향을 맞추기 위한 파일, externalcamera.cfg파일이 필요합니다. 소프트웨어의 사양, 외형은 업데이트 등으로 인해 예고업싱 변경될 수도 있습니다
externalcamera.cfg파일에는 "3번째"의 컨트롤러에서 카메라까지의 오프셋이 들어있습니다. 본래 이 기능은 카메라로 VR게임 플레이어를 초록배경으로 게임 내에 합성시키기 위한 기능입니다.
카메라에 컨트롤러를 붙여서, 현실세계에서 촬영한 VR플레이어를 게임의 위치와 맞추서 합성할 수 있다는 뜻입니다.
버츄얼모션캡쳐는 이 기능을 사용하여, 실제 카메라 대신 3D모델을 동일한 위치에 표시하고, 게임 내 합성을 할 수 있는 기능이 탑재되어 있습니다.
  
SteamVR의 일부의 게임이나, LIV에 대응하는 게임에서 이 파일을 사용하여 합성작업을 수행할 수 있습니다.
  
[기본 캘리브레이션](https://sh-akira.github.io/VirtualMotionCapture/manual/%E5%9F%BA%E6%9C%AC%E3%81%AE%E6%93%8D%E4%BD%9C%E6%96%B9%E6%B3%95.html)를 완료하여, 버츄얼모션캡쳐에서 모델의 움직임을 확인할 수 있는 상태에서 설명을 합니다.
  
externalcamera.cfg를 출력하기 위해선 기준이 되는 3번째 컨트롤러, 또는 가상 컨트롤러를 먼저 준비해야합니다. 가상 컨트롤러를 만드는 방법은 다음 페이지를 참조해 주십시오.
・[LIV초기설정](https://sh-akira.github.io/VirtualMotionCapture/manual/LIV%E3%81%AE%E5%88%9D%E6%9C%9F%E8%A8%AD%E5%AE%9A.html)  
・MixedRealityTwoController 인스톨(작성중)
  

## MR합성용 캘리브레이션 실행

컨트롤패널의 설정탭에서 캘리브레이션을 누릅니다.

![設定タブ](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/07971766022eecc8c4f78f0dcf388e1cbb444e50/docs/images/manual/2-1.png)

캘리브레이션은 MR합성모드 두 개 중 하나를 골라주세요.

![キャリブレーション画面](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/07971766022eecc8c4f78f0dcf388e1cbb444e50/docs/images/manual/2-2.png)

・MR합성모드(손의 위치를 최대한 맞추고 다리는 쭉 뻗음)를 선택하면 다리가 긴 모델의 경우 바닥에 다리가 꽂힙니다.
　BeatSaber등 바닥이 투명한 경우에는 문제 없습니다
  
・MR합성모드(손의 위치를 최대한 맞추고 다리는 바닥에 맞추어 구부림)를 선택하면 다리가 긴 모델의 경우 무릎을 굽혀 땅에 서게 합니다.
　H3VR처럼 바닥이 있는 게임의 경우 다리가 관통하는 것을 방지합니다.
  
※일반모드(몸의 움직임을 최대한 재현함)을 선택하면 플레이공간의 중심에 있는 경우에는 문제가 없지만 이동할 경우 손에서 컨트롤러가 어긋나므로 위 두 MR합성 모드를 사용해주세요.

## 카메라의 방향 결정

컨트롤패널의 카메라탭에서 자유시점카메라를 선택합니다.

![カメラタブ](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/07971766022eecc8c4f78f0dcf388e1cbb444e50/docs/images/manual/2-3.png)

버츄얼모션캡쳐의 메인화면(모델이 표시되는 화면)에서 마우스조작으로 카메라를 움직입니다.
마우스의 휠클릭으로 드래그하여 카메라 이동
마우스 우클릭으로 드래그하여 카메라 회전
모델을 180도 뒤로 회전시킬 경우에는 같은 방향으로 휠드래그와 우클릭드래그를 교대로 사용하여 회전합니다.

![カメラの回転](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/07971766022eecc8c4f78f0dcf388e1cbb444e50/docs/images/manual/2-4.png)

## externalcamera.cfg 출력

컨트롤패널의 설정탭에서 고급설정을 엽니다

![詳細設定](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/07971766022eecc8c4f78f0dcf388e1cbb444e50/docs/images/manual/2-5.png)

externalcamera.cfg(자유시점카메라 좌표를 설정)의 컨트롤러 번호로 카메라로 사용할 3번째 컨트롤러를 설정합니다.
LIV를 사용할 경우에는 컨트롤러(LIV Virtual Camera (Controller))를
MixedRealityTwoController를 사용할 경우에는 컨트롤러(Virtual Controller)를 선택해주세요.

![コントローラー番号設定](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/07971766022eecc8c4f78f0dcf388e1cbb444e50/docs/images/manual/2-6.png)

컨트롤러를 선택했으면 externalcamera.cfg를 파일로 출력합니다.
externalcamera.cfg출력 버튼을 눌러주세요.

![externalcamera.cfgを出力ボタン](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/07971766022eecc8c4f78f0dcf388e1cbb444e50/docs/images/manual/2-7.png)

파일을 저장하는 화면이 표시되므로 파일명을 바꾸지 않고(externalcamera.cfg 그대로)원하는 폴더에 저장해주세요.
이 파일은 추후 합성시에 사용됩니다.
<span style="color:red">**externalcamera.cfg를VirtualMotionCapture.exe와 같은 폴더에 저장하지 말아주세요!**</span>  
<span style="color:red">**버츄얼모션캡쳐의 화면이 4분할되어버려서 정상적으로 동작하지 않게 됩니다.**</span>

![Export externalcamera.cfg画面](https://rawcdn.githack.com/sh-akira/VirtualMotionCapture/07971766022eecc8c4f78f0dcf388e1cbb444e50/docs/images/manual/2-8.png)

## 실제로 게임과 합성

・[LIV와 버츄얼모션캡쳐를 MR합성](https://sh-akira.github.io/VirtualMotionCapture/manual/LIV%E3%81%A8%E3%83%90%E3%83%BC%E3%83%81%E3%83%A3%E3%83%AB%E3%83%A2%E3%83%BC%E3%82%B7%E3%83%A7%E3%83%B3%E3%82%AD%E3%83%A3%E3%83%97%E3%83%81%E3%83%A3%E3%83%BC%E3%81%A7MR%E5%90%88%E6%88%90.html)  
・externalcamera.cfg를 직접 게임폴더에 넣어서 MR합성(작성중)
