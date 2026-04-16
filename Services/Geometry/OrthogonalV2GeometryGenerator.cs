using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using FlowMy.Models;

namespace FlowMy.Services.Geometry
{
    /// <summary>
    /// Orthogonal V2: A*-based obstacle-aware orthogonal line routing.
    /// Builds a coarse grid from node bounding rects, runs A* with Manhattan heuristic,
    /// simplifies into minimal waypoints, and adds rounded corners.
    /// </summary>
    public sealed class OrthogonalV2GeometryGenerator : IPathGeometryGenerator
    {
        /// <summary>Margin around each obstacle rect (pixels).</summary>
        private const double ObstacleMargin = 15;

        /// <summary>Grid cell size (pixels). Smaller = more accurate but slower.</summary>
        private const double GridCellSize = 10;

        /// <summary>Maximum grid dimension to prevent performance issues.</summary>
        private const int MaxGridDim = 300;

        /// <summary>Extension length from port in the port direction before routing.</summary>
        private const double PortExtension = 30;

        // ─── IPathGeometryGenerator (4-param fallback, no obstacles) ───

        public PathGeometry Generate(Point start, Point end, PortPosition? startDir, PortPosition? endDir)
        {
            return Generate(start, end, startDir, endDir, Array.Empty<Rect>());
        }

        // ─── Main entry – obstacle-aware ───

        public PathGeometry Generate(
            Point start, Point end,
            PortPosition? startDir, PortPosition? endDir,
            IReadOnlyList<Rect> obstacles)
        {
            PortPosition sDir = startDir ?? InferDirection(start, end, isStart: true);
            PortPosition eDir = endDir ?? InferDirection(start, end, isStart: false);

            // Inflate obstacles by margin
            var inflated = new List<Rect>(obstacles.Count);
            foreach (var r in obstacles)
            {
                if (r.Width > 0 && r.Height > 0)
                {
                    inflated.Add(new Rect(
                        r.X - ObstacleMargin,
                        r.Y - ObstacleMargin,
                        r.Width + ObstacleMargin * 2,
                        r.Height + ObstacleMargin * 2));
                }
            }

            // Extend start/end in port direction so lines exit cleanly from ports
            Point extStart = Extend(start, sDir, PortExtension);
            Point extEnd = Extend(end, eDir, PortExtension);

            // Try A* pathfinding
            List<Point>? path = FindPathAStar(extStart, extEnd, inflated);

            // Build final waypoints: start → extStart → (A* path) → extEnd → end
            var waypoints = new List<Point>();
            waypoints.Add(extStart);
            if (path != null && path.Count > 0)
            {
                // path already starts at extStart and ends at extEnd, simplify
                var simplified = SimplifyPath(path);
                waypoints.Clear();
                waypoints.AddRange(simplified);
            }
            else
            {
                // Fallback: simple 3-segment bypass
                waypoints = CreateFallbackPath(start, end, sDir, eDir);
                return BuildGeometry(start, waypoints, end);
            }

            return BuildGeometry(start, waypoints, end);
        }

        // ─── A* pathfinding on a coarse grid ───

        private static List<Point>? FindPathAStar(Point start, Point end, List<Rect> obstacles)
        {
            // Determine bounding area for the grid
            double minX = Math.Min(start.X, end.X);
            double minY = Math.Min(start.Y, end.Y);
            double maxX = Math.Max(start.X, end.X);
            double maxY = Math.Max(start.Y, end.Y);

            foreach (var r in obstacles)
            {
                minX = Math.Min(minX, r.X);
                minY = Math.Min(minY, r.Y);
                maxX = Math.Max(maxX, r.Right);
                maxY = Math.Max(maxY, r.Bottom);
            }

            // Add generous padding around the entire area
            double padding = 80;
            minX -= padding;
            minY -= padding;
            maxX += padding;
            maxY += padding;

            int cols = (int)Math.Ceiling((maxX - minX) / GridCellSize);
            int rows = (int)Math.Ceiling((maxY - minY) / GridCellSize);

            // Clamp grid size to prevent huge memory usage
            if (cols > MaxGridDim || rows > MaxGridDim)
            {
                // Scale the cell size up to fit
                double scaleFactor = Math.Max((double)cols / MaxGridDim, (double)rows / MaxGridDim);
                double adjustedCellSize = GridCellSize * scaleFactor;
                cols = (int)Math.Ceiling((maxX - minX) / adjustedCellSize);
                rows = (int)Math.Ceiling((maxY - minY) / adjustedCellSize);

                return FindPathAStarOnGrid(start, end, obstacles, minX, minY, cols, rows, adjustedCellSize);
            }

            return FindPathAStarOnGrid(start, end, obstacles, minX, minY, cols, rows, GridCellSize);
        }

