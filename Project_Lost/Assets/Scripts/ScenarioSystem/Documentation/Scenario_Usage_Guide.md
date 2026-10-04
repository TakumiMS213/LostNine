# 新シナリオシステム 再生・運用ガイド

新しい ScenarioSystem での「シナリオの再生方法」について、用途別にまとめました。

---

## 1. スクリプトから直接呼び出す（一番基本の再生方法）

シナリオの再生はすべて、シーン内に配置されている `MessageWindowFacade.Instance` を経由して行います。

### A. ScenarioData (SO) を直接指定して再生
最も確実な方法です。Inspector で参照を割り当てておき、それを直接流し込みます。

```csharp
[SerializeField] private ScenarioData myScenario;

public void PlayMyScenario()
{
    // コールバック（再生終了時の処理）も設定できます
    MessageWindowFacade.Instance.StartScenario(myScenario, () => {
        Debug.Log("シナリオ再生が完了しました！");
    });
}
```

### B. シナリオID (文字列) で検索して再生
キャラクターやアイテムをクリックした時など、動的にID（例：`Ch1_Intro`）が生成される場合に用います。
※この方法を使うには、`ScenarioDataDatabase` にそのシナリオ、またはそのシナリオへ `nextScenario` / 選択肢でつながる入口を登録します。独立したキーワード詳細など、直参照でつながっていないシナリオは個別に登録します。従来の全件登録もそのまま使えます。

```csharp
public void PlayScenarioById(string scenarioId)
{
    MessageWindowFacade.Instance.StartScenarioById(scenarioId);
}
```
※ `ComuStartandEndManager` などの既存システムは、このメソッドを使って新システムに連動するように改修済みです。

---

## 2. 状況別の特殊な再生方法

### A. シーン開始時に Progress に応じたシナリオを自動再生したい場合
Title → Main や Tuning → Main など、**シーン遷移で Main に戻った時**にプロローグ等を自動再生する仕組みです。

本編のMainでは、既存の `MainSceneFlowController` と `GameFlowDirector` が進行状態に対応するFlowを開始します。Storyでは `StorySceneDirector` が担当します。既存の起動処理に重ねて自動再生コンポーネントを追加すると、会話の二重起動につながります。

独立した検証シーンなどで `AutoPlayProgressScenario` を使う場合は、`Start()` で `ProgressManager` のキー（例: `Ch1_Prologue`）を取得してDBから再生します。

### B. シナリオ内で Progress を変更し、次のフェーズに自動チェーンしたい場合
「プロローグが終わったら→対話フェーズのシナリオを自動的に続けて再生」のような流れを、**ScenarioData のアクションリスト内で宣言的に** 組む方法です。

`Create` > `Scenario` > `Actions` > `Progress Scenario` で作成し、アクションリストに配置します。

**構成例**（Ch1_Prologue のアクションリスト）:
```
1. DialogueAction（プロローグの会話テキスト）
2. ProgressUpdateAction（フェーズを Dialogue に変更）
3. ProgressScenarioAction  ← ここで Ch1_Dialogue を自動検索してチェーン再生
```

この Action は実行時に `ProgressManager.GetScenarioKey()` で現在のキーを動的に取得するため、インスペクターでの設定は不要（フィールドなし）です。

### C. ミニゲーム等で「オーバーレイ（ポップアップ）」だけを出したい場合
Mainシーンの黒い背景ウィンドウを隠したまま、画面中央などにテキストだけを表示したい場合のテクニックです。

1. 新しい `ScenarioData` を作成する。
2. インスペクターで **Show Main Window** のチェックを **外す (false)**。
3. Action に `OverlayAction`（割り込みテキスト）を追加し、秒数などを設定する。
4. 通常通り `StartScenario()` で再生する。

これでメインウィンドウを隠し、オーバーレイだけを表示できます。通常の `StartScenario` は現在のシナリオを置き換えます。元の会話へ戻したい場合は、`MessageWindowFacade.Instance.PlayTemporaryScenarioById(id)` を使います。詳細終了後、元の行を全文表示して復帰し、同じ行のログは追加しません。

