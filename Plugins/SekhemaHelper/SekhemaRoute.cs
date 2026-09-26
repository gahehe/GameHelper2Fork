// <copyright file="SekhemaRoute.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace SekhemaHelper
{
using System;
using System.Collections.Generic;
using System.Linq;
    using System.Numerics;
    using GameHelper.RemoteObjects.UiElement;
    using GameOffsets.Objects.UiElement;

    internal static class SekhemaRoute
    {
        private const double CameraAngle = 38.7 * Math.PI / 180;
        private static string cacheKey = string.Empty;
        private static List<Vector2> crystalStops = [];
        private static List<Vector2> crystalBody = [];
        private static List<Vector2> crystalPath = [];
        private static List<Vector2> leverPath = [];

        public static List<Vector2> InRoom(byte[]? walk, int bytesPerRow, Vector2 player, List<Vector2> doors, List<Vector2> crystals)
        {
            if (walk == null || walk.Length == 0 || bytesPerRow <= 0 || crystals.Count == 0)
            {
                return [];
            }

            const int step = 3;
            var seen = new HashSet<int>();
            var q = new Queue<(int X, int Y)>();
            var sx = (int)player.X / step;
            var sy = (int)player.Y / step;
            q.Enqueue((sx, sy));
            seen.Add((sy * 100000) + sx);
            var guard = 0;
            while (q.Count > 0 && guard < 60000)
            {
                guard++;
                var (x, y) = q.Dequeue();
                var gx = x * step;
                var gy = y * step;
                var blocked = false;
                foreach (var door in doors)
                {
                    if (Math.Abs(door.X - gx) < 8f && Math.Abs(door.Y - gy) < 8f)
                    {
                        blocked = true;
                        break;
                    }
                }

                if (blocked)
                {
                    continue;
                }

                foreach (var (dx, dy) in new (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    var id = (ny * 100000) + nx;
                    if (!seen.Add(id) || !Walkable(walk, bytesPerRow, nx * step, ny * step))
                    {
                        continue;
                    }

                    q.Enqueue((nx, ny));
                }
            }

            var kept = new List<Vector2>();
            foreach (var crystal in crystals)
            {
                var id = (((int)crystal.Y / step) * 100000) + ((int)crystal.X / step);
                if (seen.Contains(id))
                {
                    kept.Add(crystal);
                    continue;
                }

                for (var r = 1; r <= 6; r++)
                {
                    var hit = false;
                    for (var dy = -r; dy <= r && !hit; dy++)
                    {
                        for (var dx = -r; dx <= r; dx++)
                        {
                            var nid = ((((int)crystal.Y / step) + dy) * 100000) + ((int)crystal.X / step) + dx;
                            if (seen.Contains(nid))
                            {
                                kept.Add(crystal);
                                hit = true;
                                break;
                            }
                        }
                    }

                    if (hit)
                    {
                        break;
                    }
                }
            }

            return kept;
        }

        public static bool Walkable(byte[] data, int bytesPerRow, int x, int y)
        {
            if (x < 0 || y < 0 || bytesPerRow <= 0)
            {
                return false;
            }

            var index = (y * bytesPerRow) + (x / 2);
            if ((uint)index >= (uint)data.Length)
            {
                return false;
            }

            var shift = x % 2 == 0 ? 0 : 4;
            return ((data[index] >> shift) & 0xF) != 0;
        }

        public static void Update(string key, byte[]? walk, int bytesPerRow, Vector2 player, List<Vector2> crystals, Vector2? door, Vector2? lever)
        {
            var stops = OrderForward(player, crystals, door);
            var sameStops = key == cacheKey && Same(stops, crystalStops);
            cacheKey = key;
            if (!sameStops)
            {
                crystalStops = stops;
                crystalBody = crystalStops.Count == 0
                    ? []
                    : Chain(walk, bytesPerRow, crystalStops[0], crystalStops.Skip(1).ToList());
                leverPath = lever is { } lv ? Chain(walk, bytesPerRow, player, [lv]) : [];
            }

            RefreshPlayerLeg(walk, bytesPerRow, player);
        }

        private static bool Same(List<Vector2> a, List<Vector2> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (var i = 0; i < a.Count; i++)
            {
                if (Vector2.DistanceSquared(a[i], b[i]) > 1f)
                {
                    return false;
                }
            }

            return true;
        }

        public static void RefreshPlayerLeg(byte[]? walk, int bytesPerRow, Vector2 player)
        {
            if (crystalStops.Count == 0)
            {
                crystalPath = [];
                return;
            }

            var leg = Chain(walk, bytesPerRow, player, [crystalStops[0]]);
            if (crystalBody.Count > 1)
            {
                leg.AddRange(crystalBody.Skip(1));
            }

            crystalPath = leg;
        }

        public static IReadOnlyList<Vector2> CrystalPath => crystalPath;

        public static IReadOnlyList<Vector2> LeverPath => leverPath;

        public static Vector2 MapPoint(MapUiElement map, bool large, Vector2 grid, Vector2 playerGrid, float heightDelta)
        {
            var (cos, sin, center) = Basis(map, large);
            heightDelta /= 10.86957f;
            var delta = new Vector2((grid.X - playerGrid.X) - (grid.Y - playerGrid.Y), heightDelta - ((grid.X - playerGrid.X) + (grid.Y - playerGrid.Y)));
            return center + new Vector2(delta.X * cos, delta.Y * sin);
        }

        private static (float Cos, float Sin, Vector2 Center) Basis(MapUiElement map, bool large)
        {
            var baseRes = UiElementBaseFuncs.BaseResolution;
            var baseDiag = Math.Sqrt((baseRes.X * baseRes.X) + (baseRes.Y * baseRes.Y));
            var diag = baseDiag * map.Size.Y / baseRes.Y;
            var zoom = large ? map.Zoom * 0.187812f : map.Zoom * 0.748f;
            if (zoom < 0.001f)
            {
                zoom = 0.001f;
            }

            var mapScale = 240f / zoom;
            var cos = (float)(diag * Math.Cos(CameraAngle) / mapScale);
            var sin = (float)(diag * Math.Sin(CameraAngle) / mapScale);
            // LargeMap.Position is the window corner. The map center is LargeMap.Center.
            var center = large && map is LargeMapUiElement largeMap
                ? largeMap.Center + largeMap.Shift + largeMap.DefaultShift + new Vector2(0.6f, 0.3f)
                : map.Position + (map.Size / 2f) + map.DefaultShift + map.Shift + new Vector2(-5f, 0f);
            return (cos, sin, center);
        }

        private static List<Vector2> OrderForward(Vector2 player, List<Vector2> crystals, Vector2? door)
        {
            if (crystals.Count == 0)
            {
                return [];
            }

            if (door is not { } doorPos)
            {
                return Order(player, crystals);
            }

            var end = crystals[0];
            var endD = float.MaxValue;
            foreach (var crystal in crystals)
            {
                var d = Vector2.DistanceSquared(crystal, doorPos);
                if (d < endD)
                {
                    endD = d;
                    end = crystal;
                }
            }

            var playerDoor = Vector2.DistanceSquared(player, doorPos);
            var ahead = new List<Vector2>();
            foreach (var crystal in crystals)
            {
                if (Vector2.DistanceSquared(crystal, doorPos) <= playerDoor || crystal == end)
                {
                    ahead.Add(crystal);
                }
            }

            if (ahead.Count == 0)
            {
                return [end];
            }

            ahead.Remove(end);
            var ordered = new List<Vector2>();
            var cursor = player;
            while (ahead.Count > 0)
            {
                var best = 0;
                var bestD = float.MaxValue;
                for (var i = 0; i < ahead.Count; i++)
                {
                    var d = Vector2.DistanceSquared(cursor, ahead[i]);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = i;
                    }
                }

                cursor = ahead[best];
                ordered.Add(cursor);
                ahead.RemoveAt(best);
            }

            ordered.Add(end);
            return ordered;
        }

        private static List<Vector2> OrderEndingAtDoor(List<Vector2> crystals, Vector2? door)
        {
            if (crystals.Count == 0)
            {
                return [];
            }

            var endIndex = 0;
            if (door is { } doorPos)
            {
                var best = float.MaxValue;
                for (var i = 0; i < crystals.Count; i++)
                {
                    var d = Vector2.DistanceSquared(crystals[i], doorPos);
                    if (d < best)
                    {
                        best = d;
                        endIndex = i;
                    }
                }
            }

            var end = crystals[endIndex];
            var startIndex = 0;
            var far = -1f;
            for (var i = 0; i < crystals.Count; i++)
            {
                if (i == endIndex)
                {
                    continue;
                }

                var d = Vector2.DistanceSquared(crystals[i], end);
                if (d > far)
                {
                    far = d;
                    startIndex = i;
                }
            }

            var left = new List<Vector2>(crystals);
            left.Remove(end);
            var ordered = new List<Vector2>();
            if (left.Count > 0)
            {
                var cursor = crystals[startIndex];
                left.Remove(cursor);
                ordered.Add(cursor);
                while (left.Count > 0)
                {
                    var best = 0;
                    var bestD = float.MaxValue;
                    for (var i = 0; i < left.Count; i++)
                    {
                        var d = Vector2.DistanceSquared(cursor, left[i]);
                        if (d < bestD)
                        {
                            bestD = d;
                            best = i;
                        }
                    }

                    cursor = left[best];
                    ordered.Add(cursor);
                    left.RemoveAt(best);
                }
            }

            ordered.Add(end);
            return ordered;
        }

        private static List<Vector2> Order(Vector2 start, List<Vector2> points)
        {
            var left = new List<Vector2>(points);
            var ordered = new List<Vector2>(left.Count);
            var cursor = start;
            while (left.Count > 0)
            {
                var best = 0;
                var bestD = float.MaxValue;
                for (var i = 0; i < left.Count; i++)
                {
                    var d = Vector2.DistanceSquared(cursor, left[i]);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = i;
                    }
                }

                cursor = left[best];
                ordered.Add(cursor);
                left.RemoveAt(best);
            }

            return ordered;
        }

        private static List<Vector2> Chain(byte[]? walk, int bytesPerRow, Vector2 from, List<Vector2> stops)
        {
            var poly = new List<Vector2>();
            var cursor = from;
            foreach (var stop in stops)
            {
                var leg = Leg(walk, bytesPerRow, cursor, stop);
                if (poly.Count > 0 && leg.Count > 0)
                {
                    leg.RemoveAt(0);
                }

                poly.AddRange(leg);
                cursor = stop;
            }

            return poly;
        }

        private static Vector2 Snap(byte[] walk, int bytesPerRow, Vector2 p)
        {
            var x = (int)p.X;
            var y = (int)p.Y;
            if (Walkable(walk, bytesPerRow, x, y))
            {
                return p;
            }

            for (var r = 1; r <= 16; r++)
            {
                for (var dy = -r; dy <= r; dy++)
                {
                    for (var dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
                        {
                            continue;
                        }

                        if (Walkable(walk, bytesPerRow, x + dx, y + dy))
                        {
                            return new Vector2(x + dx, y + dy);
                        }
                    }
                }
            }

            return p;
        }

        private static List<Vector2> Leg(byte[]? walk, int bytesPerRow, Vector2 from, Vector2 to)
        {
            if (walk == null || walk.Length == 0 || bytesPerRow <= 0)
            {
                return [from, to];
            }

            const int step = 2;
            from = Snap(walk, bytesPerRow, from);
            to = Snap(walk, bytesPerRow, to);
            var x0 = (int)from.X / step;
            var y0 = (int)from.Y / step;
            var x1 = (int)to.X / step;
            var y1 = (int)to.Y / step;
            var minX = Math.Min(x0, x1) - 8;
            var minY = Math.Min(y0, y1) - 8;
            var maxX = Math.Max(x0, x1) + 8;
            var maxY = Math.Max(y0, y1) + 8;
            if (maxX - minX > 220 || maxY - minY > 220)
            {
                return [from, to];
            }

            var w = maxX - minX + 1;
            var h = maxY - minY + 1;
            var came = new int[w * h];
            Array.Fill(came, -1);
            var start = ((y0 - minY) * w) + (x0 - minX);
            var goal = ((y1 - minY) * w) + (x1 - minX);
            if ((uint)start >= (uint)came.Length || (uint)goal >= (uint)came.Length)
            {
                return [from, to];
            }

            var q = new Queue<int>();
            q.Enqueue(start);
            came[start] = start;
            int[] ox = [1, -1, 0, 0];
            int[] oy = [0, 0, 1, -1];
            var found = false;
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == goal)
                {
                    found = true;
                    break;
                }

                var cx = minX + (cur % w);
                var cy = minY + (cur / w);
                for (var k = 0; k < 4; k++)
                {
                    var nx = cx + ox[k];
                    var ny = cy + oy[k];
                    if (nx < minX || ny < minY || nx > maxX || ny > maxY)
                    {
                        continue;
                    }

                    var ni = ((ny - minY) * w) + (nx - minX);
                    if (came[ni] != -1 || !Walkable(walk, bytesPerRow, nx * step, ny * step))
                    {
                        continue;
                    }

                    came[ni] = cur;
                    q.Enqueue(ni);
                }
            }

            if (!found)
            {
                return [from, to];
            }

            var rev = new List<Vector2>();
            for (var i = goal; i != start; i = came[i])
            {
                rev.Add(new Vector2((minX + (i % w)) * step, (minY + (i / w)) * step));
                if (rev.Count > 2000)
                {
                    break;
                }
            }

            rev.Add(from);
            rev.Reverse();
            rev.Add(to);
            return rev;
        }
    }
}
