**LostNine リファクタリング・削除・キーワード色修正（2026-09-25 更新）**

大まかな構成とInspector参照を維持したリファクタリングに加え、追加指示に基づく限定的な削除と、キーワード色の再表示不具合を修正しました。以下は最終状態の報告です。

**キーワード色の根本修正**

確認した原因は、(1) 色変更がその瞬間の表示文字列にしか書き込まれない、(2) 再生・スキップ時に元テキストで上書きされる、(3) ClueAdapterがキーワード操作無効化時に発見状態までリセットする、(4) Mainの `clickableImmediately=true` では発見時の着色を通らない、の4点です。

- **発見済み・抽出済みとも黄色（#FFFF00）**を保持します。ユーザーの指定どおり、灰色には変更しません。
- ClueManagerがキーワードIDごとの色を保持し、DialogueViewが元のテキストから表示用テキストを生成します。会話アセットを書き換えません。
- 再生開始、スキップ表示、色変更通知、Viewの再有効化で同じ色適用処理を使用します。色変更だけでは表示済み文字数やタイピングの完了通知を変更しません。
- キーワード操作の無効／有効切替では色や発見状態をリセットしません。章番号が変わったとき、または `ResetForNewStage` を明示的に呼んだときにリセットします。同じ章のフェーズ進行では維持します。
- `ResetKeywordState` は対象IDの状態・色だけを消し、元の会話データに書かれていた文字色へ戻します。他のキーワードには影響しません。
- 表示前の `SetLinkColor`、同じIDの別行での再表示、改行を含むリンク、引用符付き／なしのTMPリンク、旧a href形式にも対応します。色タグの重複を防ぎ、太字など他の装飾は維持します。
- 状態の寿命はClueManagerのインスタンス単位です。セーブ／ロードやシーンをまたぐ永続保存は追加していません。ClueManagerが配置された既存のMain・Communicationで使用します。

主な変更先は `ClueManager.cs`、`KeywordHandler.cs`、新規 `KeywordTextFormatter.cs`、`DialogueView.cs`、`DialogueProviderAdapter.cs`、`ClueAdapter.cs`、`ScenarioEventBus.cs` です。

**追加対応：発見済みキーワードの再クリック防止**

- 発見済み・抽出済みのIDは再クリックできません。黄色の再表示は維持し、未発見の別IDは引き続き操作できます。ダミーキーワードも同じ扱いです。
- クリック開始とホバー表示で共通の発見判定を使用します。発見済みリンクは長押し・専用カーソルの対象外となり、通常の会話送りを妨げません。
- 長押し完了時にも再確認し、取得演出・クリック通知・キーワード会話の重複実行を防ぎます。通知前に発見状態を確定します。
- 既存の個別リセット・ステージリセット・章変更によるリセット後は再び操作できます。保存範囲は従来どおりClueManagerのインスタンス単位です。
- 追加対応後のUnity EditModeテストは **19件成功・0件失敗・0件スキップ**。再表示したTMPリンクのポインター入力、長押し完了の二重実行、即時クリック設定の両値、ダミーID、リセットを検証しました。下記の12件は追加対応前の結果です。

**削除したファイル**

Assets / Packages / ProjectSettings内のGUID参照、C#の型参照と用途を再確認しました。削除後も、削除したGUIDを参照するアセットが残っていないことを確認しています。以下のパスは `Project_Lost/` 基準です。Assets内の対象は対応する `.meta` も削除しています。