        private static List<Point>? FindPathAStarOnGrid(
            Point start, Point end, List<Rect> obstacles,
            double originX, double originY, int cols, int rows, double cellSize)
        {
            // Convert start/end to grid coords
            int startCol = (int)((start.X - originX) / cellSize);
            int startRow = (int)((start.Y - originY) / cellSize);
            int endCol = (int)((end.X - originX) / cellSize);
            int endRow = (int)((end.Y - originY) / cellSize);

            // Clamp
            startCol = Math.Clamp(startCol, 0, cols - 1);
            startRow = Math.Clamp(startRow, 0, rows - 1);
            endCol = Math.Clamp(endCol, 0, cols - 1);
            endRow = Math.Clamp(endRow, 0, rows - 1);

            // Build blocked set using obstacle rects
            var blocked = new HashSet<long>();
            foreach (var r in obstacles)
            {
                int c0 = Math.Max(0, (int)((r.X - originX) / cellSize));
                int r0 = Math.Max(0, (int)((r.Y - originY) / cellSize));
                int c1 = Math.Min(cols - 1, (int)((r.Right - originX) / cellSize));
                int r1 = Math.Min(rows - 1, (int)((r.Bottom - originY) / cellSize));
                for (int rr = r0; rr <= r1; rr++)
                    for (int cc = c0; cc <= c1; cc++)
                        blocked.Add(Key(cc, rr));
            }

            // Ensure start and end cells are not blocked
            blocked.Remove(Key(startCol, startRow));
            blocked.Remove(Key(endCol, endRow));

            // A* with orthogonal moves only
            var openSet = new SortedSet<(double f, int col, int row, int id)>();
            var gScore = new Dictionary<long, double>();
            var cameFrom = new Dictionary<long, long>();
            int idCounter = 0;

            // Penalty for changing direction (encourages fewer bends)
            const double turnPenalty = 2.0;

            long startKey = Key(startCol, startRow);
            long endKey = Key(endCol, endRow);
            gScore[startKey] = 0;
            openSet.Add((Heuristic(startCol, startRow, endCol, endRow), startCol, startRow, idCounter++));

            // Direction tracking: store dir as part of state for turn penalty
            var dirFrom = new Dictionary<long, int>(); // 0=none, 1=horiz, 2=vert
            dirFrom[startKey] = 0;

            int[] dc = { 1, -1, 0, 0 };
            int[] dr = { 0, 0, 1, -1 };
            int[] dirType = { 1, 1, 2, 2 }; // horiz, horiz, vert, vert

            while (openSet.Count > 0)
            {
                var current = openSet.Min;
                openSet.Remove(current);
                int cc = current.col, cr = current.row;
                long ck = Key(cc, cr);

                if (cc == endCol && cr == endRow)
                {
                    // Reconstruct path
                    var path = new List<Point>();
                    long k = endKey;
                    while (cameFrom.ContainsKey(k))
                    {
                        int c = (int)(k >> 16);
                        int r = (int)(k & 0xFFFF);
                        path.Add(new Point(originX + c * cellSize + cellSize / 2, originY + r * cellSize + cellSize / 2));
                        k = cameFrom[k];
                    }
                    path.Add(start);
                    path.Reverse();
                    // Replace last point with exact end
                    if (path.Count > 0)
                        path[path.Count - 1] = end;
                    path[0] = start;
                    return path;
                }

                double currentG = gScore.GetValueOrDefault(ck, double.MaxValue);
                int currentDir = dirFrom.GetValueOrDefault(ck, 0);

                for (int i = 0; i < 4; i++)
                {
                    int nc = cc + dc[i];
                    int nr = cr + dr[i];
                    if (nc < 0 || nc >= cols || nr < 0 || nr >= rows) continue;

                    long nk = Key(nc, nr);
                    if (blocked.Contains(nk)) continue;

                    double moveCost = cellSize;
                    // Add turn penalty if direction changes
                    if (currentDir != 0 && dirType[i] != currentDir)
                        moveCost += turnPenalty * cellSize;

                    double tentativeG = currentG + moveCost;
                    double existingG = gScore.GetValueOrDefault(nk, double.MaxValue);

                    if (tentativeG < existingG)
                    {
                        gScore[nk] = tentativeG;
                        cameFrom[nk] = ck;
                        dirFrom[nk] = dirType[i];
                        double f = tentativeG + Heuristic(nc, nr, endCol, endRow) * cellSize;
                        openSet.Add((f, nc, nr, idCounter++));
                    }
                }
            }

            return null; // No path found
        }

