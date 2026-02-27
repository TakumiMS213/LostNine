using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Teichaku.Core
{
    /// <summary>
    /// 一筆書きパズルの個々のタイル。
    /// マウスがタイル上に入ったことを TeichakuManager に通知する。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class TeichakuTile : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
    {
        [Header("タイル色設定")]
        [Tooltip("未訪問時のタイル色")]
        [SerializeField] private Color defaultColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        [Tooltip("訪問済みのタイル色")]
        [SerializeField] private Color visitedColor = new Color(0.3f, 0.8f, 0.5f, 1f);

        /// <summary>グリッド上のX座標</summary>
        public int GridX { get; private set; }

        /// <summary>グリッド上のY座標</summary>
        public int GridY { get; private set; }

        /// <summary>なぞり済みかどうか</summary>
        public bool IsVisited { get; private set; }

        private Image _image;
        private TeichakuManager _manager;

        /// <summary>
        /// タイルを初期化する
        /// </summary>
        public void Initialize(int x, int y, TeichakuManager manager)
        {
            GridX = x;
            GridY = y;
            _manager = manager;
            _image = GetComponent<Image>();
            ResetTile();
        }

        /// <summary>
        /// タイルを訪問済みにする
        /// </summary>
        public void SetVisited()
        {
            IsVisited = true;
            if (_image != null)
            {
                _image.color = visitedColor;
            }
        }

        /// <summary>
        /// タイルを初期状態に戻す
        /// </summary>
        public void ResetTile()
        {
            IsVisited = false;
            if (_image != null)
            {
                _image.color = defaultColor;
            }
        }

        /// <summary>
        /// マウスがタイル上に入った時（ドラッグ中の検知用）
        /// </summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_manager != null)
            {
                _manager.OnTilePointerEnter(this);
            }
        }

        /// <summary>
        /// マウスボタンを押した時（ドラッグ開始の最初のタイル検知用）
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (_manager != null)
            {
                _manager.OnTilePointerDown(this);
            }
        }
    }
}
