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
    /// Apple-style notification bell. Auto-shakes on Pulse() and when hovered.
    /// </summary>
    public class AnimatedBellIcon : UserControl
    {
        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(AnimatedBellIcon),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xF5, 0xA5, 0x24)), OnAccentBrushChanged));

        public static readonly DependencyProperty HasUnreadProperty =
            DependencyProperty.Register(nameof(HasUnread), typeof(bool), typeof(AnimatedBellIcon),
                new PropertyMetadata(false, OnHasUnreadChanged));

        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public bool HasUnread
        {
            get => (bool)GetValue(HasUnreadProperty);
            set => SetValue(HasUnreadProperty, value);
        }

        private readonly Path _bell;
        private readonly Ellipse _badge;
        private readonly RotateTransform _rotate;

        public AnimatedBellIcon()
        {
            Width = 22;
            Height = 22;

            _bell = new Path
            {
                Data = Geometry.Parse(
                    "M 11,2 C 8.5,2 6.5,4 6.5,6.5 L 6.5,10.5 L 4.5,14.5 L 17.5,14.5 L 15.5,10.5 L 15.5,6.5 " +
                    "C 15.5,4 13.5,2 11,2 Z M 9,16 C 9,17.1 9.9,18 11,18 C 12.1,18 13,17.1 13,16 L 9,16 Z"),
                Fill = AccentBrush,
                Stretch = Stretch.Uniform
            };
            _rotate = new RotateTransform(0, 11, 3);
            _bell.RenderTransform = _rotate;

            _badge = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 2, 0),
                Visibility = Visibility.Collapsed
            };

            var grid = new Grid();
            grid.Children.Add(_bell);
            grid.Children.Add(_badge);
            Content = grid;

            MouseEnter += (_, __) => Pulse();
        }

        private static void OnAccentBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedBellIcon)d)._bell?.SetCurrentValue(Shape.FillProperty, e.NewValue);
        }

        private static void OnHasUnreadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var icon = (AnimatedBellIcon)d;
            icon._badge.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            if ((bool)e.NewValue) icon.Pulse();
        }

        /// <summary>Play a quick shake animation (respects performance mode).</summary>
        public void Pulse()
        {
            double mul = AnimationService.Instance.DurationMultiplier;
            if (mul <= 0) return;

            var shake = new DoubleAnimationUsingKeyFrames
            {
                Duration = new Duration(TimeSpan.FromMilliseconds(520 * mul))
            };
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0.0), ease));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(-14, KeyTime.FromPercent(0.15), ease));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(12, KeyTime.FromPercent(0.35), ease));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(-8, KeyTime.FromPercent(0.55), ease));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(5, KeyTime.FromPercent(0.75), ease));
            shake.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1.0), ease));
            _rotate.BeginAnimation(RotateTransform.AngleProperty, shake);
        }
    }
}
