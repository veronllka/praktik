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
    /// Apple-style checkmark that draws itself stroke-by-stroke when IsChecked flips true.
    /// </summary>
    public class AnimatedCheckIcon : UserControl
    {
        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(AnimatedCheckIcon),
                new PropertyMetadata(false, OnIsCheckedChanged));

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(AnimatedCheckIcon),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59)), OnAccentBrushChanged));

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        private readonly Ellipse _ring;
        private readonly Path _check;
        private readonly ScaleTransform _scale;

        public AnimatedCheckIcon()
        {
            Width = 22;
            Height = 22;

            _ring = new Ellipse
            {
                Stroke = AccentBrush,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 60 },
                StrokeDashOffset = 60,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            var geometry = Geometry.Parse("M 5,11 L 10,16 L 17,7");
            _check = new Path
            {
                Data = geometry,
                Stroke = AccentBrush,
                StrokeThickness = 2.3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeDashArray = new DoubleCollection { 30 },
                StrokeDashOffset = 30
            };

            _scale = new ScaleTransform(1, 1);
            RenderTransformOrigin = new Point(0.5, 0.5);
            RenderTransform = _scale;

            var grid = new Grid();
            grid.Children.Add(_ring);
            grid.Children.Add(_check);
            Content = grid;

            Loaded += (_, __) => { if (IsChecked) PlayIn(); };
        }

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var icon = (AnimatedCheckIcon)d;
            if ((bool)e.NewValue) icon.PlayIn();
            else icon.PlayOut();
        }

        private static void OnAccentBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var icon = (AnimatedCheckIcon)d;
            icon._ring?.SetCurrentValue(Shape.StrokeProperty, e.NewValue);
            icon._check?.SetCurrentValue(Shape.StrokeProperty, e.NewValue);
        }

        private void PlayIn()
        {
            var svc = AnimationService.Instance;
            double mul = svc.DurationMultiplier;
            if (mul <= 0)
            {
                _ring.StrokeDashOffset = 0;
                _check.StrokeDashOffset = 0;
                _scale.ScaleX = _scale.ScaleY = 1;
                return;
            }

            var easeOut = new CubicEase { EasingMode = EasingMode.EaseOut };
            var back = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 };

            var ringAnim = new DoubleAnimation(60, 0, new Duration(TimeSpan.FromMilliseconds(340 * mul)))
            { EasingFunction = easeOut };
            _ring.BeginAnimation(Shape.StrokeDashOffsetProperty, ringAnim);

            var checkAnim = new DoubleAnimation(30, 0, new Duration(TimeSpan.FromMilliseconds(240 * mul)))
            { EasingFunction = easeOut, BeginTime = TimeSpan.FromMilliseconds(180 * mul) };
            _check.BeginAnimation(Shape.StrokeDashOffsetProperty, checkAnim);

            var pop = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(420 * mul))
            };
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(0.6, KeyTime.FromPercent(0)));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.12, KeyTime.FromPercent(0.6), back));
            pop.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0), easeOut));
            _scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
        }

        private void PlayOut()
        {
            _ring.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
            _check.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
            _ring.StrokeDashOffset = 60;
            _check.StrokeDashOffset = 30;
        }
    }
}
