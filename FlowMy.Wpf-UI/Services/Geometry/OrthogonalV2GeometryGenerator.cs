// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using FlowMy.Models;

namespace FlowMy.Services.Geometry
{
    /// <summary>
    /// Orthogonal V2: Visibility-Graph-based obstacle-aware orthogonal line routing.
    /// Instead of a coarse grid A*, this builds an orthogonal visibility graph from
    /// obstacle corners and lead lines, then runs A* on that sparse graph.
    /// Result: clean paths with minimal bends that hug obstacle contours naturally.
    /// </summary>
    public sealed class OrthogonalV2GeometryGenerator : IPathGeometryGenerator
    {
        /// <summary>Margin around each obstacle rect (pixels).</summary>
        private const double ObstacleMargin = 24;

        /// <summary>Extension length from port in the port direction before routing.</summary>
        private const double PortExtension = 28;

        /// <summary>Penalty for each bend (direction change) in A* search.</summary>
        private const double BendPenalty = 60;

        /// <summary>Max corner radius for rounded corners.</summary>
        private const double MaxCornerRadius = 12;

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
            return Generate(start, end, startDir, endDir, obstacles, MaxCornerRadius);
        }

        /// <summary>
        /// Generate with custom corner radius.
        /// Orthogonal=4, SmoothOrthogonal=24, OrthogonalV2=12 (default).
        /// </summary>
        public PathGeometry Generate(
            Point start, Point end,
            PortPosition? startDir, PortPosition? endDir,
            IReadOnlyList<Rect> obstacles,
            double maxCornerRadius)
        {
            PortPosition sDir = startDir ?? InferDirection(start, end, isStart: true);
            PortPosition eDir = endDir ?? InferDirection(start, end, isStart: false);

            // Inflate obstacles by margin
            var inflated = new List<Rect>();
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

            // Extend start/end in port direction so lines exit cleanly
            Point extStart = Extend(start, sDir, PortExtension);
            Point extEnd = Extend(end, eDir, PortExtension);

            // Build visibility graph and find path
            List<Point>? path = FindPathVisibilityGraph(extStart, extEnd, inflated);

            if (path != null && path.Count >= 2)
            {
                // Path includes extStart and extEnd
                return BuildGeometry(start, path, end, maxCornerRadius);
            }

            // Fallback: obstacle-aware detour bypass
            var fallback = CreateFallbackPath(start, end, sDir, eDir, inflated);
            return BuildGeometry(start, fallback, end, maxCornerRadius);
        }

