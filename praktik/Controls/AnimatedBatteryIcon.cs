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
    /// Battery icon that fills to a live percentage, tints by level,
    /// and shows a gentle pulse when charging.
    /// </summary>
    public class AnimatedBatteryIcon : UserControl
    {
        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(nameof(Level), typeof(double), typeof(AnimatedBatteryIcon),
                new PropertyMetadata(1.0, OnLevelChanged));

        public static readonly DependencyProperty IsChargingProperty =
            DependencyProperty.Register(nameof(IsCharging), typeof(bool), typeof(AnimatedBatteryIcon),
                new PropertyMetadata(false, OnChargingChanged));

        public static readonly DependencyProperty LowThresholdProperty =
            DependencyProperty.Register(nameof(LowThreshold), typeof(double), typeof(AnimatedBatteryIcon),
                new PropertyMetadata(0.2));

        /// <summary>Battery level in the 0.0-1.0 range.</summary>
        public double Level
        {
            get => (double)GetValue(LevelProperty);
            set => SetValue(LevelProperty, value);
        }

        public bool IsCharging
        {
            get => (bool)GetValue(IsChargingProperty);
            set => SetValue(IsChargingProperty, value);
        }

        public double LowThreshold
        {
            get => (double)GetValue(LowThresholdProperty);
            set => SetValue(LowThresholdProperty, value);
        }

        private readonly Rectangle _fill;
        private readonly Path _bolt;
        private readonly double _fillMaxWidth = 24;

        public AnimatedBatteryIcon()
        {
            Width = 34;
            Height = 18;

            var body = new Rectangle
            {
                Width = 28,
                Height = 16,
                RadiusX = 3,
                RadiusY = 3,
                Stroke = new SolidColorBrush(Color.FromArgb(0xCC, 0xAD, 0xB6, 0xC3)),
                StrokeThickness = 1.5,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var cap = new Rectangle
            {
                Width = 3,
                Height = 8,
                RadiusX = 1,
                RadiusY = 1,
                Fill = new SolidColorBrush(Color.FromArgb(0xCC, 0xAD, 0xB6, 0xC3)),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(28, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            _fill = new Rectangle
            {
                Width = _fillMaxWidth,
                Height = 12,
                RadiusX = 1.5,
                RadiusY = 1.5,
                Fill = new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            };

            _bolt = new Path
            {
                Data = Geometry.Parse("M 13,3 L 8,10 L 12,10 L 10,15 L 16,7 L 12,7 Z"),
                Fill = Brushes.White,
                Stretch = Stretch.Uniform,
                Width = 8,
                Height = 12,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                Visibility = Visibility.Collapsed,
                Opacity = 0.9
            };

            var grid = new Grid();
            grid.Children.Add(body);
            grid.Children.Add(cap);
            grid.Children.Add(_fill);
            grid.Children.Add(_bolt);
            Content = grid;

            UpdateVisuals(Level);
            AnimationService.Instance.AnimationStateChanged += (_, __) =>
                Dispatcher.BeginInvoke((Action)(() => UpdateChargingAnimation()));
        }

        private static void OnLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedBatteryIcon)d).UpdateVisuals((double)e.NewValue);
        }

        private static void OnChargingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var icon = (AnimatedBatteryIcon)d;
            icon._bolt.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            icon.UpdateChargingAnimation();
        }

        private void UpdateVisuals(double level)
        {
            level = Math.Max(0, Math.Min(1, level));

            Color color;
            if (level < LowThreshold)      color = Color.FromRgb(0xFF, 0x45, 0x3A);
            else if (level < LowThreshold * 2) color = Color.FromRgb(0xF5, 0xA5, 0x24);
            else                            color = Color.FromRgb(0x34, 0xC7, 0x59);

            double targetWidth = Math.Max(2, _fillMaxWidth * level);
            double mul = AnimationService.Instance.DurationMultiplier;

            if (mul <= 0 || !IsLoaded)
            {
                _fill.Width = targetWidth;
                _fill.Fill = new SolidColorBrush(color);
                return;
            }

            var widthAnim = new DoubleAnimation(targetWidth, new Duration(TimeSpan.FromMilliseconds(320 * mul)))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            _fill.BeginAnimation(WidthProperty, widthAnim);

            if (_fill.Fill is SolidColorBrush scb && !scb.IsFrozen)
            {
                var colorAnim = new ColorAnimation(color, new Duration(TimeSpan.FromMilliseconds(320 * mul)));
                scb.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
            }
            else
            {
                _fill.Fill = new SolidColorBrush(color);
            }
        }

        private void UpdateChargingAnimation()
        {
            double mul = AnimationService.Instance.DurationMultiplier;
            if (IsCharging && mul > 0)
            {
                var pulse = new DoubleAnimation(0.55, 1.0,
                    new Duration(TimeSpan.FromMilliseconds(1100 * mul)))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                _fill.BeginAnimation(OpacityProperty, pulse);
            }
            else
            {
                _fill.BeginAnimation(OpacityProperty, null);
                _fill.Opacity = 1.0;
            }
        }
    }
}
