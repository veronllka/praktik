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
    /// Lock icon whose shackle lifts open when IsOpen=true.
    /// </summary>
    public class AnimatedLockIcon : UserControl
    {
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(AnimatedLockIcon),
                new PropertyMetadata(false, OnIsOpenChanged));

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(AnimatedLockIcon),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0xAD, 0xB6, 0xC3)), OnAccentBrushChanged));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        private readonly Path _shackle;
        private readonly TranslateTransform _shackleTranslate;
        private readonly RotateTransform _shackleRotate;
        private readonly Rectangle _body;

        public AnimatedLockIcon()
        {
            Width = 20;
            Height = 24;

            _shackle = new Path
            {
                Data = Geometry.Parse("M 4,10 L 4,6 A 6,6 0 0 1 16,6 L 16,10"),
                Stroke = AccentBrush,
                StrokeThickness = 2,
                Fill = Brushes.Transparent,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _shackleTranslate = new TranslateTransform();
            _shackleRotate = new RotateTransform(0, 10, 10);
            var group = new TransformGroup();
            group.Children.Add(_shackleRotate);
            group.Children.Add(_shackleTranslate);
            _shackle.RenderTransform = group;

            _body = new Rectangle
            {
                Width = 16,
                Height = 12,
                RadiusX = 2.5,
                RadiusY = 2.5,
                Fill = AccentBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            var grid = new Grid();
            grid.Children.Add(_shackle);
            grid.Children.Add(_body);
            Content = grid;
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedLockIcon)d).Animate((bool)e.NewValue);
        }

        private static void OnAccentBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var icon = (AnimatedLockIcon)d;
            icon._shackle?.SetCurrentValue(Shape.StrokeProperty, e.NewValue);
            icon._body?.SetCurrentValue(Shape.FillProperty, e.NewValue);
        }

        private void Animate(bool open)
        {
            double mul = AnimationService.Instance.DurationMultiplier;
            var dur = new Duration(TimeSpan.FromMilliseconds((mul <= 0 ? 1 : 260 * mul)));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            _shackleTranslate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(open ? -3 : 0, dur) { EasingFunction = ease });
            _shackleRotate.BeginAnimation(RotateTransform.AngleProperty,
                new DoubleAnimation(open ? -22 : 0, dur) { EasingFunction = ease });
        }
    }
}
