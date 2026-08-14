using System;
using System.Windows;
using System.Windows.Media;
using FlowMy.Models;

namespace FlowMy.Services.Geometry
{
    public sealed class BezierGeometryGenerator : IPathGeometryGenerator
    {
        public PathGeometry Generate(Point start, Point end, PortPosition? startDir, PortPosition? endDir)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = start };

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            PortPosition sDir = startDir ?? PortPosition.Right;
            PortPosition eDir = endDir ?? PortPosition.Left;

            Point control1 = CalculateControlPoint(start, sDir, distance);
            Point control2 = CalculateControlPoint(end, eDir, distance);

            control1 = AdjustControlPointForSpecialCases(control1, start, sDir, dx, dy, distance);
            control2 = AdjustControlPointForSpecialCases(control2, end, eDir, dx, dy, distance);

            figure.Segments.Add(new BezierSegment(control1, control2, end, true));
            geometry.Figures.Add(figure);
            return geometry;
        }

        private static Point CalculateControlPoint(Point portPoint, PortPosition direction, double distance)
        {
            // ✅ Tăng offset mặc định để đường cong mềm mại hơn, tránh dính vật cản
            double offset = Math.Min(distance * 0.5, 250); 
            offset = Math.Max(offset, 80); // Tối thiểu 80 (cũ là 50)

            return direction switch
            {
                PortPosition.Right => new Point(portPoint.X + offset, portPoint.Y),
                PortPosition.Left => new Point(portPoint.X - offset, portPoint.Y),
                PortPosition.Bottom => new Point(portPoint.X, portPoint.Y + offset),
                PortPosition.Top => new Point(portPoint.X, portPoint.Y - offset),
                _ => new Point(portPoint.X + offset, portPoint.Y)
            };
        }

        private static Point AdjustControlPointForSpecialCases(
            Point controlPoint,
            Point portPoint,
            PortPosition direction,
            double dx,
            double dy,
            double distance)
        {
            if (direction == PortPosition.Right && dx < 0)
            {
                // ✅ Backtracking (vòng ngược lại): Đẩy cong ra xa hơn
                double extraOffset = Math.Max(Math.Abs(dx) * 0.5, 100); 
                controlPoint = new Point(portPoint.X + distance * 0.4 + extraOffset, portPoint.Y);

                if (Math.Abs(dy) < 100)
                {
                    controlPoint = new Point(portPoint.X + distance * 0.4, portPoint.Y + dy * 0.5);
                }
            }
            else if (direction == PortPosition.Left && dx > 0)
            {
                double extraOffset = Math.Max(Math.Abs(dx) * 0.5, 100);
                controlPoint = new Point(portPoint.X - distance * 0.4 - extraOffset, portPoint.Y);

                if (Math.Abs(dy) < 100)
                {
                    controlPoint = new Point(portPoint.X - distance * 0.4, portPoint.Y + dy * 0.5);
                }
            }
            else if (direction == PortPosition.Top && dy > 0)
            {
                double extraOffset = Math.Max(Math.Abs(dy) * 0.5, 100);
                controlPoint = new Point(portPoint.X, portPoint.Y - distance * 0.4 - extraOffset);

                if (Math.Abs(dx) < 100)
                {
                    controlPoint = new Point(portPoint.X + dx * 0.5, portPoint.Y - distance * 0.4);
                }
            }
            else if (direction == PortPosition.Bottom && dy < 0)
            {
                double extraOffset = Math.Max(Math.Abs(dy) * 0.5, 100);
                controlPoint = new Point(portPoint.X, portPoint.Y + distance * 0.4 + extraOffset);

                if (Math.Abs(dx) < 100)
                {
                    controlPoint = new Point(portPoint.X + dx * 0.5, portPoint.Y + distance * 0.4);
                }
            }

            return controlPoint;
        }
    }
}

