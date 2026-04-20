using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using praktik.Services;

namespace praktik.Controls
{
    public partial class AnimatedSpinner : UserControl
    {
        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(AnimatedSpinner),
                new PropertyMetadata(new SolidColorBrush(System.Windows.Media.Color.FromRgb(10, 132, 255))));

        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        public AnimatedSpinner()
        {
            InitializeComponent();
            AnimationService.Instance.AnimationStateChanged += OnAnimationStateChanged;
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
            bool shouldSpin = IsVisible && AnimationService.Instance.AnimationsEnabled;

            if (shouldSpin)
            {
                var sb = (Storyboard)FindResource("SpinStoryboard") ??
                         TryFindResource("SpinStoryboard") as Storyboard;
                SpinStoryboard?.Begin(this, true);
            }
            else
            {
                SpinStoryboard?.Stop(this);
            }
        }
    }
}
