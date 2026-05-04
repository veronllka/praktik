using System;
using System.Windows;
using System.Windows.Input;
using praktik.Services;

namespace praktik
{
    public partial class DispatcherWindow : Window
    {
        public DispatcherWindow()
        {
            InitializeComponent();
            SourceInitialized += (_, __) =>
            {
                GlassBackdropService.Apply(this);
                WindowCornerService.Apply(this);
            };
            ApplyCurrentRole("Диспетчер");
        }

        private void ApplyCurrentRole(string fallbackRole)
        {
            var role = LoginWindow.CurrentUser?.Role;
            if (string.IsNullOrWhiteSpace(role))
            {
                role = fallbackRole;
            }

            Title = $"Планировщик работ бригад - {role}";
            MainView.ApplyRole(role);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleWindowState();
                e.Handled = true;
                return;
            }

            if (e.ButtonState != MouseButtonState.Pressed)
            {
                return;
            }

            RestoreWindowUnderCursor(e);

            try
            {
                DragMove();
                e.Handled = true;
            }
            catch (InvalidOperationException)
            {
                // DragMove can throw if the mouse button was released during routing.
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            SystemCommands.MinimizeWindow(this);
            e.Handled = true;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
            e.Handled = true;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SystemCommands.CloseWindow(this);
        }

        private void ToggleWindowState()
        {
            if (WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(this);
                return;
            }

            SystemCommands.MaximizeWindow(this);
        }

        private void RestoreWindowUnderCursor(MouseButtonEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
            {
                return;
            }

            var mousePosition = e.GetPosition(this);
            var screenPosition = PointToScreen(mousePosition);
            var restoreWidth = RestoreBounds.Width > 0 ? RestoreBounds.Width : Width;
            var restoreHeight = RestoreBounds.Height > 0 ? RestoreBounds.Height : Height;
            var horizontalRatio = ActualWidth > 0 ? mousePosition.X / ActualWidth : 0.5;

            SystemCommands.RestoreWindow(this);
            Width = restoreWidth;
            Height = restoreHeight;
            Left = screenPosition.X - restoreWidth * horizontalRatio;
            Top = Math.Max(0, screenPosition.Y - 18);
        }
    }
}