        // ═══════════════════════════════════════════════════════════════
        // VISIBILITY GRAPH ROUTING
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Build an orthogonal visibility graph from obstacle corners + start/end,
        /// then run A* on it with bend penalty.
        /// </summary>
        private static List<Point>? FindPathVisibilityGraph(Point start, Point end, List<Rect> obstacles)
        {
            // Step 1: Collect key points (obstacle corners + mid-edges + start + end)
            var keyPoints = new List<Point> { start, end };
            foreach (var r in obstacles)
            {
                // 4 corners
                keyPoints.Add(new Point(r.Left, r.Top));
                keyPoints.Add(new Point(r.Right, r.Top));
                keyPoints.Add(new Point(r.Left, r.Bottom));
                keyPoints.Add(new Point(r.Right, r.Bottom));

                // 4 mid-edge points — tạo thêm đường routing dọc theo cạnh obstacle
                double midX = (r.Left + r.Right) / 2;
                double midY = (r.Top + r.Bottom) / 2;
                keyPoints.Add(new Point(midX, r.Top));    // top-center
                keyPoints.Add(new Point(midX, r.Bottom)); // bottom-center
                keyPoints.Add(new Point(r.Left, midY));   // left-center
                keyPoints.Add(new Point(r.Right, midY));  // right-center
            }

            // Step 2: Generate lead lines (horizontal + vertical rays from each key point)
            var hLines = new HashSet<double>(); // Y coords of horizontal lines
            var vLines = new HashSet<double>(); // X coords of vertical lines

            foreach (var p in keyPoints)
            {
                hLines.Add(p.Y);
                vLines.Add(p.X);
            }

            // Step 3: Generate graph nodes from all intersections of hLines × vLines
            //         that are NOT inside any obstacle
            var graphNodes = new List<Point>();
            var nodeIndex = new Dictionary<long, int>(); // packed (x,y) → index

            foreach (double y in hLines)
            {
                foreach (double x in vLines)
                {
                    var p = new Point(x, y);
                    if (!IsInsideAnyObstacle(p, obstacles))
                    {
                        int idx = graphNodes.Count;
                        graphNodes.Add(p);
                        nodeIndex[PackPoint(x, y)] = idx;
                    }
                }
            }

            // Ensure start and end are in the graph
            EnsurePointInGraph(start, graphNodes, nodeIndex);
            EnsurePointInGraph(end, graphNodes, nodeIndex);

            int startIdx = FindClosestNode(start, graphNodes);
            int endIdx = FindClosestNode(end, graphNodes);

            if (startIdx < 0 || endIdx < 0 || startIdx == endIdx)
                return null;

            // Step 4: Build adjacency list — connect nodes on same horizontal/vertical line
            //         if the segment between them doesn't intersect any obstacle
            int n = graphNodes.Count;
            var adj = new List<List<(int to, double dist)>>(n);
            for (int i = 0; i < n; i++)
                adj.Add(new List<(int, double)>());

            // Group nodes by Y (horizontal lines)
            var byY = new Dictionary<double, List<int>>();
            // Group nodes by X (vertical lines)
            var byX = new Dictionary<double, List<int>>();

            for (int i = 0; i < n; i++)
            {
                double ry = Math.Round(graphNodes[i].Y, 2);
                double rx = Math.Round(graphNodes[i].X, 2);

                if (!byY.ContainsKey(ry)) byY[ry] = new List<int>();
                byY[ry].Add(i);

                if (!byX.ContainsKey(rx)) byX[rx] = new List<int>();
                byX[rx].Add(i);
            }

            // Connect adjacent nodes on each horizontal line
            foreach (var kvp in byY)
            {
                var indices = kvp.Value.OrderBy(i => graphNodes[i].X).ToList();
                for (int k = 0; k < indices.Count - 1; k++)
                {
                    int a = indices[k], b = indices[k + 1];
                    var pa = graphNodes[a];
                    var pb = graphNodes[b];

                    if (!SegmentIntersectsAnyObstacle(pa, pb, obstacles))
                    {
                        double dist = Math.Abs(pb.X - pa.X);
                        adj[a].Add((b, dist));
                        adj[b].Add((a, dist));
                    }
                }
            }

            // Connect adjacent nodes on each vertical line
            foreach (var kvp in byX)
            {
                var indices = kvp.Value.OrderBy(i => graphNodes[i].Y).ToList();
                for (int k = 0; k < indices.Count - 1; k++)
                {
                    int a = indices[k], b = indices[k + 1];
                    var pa = graphNodes[a];
                    var pb = graphNodes[b];

                    if (!SegmentIntersectsAnyObstacle(pa, pb, obstacles))
                    {
                        double dist = Math.Abs(pb.Y - pa.Y);
                        adj[a].Add((b, dist));
                        adj[b].Add((a, dist));
                    }
                }
            }

            // Step 5: A* search with bend penalty
            return AStarOnGraph(graphNodes, adj, startIdx, endIdx);
        }

        // ─── A* on visibility graph ───

