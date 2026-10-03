# LostNine 素材権利監査

監査日: 2026-09-29  
対象: `Project_Lost/Assets`、`Project_Lost/Packages`、ビルド設定、Git履歴  

> この文書は、リポジトリ内の証跡と各配布元の公開規約を照合した実務上の監査結果です。法律相談や権利者による使用許諾そのものではありません。

## 結論

現状のままでは、プロジェクト全体を「商用利用可能」と確定できません。配布元と商用利用条件を確認できた素材はありますが、実際にゲームで使われている効果音2点と独自BGM 1点、立ち絵・背景・ロゴ・UI画像の大半について、作者、制作依頼、譲渡または利用許諾の記録がリポジトリ内にありません。また、効果音ラボ由来と確定した2点は、同一ファイルがニコニ・コモンズでも異なる条件で配布されているため、取得経路の記録が必要です。

公開前に最低限必要な対応は次の4点です。

1. 出所不明の使用中音源を、購入履歴・配布ページ・ライセンス保存によって特定する。特定できないものは差し替える。効果音ラボの2点は、公式サイトから取得したことを示す記録を残すか、公式サイトから改めて取得して配布ページと規約を保存する。
2. 立ち絵、背景、ロゴ、UI画像、シナリオ、独自BGMについて、作者と権利帰属を文書化する。外注物なら商用ゲーム、宣伝物、改変、全地域・全プラットフォームへの利用許諾または著作権譲渡を確認する。
3. ゲーム内または同梱文書にOtoLogicのクレジットを追加し、DotGothic16、UniTask等のライセンス本文を保存する。EmojiOneは版とライセンスを特定して条件を満たすか、未使用ならビルドから除外する。
4. GitHubリポジトリを公開している場合、音源・ストック画像の原本をそのまま公開しない。ゲームへの組込みは許可されても、素材ファイルの再配布は禁止される規約がある。

## 判定基準

| 判定 | 意味 |
|---|---|
| 商用可 | 配布元と規約を確認でき、現在の用途が許可範囲に入る |
| 条件付きで商用可 | クレジット、ライセンス同梱、再配布防止などの条件がある |
| 取得経路の証跡待ち | 同じファイルが異なる条件で複数サイトから配布され、どこから取得したかで適用条件が変わる |
| 権利証跡待ち | 自作・外注の可能性はあるが、作者または許諾を確認できる資料がない |
| 使用停止推奨 | 出所・許諾を確認できず、現にビルド対象から参照されている |
| 未使用 | Unity GUIDの静的参照がなく、`Resources`/Addressablesにも置かれていない。ソース配布時の権利は別途残る |

## 音源

### BGM

| ファイル | 使用状況 | 出所・根拠 | 判定 | 必要な対応 |
|---|---:|---|---|---|
| `Audio/BGM/genjitsunosukima.mp3` | ChapterSelect | ID3に「現実の隙間」「甘茶の音楽工房」。公式配布ページの曲名・長さとも一致 | 条件付きで商用可 | BGMとしての商用ゲーム利用可。音源単体の販売・二次配布、JASRAC等への登録、YouTube Content ID登録は禁止。クレジットは任意だが記載推奨 |
| `Audio/BGM/mizunishizumupiano.mp3` | Title | ID3に「水に沈むピアノ」「甘茶の音楽工房」。公式配布ページと一致 | 条件付きで商用可 | 同上 |
| `Audio/BGM/rokugatsunoamaoto.mp3` | 未使用 | ID3に「6月の雨音」「甘茶の音楽工房」。公式一覧と一致 | 条件付きで商用可・未使用 | 同上。不要なら削除候補 |
| `Audio/BGM/LostNine_TuningBGM.wav` | Memorize | 独自名だが作者・制作記録・埋込みメタデータなし | 権利証跡待ち | 作曲者、制作日、使用した素材・生成サービス、権利帰属を確認。自作なら作者による宣誓記録を残す |

