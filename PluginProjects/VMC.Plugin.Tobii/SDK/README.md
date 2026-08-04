# Tobii Unity SDK の置き場所

[Tobii Unity SDK](https://developer.tobii.com/pc-gaming/unity-sdk/) を入手し、
その `Tobii` フォルダをこのフォルダへそのまま置いてください。

```
SDK/
  Tobii/
    Framework/      ← csproj がここのソースをコンパイルします(Editor は除く)
    Plugins/        ← SimpleJSON.cs / TobiiGameIntegrationApi.cs と
                       ネイティブDLL(x64のみ native/ へ同梱)
    ...
```

SDK本体は再配布できないため、このREADME以外はリポジトリに含めていません。

**本体(VirtualMotionCapture)のビルドにこのSDKは不要です。** SDKが無い場合は
このプラグインだけがビルドできず、本体は問題なくビルド・実行できます。

Tobii SDK は EULA 同意マーカーを Unity の Resources から読む作りになっていますが、
プラグインDLLからは Resources を提供できないため、実行時に同意済みフラグを直接設定しています
（[TobiiPlugin.cs](../TobiiPlugin.cs) の AcceptTobiiEula）。
自前でビルド・配布する場合は Tobii Unity SDK の EULA に同意した上で行ってください。

ビルド手順は [../../../documents/plugins.md](../../../documents/plugins.md) を参照してください。
