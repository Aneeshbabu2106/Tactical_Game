using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
///     A* over walkable cells, followed by a string-pull pass that collapses the cell chain back
///     into a handful of straight runs. Ported from the prototype's pathfind.js.
/// </summary>
/// <remarks>
///     Used for orders the player did not draw — walking to a door he clicked. Hand-drawn paths do
///     not come through here: the stroke the player made is the route, and always was.
/// </remarks>
public static class CellPathfinder
{
    private const float Diagonal = 1.41421356f;

    /// <summary>Orthogonals first, so an equal-cost tie resolves to the straighter step.</summary>
    private static readonly Vector3Int[] Steps =
    {
        new(0, 1, 0), new(1, 0, 0), new(0, -1, 0), new(-1, 0, 0),
        new(1, 1, 0), new(1, -1, 0), new(-1, -1, 0), new(-1, 1, 0)
    };

    /// <summary>
    ///     A route from <paramref name="from" /> to <paramref name="to" /> as a short list of corner
    ///     points, starting exactly at <paramref name="from" /> and ending exactly at
    ///     <paramref name="to" />. Null when no route exists.
    /// </summary>
    public static List<Vector3> Find(
        Tilemap map, Vector3 from, Vector3 to, float step = NavigationQuery.DefaultStep)
    {
        if (map == null)
        {
            return null;
        }

        var start = map.WorldToCell(from);
        var goal = map.WorldToCell(to);

        if (!NavigationQuery.IsWalkable(map, start) || !NavigationQuery.IsWalkable(map, goal))
        {
            return null;
        }

        // Already there, or in plain sight across open floor: no search worth running.
        if (start == goal || NavigationQuery.SegmentIsWalkable(map, from, to, step))
        {
            return new List<Vector3> { from, to };
        }

        var cells = Search(map, start, goal);

        if (cells == null)
        {
            return null;
        }

        var points = new List<Vector3> { from };

        // The end cells are dropped: the caller's own start and finish are more precise than the
        // centres of whichever cells they happen to stand in.
        for (var i = 1; i < cells.Count - 1; i++)
        {
            var centre = map.GetCellCenterWorld(cells[i]);
            centre.z = from.z;
            points.Add(centre);
        }

        points.Add(to);

        return StringPull(map, points, step);
    }

    private static List<Vector3Int> Search(Tilemap map, Vector3Int start, Vector3Int goal)
    {
        // The tilemap's own bounds cap the search: nothing outside the authored map is walkable.
        var bounds = map.cellBounds;
        var width = bounds.size.x;
        var count = width * bounds.size.y;

        if (count <= 0 || !InBounds(bounds, start) || !InBounds(bounds, goal))
        {
            return null;
        }

        var cost = new float[count];
        var came = new int[count];
        var closed = new bool[count];

        for (var i = 0; i < count; i++)
        {
            cost[i] = float.MaxValue;
            came[i] = -1;
        }

        var open = new MinHeap();
        var first = Index(bounds, width, start);
        cost[first] = 0f;
        open.Push(first, Octile(start, goal));

        while (open.TryPop(out var current))
        {
            if (closed[current])
            {
                continue;
            }

            closed[current] = true;

            var cell = new Vector3Int(
                bounds.xMin + current % width,
                bounds.yMin + current / width,
                start.z);

            if (cell.x == goal.x && cell.y == goal.y)
            {
                return Retrace(came, current, bounds, width, start.z);
            }

            foreach (var offset in Steps)
            {
                var stepped = cell + offset;

                if (!InBounds(bounds, stepped) || !NavigationQuery.IsWalkable(map, stepped))
                {
                    continue;
                }

                var diagonal = offset.x != 0 && offset.y != 0;

                // No corner cutting: a diagonal needs both orthogonals beside it open, or the
                // operator clips the corner of a wall on his way past.
                if (diagonal
                    && (!NavigationQuery.IsWalkable(map, new Vector3Int(stepped.x, cell.y, cell.z))
                        || !NavigationQuery.IsWalkable(map, new Vector3Int(cell.x, stepped.y, cell.z))))
                {
                    continue;
                }

                var next = Index(bounds, width, stepped);

                if (closed[next])
                {
                    continue;
                }

                var walked = cost[current] + (diagonal ? Diagonal : 1f);

                if (walked >= cost[next])
                {
                    continue;
                }

                cost[next] = walked;
                came[next] = current;
                open.Push(next, walked + Octile(stepped, goal));
            }
        }

        return null;
    }

