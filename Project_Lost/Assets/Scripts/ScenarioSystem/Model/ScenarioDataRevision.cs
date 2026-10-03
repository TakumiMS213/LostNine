using System.Threading;

namespace ScenarioSystem.Model
{
    /// <summary>データ編集時だけ検索キャッシュを失効させ、通常のID検索は辞書参照に保つ。</summary>
    internal static class ScenarioDataRevision
    {
        private static int _version;
        public static int Version => Volatile.Read(ref _version);
        public static void Invalidate() => Interlocked.Increment(ref _version);
    }
}
