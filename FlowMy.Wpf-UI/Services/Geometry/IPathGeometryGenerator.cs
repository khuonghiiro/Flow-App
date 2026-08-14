// =========================================================================================
// AI NOTICE: Refer to README.md and FlowMy.Docs/AI_CODING_STANDARDS.md before editing code.
// =========================================================================================
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