    private static bool InBounds(BoundsInt bounds, Vector3Int cell)
    {
        return cell.x >= bounds.xMin && cell.x < bounds.xMax
                                     && cell.y >= bounds.yMin && cell.y < bounds.yMax;
    }

    private static int Index(BoundsInt bounds, int width, Vector3Int cell)
    {
        return cell.x - bounds.xMin + (cell.y - bounds.yMin) * width;
    }

    private static List<Vector3Int> Retrace(
        IReadOnlyList<int> came, int at, BoundsInt bounds, int width, int z)
    {
        var cells = new List<Vector3Int>();

        while (at >= 0)
        {
            cells.Add(new Vector3Int(bounds.xMin + at % width, bounds.yMin + at / width, z));
            at = came[at];
        }

        cells.Reverse();
        return cells;
    }

    /// <summary>
    ///     Collapses the cell-by-cell chain down to the corners that matter: from each kept point,
    ///     reach as far along the chain as stays walkable in a straight line. Without this the
    ///     operator visibly staircases across open floor.
    /// </summary>
    private static List<Vector3> StringPull(Tilemap map, List<Vector3> points, float step)
    {
        var pulled = new List<Vector3> { points[0] };
        var anchor = 0;

        while (anchor < points.Count - 1)
        {
            var furthest = anchor + 1;

            for (var j = points.Count - 1; j > anchor + 1; j--)
            {
                if (NavigationQuery.SegmentIsWalkable(map, points[anchor], points[j], step))
                {
                    furthest = j;
                    break;
                }
            }

            pulled.Add(points[furthest]);
            anchor = furthest;
        }

        return pulled;
    }

    /// <summary>Octile distance: the exact cost of an unobstructed 8-way walk.</summary>
    private static float Octile(Vector3Int a, Vector3Int b)
    {
        var dx = Mathf.Abs(a.x - b.x);
        var dy = Mathf.Abs(a.y - b.y);
        return dx + dy + (Diagonal - 2f) * Mathf.Min(dx, dy);
    }

    /// <summary>Binary heap keyed on f. Small enough to be worth not taking a dependency for.</summary>
    private sealed class MinHeap
    {
        private readonly List<int> items = new();
        private readonly List<float> keys = new();

        public void Push(int item, float key)
        {
            items.Add(item);
            keys.Add(key);

            var child = items.Count - 1;

            while (child > 0)
            {
                var parent = (child - 1) / 2;

                if (keys[parent] <= keys[child])
                {
                    break;
                }

                Swap(parent, child);
                child = parent;
            }
        }

        public bool TryPop(out int item)
        {
            item = 0;

            if (items.Count == 0)
            {
                return false;
            }

            item = items[0];

            var last = items.Count - 1;
            items[0] = items[last];
            keys[0] = keys[last];
            items.RemoveAt(last);
            keys.RemoveAt(last);

            var parent = 0;

            while (true)
            {
                var left = parent * 2 + 1;
                var right = left + 1;
                var smallest = parent;

                if (left < items.Count && keys[left] < keys[smallest])
                {
                    smallest = left;
                }

                if (right < items.Count && keys[right] < keys[smallest])
                {
                    smallest = right;
                }

                if (smallest == parent)
                {
                    break;
                }

                Swap(smallest, parent);
                parent = smallest;
            }

            return true;
        }

        private void Swap(int a, int b)
        {
            (items[a], items[b]) = (items[b], items[a]);
            (keys[a], keys[b]) = (keys[b], keys[a]);
        }
    }
}
