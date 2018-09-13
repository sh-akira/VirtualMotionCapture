# avast アバスト無料アンチウイルスにブロックされる場合
<img width="260" alt="avast1" src="https://user-images.githubusercontent.com/30430584/45487864-85a79880-b79a-11e8-898a-5b4616c3840e.png">  
avast アバスト無料アンチウイルスをインストールしたパソコンで、バーチャルモーションキャプチャーを起動した際にこの画面が出る際に  
  
<img width="271" alt="avast2" src="https://user-images.githubusercontent.com/30430584/45487898-a40d9400-b79a-11e8-9031-fcbc7f5b8ea2.png">  
このような画面が出る場合、avastによってバーチャルモーションキャプチャーの起動が阻害されています。  
詳細から許可を押して、  
  
<img width="271" alt="avast3" src="https://user-images.githubusercontent.com/30430584/45487936-c7384380-b79a-11e8-9bd3-41fb0fd79093.png">  
このような画面が表示されても正常に動作しません。  
avastの除外設定に追加する必要があります。  

# 除外設定
2018/09/13 現在ダウンロードできる最新のavastで説明します。別のバージョンで画面が違う場合は適宜読み替えてください。  
  
<img width="343" alt="avast4" src="https://user-images.githubusercontent.com/30430584/45487991-f058d400-b79a-11e8-847d-0d88c0d2f6e6.png">  
タスクトレイにあるavastのアイコンを右クリックして、「アバスト ユーザーインターフェースを開く」をクリックします。  
  
<img width="758" alt="avast5" src="https://user-images.githubusercontent.com/30430584/45488031-0feffc80-b79b-11e8-8be1-f2654db3129f.png">  
出てきた画面右上のメニューをクリックし、⚙設定をクリックします。  
  
<img width="758" alt="avast6" src="https://user-images.githubusercontent.com/30430584/45488193-8e4c9e80-b79b-11e8-8c9e-7da83fc2968a.png">  
左のコンポーネントをクリックし、右に出てきたファイルシールドのカスタマイズをクリックします。  
  
<img width="570" alt="avast7" src="https://user-images.githubusercontent.com/30430584/45488219-acb29a00-b79b-11e8-98ec-e42211af5340.png">  
左のスキャンからの除外をクリックし、リスト内右側の参照ボタンを押します。  
  
<img width="348" alt="avast8" src="https://user-images.githubusercontent.com/30430584/45488260-cd7aef80-b79b-11e8-901f-16a6f1fa888d.png">  
出てきた画面でバーチャルモーションキャプチャーを解凍したフォルダを探し、左側の□をクリックして✅チェックします。そして下のOKをクリックします。  
  
<img width="570" alt="avast9" src="https://user-images.githubusercontent.com/30430584/45488307-f8654380-b79b-11e8-91e2-ee3cc23214b1.png">  
追加したフォルダが表示されていることを確認してOKをクリックします。  
  
<img width="758" alt="avast10" src="https://user-images.githubusercontent.com/30430584/45488342-1468e500-b79c-11e8-9623-799351004368.png">  
元の画面に戻りますのでここでもOKをクリックします。  
  
  
  
以上で除外設定は完了しました。再度バーチャルモーションキャプチャーを立ち上げて、
<img width="260" alt="avast1" src="https://user-images.githubusercontent.com/30430584/45487864-85a79880-b79a-11e8-898a-5b4616c3840e.png">  
このコントロールパネルが青い画面とともに開くか確認してください。  
  
  
default.jsonを移動する回避策を行っていた場合は、元に戻してお試しください。  
