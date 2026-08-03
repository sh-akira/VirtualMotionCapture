# VIVE SRanipal SDK の置き場所

[VIVE SRanipal SDK](https://developer.vive.com/resources/vive-sense/eye-and-facial-tracking-sdk/) を入手し、
その `ViveSR` フォルダをこのフォルダへそのまま置いてください。

```
SDK/
  ViveSR/
    Scripts/        ← csproj がここのソースをコンパイルします
    Plugins/        ← ネイティブDLL(SRanipal.dll 等)を native/ へ同梱します
    ...
```

SDK本体は再配布できないため、このREADME以外はリポジトリに含めていません。

**本体(VirtualMotionCapture)のビルドにこのSDKは不要です。** SDKが無い場合は
このプラグインだけがビルドできず、本体は問題なくビルド・実行できます。

ビルド手順は [../../../documents/plugins.md](../../../documents/plugins.md) を参照してください。
