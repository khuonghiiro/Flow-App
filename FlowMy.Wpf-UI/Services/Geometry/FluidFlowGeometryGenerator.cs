// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using FlowMy.Models;

namespace FlowMy.Services.Geometry
{
    /// <summary>
    /// Generator tạo đường nối kiểu "Dòng chảy mượt mà" (Fluid Flow / Neon Flow).
    /// Tạo đường cong hữu cơ S-Curve với adaptive tangents, inflection smoothing,
    /// và xử lý backtracking mượt mà cho cảm giác dòng chảy dữ liệu neon hiện đại.
    /// </summary>
    public sealed class FluidFlowGeometryGenerator : IPathGeometryGenerator
    {
        public PathGeometry Generate(Point start, Point end, PortPosition? startDir, PortPosition? endDir)
        {
            return Generate(start, end, startDir, endDir, null);
        }

        public PathGeometry Generate(
            Point start,
            Point end,
            PortPosition? startDir,
            PortPosition? endDir,
            IReadOnlyList<Rect>? obstacles)
        {
            PortPosition sDir = startDir ?? InferDirection(start, end, isStart: true);
            PortPosition eDir = endDir ?? InferDirection(start, end, isStart: false);

            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = start,
                IsClosed = false,
                IsFilled = false
            };

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            // Xử lý các trường hợp kết nối theo hướng port
            if (IsHorizontalPort(sDir) && IsHorizontalPort(eDir))
            {
                GenerateHorizontalFluidCurve(figure, start, end, sDir, eDir, dx, dy);
            }
            else if (IsVerticalPort(sDir) && IsVerticalPort(eDir))
            {
                GenerateVerticalFluidCurve(figure, start, end, sDir, eDir, dx, dy);
            }
            else
            {
                GenerateMixedAxisFluidCurve(figure, start, end, sDir, eDir, dx, dy);
            }

            geometry.Figures.Add(figure);
            return geometry;
        }

        #region Horizontal Flow Curves

        private static void GenerateHorizontalFluidCurve(
            PathFigure figure,
            Point start,
            Point end,
            PortPosition sDir,
            PortPosition eDir,
            double dx,
            double dy)
        {
            bool isForwardFlow = (sDir == PortPosition.Right && eDir == PortPosition.Left && dx > 20) ||
                                (sDir == PortPosition.Left && eDir == PortPosition.Right && dx < -20);

            if (isForwardFlow)
            {
                // Dòng chảy xuôi chiều (Standard Forward S-Curve)
                double tangentLength = CalculateAdaptiveHorizontalTangent(Math.Abs(dx), Math.Abs(dy));
                double startSign = sDir == PortPosition.Right ? 1.0 : -1.0;
                double endSign = eDir == PortPosition.Left ? -1.0 : 1.0;

                Point c1 = new Point(start.X + startSign * tangentLength, start.Y);
                Point c2 = new Point(end.X + endSign * tangentLength, end.Y);

                figure.Segments.Add(new BezierSegment(c1, c2, end, true));
            }
            else
            {
                // Dòng chảy ngược chiều / vòng tránh (Smart Backtracking Loopback)
                GenerateHorizontalBacktrackingSpline(figure, start, end, sDir, eDir, dx, dy);
            }
        }

        private static double CalculateAdaptiveHorizontalTangent(double absDx, double absDy)
        {
            // Tangent mở rộng mềm mại theo khoảng cách ngang và độ lệch dọc
            double tangent = Math.Max(50, absDx * 0.48 + absDy * 0.12);
            if (absDx < 120 && absDy > 40)
            {
                // Khi 2 node gần nhau ngang nhưng lệch dọc cao, tăng tangent để tạo S-curve thanh thoát
                tangent = Math.Max(45, absDx * 0.35 + Math.Min(absDy * 0.25, 110));
            }
            return Math.Min(tangent, 300);
        }

        private static void GenerateHorizontalBacktrackingSpline(
            PathFigure figure,
            Point start,
            Point end,
            PortPosition sDir,
            PortPosition eDir,
            double dx,
            double dy)
        {
            double absDx = Math.Abs(dx);
            double absDy = Math.Abs(dy);

            double loopOffset = Math.Max(70, Math.Min(absDx * 0.45 + 70, 160));
            double sSign = sDir == PortPosition.Right ? 1.0 : -1.0;
            double eSign = eDir == PortPosition.Left ? -1.0 : 1.0;

            // Tính điểm trung gian uốn lượn mượt mà
            double midX = (start.X + end.X) * 0.5 + (sDir == PortPosition.Right ? loopOffset * 0.5 : -loopOffset * 0.5);
            double verticalShift = absDy < 60 ? (dy >= 0 ? 80 : -80) : 0;
            double midY = (start.Y + end.Y) * 0.5 + verticalShift;
            Point mid = new Point(midX, midY);

            // Đoạn 1: start -> mid (uốn mềm ra ngoài)
            Point c1a = new Point(start.X + sSign * loopOffset, start.Y);
            Point c1b = new Point(mid.X, mid.Y - (end.Y - start.Y) * 0.2);
            figure.Segments.Add(new BezierSegment(c1a, c1b, mid, true));

            // Đoạn 2: mid -> end (uốn mềm vào target)
            Point c2a = new Point(mid.X, mid.Y + (end.Y - start.Y) * 0.2);
            Point c2b = new Point(end.X + eSign * loopOffset, end.Y);
            figure.Segments.Add(new BezierSegment(c2a, c2b, end, true));
        }