### D. ProgressManager などの「ノードフロー」から再生したい場合
`GameFlowDirector` のフロー（進行手順）としてシナリオを組み込む場合は、旧システムの `TalkStep` の代わりに **`ScenarioTalkStep`** を使用します。

1. プロジェクトビューで右クリックし、 `Create` > `Flow` > `Steps` > `Scenario Talk Step` を作成。
2. インスペクターで再生したい `ScenarioData` を紐付ける。
3. `GameFlowDirector` の `Steps` 配列に挿入する。

### E. シナリオ作成中の「テスト再生」をしたい場合
メインシーン以外で、手軽に1つのシナリオだけをテスト再生したい場合は `ScenarioBootstrap` を使います。

1. テスト用の空のシーンを作成し、メニューから `Scenario System` > `Setup Minimal Scene` を実行。
2. 生成された `ScenarioSystem` オブジェクトを選ぶ。
3. `ScenarioBootstrap` コンポーネントの `Test Scenario` に再生したいデータを入れる。
4. **Auto Play** にチェックを入れて Unity を再生（Play）する。

### F. 対話開始ボタンをシナリオから表示したい場合
ScenarioDataのアクション一覧で `＋` → `新規作成` → `対話開始ボタン` を選び、`Visible` を設定します。独立アセットは `Create` > `Scenario` > `Actions` > `Dialogue Start Button` から作成できます。共通の表示・非表示アセットは `Actions/Other/ShowDialogueStartButton` と `HideDialogueStartButton` です。

`Visible = true` でPortrait前面に横長の `flame_Sq` と「対話開始」のボタンを表示し、`false` で隠します。非会話状態では、このボタンを押すと既存の会話開始演出と、現在の章・フェーズに応じたシナリオが始まります。Portrait本体のクリックでは開始しません。

会話中に表示すると「対話終了」に切り替わり、既存の会話終了処理を実行します。遷移アニメーション中や操作禁止中は表示を一時停止します。非表示指定は操作禁止の解除後にも保持します。

表示アクションはクリックを待たず、すぐ次のアクションへ進みます。開始待ちにする場合は案内の末尾に配置します。操作可否は既存の `PortraitInteractableAction`（`EnableClick` / `DisableClick`）と連動し、会話状態の切り替えアクション `ComuToggleAction` / `ComuToggleInstantAction` は引き続き使用できます。

---

## 3. シナリオデータ (SO) の作り方

1. **ベースの作成**: `Create` > `Scenario` > `Scenario Data` でデータを作ります。
2. **アクションの作成**: ScenarioDataのInspectorのアクション一覧で `＋` → `新規作成` → `会話`（または選択肢・オーバーレイ等）を選びます。新しいアクションはそのScenarioData内のサブアセットとして保存され、別ファイルの手動登録が不要です。
3. **本文の編集**: アクション左側の三角を開き、Entries内の話者・本文・立ち絵・背景などを編集します。項目は折りたたみ、一覧の順序はドラッグで変更できます。
4. **既存アクションの利用**: `＋` → `既存アクションの参照欄を追加` から既存アセットを割り当てます。従来どおり `Create` > `Scenario` > `Actions` で独立アセットを作る方法も使用できます。
5. **共有時の編集**: 複数シナリオが参照するアクションには件数を表示します。変更をそのシナリオだけへ適用する場合は `このシナリオ用に複製して差し替える` を使います。
6. **削除とUndo**: `－` は参照だけを外します。共有アセットやサブアセット自体は削除しません。追加・複製・参照削除はUndo / Redoに対応します。
7. **検証**: Inspectorの `シナリオデータを検証`、DBの `登録データを検証`、または `Scenario System` > `Validate Scenario Data` を実行します。指摘の対象をクリックして修正できます。検証はデータを自動変更しません。

