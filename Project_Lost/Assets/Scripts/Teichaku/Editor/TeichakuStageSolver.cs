using System;
using System.Collections.Generic;
using Teichaku.Data;
using UnityEngine;

namespace Teichaku.Editor
{
    public enum StageSolutionStatus { Solved, Unsolvable, SearchLimitReached }

    /// <summary>盤面の全マスを上下左右に一度ずつ通る経路を、編集時に確認する。</summary>
    public static class TeichakuStageSolver
    {
        public static StageSolutionStatus Solve(TeichakuStageData data, out Vector2Int[] solution, int searchLimit = 200000)
        {
            solution = Array.Empty<Vector2Int>();
            var cells = new List<Vector2Int>();
            var indices = new Dictionary<Vector2Int, int>();
            int black = 0;
            for (int y = 0; y < data.height; y++)
            {
                for (int x = 0; x < data.width; x++)
                {
                    if (!data.IsTileActive(x, y)) continue;
                    var cell = new Vector2Int(x, y);
                    indices.Add(cell, cells.Count);
                    cells.Add(cell);
                    if ((x + y) % 2 == 0) black++;
                }
            }
            int count = cells.Count;
            if (count == 0 || Mathf.Abs(black - (count - black)) > 1) return StageSolutionStatus.Unsolvable;

            var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            var neighbors = new int[count][];
            int endpoints = 0;
            for (int i = 0; i < count; i++)
            {
                var adjacent = new List<int>(4);
                foreach (var direction in directions)
                    if (indices.TryGetValue(cells[i] + direction, out int next)) adjacent.Add(next);
                neighbors[i] = adjacent.ToArray();
                if (neighbors[i].Length == 1) endpoints++;
            }
            if (endpoints > 2) return StageSolutionStatus.Unsolvable;

            var visited = new bool[count];
            var queue = new Queue<int>();
            queue.Enqueue(0);
            visited[0] = true;
            int connected = 0;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                connected++;
                foreach (int next in neighbors[current])
                {
                    if (visited[next]) continue;
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }
            if (connected != count) return StageSolutionStatus.Unsolvable;
            Array.Clear(visited, 0, count);

            var starts = new int[count];
            for (int i = 0; i < count; i++)
            {
                starts[i] = i;
                Array.Sort(neighbors[i], (a, b) => neighbors[a].Length.CompareTo(neighbors[b].Length));
            }
            Array.Sort(starts, (a, b) => neighbors[a].Length.CompareTo(neighbors[b].Length));
            var path = new int[count];
            int searched = 0;
            bool exhausted = false;

            bool Visit(int current, int depth)
            {
                if (++searched > searchLimit) { exhausted = true; return false; }
                visited[current] = true;
                path[depth] = current;
                if (depth + 1 == count) return true;
                foreach (int next in neighbors[current])
                {
                    if (!visited[next] && Visit(next, depth + 1)) return true;
                    if (exhausted) break;
                }
                visited[current] = false;
                return false;
            }

            foreach (int start in starts)
            {
                // 端点があればその端点からしか全マスを通過できない。
                if (endpoints > 0 && neighbors[start].Length != 1) continue;
                if (Visit(start, 0))
                {
                    solution = new Vector2Int[count];
                    for (int i = 0; i < count; i++) solution[i] = cells[path[i]];
                    return StageSolutionStatus.Solved;
                }
                if (exhausted) return StageSolutionStatus.SearchLimitReached;
            }
            return StageSolutionStatus.Unsolvable;
        }
    }
}