| 対象 | 根拠 |
| --- | --- |
| `Assets/Scripts/Main/MessageWindow_System 1/Core/MessageWindowManager.cs` | 現行システムへの移行後、シーン・Prefabからの参照も実行コードからの利用もない旧会話制御。依存先だった旧データ型も失われており、既存コンパイルエラーの原因だった |
| `Assets/Scripts/System_Script/SceneLoader.cs` | 未参照の独立した直接シーンロード用ヘルパー |
| `Assets/Scripts/System_Script/ObjectBlinker.cs` | 未参照の独立したSprite点滅サンプル |
| `Assets/Scripts/System_Script/BackGroundBlar.cs` | 未参照の独立した背景ぼかし実装（クラス名BackgroundBlur） |
| `Assets/Scripts/System_Script/UIMoves/ShowOutlineOnHover.cs` | 未参照の独立したアウトライン表示実装 |
| `Assets/Scripts/System_Script/UIMoves/GenerateOutlineOnHover.cs` | 未参照の独立したアウトライン生成実装 |
| `Assets/ScriptableObjects/ComunicationData/test/Test_Dl_1.asset` | 参照元がなく、m_Scriptも欠損した旧テストデータ |
| `Assets/ScriptableObjects/ComunicationData/test/Test_Ex_1_2.asset` | 同上 |
| `TestTMPLink.cs` | Assets外の単発正規表現検証コード。Unityコンパイル対象外、他コードからの利用なし |

併せて、Git追跡中の `.DS_Store` 8個を削除しました。場所はプロジェクトのリポジトリルート、`Project_Lost/`、`Assets/`、`Assets/Images/`、`Assets/Images/Character/`、`Assets/Scripts/`、`Assets/Scripts/Main/`、`Assets/Scripts/System_Script/` です。

削除により空になった `Assets/ScriptableObjects/ComunicationData/test/` と、その親 `ComunicationData/` をフォルダーの `.meta` とともに削除しました。使用中のKeywordHandler・EffectManager・CursorManagerがある旧メッセージフォルダーは残しています。

Unityの再読込に伴い、`Assets/Plugins/Demigiant/DOTween/Editor/DOTweenUpgradeManager.dll` と `.XML`、それぞれの `.meta` の4ファイルもDOTween側で自動削除されました。付属XMLには初回セットアップ／更新後にライブラリごと削除する旨が記載されており、一度復元しても再度自己削除されることを確認しています。これらは更新用補助ファイルで、DOTween本体・モジュールは保持しています。

**実施した変更**

以下のパスは `Project_Lost/Assets/Scripts/` 基準です。

| ファイル | 変更内容 | 維持した動作 |
| --- | --- | --- |
| `System_Script/UIMoves/MoveWithEasing.cs` | ワールド座標移動・UI座標移動で重複していたDOTweenフェード処理を `AppendFade` に集約 | 対象の優先順位、子Sequence、時間、完了コールバック。揺れは従来どおり独立Sequenceとしてフェードと並行実行 |
| `System_Script/ProgressManager.cs` | 7箇所のシーン遷移分岐を共通化。フェーズ数を一度だけ取得。キーワード登録の `Contains`＋`Add` を `Add` の戻り値による判定に変更 | 通常／簡易遷移の選択、SceneTransition不在時の直接ロード、進行更新・イベントの順序、重複判定、しきい値到達時のみ通知 |
| `System_Script/UIMoves/UISwitcher.cs` | 表示・非表示の境界チェックと操作を共通化 | 公開API、Escapeによる終了抑制、既存のnull・範囲チェック |
| `System_Script/UIMoves/MultiUISwitcher.cs` | グループ表示・非表示のループを共通化 | グループ順序、nullパネルのスキップ、公開API |
| `ScenarioSystem/Adapter/KeyWordDatabase.cs` | `TryAdd` で二重検索を削減。`GetById` から `TryGetById` に処理を集約 | IDのTrim、大文字小文字区別、重複時は先勝ち、未発見時の警告 |
| `ScenarioSystem/Adapter/ScenarioDataDatabase.cs` | 早期continueで入れ子を減らし、`TryAdd` を使用 | IDをTrimしない既存仕様、先勝ち、警告 |
| `ScenarioSystem/Adapter/LostNoteCharacterDatabase.cs` | 章番号登録を `TryAdd` に整理 | 遅延キャッシュ構築、先勝ち、警告 |
| `Main/MessageWindow_System 1/Core/KeywordHandler.cs` | 表示文字列の直接置換をやめ、ClueManagerのID別色状態を更新 | 既存の公開API・クリック・チャージ・会話要求を維持。再表示や改行リンクの色消失を修正 |
| `ScenarioSystem/View/DialogueView.cs` | 同じ待機時間の `WaitForSeconds` をコルーチン内で再利用 | 文字表示の順序、各ステップの待機時間、スキップ速度変更への追従、完了通知。通常再生では1文字ごとの生成を削減 |
| `Tuning/Core/TuningManager.cs` | NGゾーンのCanvasGroupをキャッシュし、取得／追加処理を共通化 | 領域判定、alpha補間、ゲーム判定。初期化時にキャッシュをクリアし、CanvasGroup破棄時には再取得／再生成 |
| `Tuning/Visuals/WaveformVisualizer.cs` | 隣接線分の共有点を再利用 | 波形の式、線分・三角形構成、更新タイミング。100分割点ならSin評価は198回から100回 |

