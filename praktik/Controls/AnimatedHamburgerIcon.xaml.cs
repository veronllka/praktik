using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using praktik.Services;

namespace praktik.Controls
{
    public partial class AnimatedHamburgerIcon : UserControl
    {
        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(AnimatedHamburgerIcon),
                new PropertyMetadata(false, OnIsOpenChanged));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public AnimatedHamburgerIcon()
        {
            InitializeComponent();
            Foreground = Brushes.White;
        }

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((AnimatedHamburgerIcon)d).AnimateToggle((bool)e.NewValue);
        }

        private void AnimateToggle(bool open)
        {
            bool animate = AnimationService.Instance.AnimationsEnabled;
            var dur = animate ? new Duration(System.TimeSpan.FromMilliseconds(220)) : new Duration(System.TimeSpan.Zero);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

            if (open)
            {
                // Top bar: shift down and rotate 45°
                Animate(TopBarRotation, "Angle", 0, 45, dur, ease);
                AnimateTranslate(TopBar, 0, 6, dur, ease);
                // Mid bar: scale to 0 (disappear)
                Animate(MidBarScale, "ScaleX", 1, 0, dur, ease);
                // Bottom bar: shift up and rotate -45°
                Animate(BottomBarRotation, "Angle", 0, -45, dur, ease);
                AnimateTranslate(BottomBar, 0, -6, dur, ease);
            }
            else
            {
                Animate(TopBarRotation, "Angle", 45, 0, dur, ease);
                AnimateTranslate(TopBar, 6, 0, dur, ease);
                Animate(MidBarScale, "ScaleX", 0, 1, dur, ease);
                Animate(BottomBarRotation, "Angle", -45, 0, dur, ease);
                AnimateTranslate(BottomBar, -6, 0, dur, ease);
            }
        }

        private static void Animate(Animatable target, string prop, double from, double to, Duration dur, IEasingFunction ease)
        {
            var anim = new DoubleAnimation(from, to, dur) { EasingFunction = ease };
            target.BeginAnimation(
                prop == "Angle" ? RotateTransform.AngleProperty : ScaleTransform.ScaleXProperty,
                anim);
        }

        private static void AnimateTranslate(FrameworkElement el, double from, double to, Duration dur, IEasingFunction ease)
        {
            var margin = el.Margin;
            var anim = new ThicknessAnimation(
                new Thickness(margin.Left, from, margin.Right, -from),
                new Thickness(margin.Left, to, margin.Right, -to),
                dur) { EasingFunction = ease };
            el.BeginAnimation(MarginProperty, anim);
        }
    }
}
