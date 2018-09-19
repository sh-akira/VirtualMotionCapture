# CorrectNormalMapImport

2018/09/17時点にて確認。
VRoidから出力したVRMにおいて、UniVRMを使ってランタイムでロードすると、
ノーマルマップのTextureTypeがdefaultのままでロードされてしまい見た目が不自然になるのを解消するスクリプト。

下記issueにも上がっているのでUniVRMの修正で直に治ると思いますが、とりあえずの対処として。
https://github.com/Santarh/MToon/issues/9


## 使い方
ChangeTextureType.cs
CorrectNormalMapImport.cs
の2つのファイルをプロジェクトの任意の場所に配置します。

VRMファイルをロードして生成したゲームオブジェクトを
CorrectNormalMapImport.CorrectNormalMap();
の引数に入れれば、NormalMapの生成及び再設定処理が走ります。

## 注意点
全てのマテリアルのノーマルマップをピクセル毎に変換して生成しているので結構処理が重たいです。
生成したノーマルマップは圧縮などしていないため、使いすぎるとメモリ容量を圧迫します。