### 💡 DialogueAction（会話）の便利な使い方
会話を登録する際、**DialogueAction は「1ファイル＝1セリフ」にする必要はありません**。
新機能の「マルチステップ機能」により、1つの `DialogueAction` ファイルの中に **Entries リスト** があり、そこに複数のセリフ（話者名やテキスト）を無数に追加できます。会話ブロックごとに1つの Action ファイルを作ると整理しやすくなります。

立ち絵は指定がなければ `Center`、背景は不要なら `None` を使用します。`None`はシナリオ用スチルを表示しない指定で、背面の通常背景を使用します。

アクション・シナリオは編集用の定義データです。再生位置・タイピング・選択待ちなどの状態は `ScenarioRuntimeState` に保持し、アセットへ書き戻しません。

### 検証で確認する項目

- IDの重複・前後空白、欠落参照、本文や選択肢の未設定
- キーワードIDに対応するシナリオ、無効な列挙値・数値
- 連続する同一アクション、末尾以外のシーン遷移など到達不能な設定
- loopとnextScenarioの競合、入力・時間待ちのない自動循環

空IDの直接参照シナリオや、入力待ちを含む通常のループは許可します。意図した繰り返しは警告の内容を確認して保持してください。

---

## 4. シナリオIDの命名ルール一覧

ゲーム内の様々なシステムが、どのシナリオデータを呼び出すかを「シナリオID」で一致判定しています。用途に応じたIDを `ScenarioData` の Inspector で設定してください。

### A. ProgressManager 連動（プロローグ・章ごとのメインシナリオ）
章とフェーズを組み合わせた以下の命名規則を使います。
* **基本フォーマット**: `Ch{章番号}_{フェーズ名}`
* **具体例**:
  * `Ch1_Prologue` （第1章プロローグ・`AutoPlayProgressScenario`等から自動再生）
  * `Ch1_Dialogue` （第1章「対話」フェーズ開始時のシナリオ）
  * `Ch1_Extraction` （第1章「抽出」フェーズ開始時）
  * `Ch2_Epilogue` （第2章クリア後）

コードから生成する場合は `ScenarioKey.ForPhase(chapter, phase)` を使用します。Story・loop・DialogueStartは `ScenarioKey.ForPurpose(chapter, purpose)` で生成します。検索は前後空白を除去しますが、大文字小文字を区別します。既存アセットのIDは自動で書き換えません。

`nextScenario`やFlowStepから直接参照するシナリオは、IDを空欄のまま使用できます。InspectorでID・参照・DB登録を編集すると検索キャッシュは次の検索で更新されます。コードから直接編集する場合は `ScenarioData.NotifyDataChanged()` / `ScenarioAction.NotifyDataChanged()`、DB登録リストなら `ScenarioDataDatabase.InvalidateCache()` を呼びます。

### B. キーワードシナリオ（クリック時）
UI上の光る単語（キーワード）をクリックした際に再生されるシナリオです。
プロローグのチュートリアル終了後、Shiftを押すたびにメモライザーのON/OFFを切り替えます。ONの間にキーワードを長押しして抽出します。抽出フェーズ以外でも使用でき、発見済みの語は黄色を保持して再抽出できなくなります。
* **ルール**: `<link="○○">` タグで囲んだ `○○` の文字列が、そのままシナリオIDになります。
* **具体例**:
  * `<link="Apple">` と記載した場合、シナリオID **`Apple`** を持つ `ScenarioData` が自動で探し出されて再生されます。

### C. ダミーキーワード（シナリオを持たないキーワード）
クリックさせたいけれど、固有のシナリオは用意せず、かつゲームの進行も進めない（ハズレの）キーワード用の命名規則です。
* **ルール**: 文字列の先頭に `dummy_` を付けます。
* **具体例**:
  * `<link="dummy_A">` にすると、クリックしても話は進まず、シナリオも呼ばれません。（SEと演出だけ光ります）
