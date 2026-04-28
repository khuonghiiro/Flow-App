using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FlowMy.Services.Workflow.NodeExecutors
{
    internal static class FrameExtractionCalculator
    {
        public static List<int> CalculateFrameIndicesPerSecond(double fps, int framesPerSecond)
        {
            if (framesPerSecond <= 0 || fps <= 0) return new List<int>();
            if (framesPerSecond >= (int)fps)
                return Enumerable.Range(0, (int)fps).ToList();

            var result = new List<int>();
            var interval = fps / framesPerSecond;
            var offset = interval / 2.0;

            for (var i = 0; i < framesPerSecond; i++)
            {
                var frameIndex = (int)Math.Floor(offset + (i * interval));
                frameIndex = Math.Clamp(frameIndex, 0, (int)fps - 1);
                result.Add(frameIndex);
            }

            return result;
        }

        public static List<double> CalculateAllExtractTimestamps(double totalDurationSec, double sourceFps, int framesPerSecond)
        {
            var timestamps = new List<double>();
            var frameIndices = CalculateFrameIndicesPerSecond(sourceFps, framesPerSecond);
            var totalSeconds = (int)Math.Floor(totalDurationSec);

            for (var sec = 0; sec < totalSeconds; sec++)
            {
                foreach (var frameIdx in frameIndices)
                {
                    var ts = sec + (frameIdx / sourceFps);
                    if (ts < totalDurationSec)
                        timestamps.Add(ts);
                }
            }

            return timestamps;
        }

        public static string BuildSelectFilterExpression(double totalDurationSec, double sourceFps, int framesPerSecond)
        {
            var timestamps = CalculateAllExtractTimestamps(totalDurationSec, sourceFps, framesPerSecond);
            if (timestamps.Count == 0) return "select='0'";

            const double epsilon = 0.001;
            var conditions = timestamps.Select(ts =>
            {
                var low = ts - epsilon;
                var high = ts + epsilon;
                return $"between(t,{low.ToString("0.######", CultureInfo.InvariantCulture)},{high.ToString("0.######", CultureInfo.InvariantCulture)})";
            });

            return $"select='{string.Join("+", conditions)}'";
        }

        public static int EstimateFrameCount(double totalDurationSec, double sourceFps, int framesPerSecond, bool extractAll)
        {
            if (extractAll) return (int)Math.Floor(totalDurationSec * sourceFps);
            return CalculateAllExtractTimestamps(totalDurationSec, sourceFps, framesPerSecond).Count;
        }
    }
}
