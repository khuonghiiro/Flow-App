using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using FlowMy.Models;

namespace FlowMy.Services.Geometry
{
    /// <summary>
    /// Circuit Board (PCB Trace) routing: High-tech 45-degree octagonal electronic circuit trace generator.
    /// Produces clean, elegant 45-degree diagonal PCB traces with smooth filleted corners and obstacle avoidance
    /// to ensure lines never pass under or get covered by other nodes.
    /// </summary>
    public sealed class CircuitBoardGeometryGenerator : IPathGeometryGenerator
    {
        /// <summary>Margin around each obstacle rect (pixels) to prevent nodes from obscuring lines.</summary>
        private const double ObstacleMargin = 18;

        /// <summary>Extension length from port in the port direction before routing.</summary>
        private const double PortExtension = 32;

        // ─── IPathGeometryGenerator (4-param fallback, no obstacles) ───

        public PathGeometry Generate(Point start, Point end, PortPosition? startDir, PortPosition? endDir)
        {
            return Generate(start, end, startDir, endDir, Array.Empty<Rect>());
        }

        // ─── Main entry – obstacle-aware PCB circuit trace ───

        public PathGeometry Generate(
            Point start, Point end,
            PortPosition? startDir, PortPosition? endDir,
            IReadOnlyList<Rect> obstacles)
        {
            PortPosition sDir = startDir ?? InferDirection(start, end, isStart: true);
            PortPosition eDir = endDir ?? InferDirection(start, end, isStart: false);

            // Inflate obstacle bounds with margin
            var inflated = new List<Rect>();
            if (obstacles != null)
            {
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
            }

            Point extStart = Extend(start, sDir, PortExtension);
            Point extEnd = Extend(end, eDir, PortExtension);

            // Try Direct 45-Degree Octagonal PCB Path first
            List<Point>? waypoints = GenerateDirectOctagonalPath(extStart, extEnd, sDir, eDir, inflated);

            // Fallback to obstacle-avoiding 45-degree octagonal channel path if direct path collides
            if (waypoints == null || waypoints.Count == 0)
            {
                waypoints = GenerateObstacleBypassOctagonalPath(extStart, extEnd, sDir, eDir, inflated);
            }

            return BuildSmoothCircuitBoardGeometry(start, extStart, waypoints, extEnd, end);
        }

        // ─── Direct 45° Octagonal PCB Routing ───

        private static List<Point>? GenerateDirectOctagonalPath(
            Point start, Point end,
            PortPosition sDir, PortPosition eDir,
            List<Rect> obstacles)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            var path = new List<Point>();

            // Case A: Almost horizontal line
            if (Math.Abs(dy) < 4.0 && dx > 0 && IsHorizontal(sDir) && IsHorizontal(eDir))
            {
                path.Add(end);
                return CheckPathFree(start, path, obstacles) ? path : null;
            }

            // Case B: Standard left-to-right connection with 45-degree diagonal leg in middle
            if (dx > 20 && IsHorizontal(sDir) && IsHorizontal(eDir))
            {
                double diagSize = Math.Min(Math.Abs(dy), dx * 0.45);
                double signY = Math.Sign(dy);

                double runBeforeDiag = (dx - diagSize) / 2.0;

                Point p1 = new Point(start.X + runBeforeDiag, start.Y);
                Point p2 = new Point(p1.X + diagSize, p1.Y + signY * diagSize);

                path.Add(p1);
                path.Add(p2);
                path.Add(end);

                return CheckPathFree(start, path, obstacles) ? path : null;
            }

            // Case C: Vertical alignment with 45-degree diagonal leg
            if (Math.Abs(dy) > 20 && !IsHorizontal(sDir) && !IsHorizontal(eDir))
            {
                double diagSize = Math.Min(Math.Abs(dx), Math.Abs(dy) * 0.45);
                double signX = Math.Sign(dx);

                double runBeforeDiag = (Math.Abs(dy) - diagSize) / 2.0;
                double signY = Math.Sign(dy);

                Point p1 = new Point(start.X, start.Y + signY * runBeforeDiag);
                Point p2 = new Point(p1.X + signX * diagSize, p1.Y + signY * diagSize);

                path.Add(p1);
                path.Add(p2);
                path.Add(end);

                return CheckPathFree(start, path, obstacles) ? path : null;
            }

            // Case D: Right-angled transition with a single 45-degree chamfer leg
            if (IsHorizontal(sDir) != IsHorizontal(eDir))
            {
                Point corner = IsHorizontal(sDir)
                    ? new Point(end.X, start.Y)
                    : new Point(start.X, end.Y);

                double d1 = Distance(start, corner);
                double d2 = Distance(corner, end);
                double chamfer = Math.Min(Math.Min(d1 * 0.4, d2 * 0.4), 40.0);

                if (chamfer > 4.0)
                {
                    Point p1 = new Point(
                        corner.X - Math.Sign(corner.X - start.X) * chamfer,
                        corner.Y - Math.Sign(corner.Y - start.Y) * chamfer);

                    Point p2 = new Point(
                        corner.X + Math.Sign(end.X - corner.X) * chamfer,
                        corner.Y + Math.Sign(end.Y - corner.Y) * chamfer);

                    path.Add(p1);
                    path.Add(p2);
                }
                else
                {
                    path.Add(corner);
                }

                path.Add(end);
                return CheckPathFree(start, path, obstacles) ? path : null;
            }

