using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace FlowMy.Helpers
{
    public sealed class GridLengthAnimation : AnimationTimeline
    {
        public GridLength? From { get; set; }
        public GridLength? To { get; set; }
        public IEasingFunction? EasingFunction { get; set; }

        public override Type TargetPropertyType => typeof(GridLength);

        protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

        public override object GetCurrentValue(
            object defaultOriginValue,
            object defaultDestinationValue,
            AnimationClock animationClock)
        {
            if (animationClock.CurrentState == ClockState.Stopped)
                return To ?? (GridLength)defaultDestinationValue;

            if (!animationClock.CurrentProgress.HasValue)
                return From ?? (GridLength)defaultOriginValue;

            double t = animationClock.CurrentProgress.Value;
            if (EasingFunction != null)
                t = EasingFunction.Ease(t);

            var fromGl = From ?? (GridLength)defaultOriginValue;
            var toGl = To ?? (GridLength)defaultDestinationValue;

            if (fromGl.GridUnitType != GridUnitType.Pixel || toGl.GridUnitType != GridUnitType.Pixel)
                return t < 1.0 ? fromGl : toGl;

            double v = fromGl.Value + (toGl.Value - fromGl.Value) * t;
            return new GridLength(v);
        }
    }
}
