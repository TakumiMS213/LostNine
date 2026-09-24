using UnityEngine;
using DG.Tweening;

public class LostNineLogoAnimation : MonoBehaviour
{
    [Header("🔸基本揺れ設定")]
    public float shakeRange = 4f;
    public float shakeDuration = 0.04f;
    public float interval = 0.15f;

    [Header("🔸揺れタイミングのランダム性")]
    public float intervalRandom = 0.08f;

    [Header("🔸回転ノイズ")]
    public bool enableRotation = true;
    public float rotationRange = 3f;

    [Header("🔸バーストモード（連続グリッチ）")]
    public bool enableBurst = true;
    public int burstCountMin = 2;
    public int burstCountMax = 5;
    public float burstChance = 0.18f;

    Vector3 originalPos;
    Quaternion originalRot;

    // 現在動いているシーケンスの参照（Kill用）
    Sequence _activeSeq;

    void Start()
    {
        originalPos = transform.localPosition;
        originalRot = transform.localRotation;
        StartGlitchLoop();
    }

    void StartGlitchLoop()
    {
        // 前のシーケンスをクリア
        _activeSeq?.Kill();

        var seq = DOTween.Sequence().SetLink(gameObject);
        _activeSeq = seq;

        // 1. 揺らす
        seq.AppendCallback(() =>
        {
            Vector3 offset = new Vector3(
                Random.Range(-shakeRange, shakeRange),
                Random.Range(-shakeRange, shakeRange),
                0
            );
            transform.DOLocalMove(originalPos + offset, shakeDuration).SetEase(Ease.OutQuad).SetLink(gameObject);

            if (enableRotation)
            {
                float rot = Random.Range(-rotationRange, rotationRange);
                transform.DOLocalRotate(new Vector3(0, 0, rot), shakeDuration).SetLink(gameObject);
            }
        });

        seq.AppendInterval(shakeDuration);

        // 2. 戻す
        seq.AppendCallback(() =>
        {
            transform.DOLocalMove(originalPos, 0.02f).SetLink(gameObject);
            if (enableRotation) transform.DOLocalRotateQuaternion(originalRot, 0.02f).SetLink(gameObject);
        });

        seq.AppendInterval(GetInterval());

        // 3. バースト抽選 → バーストが出たら StartBurst が次のループも責任を持つ
        seq.AppendCallback(() =>
        {
            if (enableBurst && Random.value < burstChance)
            {
                // ── FIX: バースト用に独立したシーケンスを起動し、
                //         既存の seq（ロック済み）には一切触らない ──
                StartBurst();
                return; // StartBurst 内で次ループを呼ぶ
            }
            // バーストなし → そのまま次ループ
            StartGlitchLoop();
        });
    }

    void StartBurst()
    {
        // ── FIX: 実行中の seq には追記せず、完全に新しいシーケンスを作る ──
        _activeSeq?.Kill();

        int burstCount = Random.Range(burstCountMin, burstCountMax + 1);
        var burst = DOTween.Sequence().SetLink(gameObject);
        _activeSeq = burst;

        for (int i = 0; i < burstCount; i++)
        {
            // ループ変数をキャプチャするためローカルにコピー不要（ラムダ内は値を直接使う）
            burst.AppendCallback(() =>
            {
                Vector3 offset = new Vector3(
                    Random.Range(-shakeRange, shakeRange),
                    Random.Range(-shakeRange, shakeRange),
                    0
                );
                transform.localPosition = originalPos + offset;

                if (enableRotation)
                {
                    float rot = Random.Range(-rotationRange, rotationRange);
                    transform.localRotation = Quaternion.Euler(0, 0, rot);
                }
            });

            burst.AppendInterval(shakeDuration * Random.Range(0.6f, 1.2f));
        }

        // バースト後に元位置へ戻す
        burst.AppendCallback(() =>
        {
            transform.localPosition = originalPos;
            if (enableRotation) transform.localRotation = originalRot;
        });

        // バースト完了 → 通常ループを再開
        burst.OnComplete(StartGlitchLoop);
    }

    float GetInterval()
    {
        return interval + Random.Range(-intervalRandom, intervalRandom);
    }

    void OnDisable()
    {
        _activeSeq?.Kill();
        _activeSeq = null;
        transform.localPosition = originalPos;
        transform.localRotation = originalRot;
    }
}