        private static long Key(int col, int row) => ((long)col << 16) | (long)(row & 0xFFFF);

        private static double Heuristic(int c1, int r1, int c2, int r2)
            => Math.Abs(c1 - c2) + Math.Abs(r1 - r2); // Manhattan distance

        // ─── Path simplification: collapse collinear segments ───

        private static List<Point> SimplifyPath(List<Point> path)
        {
            if (path.Count <= 2) return path;

            var result = new List<Point> { path[0] };

            for (int i = 1; i < path.Count - 1; i++)
            {
                Point prev = result[result.Count - 1];
                Point curr = path[i];
                Point next = path[i + 1];

                // Check if prev → curr → next are collinear (same direction)
                bool collinearH = Math.Abs(prev.Y - curr.Y) < 1 && Math.Abs(curr.Y - next.Y) < 1;
                bool collinearV = Math.Abs(prev.X - curr.X) < 1 && Math.Abs(curr.X - next.X) < 1;

                if (!collinearH && !collinearV)
                {
                    result.Add(curr);
                }
            }

            result.Add(path[path.Count - 1]);

            // Align waypoints to create clean orthogonal segments
            return AlignWaypoints(result);
        }

        /// <summary>
        /// Snap intermediate waypoints so each segment is perfectly horizontal or vertical.
        /// </summary>
        private static List<Point> AlignWaypoints(List<Point> points)
        {
            if (points.Count <= 2) return points;

            var aligned = new List<Point> { points[0] };

            for (int i = 1; i < points.Count; i++)
            {
                Point prev = aligned[aligned.Count - 1];
                Point curr = points[i];

                double dx = Math.Abs(curr.X - prev.X);
                double dy = Math.Abs(curr.Y - prev.Y);

                if (dx < 2 || dy < 2)
                {
                    // Already nearly axis-aligned, just snap
                    if (dx < dy)
                        aligned.Add(new Point(prev.X, curr.Y));
                    else
                        aligned.Add(new Point(curr.X, prev.Y));
                }
                else
                {
                    // Need a bend point: first go horizontal, then vertical
                    aligned.Add(new Point(curr.X, prev.Y));
                    aligned.Add(curr);
                }
            }

            return aligned;
        }

        // ─── Fallback path when A* fails ───

        private static List<Point> CreateFallbackPath(Point start, Point end, PortPosition sDir, PortPosition eDir)
        {
            var waypoints = new List<Point>();
            double ext = PortExtension;

            Point extStart = Extend(start, sDir, ext);
            Point extEnd = Extend(end, eDir, ext);

            waypoints.Add(extStart);

            // Simple 3-segment: extStart → midpoint → extEnd
            if (IsHorizontal(sDir))
            {
                double midX = (extStart.X + extEnd.X) / 2;
                waypoints.Add(new Point(midX, extStart.Y));
                waypoints.Add(new Point(midX, extEnd.Y));
            }
            else
            {
                double midY = (extStart.Y + extEnd.Y) / 2;
                waypoints.Add(new Point(extStart.X, midY));
                waypoints.Add(new Point(extEnd.X, midY));
            }

            waypoints.Add(extEnd);
            return waypoints;
        }

        // ─── Geometry building with rounded corners ───

        private static PathGeometry BuildGeometry(Point start, List<Point> waypoints, Point end)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = start };

            // All points: start → waypoints → end
            var allPoints = new List<Point>();
            allPoints.AddRange(waypoints);
            allPoints.Add(end);