演出とゲーム進行のタイミング維持を優先し、既存コルーチンのUniTaskへの一括置換は行っていません。既存のUniTask利用箇所は維持し、DOTweenは既存Sequenceの構造のまま共通化しました。

**検証結果と制約**

- Unity 6000.0.67f1でランタイム・Editor・テストアセンブリのコンパイル成功。旧MessageWindowManagerの削除により、初回調査で確認した12件のC#エラーは解消しました。
- Unity標準Test RunnerのEditMode回帰テストは **12件成功・0件失敗・0件スキップ**。テストは `Project_Lost/Assets/Tests/Editor/KeywordColorTests.cs` に保存しています。UnityのTest Runnerで `LostNine.EditModeTests` を選択して再実行できます。
- 検証対象は、発見／抽出後の再表示、即時クリック設定、操作無効／有効切替、個別／全体リセット、章とフェーズの違い、3種類のリンク記法と改行、表示前の色設定、文字表示数の維持、タイピングスキップ、View再有効化とProviderの整合です。
- テストでは一時Preview Sceneを使用し、終了時に閉じ、Singletonとイベント購読を復元しています。ゲーム側のAssembly-CSharp構成を変更しないため、テストアセンブリからはreflectionで対象へアクセスしています。
- Test Runner起動用に追加した一時Editorコードは除去済み。結果XMLは今回の作業環境の `/tmp/lostnine-keyword-tests.xml` にあります。
- 初回の一般リファクタリングでは、変更前後のDB・進行・波形30条件・DOTween移動8条件、UI切替・NGゾーン等をUnity上で比較済み。波形の計算整理による丸め差は最大0.0000577268438 UI単位で、ビット単位の一致ではありません。FPSは実測していません。
- `git diff --check` 成功。既存のシーン・Prefab・使用中のScriptableObjectデータ・パッケージ設定は変更していません。
- 全章の通しプレイ、マウス操作による実時間のチャージ演出、実シーン遷移は未検証です。今回のEditModeテストはゲーム全体のPlay Mode検証を置き換えるものではありません。

**残した未使用候補**

以下は配置・呼び出しが見つからなくても、現行の進行システムや他クラスとの依存関係があるため、今回の限定的な削除許可では残しました。パスは `Assets/Scripts/` 基準です。

| ファイル | 機能・判断材料 |
| --- | --- |
| `System_Script/ProgressBasedScenarioStarter.cs` | ボタンから進行状態に応じた会話を開始 |
| `System_Script/ChapterStartButton.cs` | 章を指定して開始するボタン用ヘルパー |
| `System_Script/Flow/Steps/JumpSequenceStep.cs` | 別StorySequenceへのジャンプ |
| `System_Script/Flow/Steps/ComuSteps.cs` | `ComuStep` / `ComuStartStep` / `ComuEndStep`。コミュニケーション開始・終了を待つFlow実装 |
| `System_Script/Flow/Steps/ObjectiveStep.cs` | 目標テキストを更新するFlow実装 |
| `System_Script/Flow/Steps/TeichakuStep.cs` | 定着クリアを待つFlow実装 |
| `System_Script/Flow/Steps/FadeInStep.cs` | フェードイン完了を待つFlow実装 |
| `ScenarioSystem/Adapter/ComuAdapter.cs` | コミュニケーション操作をEventBusから中継。`ComuAdapterExtended` との用途比較が必要 |
| `Communication/PortraitDropHandler.cs` | 立ち絵へのドロップから調律へ遷移 |
| `Main/DragToSceneItem.cs` | ドラッグ対象側で立ち絵へのドロップを判定して調律へ遷移 |
| `Main/MainSceneFlowController.cs` | メイン開始時のフェーズ分岐・シーケンス起動。未配置の将来実装か確認 |