        private static List<Point>? AStarOnGraph(
            List<Point> nodes,
            List<List<(int to, double dist)>> adj,
            int startIdx, int endIdx)
        {
            int n = nodes.Count;
            var gScore = new double[n];
            var cameFrom = new int[n];
            var dirFrom = new int[n]; // 0=none, 1=horiz, 2=vert
            var closed = new bool[n];

            for (int i = 0; i < n; i++)
            {
                gScore[i] = double.MaxValue;
                cameFrom[i] = -1;
                dirFrom[i] = 0;
            }

            gScore[startIdx] = 0;
            Point endPt = nodes[endIdx];

            // Priority queue: (f, nodeIndex, idCounter)
            var openSet = new SortedSet<(double f, int node, int id)>();
            int idCounter = 0;
            openSet.Add((ManhattanDist(nodes[startIdx], endPt), startIdx, idCounter++));

            while (openSet.Count > 0)
            {
                var current = openSet.Min;
                openSet.Remove(current);
                int ci = current.node;

                if (ci == endIdx)
                {
                    // Reconstruct path
                    var path = new List<Point>();
                    int k = endIdx;
                    while (k != -1)
                    {
                        path.Add(nodes[k]);
                        k = cameFrom[k];
                    }
                    path.Reverse();
                    return SimplifyPath(path);
                }

                if (closed[ci]) continue;
                closed[ci] = true;

                foreach (var (ni, dist) in adj[ci])
                {
                    if (closed[ni]) continue;

                    // Determine direction of this edge
                    int edgeDir = GetEdgeDirection(nodes[ci], nodes[ni]);

                    // Apply bend penalty if direction changes
                    double moveCost = dist;
                    if (dirFrom[ci] != 0 && edgeDir != dirFrom[ci])
                        moveCost += BendPenalty;

                    double tentativeG = gScore[ci] + moveCost;

                    if (tentativeG < gScore[ni])
                    {
                        gScore[ni] = tentativeG;
                        cameFrom[ni] = ci;
                        dirFrom[ni] = edgeDir;
                        double f = tentativeG + ManhattanDist(nodes[ni], endPt);
                        openSet.Add((f, ni, idCounter++));
                    }
                }
            }

            return null; // No path found
        }

        // ─── Geometry helpers ───

        private static int GetEdgeDirection(Point a, Point b)
        {
            double dx = Math.Abs(b.X - a.X);
            double dy = Math.Abs(b.Y - a.Y);
            return dx > dy ? 1 : 2; // 1=horizontal, 2=vertical
        }

        private static double ManhattanDist(Point a, Point b)
            => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        /// <summary>
        /// Pack a point's coordinates into a single long for dictionary keying.
        /// Uses 0.5px rounding to handle floating point precision.
        /// </summary>
        private static long PackPoint(double x, double y)
        {
            int ix = (int)Math.Round(x * 2);
            int iy = (int)Math.Round(y * 2);
            return ((long)ix << 32) | (uint)iy;
        }

        private static void EnsurePointInGraph(Point p, List<Point> nodes, Dictionary<long, int> index)
        {
            long key = PackPoint(p.X, p.Y);
            if (!index.ContainsKey(key))
            {
                int idx = nodes.Count;
                nodes.Add(p);
                index[key] = idx;
            }
        }

