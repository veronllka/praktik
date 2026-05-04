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
    /// Radial CPU usage gauge — the stroke sweeps to the current value.
    /// Tints green → amber → red as load increases.
    /// </summary>
    public class AnimatedCpuGauge : UserControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(AnimatedCpuGauge),
                new PropertyMetadata(0.0, OnValueChanged));

        public static readonly DependencyProperty MaxProperty =
            DependencyProperty.Register(nameof(Max), typeof(double), typeof(AnimatedCpuGauge),
                new PropertyMetadata(100.0));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Max
        {
            get => (double)GetValue(MaxProperty);
            set => SetValue(MaxProperty, value);
        }

        private readonly Ellipse _arc;
        private readonly double _circumference;

        public AnimatedCpuGauge()
        {
            Width = 56;
            Height = 56;

            var track = new Ellipse
            {
                Width = 52,
                Height = 52,
                Stroke = new SolidColorBrush(Color.FromArgb(0x38, 0xAD, 0xB6, 0xC3)),
                StrokeThickness = 4
            };

            _circumference = Math.PI * 52;

            _arc = new Ellipse
            {
                Width = 52,
                Height = 52,
                Stroke = new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59)),
                StrokeThickness = 4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeDashArray = new DoubleCollection { _circumference },
                StrokeDashOffset = _circumference,
                RenderTransform = new RotateTransform(-90, 26, 26)
            };

            var label = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            var binding = new System.Windows.Data.Binding(nameof(Value))
            {
                Source = this,
                StringFormat = "{0:F0}%"
            };
            label.SetBinding(TextBlock.TextProperty, binding);
            label.SetBinding(TextBlock.ForegroundProperty,
                new System.Windows.Data.Binding("Stroke") { Source = _arc });

            var grid = new Grid();
            grid.Children.Add(track);
            grid.Children.Add(_arc);
            grid.Children.Add(label);
            Content = grid;

            Loaded += (_, __) => ApplyValue(Value);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedCpuGauge)d).ApplyValue((double)e.NewValue);
        }

        private void ApplyValue(double value)
        {
            if (!IsLoaded) return;

            double ratio = Math.Max(0, Math.Min(1, value / Math.Max(1, Max)));
            double targetOffset = _circumference * (1 - ratio);

            Color color;
            if (ratio < 0.55)      color = Color.FromRgb(0x34, 0xC7, 0x59);
            else if (ratio < 0.80) color = Color.FromRgb(0xF5, 0xA5, 0x24);
            else                    color = Color.FromRgb(0xFF, 0x45, 0x3A);

            double mul = AnimationService.Instance.DurationMultiplier;

            if (mul <= 0)
            {
                _arc.StrokeDashOffset = targetOffset;
                _arc.Stroke = new SolidColorBrush(color);
                return;
            }

            var arcAnim = new DoubleAnimation(targetOffset,
                new Duration(TimeSpan.FromMilliseconds(400 * mul)))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            _arc.BeginAnimation(Shape.StrokeDashOffsetProperty, arcAnim);

            if (_arc.Stroke is SolidColorBrush scb && !scb.IsFrozen)
            {
                scb.BeginAnimation(SolidColorBrush.ColorProperty,
                    new ColorAnimation(color, new Duration(TimeSpan.FromMilliseconds(400 * mul))));
            }
            else
            {
                _arc.Stroke = new SolidColorBrush(color);
            }
        }
    }
}
