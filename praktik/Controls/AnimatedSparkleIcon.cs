using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using praktik.Services;

namespace praktik.Controls
{
    /// <summary>
    /// Decorative sparkle — three stars twinkle on a loop.
    /// Used to mark accent areas (e.g. Auto mode).
    /// </summary>
    public class AnimatedSparkleIcon : UserControl
    {
        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(AnimatedSparkleIcon),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x8D, 0x6E, 0x63)), OnAccentBrushChanged));

        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        private readonly Path _big;
        private readonly Path _small;
        private readonly Path _tiny;

        public AnimatedSparkleIcon()
        {
            Width = 22;
            Height = 22;

            _big = MakeStar(11, 11, 7, 0);
            _small = MakeStar(5, 5, 3.5, 0);
            _tiny = MakeStar(17, 16, 2.5, 0);

            var grid = new Grid();
            grid.Children.Add(_big);
            grid.Children.Add(_small);
            grid.Children.Add(_tiny);
            Content = grid;

            Loaded += (_, __) => StartAnim();
            AnimationService.Instance.AnimationStateChanged += (_, __) =>
                Dispatcher.BeginInvoke((Action)StartAnim);
        }

        private static void OnAccentBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var icon = (AnimatedSparkleIcon)d;
            if (icon._big != null) icon._big.Fill = (Brush)e.NewValue;
            if (icon._small != null) icon._small.Fill = (Brush)e.NewValue;
            if (icon._tiny != null) icon._tiny.Fill = (Brush)e.NewValue;
        }

        private Path MakeStar(double cx, double cy, double size, double startAngle)
        {
            // Four-point diamond sparkle (Apple-style)
            var geom = new StreamGeometry();
            using (var context = geom.Open())
            {
                context.BeginFigure(new Point(cx, cy - size), true, true);
                context.BezierTo(
                    new Point(cx + size * 0.15, cy - size * 0.25),
                    new Point(cx + size * 0.25, cy - size * 0.15),
                    new Point(cx + size, cy),
                    true,
                    true);
                context.BezierTo(
                    new Point(cx + size * 0.25, cy + size * 0.15),
                    new Point(cx + size * 0.15, cy + size * 0.25),
                    new Point(cx, cy + size),
                    true,
                    true);
                context.BezierTo(
                    new Point(cx - size * 0.15, cy + size * 0.25),
                    new Point(cx - size * 0.25, cy + size * 0.15),
                    new Point(cx - size, cy),
                    true,
                    true);
                context.BezierTo(
                    new Point(cx - size * 0.25, cy - size * 0.15),
                    new Point(cx - size * 0.15, cy - size * 0.25),
                    new Point(cx, cy - size),
                    true,
                    true);
            }
            geom.Freeze();

            var p = new Path
            {
                Data = geom,
                Fill = AccentBrush,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var transform = new ScaleTransform(0.9, 0.9, cx, cy);
            p.RenderTransform = transform;
            p.Tag = transform;
            return p;
        }

        private void StartAnim()
        {
            if (!IsLoaded) return;
            double mul = AnimationService.Instance.DurationMultiplier;
            Animate(_big, mul, 0);
            Animate(_small, mul, 350);
            Animate(_tiny, mul, 700);
        }

        private void Animate(Path p, double mul, double delayMs)
        {
            var transform = p.Tag as ScaleTransform;
            if (transform == null) return;

            if (mul <= 0)
            {
                p.BeginAnimation(OpacityProperty, null);
                transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                p.Opacity = 0.9;
                transform.ScaleX = transform.ScaleY = 1;
                return;
            }

            var op = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(2200 * (2 - mul))),
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(delayMs)
            };
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            op.KeyFrames.Add(new EasingDoubleKeyFrame(0.35, KeyTime.FromPercent(0), ease));
            op.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0.5), ease));
            op.KeyFrames.Add(new EasingDoubleKeyFrame(0.35, KeyTime.FromPercent(1.0), ease));
            p.BeginAnimation(OpacityProperty, op);

            var scale = new DoubleAnimationUsingKeyFrames
            {
                Duration = op.Duration,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(delayMs)
            };
            scale.KeyFrames.Add(new EasingDoubleKeyFrame(0.7, KeyTime.FromPercent(0), ease));
            scale.KeyFrames.Add(new EasingDoubleKeyFrame(1.15, KeyTime.FromPercent(0.5), ease));
            scale.KeyFrames.Add(new EasingDoubleKeyFrame(0.7, KeyTime.FromPercent(1.0), ease));
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        }
    }
}