        private static int FindClosestNode(Point target, List<Point> nodes)
        {
            int best = -1;
            double bestDist = double.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                double d = ManhattanDist(target, nodes[i]);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        // ─── Obstacle intersection checks ───

        /// <summary>Check if a point is strictly inside any obstacle rect.</summary>
        private static bool IsInsideAnyObstacle(Point p, List<Rect> obstacles)
        {
            foreach (var r in obstacles)
            {
                // Use strict interior check (not touching edges)
                if (p.X > r.Left + 0.5 && p.X < r.Right - 0.5 &&
                    p.Y > r.Top + 0.5 && p.Y < r.Bottom - 0.5)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if a horizontal or vertical segment intersects the interior of any obstacle.
        /// </summary>
        private static bool SegmentIntersectsAnyObstacle(Point a, Point b, List<Rect> obstacles)
        {
            // Determine if segment is horizontal or vertical
            bool isHoriz = Math.Abs(a.Y - b.Y) < 1;

            foreach (var r in obstacles)
            {
                if (isHoriz)
                {
                    // Horizontal segment: check if Y is within obstacle and X range overlaps
                    double minX = Math.Min(a.X, b.X);
                    double maxX = Math.Max(a.X, b.X);
                    double y = a.Y;

                    if (y > r.Top + 0.5 && y < r.Bottom - 0.5 &&
                        minX < r.Right - 0.5 && maxX > r.Left + 0.5)
                        return true;
                }
                else
                {
                    // Vertical segment
                    double minY = Math.Min(a.Y, b.Y);
                    double maxY = Math.Max(a.Y, b.Y);
                    double x = a.X;

                    if (x > r.Left + 0.5 && x < r.Right - 0.5 &&
                        minY < r.Bottom - 0.5 && maxY > r.Top + 0.5)
                        return true;
                }
            }
            return false;
        }

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

                // Skip collinear points (same horizontal or vertical line)
                bool collinearH = Math.Abs(prev.Y - curr.Y) < 1.5 && Math.Abs(curr.Y - next.Y) < 1.5;
                bool collinearV = Math.Abs(prev.X - curr.X) < 1.5 && Math.Abs(curr.X - next.X) < 1.5;

                if (!collinearH && !collinearV)
                {
                    result.Add(curr);
                }
            }

            result.Add(path[path.Count - 1]);

            // Snap waypoints to be strictly orthogonal
            return SnapOrthogonal(result);
        }

        /// <summary>
        /// Ensure each segment is perfectly horizontal or vertical by snapping coordinates.
        /// </summary>
        private static List<Point> SnapOrthogonal(List<Point> points)
        {
            if (points.Count <= 2) return points;

            var snapped = new List<Point> { points[0] };
            for (int i = 1; i < points.Count; i++)
            {
                Point prev = snapped[snapped.Count - 1];
                Point curr = points[i];

                double dx = Math.Abs(curr.X - prev.X);
                double dy = Math.Abs(curr.Y - prev.Y);

                // Snap to the dominant axis
                if (dx < dy)
                    snapped.Add(new Point(prev.X, curr.Y)); // vertical segment
                else
                    snapped.Add(new Point(curr.X, prev.Y)); // horizontal segment
            }
            return snapped;
        }

        // ─── Fallback path when visibility graph fails ───

        private static List<Point> CreateFallbackPath(Point start, Point end, PortPosition sDir, PortPosition eDir)
        {
            return CreateFallbackPath(start, end, sDir, eDir, null);
        }

        /// <summary>
        /// Fallback path with obstacle detection: nếu đường đi đơn giản xuyên qua obstacle,
        /// tạo detour vòng quanh obstacle đó.
        /// </summary>
        private static List<Point> CreateFallbackPath(Point start, Point end, PortPosition sDir, PortPosition eDir, IReadOnlyList<Rect>? obstacles)
        {
            var waypoints = new List<Point>();
            double ext = PortExtension;

            Point extStart = Extend(start, sDir, ext);
            Point extEnd = Extend(end, eDir, ext);

            waypoints.Add(extStart);

            // Simple 3-segment path
            List<Point> midPoints;
            if (IsHorizontal(sDir))
            {
                double midX = (extStart.X + extEnd.X) / 2;
                midPoints = new List<Point>
                {
                    new Point(midX, extStart.Y),
                    new Point(midX, extEnd.Y)
                };
            }
            else
            {
                double midY = (extStart.Y + extEnd.Y) / 2;
                midPoints = new List<Point>
                {
                    new Point(extStart.X, midY),
                    new Point(extEnd.X, midY)
                };
            }

            // Kiểm tra nếu đường đi xuyên qua obstacle → tạo detour
            if (obstacles != null && obstacles.Count > 0)
            {
                var allSegmentPoints = new List<Point> { extStart };
                allSegmentPoints.AddRange(midPoints);
                allSegmentPoints.Add(extEnd);

                // Tìm obstacle bị xuyên qua
                Rect? blockingObstacle = null;
                for (int i = 0; i < allSegmentPoints.Count - 1; i++)
                {
                    foreach (var obs in obstacles)
                    {
                        if (SegmentIntersectsAnyObstacle(allSegmentPoints[i], allSegmentPoints[i + 1], 
                            new List<Rect> { obs }))
                        {
                            blockingObstacle = obs;
                            break;
                        }
                    }
                    if (blockingObstacle.HasValue) break;
                }

                if (blockingObstacle.HasValue)
                {
                    var obs = blockingObstacle.Value;
                    // Tạo detour: đi vòng quanh obstacle
                    // Chọn hướng vòng (trên/dưới hoặc trái/phải) dựa trên vị trí start/end
                    double obsCenterX = (obs.Left + obs.Right) / 2;
                    double obsCenterY = (obs.Top + obs.Bottom) / 2;
                    double margin = ObstacleMargin + 4; // Extra margin cho detour

                    midPoints.Clear();

                    if (IsHorizontal(sDir))
                    {
                        // Đang đi ngang → vòng lên/xuống
                        bool goAbove = extStart.Y < obsCenterY; // vòng phía gần hơn
                        double detourY = goAbove ? obs.Top - margin : obs.Bottom + margin;

                        midPoints.Add(new Point(extStart.X, detourY));
                        midPoints.Add(new Point(extEnd.X, detourY));
                    }
                    else
                    {
                        // Đang đi dọc → vòng trái/phải
                        bool goLeft = extStart.X < obsCenterX;
                        double detourX = goLeft ? obs.Left - margin : obs.Right + margin;

                        midPoints.Add(new Point(detourX, extStart.Y));
                        midPoints.Add(new Point(detourX, extEnd.Y));
                    }
                }
            }

            waypoints.AddRange(midPoints);
            waypoints.Add(extEnd);
            return waypoints;
        }

        // ═══════════════════════════════════════════════════════════════
        // GEOMETRY BUILDING WITH ROUNDED CORNERS
        // ═══════════════════════════════════════════════════════════════

        private static PathGeometry BuildGeometry(Point start, List<Point> waypoints, Point end, double maxCornerRadius = 12)
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
                    Point prev = i == 0 ? start : allPoints[i - 1];

                    bool hasBend = HasDirectionChange(prev, current, next);

                    if (hasBend)
                    {
                        double cornerRadius = CalculateCornerRadius(prev, current, next, maxCornerRadius);
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

            bool seg1Horiz = Math.Abs(dy1) < 2;
            bool seg1Vert = Math.Abs(dx1) < 2;
            bool seg2Horiz = Math.Abs(dy2) < 2;
            bool seg2Vert = Math.Abs(dx2) < 2;

            return (seg1Horiz && seg2Vert) || (seg1Vert && seg2Horiz);
        }

        private static double CalculateCornerRadius(Point prev, Point current, Point next, double maxRadius = 12)
        {
            double d1 = Distance(prev, current);
            double d2 = Distance(current, next);
            double minDist = Math.Min(d1, d2);
            // Use up to 40% of the shorter segment, capped at MaxCornerRadius
            double radius = Math.Min(minDist * 0.4, maxRadius);
            return Math.Max(radius, 2);
        }

        private static void AddRoundedCorner(PathFigure figure, Point prev, Point corner, Point next, double radius)
        {
            double d1 = Distance(prev, corner);
            double d2 = Distance(corner, next);

            // Dynamically scale radius to fit available segment length
            double r = Math.Min(radius, Math.Min(d1 * 0.45, d2 * 0.45));

            if (r < 1.5)
            {
                // Too short for rounding
                figure.Segments.Add(new LineSegment(corner, true));
                return;
            }

            // Point on the line from prev → corner, r before the corner
            double ratio1 = (d1 - r) / d1;
            Point beforeCorner = new Point(
                prev.X + (corner.X - prev.X) * ratio1,
                prev.Y + (corner.Y - prev.Y) * ratio1);

            // Point on the line from corner → next, r after the corner
            double ratio2 = r / d2;
            Point afterCorner = new Point(
                corner.X + (next.X - corner.X) * ratio2,
                corner.Y + (next.Y - corner.Y) * ratio2);

            // Line to before corner
            figure.Segments.Add(new LineSegment(beforeCorner, true));

            // Smooth quadratic bezier through the corner point
            figure.Segments.Add(new QuadraticBezierSegment(corner, afterCorner, true));
        }

        // ─── Common Helpers ───

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
