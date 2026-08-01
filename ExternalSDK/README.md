# ExternalSDK

外部デバイスプラグインのビルドに使うSDKの置き場所です。
SDK本体は再配布できないため、このREADME以外はリポジトリに含めていません
（`.gitignore` で `ExternalSDK/*` を除外しています）。

**本体のビルドにはこれらのSDKは不要です。** 空のままでも VirtualMotionCapture は
クローンしてそのままビルド・実行できます（設定画面の「外部デバイス」欄が空になるだけです）。
プラグインをビルドしたい場合だけ、以下を各自で入手して配置してください。

```
ExternalSDK/
  MocopiReceiver/     mocopi Receiver Plugin for Unity の Runtime などを含むフォルダ
  ViveSR/             VIVE SRanipal SDK の ViveSR フォルダ (Scripts / Plugins を使用)
  Tobii/              Tobii Unity SDK の Tobii フォルダ (Framework / Plugins を使用)
```

- [mocopi Receiver Plugin for Unity](https://www.sony.net/Products/mocopi-dev/)
- [VIVE SRanipal SDK](https://developer.vive.com/resources/vive-sense/eye-and-facial-tracking-sdk/)
- [Tobii Unity SDK](https://developer.tobii.com/pc-gaming/unity-sdk/)

ビルド手順は [../documents/plugins.md](../documents/plugins.md) を参照してください。
