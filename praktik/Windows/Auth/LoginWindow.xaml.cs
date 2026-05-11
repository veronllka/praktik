using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MahApps.Metro.IconPacks;
using praktik.Models;
using praktik.Models.Patterns;
using praktik.Models.Patterns.Factories;

namespace praktik
{
    /// <summary>
    /// Окно аутентификации пользователя.
    /// Обрабатывает ввод логина/пароля и переход к регистрации.
    /// </summary>
    public partial class LoginWindow : Window
    {
        public static User CurrentUser { get; set; }
        private readonly WorkPlannerFacade facade = new WorkPlannerFacade();
        private bool isPasswordVisible = false;
        private string currentPassword = string.Empty;

        public LoginWindow()
        {
            InitializeComponent();
            SourceInitialized += LoginWindow_SourceInitialized;
            Loaded += LoginWindow_Loaded;
            UpdatePasswordPlaceholder();
        }

        private void LoginWindow_SourceInitialized(object sender, EventArgs e)
        {
            EnableGlassBackdrop();
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            FitWindowToWorkArea();
        }

        private void FitWindowToWorkArea()
        {
            const double WindowMargin = 24;
            var workArea = SystemParameters.WorkArea;
            var maxHeight = Math.Max(420, workArea.Height - WindowMargin);

            if (MinHeight > maxHeight)
            {
                MinHeight = maxHeight;
            }

            MaxHeight = maxHeight;
            if (Height > maxHeight)
            {
                Height = maxHeight;
            }

            var windowHeight = double.IsNaN(Height) ? ActualHeight : Height;
            var windowWidth = double.IsNaN(Width) ? ActualWidth : Width;
            Top = workArea.Top + Math.Max(WindowMargin / 2, (workArea.Height - windowHeight) / 2);
            Left = workArea.Left + Math.Max(WindowMargin / 2, (workArea.Width - windowWidth) / 2);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
                e.Handled = true;
                return;
            }

            StartWindowDrag(e);
        }

