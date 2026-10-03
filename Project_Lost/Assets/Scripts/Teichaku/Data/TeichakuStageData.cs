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
        [Header("なくしもの（盤面編集の下絵・クリア結果）")]
        public Sprite lostThingImage;
        public string lostThingName;

        [Header("盤面の密度")]
        [Tooltip("マスの希望サイズ。大きな盤面は表示領域へ収まるよう自動縮小する")]
        [Min(24f)] public float preferredTileSize = 160f;
        [Min(0f)] public float tileSpacing = 8f;

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
            if (tileActive == null || index < 0 || index >= tileActive.Length) return false;
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
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                        if (IsTileActive(x, y)) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// グリッドサイズ変更時に配列をリサイズする
        /// </summary>
        public void ResizeGrid(int newWidth, int newHeight)
        {
            newWidth = Mathf.Clamp(newWidth, 1, 20);
            newHeight = Mathf.Clamp(newHeight, 1, 20);
            bool[] newArray = new bool[newWidth * newHeight];

            // 既存データを可能な限りコピー
            for (int y = 0; y < Mathf.Min(height, newHeight); y++)
            {
                for (int x = 0; x < Mathf.Min(width, newWidth); x++)
                {
                    int oldIndex = y * width + x;
                    int newIndex = y * newWidth + x;
                    if (tileActive != null && oldIndex < tileActive.Length)
                    {
                        newArray[newIndex] = tileActive[oldIndex];
                    }
                }
            }

            width = newWidth;
            height = newHeight;
            tileActive = newArray;
        }

        public bool TryGetActiveBounds(out RectInt bounds)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsTileActive(x, y)) continue;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
            bounds = maxX < 0 ? new RectInt() : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return maxX >= 0;
        }
    }
}
