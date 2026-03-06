using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Teichakuクリア後にMainシーンへ戻った際、
/// チャプター固有のLostThingsオブジェクトを動的に生成・有効化する。
/// </summary>
public class LostThingsManager : MonoBehaviour
{
    public static LostThingsManager Instance { get; private set; }

    [Serializable]
    public class ChapterLostThings
    {
        [Tooltip("チャプター番号")]
        public int chapter;

        [Tooltip("生成するLostThingsプレハブ（3つ）")]
        public GameObject[] prefabs;

        [Tooltip("生成先の位置（プレハブ数に対応）")]
        public Transform[] spawnPoints;
    }

    [Header("Chapter Configuration")]
    [SerializeField] private List<ChapterLostThings> chapterConfigs;

    [Header("Default Spawn Parent")]
    [Tooltip("生成先の親Transform（nullの場合シーンルートに配置）")]
    [SerializeField] private Transform spawnParent;

    private readonly List<GameObject> _spawnedObjects = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        // Fixationフェーズであれば、Teichakuクリア後にMain復帰したということ
        if (ProgressManager.Instance != null &&
            ProgressManager.Instance.CurrentPhase == GamePhase.Fixation)
        {
            ActivateForCurrentChapter();
            // Fixation→Presentationフェーズへ移行
            ProgressManager.Instance.SetProgress(
                ProgressManager.Instance.CurrentChapter, GamePhase.Presentation);
        }
    }

    /// <summary>
    /// 現在のチャプターに対応するLostThingsオブジェクトを生成する。
    /// </summary>
    public void ActivateForCurrentChapter()
    {
        if (ProgressManager.Instance == null) return;

        int chapter = ProgressManager.Instance.CurrentChapter;
        var config = chapterConfigs?.Find(c => c.chapter == chapter);

        if (config == null || config.prefabs == null)
        {
            Debug.LogWarning($"[LostThingsManager] No config for chapter {chapter}.");
            return;
        }

        // 既存のオブジェクトをクリア
        ClearSpawned();

        for (int i = 0; i < config.prefabs.Length; i++)
        {
            if (config.prefabs[i] == null) continue;

            Transform spawnPoint = (config.spawnPoints != null && i < config.spawnPoints.Length)
                ? config.spawnPoints[i] : null;

            Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
            Transform parent = spawnParent != null ? spawnParent : null;

            var obj = Instantiate(config.prefabs[i], pos, rot, parent);
            _spawnedObjects.Add(obj);

            Debug.Log($"[LostThingsManager] Spawned LostThing: {config.prefabs[i].name} for Ch{chapter}");
        }
    }

    /// <summary>
    /// 生成済みのLostThingsをすべて破棄する。
    /// </summary>
    public void ClearSpawned()
    {
        foreach (var obj in _spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedObjects.Clear();
    }
}
