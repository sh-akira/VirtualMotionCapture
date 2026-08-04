# mocopi Receiver Plugin for Unity の置き場所

[mocopi Receiver Plugin for Unity](https://xyn.sony.net/ja/developer/downloads/mocopi) を入手し、
その `MocopiReceiver` フォルダをこのフォルダへそのまま置いてください。

```
SDK/
  MocopiReceiver/
    Runtime/        ← csproj がここのソースをコンパイルします
    ...
```

SDK本体は再配布できないため、このREADME以外はリポジトリに含めていません。

**本体(VirtualMotionCapture)のビルドにこのSDKは不要です。** SDKが無い場合は
このプラグインだけがビルドできず、本体は問題なくビルド・実行できます。

ビルド手順は [../../../documents/plugins.md](../../../documents/plugins.md) を参照してください。
