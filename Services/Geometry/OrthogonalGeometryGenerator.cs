using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using FlowMy.Models;

namespace FlowMy.Services.Geometry
{
    public sealed class OrthogonalGeometryGenerator : IPathGeometryGenerator
    {
        public PathGeometry Generate(Point start, Point end, PortPosition? startDir, PortPosition? endDir)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = start };

            PortPosition sDir = startDir ?? InferStartDirection(start, end);
            PortPosition eDir = endDir ?? InferEndDirection(start, end);

            List<Point> waypoints = CalculateOrthogonalWaypoints(start, end, sDir, eDir);

            for (int i = 0; i < waypoints.Count; i++)
            {
                if (i == waypoints.Count - 1)
                {
                    figure.Segments.Add(new LineSegment(waypoints[i], true));
                }
                else
                {
                    Point current = waypoints[i];
                    Point next = waypoints[i + 1];
                    double cornerRadius = CalculateCornerRadius(current, next);
                    AddRoundedCorner(figure, current, next, cornerRadius);
                }
            }

            geometry.Figures.Add(figure);
            return geometry;
        }

        private static PortPosition InferStartDirection(Point start, Point end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            return Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? PortPosition.Right : PortPosition.Left)
                : (dy >= 0 ? PortPosition.Bottom : PortPosition.Top);
        }

        private static PortPosition InferEndDirection(Point start, Point end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            return Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? PortPosition.Left : PortPosition.Right)
                : (dy >= 0 ? PortPosition.Top : PortPosition.Bottom);
        }

        private static List<Point> CalculateOrthogonalWaypoints(Point start, Point end, PortPosition startDir, PortPosition endDir)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            double minOffset = 50;

            var candidatePaths = new List<List<Point>>();

            if ((startDir == PortPosition.Right && endDir == PortPosition.Left) ||
                (startDir == PortPosition.Left && endDir == PortPosition.Right))
            {
                if (IsDirectPathPossible(start, end, startDir))
                {
                    candidatePaths.Add(CreateHorizontalDirectPath(start, end));
                    if (Math.Abs(dx) > 200 || Math.Abs(dy) > 200)
                    {
                        candidatePaths.Add(CreateOptimizedHorizontalPath(start, end, minOffset));
                    }
                }
                else
                {
                    candidatePaths.Add(CreateBypassPath(start, end, startDir, endDir));
                }
            }
            else if ((startDir == PortPosition.Bottom && endDir == PortPosition.Top) ||
                     (startDir == PortPosition.Top && endDir == PortPosition.Bottom))
            {
                if (IsDirectPathPossible(start, end, startDir))
                {
                    candidatePaths.Add(CreateVerticalDirectPath(start, end));
                    if (Math.Abs(dx) > 200 || Math.Abs(dy) > 200)
                    {
                        candidatePaths.Add(CreateOptimizedVerticalPath(start, end, minOffset));
                    }
                }
                else
                {
                    candidatePaths.Add(CreateBypassPath(start, end, startDir, endDir));
                }
            }
            else if (startDir == endDir)
            {
                // Cùng phía (Right+Right, Left+Left, Top+Top, Bottom+Bottom): cần đường bypass vuông góc
                candidatePaths.Add(CreateSameSidePath(start, end, startDir, minOffset));
            }
            else
            {
                candidatePaths.Add(CreatePerpendicularPath(start, end, startDir, endDir, minOffset));
                if (Math.Abs(dx) > 150 || Math.Abs(dy) > 150)
                {
                    candidatePaths.Add(CreateOptimizedPerpendicularPath(start, end, startDir, endDir, minOffset));
                }
            }

            return SelectShortestPath(candidatePaths, start, end);
        }

        private static List<Point> CreateHorizontalDirectPath(Point start, Point end)
        {
            var path = new List<Point>();
            double midX = (start.X + end.X) / 2;
            path.Add(new Point(midX, start.Y));
            path.Add(new Point(midX, end.Y));
            return path;
        }

        private static List<Point> CreateVerticalDirectPath(Point start, Point end)
        {
            var path = new List<Point>();
            double midY = (start.Y + end.Y) / 2;
            path.Add(new Point(start.X, midY));
            path.Add(new Point(end.X, midY));
            return path;
        }

        private static List<Point> CreateOptimizedHorizontalPath(Point start, Point end, double minOffset)
        {
            var path = new List<Point>();
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            if (Math.Abs(dy) > 300)
            {
                double step1 = start.X + (dx * 0.3);
                double step2 = start.Y + (dy * 0.5);
                double step3 = start.X + (dx * 0.7);

                path.Add(new Point(step1, start.Y));
                path.Add(new Point(step1, step2));
                path.Add(new Point(step3, step2));
                path.Add(new Point(step3, end.Y));
            }
            else
            {
                double midX = (start.X + end.X) / 2;
                path.Add(new Point(midX, start.Y));
                path.Add(new Point(midX, end.Y));
            }

            return path;
        }

        private static List<Point> CreateOptimizedVerticalPath(Point start, Point end, double minOffset)
        {
            var path = new List<Point>();
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            if (Math.Abs(dx) > 300)
            {
                double step1 = start.Y + (dy * 0.3);
                double step2 = start.X + (dx * 0.5);
                double step3 = start.Y + (dy * 0.7);

                path.Add(new Point(start.X, step1));
                path.Add(new Point(step2, step1));
                path.Add(new Point(step2, step3));
                path.Add(new Point(end.X, step3));
            }
            else
            {
                double midY = (start.Y + end.Y) / 2;
                path.Add(new Point(start.X, midY));
                path.Add(new Point(end.X, midY));
            }

            return path;
        }

        private static List<Point> CreateOptimizedPerpendicularPath(Point start, Point end, PortPosition startDir, PortPosition endDir, double minOffset)
        {
            var path = new List<Point>();
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double offset = Math.Max(minOffset, 50);

            if ((startDir == PortPosition.Right || startDir == PortPosition.Left) &&
                (endDir == PortPosition.Top || endDir == PortPosition.Bottom))
            {
                int startMult = startDir == PortPosition.Right ? 1 : -1;
                double midX = start.X + (offset * startMult);
                double bypassY = end.Y < start.Y ? Math.Min(start.Y, end.Y) - offset : Math.Max(start.Y, end.Y) + offset;
                double approachY = endDir == PortPosition.Top ? end.Y - offset : end.Y + offset;

                if (Math.Abs(dy) > 200)
                {
                    double midY = (start.Y + end.Y) / 2;
                    path.Add(new Point(midX, start.Y));
                    path.Add(new Point(midX, midY));
                    path.Add(new Point(end.X, midY));
                }
                else
                {
                    path.Add(new Point(midX, start.Y));
                    path.Add(new Point(midX, bypassY));
                    path.Add(new Point(end.X, bypassY));
                }
                path.Add(new Point(end.X, approachY)); // Vuông góc vào port
            }
            else if ((startDir == PortPosition.Bottom || startDir == PortPosition.Top) &&
                     (endDir == PortPosition.Right || endDir == PortPosition.Left))
            {
                int startMult = startDir == PortPosition.Bottom ? 1 : -1;
                double midY = start.Y + (offset * startMult);
                double bypassX = end.X < start.X ? Math.Min(start.X, end.X) - offset : Math.Max(start.X, end.X) + offset;
                double approachX = endDir == PortPosition.Right ? end.X - offset : end.X + offset;

                if (Math.Abs(dx) > 200)
                {
                    double midX = (start.X + end.X) / 2;
                    path.Add(new Point(start.X, midY));
                    path.Add(new Point(midX, midY));
                    path.Add(new Point(midX, end.Y));
                }
                else
                {
                    path.Add(new Point(start.X, midY));
                    path.Add(new Point(bypassX, midY));
                    path.Add(new Point(bypassX, end.Y));
                }
                path.Add(new Point(approachX, end.Y)); // Vuông góc vào port
            }

            return path;
        }

        private static bool IsDirectPathPossible(Point start, Point end, PortPosition startDir)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            if (startDir == PortPosition.Right) return dx > 0;
            if (startDir == PortPosition.Left) return dx < 0;
            if (startDir == PortPosition.Bottom) return dy > 0;
            if (startDir == PortPosition.Top) return dy < 0;
            return true;
        }

        private static List<Point> CreateBypassPath(Point start, Point end, PortPosition startDir, PortPosition endDir)
        {
            var path = new List<Point>();
            double bypassOffset = 80;

            if (startDir == PortPosition.Right || startDir == PortPosition.Left)
            {
                int dirMultiplier = startDir == PortPosition.Right ? 1 : -1;
                double outX = start.X + (bypassOffset * dirMultiplier);
                path.Add(new Point(outX, start.Y));

                double topBypassY = Math.Min(start.Y, end.Y) - bypassOffset;
                double bottomBypassY = Math.Max(start.Y, end.Y) + bypassOffset;

                double topPathLength = Math.Abs(start.Y - topBypassY) + Math.Abs(end.Y - topBypassY);
                double bottomPathLength = Math.Abs(start.Y - bottomBypassY) + Math.Abs(end.Y - bottomBypassY);

                double bypassY = topPathLength < bottomPathLength ? topBypassY : bottomBypassY;

                path.Add(new Point(outX, bypassY));

                int endDirMultiplier = endDir == PortPosition.Right ? 1 : -1;
                double inX = end.X + (bypassOffset * endDirMultiplier);
                path.Add(new Point(inX, bypassY));
                path.Add(new Point(inX, end.Y));
            }
            else
            {
                int dirMultiplier = startDir == PortPosition.Bottom ? 1 : -1;
                double outY = start.Y + (bypassOffset * dirMultiplier);
                path.Add(new Point(start.X, outY));

                double leftBypassX = Math.Min(start.X, end.X) - bypassOffset;
                double rightBypassX = Math.Max(start.X, end.X) + bypassOffset;

                double leftPathLength = Math.Abs(start.X - leftBypassX) + Math.Abs(end.X - leftBypassX);
                double rightPathLength = Math.Abs(start.X - rightBypassX) + Math.Abs(end.X - rightBypassX);

                double bypassX = leftPathLength < rightPathLength ? leftBypassX : rightBypassX;

                path.Add(new Point(bypassX, outY));

                int endDirMultiplier = endDir == PortPosition.Bottom ? 1 : -1;
                double inY = end.Y + (bypassOffset * endDirMultiplier);
                path.Add(new Point(bypassX, inY));
                path.Add(new Point(end.X, inY));
            }

            return path;
        }

        /// <summary>
        /// Tạo đường vuông góc khi cả hai port cùng phía (Right+Right, Left+Left, Top+Top, Bottom+Bottom).
        /// Đường đi NGẮN NHẤT: đi thẳng ra → rẽ đúng hướng (lên/xuống hoặc trái/phải) đến level của port → rẽ vào port (đâm xuôi hướng mũi tên).
        /// </summary>
        private static List<Point> CreateSameSidePath(Point start, Point end, PortPosition portDir, double minOffset)
        {
            var path = new List<Point>();
            double offset = Math.Max(minOffset, 50);

            if (portDir == PortPosition.Right || portDir == PortPosition.Left)
            {
                int dirMult = portDir == PortPosition.Right ? 1 : -1;
                double outX = start.X + (offset * dirMult);
                double inX = end.X - (offset * dirMult);

                // Đường ngắn nhất: đi thẳng ra → rẽ lên/xuống ĐẾN ĐÚNG end.Y (không vòng xa) → rẽ ngang vào port
                path.Add(new Point(outX, start.Y));   // 1. Đi thẳng phải (hoặc trái)
                path.Add(new Point(outX, end.Y));     // 2. Rẽ lên/xuống thẳng đến level port B (ngắn nhất)
                path.Add(new Point(inX, end.Y));      // 3. Đi ngang
                // 4. (inX,end.Y)→end: đâm xuôi hướng mũi tên (từ trái vào cạnh phải node B)
            }
            else
            {
                int dirMult = portDir == PortPosition.Bottom ? 1 : -1;
                double outY = start.Y + (offset * dirMult);
                double inY = end.Y - (offset * dirMult);

                // Đường ngắn nhất: đi thẳng ra → rẽ trái/phải ĐẾN ĐÚNG end.X → rẽ dọc vào port
                path.Add(new Point(start.X, outY));   // 1. Đi thẳng ra
                path.Add(new Point(end.X, outY));     // 2. Rẽ trái/phải thẳng đến level port B (ngắn nhất)
                path.Add(new Point(end.X, inY));      // 3. Đi dọc
                // 4. (end.X,inY)→end: đâm xuôi hướng mũi tên
            }

            return path;
        }

        /// <summary>
        /// Port out ngang (R/L) + Port in dọc (T/B): đi thẳng ra → rẽ lên/xuống → rẽ ngang → rẽ vuông góc vào port.
        /// Port out dọc (T/B) + Port in ngang (R/L): đi thẳng ra → rẽ trái/phải → rẽ dọc → rẽ vuông góc vào port.
        /// Luôn thêm điểm approach vuông góc với cạnh port trước khi vào end.
        /// </summary>
        private static List<Point> CreatePerpendicularPath(Point start, Point end, PortPosition startDir, PortPosition endDir, double minOffset)
        {
            var path = new List<Point>();
            double offset = Math.Max(minOffset, 50);

            if ((startDir == PortPosition.Right || startDir == PortPosition.Left) &&
                (endDir == PortPosition.Top || endDir == PortPosition.Bottom))
            {
                // Port out phải/trái, port in trên/dưới: đi thẳng ra → rẽ lên/xuống → rẽ ngang → rẽ vuông góc vào port
                int startMult = startDir == PortPosition.Right ? 1 : -1;
                double midX = start.X + (offset * startMult);
                // Port in above: rẽ lên (bypassY = min - offset); Port in below: rẽ xuống (bypassY = max + offset)
                double bypassY = end.Y < start.Y ? Math.Min(start.Y, end.Y) - offset : Math.Max(start.Y, end.Y) + offset;

                path.Add(new Point(midX, start.Y));   // 1. Đi thẳng ra
                path.Add(new Point(midX, bypassY));  // 2. Rẽ lên top hoặc xuống bottom
                path.Add(new Point(end.X, bypassY));  // 3. Rẽ trái/phải
                // 4. Vuông góc vào port: Top port → approach từ top (end.Y - offset, đi xuống); Bottom port → approach từ bottom (end.Y + offset, đi lên)
                double approachY = endDir == PortPosition.Top ? end.Y - offset : end.Y + offset;
                path.Add(new Point(end.X, approachY)); // 5. Điểm approach vuông góc
            }
            else if ((startDir == PortPosition.Bottom || startDir == PortPosition.Top) &&
                     (endDir == PortPosition.Right || endDir == PortPosition.Left))
            {
                // Port out trên/dưới, port in phải/trái: đi thẳng ra → rẽ trái/phải → rẽ dọc → rẽ vuông góc vào port
                int startMult = startDir == PortPosition.Bottom ? 1 : -1;
                double midY = start.Y + (offset * startMult);
                double bypassX = end.X < start.X ? Math.Min(start.X, end.X) - offset : Math.Max(start.X, end.X) + offset;

                path.Add(new Point(start.X, midY));   // 1. Đi thẳng ra
                path.Add(new Point(bypassX, midY));   // 2. Rẽ trái/phải
                path.Add(new Point(bypassX, end.Y));  // 3. Rẽ lên/xuống
                // 4. Vuông góc vào port: Right → approach từ trái (end.X - offset); Left → approach từ phải (end.X + offset)
                double approachX = endDir == PortPosition.Right ? end.X - offset : end.X + offset;
                path.Add(new Point(approachX, end.Y)); // 5. Điểm approach vuông góc
            }

            return path;
        }

        private static List<Point> SelectShortestPath(List<List<Point>> candidatePaths, Point start, Point end)
        {
            if (candidatePaths.Count == 0)
            {
                return new List<Point> { start, end };
            }

            List<Point> shortestPath = candidatePaths[0];
            double shortestLength = CalculatePathLength(shortestPath);

            foreach (var path in candidatePaths)
            {
                double length = CalculatePathLength(path);
                if (length < shortestLength)
                {
                    shortestLength = length;
                    shortestPath = path;
                }
            }

            if (shortestPath.Count == 0 || shortestPath[0] != start)
            {
                shortestPath.Insert(0, start);
            }
            if (shortestPath.Count == 0 || shortestPath[^1] != end)
            {
                shortestPath.Add(end);
            }

            return shortestPath;
        }

        private static double CalculatePathLength(List<Point> path)
        {
            if (path.Count < 2) return 0;

            double totalLength = 0;
            for (int i = 0; i < path.Count - 1; i++)
            {
                double dx = path[i + 1].X - path[i].X;
                double dy = path[i + 1].Y - path[i].Y;
                totalLength += Math.Sqrt(dx * dx + dy * dy);
            }
            return totalLength;
        }

        private static double CalculateCornerRadius(Point p1, Point p2)
        {
            double distance = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            double radius = Math.Min(distance * 0.3, 15);
            return Math.Max(radius, 5);
        }

        private static void AddRoundedCorner(PathFigure figure, Point current, Point next, double radius)
        {
            double dx = next.X - current.X;
            double dy = next.Y - current.Y;

            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance < radius * 2)
            {
                figure.Segments.Add(new LineSegment(next, true));
                return;
            }

            double ratio = (distance - radius) / distance;
            Point beforeCorner = new Point(
                current.X + dx * ratio,
                current.Y + dy * ratio
            );

            figure.Segments.Add(new LineSegment(beforeCorner, true));

            Point controlPoint = next;
            figure.Segments.Add(new QuadraticBezierSegment(controlPoint, next, true));
        }
    }
}

