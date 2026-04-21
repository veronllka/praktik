using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace praktik.Services
{
    /// <summary>
    /// Monitors CPU and battery load, automatically disabling animations under high load.
    /// Respects user preferences stored in application settings.
    /// </summary>
    public sealed class AnimationService
    {
        private static readonly Lazy<AnimationService> _lazy =
            new Lazy<AnimationService>(() => new AnimationService());
        public static AnimationService Instance => _lazy.Value;

        private readonly PerformanceCounter _cpuCounter;
        private readonly Timer _monitorTimer;
        private bool _animationsEnabled = true;

        public event EventHandler AnimationStateChanged;

        public bool AnimationsEnabled
        {
            get => _animationsEnabled;
            private set
            {
                if (_animationsEnabled == value) return;
                _animationsEnabled = value;
                // Marshal to UI thread
                Application.Current?.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    (Action)(() => AnimationStateChanged?.Invoke(this, EventArgs.Empty)));
            }
        }

        // User-configurable preferences
        public bool UserEnabledAnimations { get; set; } = true;
        public bool AutoDisableOnHighCpu { get; set; } = true;
        public int CpuThreshold { get; set; } = 80;
        public bool AutoDisableOnLowBattery { get; set; } = true;
        public int BatteryThreshold { get; set; } = 20;

        // Runtime telemetry (read-only, updated by background timer)
        public float CurrentCpuUsage { get; private set; }
        public float CurrentBatteryLevel { get; private set; } = 100f;
        public bool IsOnBattery { get; private set; }

        private AnimationService()
        {
            LoadSettings();
            _cpuCounter = TryCreateCpuCounter();
            _monitorTimer = new Timer(_ => Monitor(), null, 1500, 3000);
        }

        private void LoadSettings()
        {
            try
            {
                UserEnabledAnimations = Properties.Settings.Default.AnimationsEnabled;
                AutoDisableOnHighCpu = Properties.Settings.Default.AutoDisableOnHighCpu;
                CpuThreshold = Properties.Settings.Default.CpuThreshold;
                AutoDisableOnLowBattery = Properties.Settings.Default.AutoDisableOnLowBattery;
                BatteryThreshold = Properties.Settings.Default.BatteryThreshold;
            }
            catch { /* Use defaults on failure */ }
        }

        public void SaveAndApply()
        {
            try
            {
                Properties.Settings.Default.AnimationsEnabled = UserEnabledAnimations;
                Properties.Settings.Default.AutoDisableOnHighCpu = AutoDisableOnHighCpu;
                Properties.Settings.Default.CpuThreshold = CpuThreshold;
                Properties.Settings.Default.AutoDisableOnLowBattery = AutoDisableOnLowBattery;
                Properties.Settings.Default.BatteryThreshold = BatteryThreshold;
                Properties.Settings.Default.Save();
            }
            catch { }

            EvaluateState();
        }

        private PerformanceCounter TryCreateCpuCounter()
        {
            try
            {
                var counter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                counter.NextValue();
                return counter;
            }
            catch
            {
                return null;
            }
        }

        private void Monitor()
        {
            try { CurrentCpuUsage = _cpuCounter?.NextValue() ?? 0f; }
            catch { }

            try
            {
                SystemPowerStatus powerStatus;
                if (GetSystemPowerStatus(out powerStatus))
                {
                    IsOnBattery = powerStatus.ACLineStatus == 0;
                    CurrentBatteryLevel = powerStatus.BatteryLifePercent <= 100
                        ? powerStatus.BatteryLifePercent
                        : 100f;
                }
            }
            catch { }

            EvaluateState();
        }

        public void EvaluateState()
        {
            if (!UserEnabledAnimations) { AnimationsEnabled = false; return; }
            if (AutoDisableOnHighCpu && CurrentCpuUsage > CpuThreshold) { AnimationsEnabled = false; return; }
            if (AutoDisableOnLowBattery && IsOnBattery && CurrentBatteryLevel < BatteryThreshold) { AnimationsEnabled = false; return; }
            AnimationsEnabled = true;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemPowerStatus
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }
    }
}