甘茶の音楽工房は、商用・非商用を問わずゲームBGMとしての利用を許可しています。著作権表示は必須ではありませんが、音源だけの販売・二次配布、著作権管理団体への登録、Content ID登録は禁止されています。  
公式: [利用規約](https://amachamusic.chagasi.com/terms.html) / [現実の隙間](https://amachamusic.chagasi.com/music_genjitsunosukima.html) / [水に沈むピアノ](https://amachamusic.chagasi.com/music_mizunishizumupiano.html) / [6月の雨音掲載ページ](https://amachamusic.chagasi.com/image_kurai.html)

### 効果音

| ファイル | 使用状況 | 出所・根拠 | 判定 | 必要な対応 |
|---|---:|---|---|---|
| `Audio/SE/Inspiration11-1(Low).mp3` | Main | ID3: OtoLogic / OtoLogic-SE | 条件付きで商用可 | 無償利用では「OtoLogic」のクレジット必須。クレジットなしなら有償ライセンスが必要 |
| `Audio/SE/Inspiration11-2(Mid).mp3` | 未使用 | ID3: OtoLogic / OtoLogic-SE | 条件付きで商用可・未使用 | 同上。不要なら削除候補 |
| `Audio/SE/Inspiration11-3(High).mp3` | 未使用 | ID3: OtoLogic / OtoLogic-SE | 条件付きで商用可・未使用 | 同上。不要なら削除候補 |
| `Audio/SE/Page_OtoLogic.mp3` | Title、Story、Main | ID3: `Book01-2(Flip)` / OtoLogic | 条件付きで商用可 | OtoLogicクレジット必須 |
| `Audio/SE/minimalist-button-hover-sound-effect-399749.mp3` | Title、Story、Main | ファイル名がPixabayの素材ID 399749と一致。作者Lesiakower、2025-09-08公開 | 条件付きで商用可 | Pixabay Content Licenseでは帰属表示なしで使用・改変可。素材単体での販売・配布は禁止。配布ページと取得時ライセンスの保存を推奨 |
| `Audio/SE/お皿が割れる音響きあり1.mp3` | Title、Story、Main、Memorize | Springin’ Sound Stock公式一覧に同名素材あり | 条件付きで商用可 | ゲーム販売可、通常クレジット任意。素材再配布、Content ID、成人向け利用は禁止。ゲームから音源を単体で取り出しにくくする |
| `Audio/SE/hito_ge_ugoki05.mp3` | Ch1_Dialogue | On-Jin ～音人～「動き・服の擦れ07」の公式配布MP3とSHA-256が完全一致 | 条件付きで商用可 | 個人・サークルによるゲーム販売は可。企業・営利事業・プロ活動では事前または事後に利用詳細を連絡し、使用許諾メールを得る。素材ファイルを第三者が取得できる形で公開しない |
| `Audio/SE/フィルム巻き音.mp3` | Title、Story、Main | 出所を特定できるタグ・同梱規約なし | 使用停止推奨 | 配布ページまたは購入証明を提示できなければ差し替え |
| `Audio/SE/弓矢が刺さる.mp3` | Ch1_Dialogue | 効果音ラボ公式配布 `arrow-pierce1.mp3` とSHA-256・サイズが完全一致。ニコニ・コモンズ `nc278975` にも同一サイズ・仕様の音源あり | 取得経路の証跡待ち | 効果音ラボ公式サイト取得なら商用可・クレジット不要。ニコニ・コモンズ取得ならニコニコ関連のみ利用可で、ゲーム販売等は許可が必要 |
| `Audio/SE/拒否音.mp3` | Main | 出所を特定できるタグ・同梱規約なし | 使用停止推奨 | 配布ページまたは購入証明を提示できなければ差し替え |
| `Audio/SE/自動車事故.mp3` | Ch1_Dialogue | 効果音ラボ公式配布 `car-accident1.mp3` とSHA-256・サイズが完全一致。ニコニ・コモンズ `nc278952` にも同一サイズ・仕様の音源あり | 取得経路の証跡待ち | 効果音ラボ公式サイト取得なら商用可・クレジット不要。ニコニ・コモンズ取得ならニコニコ関連のみ利用可で、ゲーム販売等は許可が必要 |

### ニコニ・コモンズ経由の可能性

効果音ラボは公式サイトとニコニ・コモンズで同じ音源を配布していますが、公式FAQは「ダウンロードしたサイトにより利用規約が異なる」と明記しています。公式サイト版は個人・法人とも商用利用無料、報告・クレジット不要です。ニコニ・コモンズ版はコモンズ対応サイトでのみ利用でき、営利利用には許可が必要です。

- `弓矢が刺さる.mp3`: ローカル23,195 bytes、効果音ラボ公式版23,195 bytes、ニコニ・コモンズ `nc278975` 23,195 bytes。ファイルサイズと仕様が同一で、取得経路はバイナリから判別不能。
- `自動車事故.mp3`: ローカル74,604 bytes、効果音ラボ公式版74,604 bytes、ニコニ・コモンズ `nc278952` 74,604 bytes。同様に取得経路は判別不能。
- 両ローカルファイルの日本語名は、効果音ラボ公式ページのダウンロード名と一致するため公式サイト取得を示す状況証拠にはなるが、決定的な証明にはならない。
- `hito_ge_ugoki05.mp3`: On-Jin公式サイトのMP3と完全一致。On-Jinのニコニ・コモンズ投稿一覧および公開検索には該当する服擦れ音がなく、通常サイト版と判断できる。
- `フィルム巻き音.mp3`: ニコニ・コモンズに類似タイトルはあるが、形式・長さ・サイズが一致する候補を確認できない。
- `拒否音.mp3`: ニコニ・コモンズの「拒否」関連候補に、形式・長さ・サイズが一致するものを確認できない。

保守的には、`弓矢が刺さる.mp3` と `自動車事故.mp3` を効果音ラボ公式ページから改めて取得し、取得日、配布ページ、当日の利用規約、SHA-256を一緒に保存してください。今回の監査では公式サイトから取得したファイルが既存ファイルと完全一致するところまで確認済みです。

効果音ラボ公式: [利用規約](https://soundeffect-lab.info/agreement/) / [取得元によって条件が変わる旨のFAQ](https://soundeffect-lab.info/faq/) / [弓矢が刺さる](https://soundeffect-lab.info/sound/battle/) / [自動車事故](https://soundeffect-lab.info/sound/machine/machine2.html)  
ニコニ・コモンズ: [弓矢が刺さる nc278975](https://commons.nicovideo.jp/works/nc278975) / [自動車事故 nc278952](https://commons.nicovideo.jp/works/nc278952)

On-Jin公式サイト版は、個人・サークル・学校関係・ボランティアの組込み利用について、営利・非営利を問わず販売等を許可しています。企業・営利事業・プロ活動では利用詳細の連絡と許諾返信が必要です。ニコニ・コモンズ向け高音質版はニコニコ関連制作に限定され、すべての営利利用が禁止されています。  
公式: [On-Jin利用規約](https://on-jin.com/kiyaku.php) / [動作カテゴリ](https://on-jin.com/sound/hito.php?kate=%E5%8B%95%E4%BD%9C) / [ニコニ・コモンズ投稿一覧](https://on-jin.com/sound/comm.php)

OtoLogicはCC BY 4.0で商用・改変を許可していますが、無償利用ではクレジットが必須です。現在、ゲーム内クレジットまたは第三者表記ファイルは見つかりませんでした。  
公式: [OtoLogic利用規約](https://otologic.jp/free/license) / [クレジット表記FAQ](https://otologic.jp/free/faq)

Pixabayは素材ID 399749のページでPixabay Content Licenseを明示しています。同ライセンスは無償利用、無帰属、改変を許可し、素材の単体配布等を禁止しています。  
公式: [素材ページ](https://pixabay.com/sound-effects/film-special-effects-minimalist-button-hover-sound-effect-399749/) / [ライセンス概要](https://pixabay.com/service/license-summary/)

Springin’ Sound Stockは商用ゲームでの利用を許可しています。クレジットは通常必須ではありませんが、素材の再配布、Content ID登録等は禁止されています。  
公式: [該当素材一覧](https://www.springin.org/sound-stock/subcategory/meal/) / [利用規約・FAQ](https://www.springin.org/sound-stock/guideline/)

## 画像・UI

### 配布元を確認できたもの

| ファイル | 使用状況 | 出所 | 判定 | 条件 |
|---|---:|---|---|---|
| `Images/ic_system_caret-down_01_128.png` | Main、Memorize、Memorize_2 | macOSダウンロード属性に旧 `free-ui-assets.yurinchi2525.com`。現在はFree Game UI Assetsへ移転 | 商用可 | CC0 1.0。商用利用、改変、再配布可、帰属表示不要。公式の[ライセンス](https://freegameui.net/license/)を保存推奨 |
| `Images/Main/e1421_1.png` | Main | macOSダウンロード属性に `fukidesign.com` | 条件付きで商用可 | ゲーム利用可、20素材まで無料、クレジット不要。素材販売・データ再配布は禁止。本作で確認できた同サイト素材は1点。公式の[利用規約](https://fukidesign.com/terms/)参照 |
| `Images/Texture/25071032.jpg` | 未使用 | macOSダウンロード属性にイラストACと素材ID 25071032 | 条件付きで商用可・未使用 | ゲームの構成要素として商用利用可。素材単体の頒布等は禁止。現在未参照のため削除候補。公式の[利用規約](https://www.ac-illust.com/main/terms.php)参照 |

### 作者・許諾記録が必要なもの

次のグループは、見た目の統一性やファイル名からプロジェクト用の自作・依頼制作物である可能性があります。しかし、著作権はファイルの所持やGitへのコミットだけでは確認できません。リポジトリ内には作者名、発注書、利用許諾、譲渡契約、生成ツールの利用記録がありません。

- `Images/Character` の立ち絵、アイコン、黒い怪物、`dd.png`
- `Images/BackGround` の背景6点
- `Images/Title` のタイトルロゴ、背景、ボタン類
- `Images/Main` のUI一式。ただし上表の `e1421_1.png` を除く
- `Images/Tuning` の調律・定着UI一式
- `CameraFilms.png`、`Loupe.png`、`blackFlame.png`、`cursor.png`、`hand.png`、`target.png`
- `LostNine_アイコン.png`、`SofumeLogo_WhiteTextAlpha_1920x1080.png`、`Takumi_logo.png`、`logo.png`

このグループの判定はすべて **権利証跡待ち** です。作者本人が本プロジェクトの権利者なら、少なくとも次を記載した署名またはメール記録を保存してください。

- 作者、制作日、対象ファイル一覧
- 完全な自作か、写真・フォント・ブラシ・生成AI・テンプレート等を使用したか
- LostNineでの商用利用、改変、宣伝素材利用、全地域・全プラットフォームでの配布の可否
- 外注の場合は著作権譲渡の範囲、または永続的な利用許諾。必要に応じて著作者人格権を行使しない旨

Google Driveから取得した履歴が残る `main_Panel.png`、`main_counter*.png`、`main_lostshelf.png`、`memorizer.png`、`name_bg.png`、`notebook.png` 等についても、Google Driveは受け渡し方法しか示さず、権利の証明にはなりません。

### 明確に注意が必要な画像

`Images/Character/undefined - Imgur.png` はファイル名からImgur経由と推測され、出所・作者・許諾を確認できません。現在は参照されていないため、ソース配布にも含めないことを推奨します。

## フォントとTextMesh Pro素材

| 素材 | 判定 | 条件・現状 |
|---|---|---|
| `Font/DotGothic16-Regular.ttf` と生成済みSDF | 条件付きで商用可 | FontworksのDotGothic16はSIL Open Font License 1.1。ゲームへの埋込み・商用配布可。フォント単体販売は禁止。著作権表示とOFL本文を各配布物に含める必要がある。現在、DotGothic16用のOFL本文はプロジェクト内にない |
| `TextMesh Pro/Fonts/LiberationSans.ttf` とSDF | 条件付きで商用可 | SIL OFL 1.1本文が同梱済み。フォント単体販売は禁止。著作権表示とライセンスを維持する |
| `TextMesh Pro/Sprites/EmojiOne.png` / `EmojiOne.asset` | 権利版の確認待ち | TMP Settingsの既定Sprite Assetとして参照され、`Resources`配下のAssetから画像が参照されている。同梱のAttribution.txtはEmojiOneを出所として示すだけで、バージョンと適用ライセンスが書かれていない。EmojiOne v2系画像はCC BY 4.0で配布された実績があるが、この画像がその版だと断定できない。絵文字を使わないなら既定参照を解除して素材を除外するのが確実。使う場合はUnity/TMP配布時点の版とライセンスを特定し、必要な帰属表示を行う |

DotGothic16公式: [README](https://github.com/fontworks-fonts/DotGothic16/blob/master/README.md) / [OFL 1.1本文と著作権表示](https://github.com/fontworks-fonts/DotGothic16/blob/master/OFL.txt)  
EmojiOne v2のライセンス参考（ローカル画像の版を証明する資料ではない）: [EmojiOne 2.2.7由来素材の説明](https://github.com/EmojiTwo/emojitwo/blob/master/README.md) / [CC BY 4.0原文](https://creativecommons.org/licenses/by/4.0/legalcode)

## コード・パッケージ

| パッケージ | 判定 | 条件・現状 |
|---|---|---|
| DOTween Free | 商用可 | 公式ライセンスは商用・非商用利用を許可。改変版の再配布は禁止。元のcopyright、disclaimer、readmeを維持する。プロジェクトにはreadmeと公式ライセンスURLあり。Pro用DLLは確認されなかった |
| UniTask | 商用可 | MIT License。Copyright (c) 2019 Yoshifumi Kawai / Cysharp, Inc. ライセンスと著作権表示を複製物または重要部分に含める |
| UnityMCP | 商用可 | MIT License。Copyright (c) 2025 CoplayDev。通常はEditor開発用でゲーム本体には入らないが、ソース配布ではライセンスを維持する |
| Unity公式パッケージ | 条件付きで商用可 | 各Package CacheにLICENSE.mdやThird Party Noticesがある。Unityの有効なライセンスと各パッケージ条件に従う。Unity依存コードの多くはUnity Companion LicenseでUnity製品への組込み・配布が許可される |

公式: [DOTween License](https://dotween.demigiant.com/license.php) / [UniTask MIT License](https://github.com/Cysharp/UniTask/blob/master/LICENSE) / [UnityMCP MIT License](https://github.com/CoplayDev/unity-mcp/blob/main/LICENSE) / [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license)

## シナリオ、プログラム、ロゴ名

シナリオ本文、ゲーム設計、独自コードも著作物です。Git履歴では `Takumi` / `Takumi-UNV3090` という表示名に複数メールアドレスとGitHubアカウントが使われています。同一人物であるか、共同制作物が含まれるかを確認し、共同制作者がいる場合は商用化への同意と権利帰属を文書化してください。

学校課題、コンテスト、サークル制作として作成した素材がある場合は、その提出規約や所属先との契約も確認対象です。Gitのコミット記録は制作時期の補助証拠にはなりますが、第三者からの権利譲渡や素材サイトの使用許諾を証明するものではありません。

作品名、ロゴ、キャラクター名については著作権とは別に商標の確認が必要です。販売地域ごとに「LostNine」「ロストナイン」および主要ロゴ・名称の先行商標を公開前に検索してください。

## 現在参照されていない素材

以下はUnity GUID参照の静的走査で参照が見つからなかったものです。`Assets/Resources`内にメディア素材はなく、Addressables設定も見つかりませんでした。削除前にはUnity Editor上で再確認してください。

### 音源

- `Audio/BGM/rokugatsunoamaoto.mp3`
- `Audio/SE/Inspiration11-2(Mid).mp3`
- `Audio/SE/Inspiration11-3(High).mp3`

### 画像

- `Images/Character/undefined - Imgur.png`
- `Images/LostNine_アイコン.png`
- `Images/Main/main_Panel_ex.png`
- `Images/Main/main_Panel_main.png`
- `Images/SofumeLogo_WhiteTextAlpha_1920x1080.png`
- `Images/Takumi_logo.png`
- `Images/Texture/25071032.jpg`
- `Images/Texture/texture_PixelNoise_512px.png`（`(1)`付きの別ファイルは使用中）
- `Images/Tuning/flame_Sq.png`
- `Images/Tuning/texture_Concentric_512px.png`
- `Images/logo.png`
- `Images/golden_flower.png`（ビルド対象外の `Scenes/test.unity` からのみ参照）
- `Images/Character/ヒイラギ` 内のファイル名末尾が ` 1.PNG` の18点。対応する末尾なしファイルとSHA-256が完全一致する重複
- `Images/Character/ヒイラギ/egao_kuchitoji.PNG`
- `Images/Character/ヒイラギ/ginen_kuchiake.PNG`
- `Images/Character/ヒイラギ/ginen_kuchitoji.PNG`
- `Images/Character/ヒイラギ/odoroki_kuchitoji.PNG`
- `Images/Character/ヒイラギ/oko_kuchiake.PNG`
- `Images/Character/ヒイラギ/oko_kuchitoji.PNG`
- `Images/Character/ヒイラギ/tsujou_kuchiake.PNG`

DOTweenの`Editor/Imgs`はゲーム素材ではなくEditor拡張の構成物です。シーンから参照されないことを理由に個別削除せず、DOTweenパッケージとして扱ってください。

## 推奨クレジット案

ライセンス条件を満たす最低限の叩き台です。EmojiOneを除外する場合は該当行を削除できます。

```text
Music: 甘茶の音楽工房 (https://amachamusic.chagasi.com/)
Sound effects: OtoLogic (https://otologic.jp/) / CC BY 4.0
Sound effects: On-Jin ～音人～ (https://on-jin.com/)
Sound effects: Springin’ Sound Stock
“Minimalist Button Hover Sound Effect” by Lesiakower, Pixabay Content License
DotGothic16: Copyright 2020 The DotGothic16 Project Authors, SIL Open Font License 1.1
Emoji artwork: EmojiOne, CC BY 4.0（ローカル画像が該当版だと確認できた場合のみ）
UniTask: Copyright (c) 2019 Yoshifumi Kawai / Cysharp, Inc., MIT License
DOTween: Copyright (c) 2014 Daniele Giardini - Demigiant
```

クレジット画面だけでなく、OFL・MIT・CC BYの必要な全文またはリンク、著作権表示を `ThirdPartyNotices` としてビルド配布物にも含めるのが安全です。

## 証跡として保存すべきもの

- 各素材の配布ページURL、ダウンロード日、当時の規約PDFまたは画面保存
- 有償素材の注文番号、領収書、購入アカウント、素材ID
- 作者・外注先との契約、メール、チャット。対象ファイルが分かる一覧
- 自作素材の作者宣誓、制作元データ、生成履歴
- リリース時点の `ThirdPartyNotices` とゲーム内クレジットの画面保存
- ストア提出日ごとのライセンス監査版。規約は変更され得るため、取得時と公開時の双方を残す

## 監査方法と限界

- `.meta`のGUIDを、Scene、Prefab、ScriptableObject、Assetから静的に逆引きした。
- `EditorBuildSettings.asset`ではTitle、Main、ChapterSelect、Story、Communication、Memorize、Memorize_2、ScenarioTestが有効。`test.unity`はビルド対象外。
- 音源のID3、長さ、ビットレート、画像のmacOSダウンロード元属性、同一ファイルのSHA-256を確認した。
- 効果音ラボとOn-Jinについては、監査日時点の公式配布ファイルを一時取得し、ローカルファイルとのSHA-256・バイト数を照合した。ニコニ・コモンズは公開APIで素材ID、ファイル形式、サイズ、利用条件を照合した。
- ファイル名だけで似たオンライン素材が見つかった場合は、音声内容が一致しない限り出所確定とは扱っていない。
- Unityの動的ロードやビルド時処理を完全に証明するものではない。最終リリースビルドに対するファイル一覧とクレジット表示の確認が別途必要。
