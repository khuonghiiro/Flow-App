using System.Windows;
using System.Windows.Media;
using FlowMy.Models;

namespace FlowMy.Services.Geometry
{
    public sealed class StraightGeometryGenerator : IPathGeometryGenerator
    {
        public PathGeometry Generate(Point start, Point end, PortPosition? startDir, PortPosition? endDir)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = start };
            figure.Segments.Add(new LineSegment(end, true));
            geometry.Figures.Add(figure);
            return geometry;
        }
    }
}

