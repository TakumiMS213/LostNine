using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// シーンを跨いで使用可能な音声再生マネージャー。
/// DontDestroyOnLoad で永続化し、AudioSource を動的生成するため
/// シーン遷移時の参照漏れが発生しない。
/// 音量制御は AudioMixerGroup に委譲する。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer Groups (Inspector設定)")]
    [Tooltip("SE用 AudioMixerGroup（未設定でも動作する）")]
    [SerializeField] private AudioMixerGroup seMixerGroup;
    [Tooltip("BGM用 AudioMixerGroup（未設定でも動作する）")]
    [SerializeField] private AudioMixerGroup bgmMixerGroup;

    private AudioSource _seSource;
    private AudioSource _bgmSource;

    #region Unity Lifecycle

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitAudioSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #endregion

    #region Initialization

    private void InitAudioSources()
    {
        // SE用 AudioSource
        _seSource = gameObject.AddComponent<AudioSource>();
        _seSource.playOnAwake = false;
        _seSource.loop = false;
        if (seMixerGroup != null) _seSource.outputAudioMixerGroup = seMixerGroup;

        // BGM用 AudioSource
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        if (bgmMixerGroup != null) _bgmSource.outputAudioMixerGroup = bgmMixerGroup;
    }

    #endregion

    #region SE

    /// <summary>
    /// SEをワンショット再生する。
    /// </summary>
    public void PlaySE(AudioClip clip)
    {
        if (_seSource == null || clip == null) return;
        _seSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Resources フォルダからSEをロードしてワンショット再生する。
    /// パスは "Audio/SE/" 以下のファイル名（拡張子なし）。
    /// </summary>
    public void PlaySE(string resourcePath)
    {
        if (_seSource == null || string.IsNullOrEmpty(resourcePath)) return;
        var clip = Resources.Load<AudioClip>($"Audio/SE/{resourcePath}");
        if (clip != null)
            _seSource.PlayOneShot(clip);
        else
            Debug.LogWarning($"[AudioManager] SE not found: Audio/SE/{resourcePath}");
    }

    #endregion

    #region BGM

    /// <summary>
    /// BGMをループ再生する。同一クリップが再生中の場合は無視する。
    /// </summary>
    public void PlayBGM(AudioClip clip)
    {
        if (_bgmSource == null || clip == null) return;
        if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

        _bgmSource.clip = clip;
        _bgmSource.loop = true;
        _bgmSource.Play();
    }

    /// <summary>
    /// Resources フォルダからBGMをロードしてループ再生する。
    /// パスは "Audio/BGM/" 以下のファイル名（拡張子なし）。
    /// </summary>
    public void PlayBGM(string resourcePath)
    {
        if (_bgmSource == null || string.IsNullOrEmpty(resourcePath)) return;
        var clip = Resources.Load<AudioClip>($"Audio/BGM/{resourcePath}");
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] BGM not found: Audio/BGM/{resourcePath}");
            return;
        }
        PlayBGM(clip);
    }

    /// <summary>
    /// BGMを停止する。
    /// </summary>
    public void StopBGM()
    {
        if (_bgmSource == null) return;
        _bgmSource.Stop();
        _bgmSource.clip = null;
    }

    /// <summary>
    /// BGMを一時停止する。
    /// </summary>
    public void PauseBGM()
    {
        _bgmSource?.Pause();
    }

    /// <summary>
    /// 一時停止中のBGMを再開する。
    /// </summary>
    public void ResumeBGM()
    {
        _bgmSource?.UnPause();
    }

    #endregion
}
