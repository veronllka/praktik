using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using praktik.Services;

namespace praktik.Controls
{
    public class AnimatedHamburgerIcon : UserControl
    {
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(AnimatedHamburgerIcon),
                new PropertyMetadata(false, OnIsOpenChanged));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        private readonly RotateTransform _topBarRotation;
        private readonly RotateTransform _bottomBarRotation;
        private readonly ScaleTransform _midBarScale;
        private readonly TranslateTransform _topBarTranslate;
        private readonly TranslateTransform _bottomBarTranslate;

        public AnimatedHamburgerIcon()
        {
            Foreground = Brushes.White;

            _topBarRotation = new RotateTransform();
            _bottomBarRotation = new RotateTransform();
            _midBarScale = new ScaleTransform(1, 1);
            _topBarTranslate = new TranslateTransform();
            _bottomBarTranslate = new TranslateTransform();

            var topBar = CreateBar(-6, CreateTransformGroup(_topBarRotation, _topBarTranslate));
            var midBar = CreateBar(0, _midBarScale);
            var bottomBar = CreateBar(6, CreateTransformGroup(_bottomBarRotation, _bottomBarTranslate));

            var grid = new Grid();
            grid.Children.Add(topBar);
            grid.Children.Add(midBar);
            grid.Children.Add(bottomBar);
            Content = grid;
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedHamburgerIcon)d).AnimateToggle((bool)e.NewValue);
        }

        private Rectangle CreateBar(double verticalOffset, Transform transform)
        {
            var bar = new Rectangle
            {
                Width = 18,
                Height = 2,
                VerticalAlignment = VerticalAlignment.Center,
                RadiusX = 1,
                RadiusY = 1,
                Margin = new Thickness(0, verticalOffset, 0, 0),
                RenderTransform = transform,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            BindingOperations.SetBinding(bar, Shape.FillProperty, new Binding(nameof(Foreground))
            {
                Source = this
            });

            return bar;
        }

        private static TransformGroup CreateTransformGroup(Transform first, Transform second)
        {
            var group = new TransformGroup();
            group.Children.Add(first);
            group.Children.Add(second);
            return group;
        }

        private void AnimateToggle(bool open)
        {
            bool animate = AnimationService.Instance.AnimationsEnabled;
            var dur = animate ? new Duration(System.TimeSpan.FromMilliseconds(220)) : new Duration(System.TimeSpan.Zero);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            if (open)
            {
                Animate(_topBarRotation, RotateTransform.AngleProperty, 0, 45, dur, ease);
                Animate(_topBarTranslate, TranslateTransform.YProperty, 0, 6, dur, ease);
                Animate(_midBarScale, ScaleTransform.ScaleXProperty, 1, 0, dur, ease);
                Animate(_bottomBarRotation, RotateTransform.AngleProperty, 0, -45, dur, ease);
                Animate(_bottomBarTranslate, TranslateTransform.YProperty, 0, -6, dur, ease);
            }
            else
            {
                Animate(_topBarRotation, RotateTransform.AngleProperty, 45, 0, dur, ease);
                Animate(_topBarTranslate, TranslateTransform.YProperty, 6, 0, dur, ease);
                Animate(_midBarScale, ScaleTransform.ScaleXProperty, 0, 1, dur, ease);
                Animate(_bottomBarRotation, RotateTransform.AngleProperty, -45, 0, dur, ease);
                Animate(_bottomBarTranslate, TranslateTransform.YProperty, -6, 0, dur, ease);
            }
        }

        private static void Animate(Animatable target, DependencyProperty property, double from, double to, Duration dur, IEasingFunction ease)
        {
            var anim = new DoubleAnimation(from, to, dur) { EasingFunction = ease };
            target.BeginAnimation(property, anim);
        }
    }
}
