using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Teichaku.Data;

namespace Teichaku.Core
{
    /// <summary>
    /// 定着ミニゲーム（一筆書きパズル）のメインコントローラ。
    /// ステージ生成、入力ハンドリング、クリア/失敗判定を管理する。
    /// </summary>
    public class TeichakuManager : MonoBehaviour
    {
        #region Serialized Fields

        [Header("ステージ設定")]
        [Tooltip("章ごとのステージデータ（インデックス = 章番号 - 1）")]
        [SerializeField] private TeichakuStageData[] stageDataList;

        [Header("タイル")]
        [Tooltip("タイルのプレハブ（Image + TeichakuTile を持つ）")]
        [SerializeField] private TeichakuTile tilePrefab;

        [Tooltip("タイルの親となるRectTransform（Canvas内のコンテナ）")]
        [SerializeField] private RectTransform gridParent;

        [Header("レイアウト")]
        [Tooltip("タイル1つのサイズ（ピクセル）")]
        [SerializeField] private float tileSize = 80f;

        [Tooltip("タイル間の隙間（ピクセル）")]
        [SerializeField] private float tileSpacing = 4f;

        [Header("コンポーネント")]
        [Tooltip("演出処理を行うTeichakuFeedbackコンポーネント")]
        [SerializeField] private TeichakuFeedback feedback;

        #endregion

        #region Events

        /// <summary>クリア時に発火</summary>
        public event Action OnTeichakuClear;

        /// <summary>失敗時に発火</summary>
        public event Action OnTeichakuFail;

        #endregion

        #region Private State

        private TeichakuStageData _currentStageData;
        private List<TeichakuTile> _activeTiles = new List<TeichakuTile>();
        private List<TeichakuTile> _currentPath = new List<TeichakuTile>();
        private bool _isDragging;
        private bool _isActive;
        private int _totalActiveTileCount;

        #endregion

        #region Properties

        public bool IsActive => _isActive;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_isActive) return;

