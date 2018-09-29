# CorrectNormalMapImport
2018/09/28時点にて確認。
VRoidから出力したVRMにおいて、UniVRMを使ってランタイムでロードすると、
ノーマルマップのTextureTypeがdefaultのままでロードされてしまい見た目が不自然になるのを解消するスクリプト。

下記issueにも上がっているのでUniVRMの修正で直に治ると思いますが、とりあえずの対処として。
https://github.com/Santarh/MToon/issues/9


## 使い方
CorrectNormalMapImport.cs
の1つのファイルをプロジェクトの任意の場所に配置します。
(ChangeTextureType.csは不要となりました。)

VRMファイルをロードして生成したゲームオブジェクトを
CorrectNormalMapImport.CorrectNormalMap();
の第一引数に入れれば、NormalMapの生成及び再設定処理が走ります。
髪の毛のnormalMapがある場合にちらつきが発生する現象があるため(UniVRM　v0.43にてインポート時に確認)、髪の毛のnormalMapを削除するようにしています。
髪の毛のnormalMapを残したい場合は第二引数にfalseを入れてください。（第二引数なしの場合はtrueになります。）

## 注意点
全てのマテリアルのノーマルマップをピクセル毎に変換して生成しているので結構処理が重たいです。
生成したノーマルマップは圧縮などしていないため、使いすぎるとメモリ容量を圧迫します。
※Windows上での動作のみ確認。(Androidでは正常に動作しないことは確認。対応予定。)