            return null;
        }

        // ─── Obstacle Avoidance 45° Channel Path Generator ───

        private static List<Point> GenerateObstacleBypassOctagonalPath(
            Point start, Point end,
            PortPosition sDir, PortPosition eDir,
            List<Rect> obstacles)
        {
            var waypoints = new List<Point>();

            // Find overlapping obstacles between start and end
            double minX = Math.Min(start.X, end.X);
            double maxX = Math.Max(start.X, end.X);
            double minY = Math.Min(start.Y, end.Y);
            double maxY = Math.Max(start.Y, end.Y);

            var blocking = obstacles.Where(r => r.Right > minX && r.X < maxX && r.Bottom > minY && r.Y < maxY).ToList();

            if (blocking.Count > 0)
            {
                // Route via top or bottom clear channel
                double topY = blocking.Min(r => r.Y) - 20;
                double botY = blocking.Max(r => r.Bottom) + 20;

                double chosenY = Math.Abs(start.Y - topY) < Math.Abs(start.Y - botY) ? topY : botY;

                double dx1 = Math.Abs(chosenY - start.Y);
                double dx2 = Math.Abs(chosenY - end.Y);

                Point p1 = new Point(start.X + dx1, chosenY);
                Point p2 = new Point(end.X - dx2, chosenY);

                waypoints.Add(p1);
                waypoints.Add(p2);
                waypoints.Add(end);
            }
            else
            {
                // Simple 3-segment octagonal mid-break
                double midX = (start.X + end.X) / 2.0;
                double dy = end.Y - start.Y;
                double diag = Math.Min(Math.Abs(dy) / 2.0, 30.0);
                double signY = Math.Sign(dy);

                Point p1 = new Point(midX - diag / 2.0, start.Y);
                Point p2 = new Point(midX + diag / 2.0, start.Y + signY * Math.Abs(dy));

                waypoints.Add(p1);
                waypoints.Add(p2);
                waypoints.Add(end);
            }

            return waypoints;
        }

        // ─── Check path collision with obstacles ───

        private static bool CheckPathFree(Point start, List<Point> waypoints, List<Rect> obstacles)
        {
            if (obstacles.Count == 0) return true;

            Point prev = start;
            foreach (var curr in waypoints)
            {
                var lineRect = new Rect(
                    Math.Min(prev.X, curr.X),
                    Math.Min(prev.Y, curr.Y),
                    Math.Max(Math.Abs(curr.X - prev.X), 1.0),
                    Math.Max(Math.Abs(curr.Y - prev.Y), 1.0));

                foreach (var obs in obstacles)
                {
                    if (obs.IntersectsWith(lineRect))
                    {
                        return false;
                    }
                }
                prev = curr;
            }

            return true;
        }

        // ─── Smooth 45° PCB Filleted Geometry Builder ───

        private static PathGeometry BuildSmoothCircuitBoardGeometry(
            Point origStart, Point extStart, List<Point> waypoints, Point extEnd, Point origEnd)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = origStart,
                IsClosed = false,
                IsFilled = false
            };

            var fullPoints = new List<Point> { origStart, extStart };
            fullPoints.AddRange(waypoints);
            if (Distance(fullPoints.Last(), extEnd) > 0.1) fullPoints.Add(extEnd);
            if (Distance(fullPoints.Last(), origEnd) > 0.1) fullPoints.Add(origEnd);

            // Deduplicate consecutive identical points
            var cleanPoints = new List<Point>();
            foreach (var p in fullPoints)
            {
                if (cleanPoints.Count == 0 || Distance(cleanPoints.Last(), p) > 0.5)
                {
                    cleanPoints.Add(p);
                }
            }

            if (cleanPoints.Count < 2)
            {
                figure.Segments.Add(new LineSegment(origEnd, true));
                geometry.Figures.Add(figure);
                return geometry;
            }

            // Render path with smooth fillet corners for elegant 45° PCB trace look
            for (int i = 1; i < cleanPoints.Count; i++)
            {
                Point prev = cleanPoints[i - 1];
                Point curr = cleanPoints[i];

                if (i < cleanPoints.Count - 1)
                {
                    Point next = cleanPoints[i + 1];
                    AddFilletedPcbCorner(figure, prev, curr, next);
                }
                else
                {
                    figure.Segments.Add(new LineSegment(curr, true));
                }
            }

            geometry.Figures.Add(figure);
            return geometry;
        }

        private static void AddFilletedPcbCorner(PathFigure figure, Point prev, Point corner, Point next)
        {
            double d1 = Distance(prev, corner);
            double d2 = Distance(corner, next);

            // Radius for smooth PCB arc fillet
            double radius = Math.Min(Math.Min(d1 * 0.45, d2 * 0.45), 18.0);

            if (radius < 2.5)
            {
                figure.Segments.Add(new LineSegment(corner, true));
                return;
            }

            Point beforeCorner = new Point(
                corner.X - (corner.X - prev.X) * (radius / d1),
                corner.Y - (corner.Y - prev.Y) * (radius / d1));

            Point afterCorner = new Point(
                corner.X + (next.X - corner.X) * (radius / d2),
                corner.Y + (next.Y - corner.Y) * (radius / d2));

            figure.Segments.Add(new LineSegment(beforeCorner, true));
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