            // マウスボタンを離した時の検知
            if (_isDragging && Input.GetMouseButtonUp(0))
            {
                OnDragEnd();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// ステージを初期化する。ProgressManagerから章番号を取得してステージデータを選択。
        /// </summary>
        public void Initialize()
        {
            if (stageDataList == null || stageDataList.Length == 0)
            {
                Debug.LogError("[TeichakuManager] No stage data assigned!");
                return;
            }

            // ProgressManagerから現在の章を取得
            int chapter = ProgressManager.Instance != null ? ProgressManager.Instance.CurrentChapter : 1;
            int index = Mathf.Clamp(chapter - 1, 0, stageDataList.Length - 1);

            _currentStageData = stageDataList[index];
            if (_currentStageData == null)
            {
                Debug.LogError("[TeichakuManager] Stage data is null!");
                return;
            }

            BuildStage();
            _isActive = true;

            Debug.Log($"[TeichakuManager] Initialized for Chapter {chapter} ({_totalActiveTileCount} tiles)");
        }

        /// <summary>
        /// 外部からステージデータを設定して初期化
        /// </summary>
        public void SetStageData(TeichakuStageData data)
        {
            _currentStageData = data;
            BuildStage();
            _isActive = true;
        }

        public void SetActive(bool active) => _isActive = active;

        #endregion

        #region Stage Building

        /// <summary>
        /// ステージデータに基づいてタイルを動的生成する
        /// </summary>
        private void BuildStage()
        {
            // 既存タイルをクリア
            ClearTiles();

            if (_currentStageData == null || tilePrefab == null || gridParent == null) return;

            int w = _currentStageData.width;
            int h = _currentStageData.height;

            // グリッド全体のサイズを計算
            float gridWidth = w * tileSize + (w - 1) * tileSpacing;
            float gridHeight = h * tileSize + (h - 1) * tileSpacing;

            // グリッドの左上基準のオフセット（中央揃え）
            float startX = -gridWidth * 0.5f + tileSize * 0.5f;
            float startY = gridHeight * 0.5f - tileSize * 0.5f;

            _totalActiveTileCount = 0;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!_currentStageData.IsTileActive(x, y)) continue;

                    TeichakuTile tile = Instantiate(tilePrefab, gridParent);
                    tile.Initialize(x, y, this);

                    // RectTransformの設定
                    RectTransform rt = tile.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(tileSize, tileSize);
                    rt.anchoredPosition = new Vector2(
                        startX + x * (tileSize + tileSpacing),
                        startY - y * (tileSize + tileSpacing)
                    );

                    _activeTiles.Add(tile);
                    _totalActiveTileCount++;
                }
            }
        }

        /// <summary>
        /// 既存のタイルをすべて破棄する
        /// </summary>
        private void ClearTiles()
        {
            foreach (var tile in _activeTiles)
            {
                if (tile != null)
                {
                    Destroy(tile.gameObject);
                }
            }
            _activeTiles.Clear();
            _currentPath.Clear();
            _isDragging = false;
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// タイル上でマウスボタンを押した時（ドラッグ開始）
        /// </summary>
        public void OnTilePointerDown(TeichakuTile tile)
        {
            if (!_isActive || tile == null) return;

            // ドラッグ開始
            _isDragging = true;
            _currentPath.Clear();

            // 最初のタイルを訪問
            tile.SetVisited();
            _currentPath.Add(tile);

            feedback?.OnTileVisited(tile);
        }

        /// <summary>
        /// ドラッグ中にタイルの上にカーソルが入った時
        /// </summary>
        public void OnTilePointerEnter(TeichakuTile tile)
        {
            if (!_isActive || !_isDragging || tile == null) return;

            // 訪問済みタイルを再度なぞった場合 → 失敗
            if (tile.IsVisited)
            {
                OnFail();
                return;
            }

            // 最後に訪問したタイルと隣接しているか検証
            if (_currentPath.Count > 0)
            {
                TeichakuTile lastTile = _currentPath[_currentPath.Count - 1];
                if (!IsAdjacent(lastTile, tile))
                {
                    // 隣接していない場合は無視
                    return;
                }
            }

            // タイルを訪問
            tile.SetVisited();
            _currentPath.Add(tile);

            feedback?.OnTileVisited(tile);

            // 全タイルをなぞったかチェック
            if (_currentPath.Count >= _totalActiveTileCount)
            {
                OnClear();
            }
        }

        /// <summary>
        /// マウスを離した時
        /// </summary>
        private void OnDragEnd()
        {
            if (!_isDragging) return;
            _isDragging = false;

            // 全タイルをなぞり終わっていたらクリア済みなので何もしない
            if (_currentPath.Count >= _totalActiveTileCount) return;

            // まだなぞり終わっていない場合はリセット
            ResetStage();
        }

        #endregion

        #region Adjacency Check

        /// <summary>
        /// 2つのタイルが上下左右で隣接しているかを判定する
        /// </summary>
        private bool IsAdjacent(TeichakuTile a, TeichakuTile b)
        {
            int dx = Mathf.Abs(a.GridX - b.GridX);
            int dy = Mathf.Abs(a.GridY - b.GridY);

            // 上下左右のみ（斜めは不可）
            return (dx + dy) == 1;
        }

        #endregion

        #region Game Results

        /// <summary>
        /// クリア判定
        /// </summary>
        private void OnClear()
        {
            _isActive = false;
            _isDragging = false;

            feedback?.OnClear();
            OnTeichakuClear?.Invoke();
            Debug.Log("[TeichakuManager] Stage Clear!");
        }

        /// <summary>
        /// 失敗判定（訪問済みタイルを再なぞり）
        /// </summary>
        private void OnFail()
        {
            _isDragging = false;

            feedback?.OnFail();
            OnTeichakuFail?.Invoke();
            Debug.Log("[TeichakuManager] Failed! Resetting stage...");

            ResetStage();
        }

        /// <summary>
        /// ステージを初期状態にリセットする
        /// </summary>
        private void ResetStage()
        {
            _currentPath.Clear();
            foreach (var tile in _activeTiles)
            {
                if (tile != null)
                {
                    tile.ResetTile();
                }
            }
        }

        #endregion
    }
}
