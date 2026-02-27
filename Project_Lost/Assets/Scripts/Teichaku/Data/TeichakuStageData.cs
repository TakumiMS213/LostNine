using UnityEngine;

namespace Teichaku.Data
{
    /// <summary>
    /// 定着ミニゲームのステージ形状データ
    /// tileActive は 1次元配列で管理し、index = y * width + x で座標変換する。
    /// </summary>
    [CreateAssetMenu(fileName = "TeichakuStageData", menuName = "Teichaku/Stage Data")]
    public class TeichakuStageData : ScriptableObject
    {
        [Header("グリッドサイズ")]
        [Tooltip("横方向のタイル数")]
        public int width = 3;

        [Tooltip("縦方向のタイル数")]
        public int height = 3;

        [Header("タイル配置")]
        [Tooltip("各セルがアクティブかどうか（長さ = width * height）")]
        public bool[] tileActive = new bool[9]
        {
            true, true, true,
            true, true, true,
            true, true, true
        };

        /// <summary>
        /// 指定座標のタイルがアクティブかどうかを返す
        /// </summary>
        public bool IsTileActive(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return false;
            int index = y * width + x;
            if (index < 0 || index >= tileActive.Length) return false;
            return tileActive[index];
        }

        /// <summary>
        /// アクティブなタイルの総数を返す
        /// </summary>
        public int ActiveTileCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < tileActive.Length; i++)
                {
                    if (tileActive[i]) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// グリッドサイズ変更時に配列をリサイズする
        /// </summary>
        public void ResizeGrid(int newWidth, int newHeight)
        {
            bool[] newArray = new bool[newWidth * newHeight];

            // 既存データを可能な限りコピー
            for (int y = 0; y < Mathf.Min(height, newHeight); y++)
            {
                for (int x = 0; x < Mathf.Min(width, newWidth); x++)
                {
                    int oldIndex = y * width + x;
                    int newIndex = y * newWidth + x;
                    if (oldIndex < tileActive.Length)
                    {
                        newArray[newIndex] = tileActive[oldIndex];
                    }
                }
            }

            width = newWidth;
            height = newHeight;
            tileActive = newArray;
        }
    }
}
