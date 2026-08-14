using System.Windows;
using System.Windows.Media;
using FlowMy.Models;

namespace FlowMy.Services.Geometry
{
    public interface IPathGeometryGenerator
    {
        PathGeometry Generate(
            Point start,
            Point end,
            PortPosition? startDir,
            PortPosition? endDir);
    }
}

