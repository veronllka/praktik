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
    /// Refresh / loading icon that rotates while IsBusy is true.
    /// </summary>
    public class AnimatedRefreshIcon : UserControl
    {
        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(AnimatedRefreshIcon),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)), OnAccentBrushChanged));

        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.Register(nameof(IsBusy), typeof(bool), typeof(AnimatedRefreshIcon),
                new PropertyMetadata(false, OnIsBusyChanged));

        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public bool IsBusy
        {
            get => (bool)GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        private readonly Path _icon;
        private readonly RotateTransform _rotate;

        public AnimatedRefreshIcon()
        {
            Width = 22;
            Height = 22;

            _icon = new Path
            {
                Data = Geometry.Parse(
                    "M 11,3 A 8,8 0 1 1 3.5,9 M 11,3 L 8,3 M 11,3 L 11,6"),
                Stroke = AccentBrush,
                StrokeThickness = 2.1,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Fill = Brushes.Transparent,
                Stretch = Stretch.None
            };

            _rotate = new RotateTransform(0, 11, 11);
            _icon.RenderTransform = _rotate;

            Content = _icon;

            AnimationService.Instance.AnimationStateChanged += (_, __) =>
                Dispatcher.BeginInvoke((Action)UpdateSpin);
            Loaded += (_, __) => UpdateSpin();
        }

        private static void OnAccentBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedRefreshIcon)d)._icon?.SetCurrentValue(Shape.StrokeProperty, e.NewValue);
        }

        private static void OnIsBusyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedRefreshIcon)d).UpdateSpin();
        }

        private void UpdateSpin()
        {
            if (!IsLoaded) return;
            double mul = AnimationService.Instance.DurationMultiplier;
            if (IsBusy && mul > 0)
            {
                var anim = new DoubleAnimation(0, 360,
                    new Duration(TimeSpan.FromMilliseconds(800 * (2 - mul))))
                {
                    RepeatBehavior = RepeatBehavior.Forever
                };
                _rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
            }
            else
            {
                _rotate.BeginAnimation(RotateTransform.AngleProperty, null);
                _rotate.Angle = 0;
            }
        }
    }
}
