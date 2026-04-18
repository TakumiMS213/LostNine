using UnityEngine;

namespace MessageWindowSystem.Core
{
    /// <summary>
    /// カーソルの見た目を変更する処理だけを担当するシングルトン。
    /// ハードウェアカーソル方式（Cursor.SetCursor）を使用。
    /// どのタイミングで変更するかのロジックは持たない（MVP の Presenter 層）。
    /// </summary>
    public class CursorManager : MonoBehaviour
    {
        #region Singleton

        public static CursorManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Default Cursor")]
        [Tooltip("ゲームプレイ中の通常カーソル。null の場合は OS デフォルトを使用。")]
        [SerializeField] private Texture2D defaultCursor;

        [Tooltip("通常カーソルのクリック位置オフセット（左上からのピクセル数）。")]
        [SerializeField] private Vector2 defaultHotspot = Vector2.zero;

        #endregion

        #region Private Fields

        private Texture2D _currentTexture;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            ResetToDefault();
        }

        private void OnDisable()
        {
            // マネージャー無効化時は OS カーソルに戻す
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            _currentTexture = null;
        }

        #endregion

        #region Public API

        /// <summary>指定したテクスチャにカーソルを変更する。</summary>
        public void SetCursor(Texture2D texture, Vector2 hotspot)
        {
            if (_currentTexture == texture) return;
            _currentTexture = texture;
            Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
        }

        /// <summary>デフォルトカーソルに戻す。</summary>
        public void ResetToDefault()
        {
            _currentTexture = defaultCursor;
            Cursor.SetCursor(defaultCursor, defaultHotspot, CursorMode.Auto);
        }

        /// <summary>現在適用中のカーソルテクスチャ。</summary>
        public Texture2D CurrentTexture => _currentTexture;

        #endregion
    }
}