| `System_Script/Flow/Testing/VerifyFlow.cs` | 未配置の手動テストだが、GameFlowDirectorやProgressManagerを利用するため保持 |

**ファイルの一部分だけが不要と思われるもの（未削除）**

| 対象 | 根拠・概要 |
| --- | --- |
| `Main/MemoryFragment.cs` の `startAngle` | フィールドの読み取りなし。実際の角度は `Initialize` の `startAngleDeg` を使用。コンパイラも未使用警告を出す |
| `Tuning/Visuals/WaveformVisualizer.cs` の `height`、`System.Collections.Generic`、`OnEnable` override | 高さとusingは未使用。overrideはbase呼び出しのみ |
| `Tuning/Visuals/NoiseVisualizer.cs` の `Update` の空のelse-if | 判定後に処理を行っていない |
| `Tuning/Core/TuningManager.cs` の `RandomizeTargetPlacement` の `defaultPos` / `margin` | 引数を受け取るが実装では読まず、固定paddingを使用 |
| `ScenarioSystem/View/DialogueLogView.cs` の `HandleScenarioStarted` とその購読 | ハンドラー本体がコメントだけで処理なし。ログ蓄積機能自体は使用中 |
| `Main/MessageWindow_System 1/Core/MessageWindowTester.cs` の `enableKeywords` | 宣言のみ。Startの再生処理はこの値を使用しない |
| `System_Script/UIMoves/MoveWithEasing.cs` のTextMeshProUGUI専用フェード分岐 | 先に全 `Graphic` を検索するため、Graphicの派生型であるTextMeshProUGUI専用の後段分岐は通常到達しない。今回は共通メソッド内にそのまま保持 |

**削除を急がないもの**

- `SampleMoveTester.cs` は `Main.unity` に配置済み。`MessageWindowTester.cs` は `Main.unity` と `Communication.unity`、`MessageWindowIndexStarter.cs` は `Main.unity` に参照があります。名前だけで不要と判断できません。
- `ScenarioTest.unity` はBuild Settingsで有効です。本番ビルドからの除外と、開発用シーン自体の削除は別に判断する必要があります。
- `EffectManager`、`CursorManager`、`KeywordHandler` は旧フォルダー内にありますが、現行のゲームコードから参照されています。`MessageWindow_System 1` フォルダー全体を削除する判断はできません。
- `ScenarioTalkStep` は進行用の3アセットから参照されています。似た名前の `TalkStep` と一括削除・統合はしていません。
- Editor用の `ScenarioSystemSetup`、`SceneSwitcherWindow`、`TeichakuStageDataEditor` は、シーンから参照されなくてもMenuItem・CustomEditorとして機能します。
- `Library/`、`Temp/`、`Logs/`、生成された `.csproj` / `.slnx` は再生成可能な開発用データですが、今回の削除対象外です。UniTask、DOTween、UnityMCPも保持しています。

**仕様変更になり得るため触れていない既存挙動**

`TuningManager.Initialize` はnullチェックより前に `stageSettingsList.Length` を読み、`SetSettings` で渡した設定は `Initialize` の章設定で上書きされます。また、`ProgressManager.StartFromChapter` は獲得数を0にしますが獲得済みID集合をクリアしません。これらは不具合の可能性がありますが、挙動を変える修正になるため今回は維持しました。