        #endregion

        #region Vertical Flow Curves

        private static void GenerateVerticalFluidCurve(
            PathFigure figure,
            Point start,
            Point end,
            PortPosition sDir,
            PortPosition eDir,
            double dx,
            double dy)
        {
            bool isForwardFlow = (sDir == PortPosition.Bottom && eDir == PortPosition.Top && dy > 20) ||
                                (sDir == PortPosition.Top && eDir == PortPosition.Bottom && dy < -20);

            if (isForwardFlow)
            {
                double absDx = Math.Abs(dx);
                double absDy = Math.Abs(dy);
                double tangent = Math.Max(50, Math.Min(absDy * 0.48 + absDx * 0.12, 300));
                double startSign = sDir == PortPosition.Bottom ? 1.0 : -1.0;
                double endSign = eDir == PortPosition.Top ? -1.0 : 1.0;

                Point c1 = new Point(start.X, start.Y + startSign * tangent);
                Point c2 = new Point(end.X, end.Y + endSign * tangent);

                figure.Segments.Add(new BezierSegment(c1, c2, end, true));
            }
            else
            {
                // Vertical backtracking
                double absDy = Math.Abs(dy);
                double loopOffset = Math.Max(70, Math.Min(absDy * 0.45 + 70, 160));
                double sSign = sDir == PortPosition.Bottom ? 1.0 : -1.0;
                double eSign = eDir == PortPosition.Top ? -1.0 : 1.0;

                double midY = (start.Y + end.Y) * 0.5 + (sDir == PortPosition.Bottom ? loopOffset * 0.5 : -loopOffset * 0.5);
                double horizontalShift = Math.Abs(dx) < 60 ? (dx >= 0 ? 80 : -80) : 0;
                double midX = (start.X + end.X) * 0.5 + horizontalShift;
                Point mid = new Point(midX, midY);

                Point c1a = new Point(start.X, start.Y + sSign * loopOffset);
                Point c1b = new Point(mid.X - (end.X - start.X) * 0.2, mid.Y);
                figure.Segments.Add(new BezierSegment(c1a, c1b, mid, true));

                Point c2a = new Point(mid.X + (end.X - start.X) * 0.2, mid.Y);
                Point c2b = new Point(end.X, end.Y + eSign * loopOffset);
                figure.Segments.Add(new BezierSegment(c2a, c2b, end, true));
            }
        }

        #endregion

        #region Mixed-Axis Curves & Utilities

        private static void GenerateMixedAxisFluidCurve(
            PathFigure figure,
            Point start,
            Point end,
            PortPosition sDir,
            PortPosition eDir,
            double dx,
            double dy)
        {
            Vector startVec = GetDirectionVector(sDir);
            Vector endVec = GetDirectionVector(eDir);

            double absDx = Math.Abs(dx);
            double absDy = Math.Abs(dy);
            double dist = Math.Sqrt(dx * dx + dy * dy);

            // Tính tangent theo từng trục tương ứng
            double startTangent = IsHorizontalPort(sDir)
                ? Math.Max(40, Math.Min(absDx * 0.6 + 30, 220))
                : Math.Max(40, Math.Min(absDy * 0.6 + 30, 220));

            double endTangent = IsHorizontalPort(eDir)
                ? Math.Max(40, Math.Min(absDx * 0.6 + 30, 220))
                : Math.Max(40, Math.Min(absDy * 0.6 + 30, 220));

            if (dist < 80)
            {
                startTangent = dist * 0.4;
                endTangent = dist * 0.4;
            }

            Point c1 = new Point(start.X + startVec.X * startTangent, start.Y + startVec.Y * startTangent);
            Point c2 = new Point(end.X - endVec.X * endTangent, end.Y - endVec.Y * endTangent);

            figure.Segments.Add(new BezierSegment(c1, c2, end, true));
        }

        private static bool IsHorizontalPort(PortPosition pos) =>
            pos == PortPosition.Left || pos == PortPosition.Right;

        private static bool IsVerticalPort(PortPosition pos) =>
            pos == PortPosition.Top || pos == PortPosition.Bottom;

        private static Vector GetDirectionVector(PortPosition pos) => pos switch
        {
            PortPosition.Right => new Vector(1, 0),
            PortPosition.Left => new Vector(-1, 0),
            PortPosition.Bottom => new Vector(0, 1),
            PortPosition.Top => new Vector(0, -1),
            _ => new Vector(1, 0)
        };

        private static PortPosition InferDirection(Point start, Point end, bool isStart)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;

            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                return isStart
                    ? (dx >= 0 ? PortPosition.Right : PortPosition.Left)
                    : (dx >= 0 ? PortPosition.Left : PortPosition.Right);
            }
            else
            {
                return isStart
                    ? (dy >= 0 ? PortPosition.Bottom : PortPosition.Top)
                    : (dy >= 0 ? PortPosition.Top : PortPosition.Bottom);
            }
        }

        #endregion
    }
}
