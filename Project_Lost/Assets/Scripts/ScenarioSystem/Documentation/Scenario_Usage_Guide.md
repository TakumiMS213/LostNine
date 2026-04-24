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
※この方法を使うには、あらかじめ作成した `ScenarioData` を **`ScenarioDataDatabase`** に登録しておく必要があります。

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

Main シーンの任意のオブジェクトに **`AutoPlayProgressScenario`** コンポーネントをアタッチしておくだけで動きます。
`Start()` 実行時に `ProgressManager` の現在のキー（例: `Ch1_Prologue`）を取得し、DB から検索して自動再生します。

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

これだけで、裏の進行を一時停止させたまま、オーバーレイだけが実行されます。

### B. ProgressManager などの「ノードフロー」から再生したい場合
`GameFlowDirector` のフロー（進行手順）としてシナリオを組み込む場合は、旧システムの `TalkStep` の代わりに **`ScenarioTalkStep`** を使用します。

1. プロジェクトビューで右クリックし、 `Create` > `Flow` > `Steps` > `Scenario Talk Step` を作成。
2. インスペクターで再生したい `ScenarioData` を紐付ける。
3. `GameFlowDirector` の `Steps` 配列に挿入する。

### C. シナリオ作成中の「テスト再生」をしたい場合
メインシーン以外で、手軽に1つのシナリオだけをテスト再生したい場合は `ScenarioBootstrap` を使います。

1. テスト用の空のシーンを作成し、メニューから `Scenario System` > `Setup Minimal Scene` を実行。
2. 生成された `ScenarioSystem` オブジェクトを選ぶ。
3. `ScenarioBootstrap` コンポーネントの `Test Scenario` に再生したいデータを入れる。
4. **Auto Play** にチェックを入れて Unity を再生（Play）する。

---

## 3. シナリオデータ (SO) の作り方

1. **ベースの作成**: `Create` > `Scenario` > `Scenario Data` でデータを作ります。
2. **アクションの作成**: `Create` > `Scenario` > `Actions` > `Dialogue`（または `Wait`, `Choice`, `Overlay` 等）を作ります。
3. **リストへの登録**: 作った Action を シナリオデータ（SO）の `Actions` リストの一番下にドラッグ＆ドロップして並べます。

### 💡 DialogueAction（会話）の便利な使い方
会話を登録する際、**DialogueAction は「1ファイル＝1セリフ」にする必要はありません**。
新機能の「マルチステップ機能」により、1つの `DialogueAction` ファイルの中に **Entries リスト** があり、そこに複数のセリフ（話者名やテキスト）を無数に追加できます。会話ブロックごとに1つの Action ファイルを作ると整理しやすくなります。

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

### B. キーワードシナリオ（クリック時）
UI上の光る単語（キーワード）をクリックした際に再生されるシナリオです。
* **ルール**: `<link="○○">` タグで囲んだ `○○` の文字列が、そのままシナリオIDになります。
* **具体例**:
  * `<link="Apple">` と記載した場合、シナリオID **`Apple`** を持つ `ScenarioData` が自動で探し出されて再生されます。

### C. ダミーキーワード（シナリオを持たないキーワード）
クリックさせたいけれど、固有のシナリオは用意せず、かつゲームの進行も進めない（ハズレの）キーワード用の命名規則です。
* **ルール**: 文字列の先頭に `dummy_` を付けます。
* **具体例**:
  * `<link="dummy_A">` にすると、クリックしても話は進まず、シナリオも呼ばれません。（SEと演出だけ光ります）
