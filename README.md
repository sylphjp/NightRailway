# Night Railway

光の点だけで、夜間の鉄道前面展望を描くシングルページのCanvasアニメーションです。

![Night Railway](rail-mode.png)

## Demo

GitHub Pages: <https://sylphjp.github.io/NightRailway/>

`index.html`をMicrosoft Edge、Google Chrome、Firefoxなどのブラウザーで直接開いても動作します。ビルドや追加ライブラリは不要です。

## Features

- 1067 mm軌間の複線
- 門型架線柱と架線
- G現示を基本とし、たまにYG現示になる四灯式信号機
- 3灯の前照灯と、最後尾の赤色尾灯を備えた対向列車
- 上下線の両側に現れる駅ホーム
- 星空、遠くの街明かり、流れ星などの夜景
- 0 km/hまでの速度変更、一時停止、全画面表示

## Controls

| キー | 動作 |
| --- | --- |
| `↑` / `↓` | 加速 / 減速（0 km/hまで） |
| `Space` | 一時停止 / 再開 |
| `F` | 全画面表示 / 解除 |

画面左下のボタンでも操作できます。

## Run locally

このリポジトリをダウンロードまたはcloneし、`index.html`をブラウザーで開いてください。ローカルHTTPサーバーを使う場合は、リポジトリのルートをそのまま公開ディレクトリにできます。

## Credits

この作品は、Masayuki Hatta様による[Original Night Freeway](https://github.com/mhatta/nightfreeway)を基に、鉄道専用の描画へ変更した派生作品です。

Original Night Freeway was inspired by the original FreeWay screensaver by FKS and the Windows port by Yoshihiro Sugahara.

## License

MIT Licenseです。元作品の著作権表示とライセンスを含む全文は[LICENSE](LICENSE)を参照してください。
