using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using praktik.Services;

namespace praktik.Controls
{
    public class AnimatedSpinner : UserControl
    {
        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(AnimatedSpinner),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(10, 132, 255)), OnAccentBrushChanged));

        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        private readonly RotateTransform _rotation;
        private readonly Ellipse _arcEllipse;
        private readonly DoubleAnimation _spinAnimation;

        public AnimatedSpinner()
        {
            var grid = new Grid();

            var trackEllipse = new Ellipse
            {
                Width = 20,
                Height = 20,
                Stroke = new SolidColorBrush(Color.FromArgb(40, 150, 150, 150)),
                StrokeThickness = 2.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _arcEllipse = new Ellipse
            {
                Width = 20,
                Height = 20,
                Stroke = AccentBrush,
                StrokeThickness = 2.5,
                StrokeDashArray = new DoubleCollection { 5.0, 20.0 },
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            _rotation = new RotateTransform();
            _arcEllipse.RenderTransform = _rotation;

            grid.Children.Add(trackEllipse);
            grid.Children.Add(_arcEllipse);
            Content = grid;

            _spinAnimation = new DoubleAnimation(0, 360, new Duration(TimeSpan.FromMilliseconds(900)))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };

            Loaded += OnLoaded;
            IsVisibleChanged += OnVisibleChanged;
            AnimationService.Instance.AnimationStateChanged += OnAnimationStateChanged;
        }

        private static void OnAccentBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var spinner = (AnimatedSpinner)d;
            spinner._arcEllipse?.SetCurrentValue(Shape.StrokeProperty, e.NewValue);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateSpinState();
        }

        private void OnAnimationStateChanged(object sender, System.EventArgs e)
        {
            UpdateSpinState();
        }

        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateSpinState();
        }

        private void UpdateSpinState()
        {
            if (!IsLoaded) return;
            var svc = AnimationService.Instance;
            bool shouldSpin = IsVisible && svc.AnimationsEnabled;

            if (!shouldSpin)
            {
                _rotation.BeginAnimation(RotateTransform.AngleProperty, null);
                return;
            }

            // Slow the spin slightly in reduced-motion mode
            double mul = svc.DurationMultiplier;
            var anim = new DoubleAnimation(0, 360,
                new Duration(TimeSpan.FromMilliseconds(900 * (mul <= 0 ? 1 : (2 - mul)))))
            { RepeatBehavior = RepeatBehavior.Forever };
            _rotation.BeginAnimation(RotateTransform.AngleProperty, anim);
        }
    }
}