        private void LoginPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsInteractiveSource(e.OriginalSource))
            {
                StartWindowDrag(e);
            }
        }

        private void StartWindowDrag(MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                return;
            }

            e.Handled = true;
        }

        private bool IsInteractiveSource(object source)
        {
            var current = source as DependencyObject;
            while (current != null && current != LoginPanel)
            {
                if (current is ButtonBase || current is TextBox || current is PasswordBox)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void EnableGlassBackdrop()
        {
            try
            {
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.EnableBlurBehind,
                    AccentFlags = 0,
                    GradientColor = 0
                };

                var accentSize = Marshal.SizeOf(accent);
                var accentPtr = Marshal.AllocHGlobal(accentSize);

                try
                {
                    Marshal.StructureToPtr(accent, accentPtr, false);
                    var data = new WindowCompositionAttributeData
                    {
                        Attribute = WindowCompositionAttribute.AccentPolicy,
                        Data = accentPtr,
                        SizeOfData = accentSize
                    };

                    SetWindowCompositionAttribute(new WindowInteropHelper(this).Handle, ref data);
                }
                finally
                {
                    Marshal.FreeHGlobal(accentPtr);
                }
            }
            catch
            {
                // Acrylic is only a visual enhancement; the XAML glass tint is the fallback.
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleWindowState()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            var username = txtUsername.Text.Trim();
            var password = currentPassword.Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ClearMessages();
                txtError.Text = "Введите логин или пароль";
                return;
            }

            ClearMessages();
            TryLogin(username, password);
        }

        private void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            ShowRegisterWindow();
        }

        private void RegisterLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ShowRegisterWindow();
        }

        private void InputField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnLogin_Click(sender, e);
            }
        }

        private void ClearMessages()
        {
            txtSuccess.Text = "";
            txtError.Text = "";
        }

        private void ShowRegisterWindow()
        {
            var registerWindow = new RegisterWindow();
            if (registerWindow.ShowDialog() == true)
            {
                ClearMessages();
                txtSuccess.Text = "Пользователь успешно зарегистрирован";
            }
        }

        /// <param name="username">Имя пользователя.</param>
        /// <param name="password">Пароль.</param>
        private void TryLogin(string username, string password)
        {
            User user;
            try
            {
                user = facade.GetUser(username, password);
            }
            catch (Exception)
            {
                txtError.Text = "Не удалось подключиться к базе данных. Проверьте подключение и повторите вход.";
                return;
            }

            if (user == null)
            {
                txtError.Text = "Неверный логин или пароль";
                return;
            }

            OpenMainWindow(user);
        }

        private void OpenMainWindow(User user)
        {
            CurrentUser = user;
            AppThemeManager.ApplyTheme(user);
            try
            {
                var mainWindow = RoleWindowFactory.CreateWindow(user.Role);
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть рабочее окно для роли пользователя.\n\n{ex.Message}", "Авторизация", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            isPasswordVisible = !isPasswordVisible;
            UpdatePasswordVisibility();
            AnimatePasswordEye();
        }

        private void BtnTogglePassword_MouseEnter(object sender, MouseEventArgs e)
        {
            AnimatePasswordEyeHover(1.08, 0.96);
        }

        private void BtnTogglePassword_MouseLeave(object sender, MouseEventArgs e)
        {
            AnimatePasswordEyeHover(1, 0.88);
        }

        private void AnimatePasswordEye()
        {
            if (IconPasswordEye == null || EyeScale == null || EyeRotate == null)
            {
                return;
            }

            var targetScale = btnTogglePassword.IsMouseOver ? 1.08 : 1;
            var targetOpacity = btnTogglePassword.IsMouseOver ? 0.96 : 0.88;
            var scaleEase = new BackEase
            {
                EasingMode = EasingMode.EaseOut,
                Amplitude = 0.35
            };
            var fadeEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(180);

            EyeScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
            {
                From = 0.82,
                To = targetScale,
                Duration = duration,
                EasingFunction = scaleEase
            });
            EyeScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
            {
                From = 0.82,
                To = targetScale,
                Duration = duration,
                EasingFunction = scaleEase
            });
            EyeRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation
            {
                From = isPasswordVisible ? -12 : 12,
                To = 0,
                Duration = duration,
                EasingFunction = fadeEase
            });
            IconPasswordEye.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                From = 0.55,
                To = targetOpacity,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = fadeEase
            });
        }

        private void AnimatePasswordEyeHover(double scale, double opacity)
        {
            if (IconPasswordEye == null || EyeScale == null)
            {
                return;
            }

            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(120);

            EyeScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation
            {
                To = scale,
                Duration = duration,
                EasingFunction = ease
            });
            EyeScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation
            {
                To = scale,
                Duration = duration,
                EasingFunction = ease
            });
            IconPasswordEye.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                To = opacity,
                Duration = duration,
                EasingFunction = ease
            });
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!isPasswordVisible)
            {
                currentPassword = txtPassword.Password;
            }

            UpdatePasswordPlaceholder();
        }

        private void TxtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isPasswordVisible)
            {
                currentPassword = txtPasswordVisible.Text;
            }

            UpdatePasswordPlaceholder();
        }

        /// <summary>
        /// Переключает видимость пароля (текст/звездочки) в интерфейсе.
        /// </summary>
        private void UpdatePasswordVisibility()
        {
            if (isPasswordVisible)
            {
                txtPasswordVisible.Text = currentPassword;
                txtPasswordVisible.Visibility = Visibility.Visible;
                txtPassword.Visibility = Visibility.Collapsed;
                IconPasswordEye.Kind = PackIconMaterialKind.Eye;
                btnTogglePassword.ToolTip = "Скрыть пароль";
            }
            else
            {
                txtPassword.Password = currentPassword;
                txtPassword.Visibility = Visibility.Visible;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                IconPasswordEye.Kind = PackIconMaterialKind.EyeOff;
                btnTogglePassword.ToolTip = "Показать пароль";
            }

            UpdatePasswordPlaceholder();
        }

        private void UpdatePasswordPlaceholder()
        {
            if (PasswordPlaceholder == null)
            {
                return;
            }

            PasswordPlaceholder.Visibility = string.IsNullOrEmpty(currentPassword)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private enum AccentState
        {
            Disabled = 0,
            EnableGradient = 1,
            EnableTransparentGradient = 2,
            EnableBlurBehind = 3,
            EnableAcrylicBlurBehind = 4
        }

        private enum WindowCompositionAttribute
        {
            AccentPolicy = 19
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(
            IntPtr hwnd,
            ref WindowCompositionAttributeData data);
    }
}