            for (int i = 0; i < allPoints.Count; i++)
            {
                if (i == allPoints.Count - 1)
                {
                    // Last segment: straight line to end
                    figure.Segments.Add(new LineSegment(allPoints[i], true));
                }
                else
                {
                    Point current = allPoints[i];
                    Point next = allPoints[i + 1];

                    // Determine previous point for corner calculation
                    Point prev = i == 0 ? start : allPoints[i - 1];

                    // Check if there's a direction change (bend)
                    bool hasBend = HasDirectionChange(prev, current, next);

                    if (hasBend)
                    {
                        double cornerRadius = CalculateCornerRadius(prev, current, next);
                        AddRoundedCorner(figure, prev, current, next, cornerRadius);
                    }
                    else
                    {
                        figure.Segments.Add(new LineSegment(current, true));
                    }
                }
            }

            geometry.Figures.Add(figure);
            return geometry;
        }

        private static bool HasDirectionChange(Point prev, Point current, Point next)
        {
            double dx1 = current.X - prev.X;
            double dy1 = current.Y - prev.Y;
            double dx2 = next.X - current.X;
            double dy2 = next.Y - current.Y;

            // Direction change if one segment is horizontal and the other is vertical
            bool seg1Horiz = Math.Abs(dy1) < 2;
            bool seg1Vert = Math.Abs(dx1) < 2;
            bool seg2Horiz = Math.Abs(dy2) < 2;
            bool seg2Vert = Math.Abs(dx2) < 2;

            return (seg1Horiz && seg2Vert) || (seg1Vert && seg2Horiz);
        }

        private static double CalculateCornerRadius(Point prev, Point current, Point next)
        {
            double d1 = Distance(prev, current);
            double d2 = Distance(current, next);
            double minDist = Math.Min(d1, d2);
            double radius = Math.Min(minDist * 0.4, 15);
            return Math.Max(radius, 4);
        }

        private static void AddRoundedCorner(PathFigure figure, Point prev, Point corner, Point next, double radius)
        {
            double d1 = Distance(prev, corner);
            double d2 = Distance(corner, next);

            if (d1 < radius * 2 || d2 < radius * 2)
            {
                // Too short for rounding, just draw lines
                figure.Segments.Add(new LineSegment(corner, true));
                return;
            }

            // Point on the line from prev → corner, radius before the corner
            double ratio1 = (d1 - radius) / d1;
            Point beforeCorner = new Point(
                prev.X + (corner.X - prev.X) * ratio1,
                prev.Y + (corner.Y - prev.Y) * ratio1);

            // Point on the line from corner → next, radius after the corner
            double ratio2 = radius / d2;
            Point afterCorner = new Point(
                corner.X + (next.X - corner.X) * ratio2,
                corner.Y + (next.Y - corner.Y) * ratio2);

            // Line to before corner
            figure.Segments.Add(new LineSegment(beforeCorner, true));

            // Quadratic bezier through the corner point
            figure.Segments.Add(new QuadraticBezierSegment(corner, afterCorner, true));
        }

        // ─── Helpers ───

        private static Point Extend(Point p, PortPosition dir, double distance)
        {
            return dir switch
            {
                PortPosition.Right => new Point(p.X + distance, p.Y),
                PortPosition.Left => new Point(p.X - distance, p.Y),
                PortPosition.Bottom => new Point(p.X, p.Y + distance),
                PortPosition.Top => new Point(p.X, p.Y - distance),
                _ => p
            };
        }

        private static bool IsHorizontal(PortPosition dir) => dir == PortPosition.Left || dir == PortPosition.Right;

        private static double Distance(Point a, Point b)
            => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static PortPosition InferDirection(Point start, Point end, bool isStart)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            if (isStart)
            {
                return Math.Abs(dx) >= Math.Abs(dy)
                    ? (dx >= 0 ? PortPosition.Right : PortPosition.Left)
                    : (dy >= 0 ? PortPosition.Bottom : PortPosition.Top);
            }
            else
            {
                return Math.Abs(dx) >= Math.Abs(dy)
                    ? (dx >= 0 ? PortPosition.Left : PortPosition.Right)
                    : (dy >= 0 ? PortPosition.Top : PortPosition.Bottom);
            }
        }
    }
}
