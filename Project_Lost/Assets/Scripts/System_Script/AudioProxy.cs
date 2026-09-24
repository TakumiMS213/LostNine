using UnityEngine;

/// <summary>
/// Button.OnClick 等から AudioManager を安全に呼び出すための中継スクリプト。
/// シーン内のオブジェクトにアタッチして使用する。
/// DontDestroyOnLoad の AudioManager への直接参照を避けることで、
/// シーン遷移時の参照漏れを防止する。
///
/// 使い方:
///   Button.OnClick → AudioProxy.PlaySE(AudioClip) を選択し、
///   パラメータ欄に任意の AudioClip をドラッグ＆ドロップする。
///   1つの AudioProxy で複数のボタンから異なるクリップを再生可能。
/// </summary>
public class AudioProxy : MonoBehaviour
{
    /// <summary>
    /// 指定した SE を再生する。
    /// Button.OnClick のダイナミック引数で AudioClip を直接指定できる。
    /// </summary>
    public void PlaySE(AudioClip clip)
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[AudioProxy] AudioManager.Instance が見つかりません。");
            return;
        }
        AudioManager.Instance.PlaySE(clip);
    }

    /// <summary>
    /// 指定した BGM を再生する。
    /// Button.OnClick のダイナミック引数で AudioClip を直接指定できる。
    /// </summary>
    public void PlayBGM(AudioClip clip)
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[AudioProxy] AudioManager.Instance が見つかりません。");
            return;
        }
        AudioManager.Instance.PlayBGM(clip);
    }

    /// <summary>
    /// BGM を停止する。
    /// </summary>
    public void StopBGM()
    {
        AudioManager.Instance?.StopBGM();
    }

    /// <summary>
    /// BGM を一時停止する。
    /// </summary>
    public void PauseBGM()
    {
        AudioManager.Instance?.PauseBGM();
    }

    /// <summary>
    /// BGM を再開する。
    /// </summary>
    public void ResumeBGM()
    {
        AudioManager.Instance?.ResumeBGM();
    }
}
