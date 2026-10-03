using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// 立ち絵の可視ピクセルをステンシルに記録する。後から描くUIはその印を消すため、
/// 会話ウィンドウとの重なり順を変えずに立ち絵だけを色フィルターから除外できる。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public sealed class MemorizerTintMask : MonoBehaviour, IMaterialModifier
{
    // 通常のUI Maskが使う下位ビットとは分離する（Maskの入れ子は7段まで）。
    public const int PortraitBit = 128;
    private bool _isPortrait;
    private Material _material;

    public static void MarkPortrait(Image image)
    {
        if (image == null) return;
        var mask = image.GetComponent<MemorizerTintMask>();
        if (mask == null) mask = image.gameObject.AddComponent<MemorizerTintMask>();
        mask._isPortrait = true;
        image.SetMaterialDirty();
    }

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        var progress = ProgressManager.Instance;
        if (!isActiveAndEnabled || progress == null || !progress.IsMemorizerActive
            || gameObject.scene.name != progress.MainSceneName
            || !baseMaterial.HasProperty("_Stencil"))
            return baseMaterial;

        if (_material == null || _material.shader != baseMaterial.shader)
        {
            ReleaseMaterial();
            _material = new Material(baseMaterial) { hideFlags = HideFlags.HideAndDontSave };
        }
        _material.CopyPropertiesFromMaterial(baseMaterial);
        int reference = (int)baseMaterial.GetFloat("_Stencil") & ~PortraitBit;
        int readMask = (int)baseMaterial.GetFloat("_StencilReadMask") & ~PortraitBit;
        int writeMask = (int)baseMaterial.GetFloat("_StencilWriteMask") & ~PortraitBit;
        var operation = (StencilOp)(int)baseMaterial.GetFloat("_StencilOp");
        // 通常のGraphicはKeep。下位のUIマスクを変更せず、専用ビットだけを書く。
        if (operation == StencilOp.Keep) writeMask = 0;
        _material.SetInt("_Stencil", reference | (_isPortrait ? PortraitBit : 0));
        _material.SetInt("_StencilReadMask", readMask);
        _material.SetInt("_StencilWriteMask", writeMask | PortraitBit);
        _material.SetInt("_StencilOp", (int)StencilOp.Replace);
        _material.EnableKeyword("UNITY_UI_ALPHACLIP");
        if (_material.HasProperty("_UseUIAlphaClip")) _material.SetFloat("_UseUIAlphaClip", 1f);
        return _material;
    }

    private void OnEnable() => GetComponent<Graphic>().SetMaterialDirty();

    private void OnDisable()
    {
        GetComponent<Graphic>().SetMaterialDirty();
        ReleaseMaterial();
    }

    private void OnDestroy() => ReleaseMaterial();

    private void ReleaseMaterial()
    {
        if (_material != null) Destroy(_material);
        _material = null;
    }
}
