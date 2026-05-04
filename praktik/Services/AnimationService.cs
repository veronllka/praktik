using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace praktik.Services
{
    /// <summary>
    /// Performance-aware animation coordinator.
    /// Monitors CPU, RAM and battery, and picks one of four power modes:
    /// Full → Balanced → Reduced → Off. UI controls subscribe to the state
    /// change event and throttle their own animations accordingly.
    /// </summary>
    public sealed class AnimationService
    {
        private static readonly Lazy<AnimationService> _lazy =
            new Lazy<AnimationService>(() => new AnimationService());
        public static AnimationService Instance => _lazy.Value;

        public enum PerformanceMode
        {
            /// <summary>Auto — decides based on live CPU/RAM/battery readings.</summary>
            Auto = 0,
            /// <summary>Always on — ignore load, keep animations full.</summary>
            AlwaysOn = 1,
            /// <summary>Reduced — keep essential animations only, shorter durations.</summary>
            Reduced = 2,
            /// <summary>Off — disable all optional animations.</summary>
            Off = 3
        }

        public enum AnimationLevel
        {
            Full = 0,
            Reduced = 1,
            Off = 2
        }

        private readonly PerformanceCounter _cpuCounter;
        private readonly PerformanceCounter _ramCounter;
        private readonly Timer _monitorTimer;
        private AnimationLevel _currentLevel = AnimationLevel.Full;
        private readonly object _evalLock = new object();

        public event EventHandler AnimationStateChanged;

        // ─── Derived state (subscribers read these) ─────────────────────────
        /// <summary>Convenience: true iff current level == Full.</summary>
        public bool AnimationsEnabled => _currentLevel != AnimationLevel.Off;

        /// <summary>Animations are running but at a reduced duration/complexity.</summary>
        public bool IsReducedMotion => _currentLevel == AnimationLevel.Reduced;

        /// <summary>Current level after evaluating user prefs + telemetry.</summary>
        public AnimationLevel CurrentLevel => _currentLevel;

        /// <summary>Duration multiplier UI controls apply to their animation lengths.</summary>
        public double DurationMultiplier
        {
            get
            {
                switch (_currentLevel)
                {
                    case AnimationLevel.Full: return 1.0;
                    case AnimationLevel.Reduced: return 0.55;
                    default: return 0.0;
                }
            }
        }

        // ─── User preferences (bound to Settings.Default) ───────────────────
        public bool UserEnabledAnimations { get; set; } = true;
        public PerformanceMode Mode { get; set; } = PerformanceMode.Auto;
        public bool AutoDisableOnHighCpu { get; set; } = true;
        public int CpuThreshold { get; set; } = 80;
        public bool AutoDisableOnLowBattery { get; set; } = true;
        public int BatteryThreshold { get; set; } = 20;
        public bool AutoDisableOnHighRam { get; set; } = false;
        public int RamThreshold { get; set; } = 85;

        // ─── Live telemetry (updated by background timer) ───────────────────
        public float CurrentCpuUsage { get; private set; }
        public float CurrentRamUsage { get; private set; }
        public float CurrentBatteryLevel { get; private set; } = 100f;
        public bool IsOnBattery { get; private set; }
        public bool IsCharging { get; private set; }
        public string LastTriggerReason { get; private set; } = "Нормальная работа";

        private AnimationService()
        {
            LoadSettings();
            _cpuCounter = TryCreateCounter("Processor", "% Processor Time", "_Total");
            _ramCounter = TryCreateCounter("Memory", "% Committed Bytes In Use", null);
            _monitorTimer = new Timer(_ => Monitor(), null, 1500, 2500);
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
                AutoDisableOnHighRam = Properties.Settings.Default.AutoDisableOnHighRam;
                RamThreshold = Properties.Settings.Default.RamThreshold;

                int modeCode = Properties.Settings.Default.PerformanceMode;
                if (modeCode >= 0 && modeCode <= 3)
                    Mode = (PerformanceMode)modeCode;
            }
            catch { /* keep defaults */ }
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
                Properties.Settings.Default.AutoDisableOnHighRam = AutoDisableOnHighRam;
                Properties.Settings.Default.RamThreshold = RamThreshold;
                Properties.Settings.Default.PerformanceMode = (int)Mode;
                Properties.Settings.Default.Save();
            }
            catch { }

            EvaluateState();
        }

        private static PerformanceCounter TryCreateCounter(string category, string counter, string instance)
        {
            try
            {
                var c = instance == null
                    ? new PerformanceCounter(category, counter)
                    : new PerformanceCounter(category, counter, instance);
                c.NextValue();
                return c;
            }
            catch { return null; }
        }

        private void Monitor()
        {
            try { CurrentCpuUsage = _cpuCounter?.NextValue() ?? 0f; } catch { }
            try { CurrentRamUsage = _ramCounter?.NextValue() ?? 0f; } catch { }

            try
            {
                if (GetSystemPowerStatus(out var powerStatus))
                {
                    IsOnBattery = powerStatus.ACLineStatus == 0;
                    IsCharging = powerStatus.ACLineStatus == 1 && (powerStatus.BatteryFlag & 0x08) != 0;
                    CurrentBatteryLevel = powerStatus.BatteryLifePercent <= 100
                        ? powerStatus.BatteryLifePercent
                        : 100f;
                }
            }
            catch { }

            EvaluateState();
        }

        /// <summary>
        /// Compute the effective animation level. Precedence:
        /// 1) user master toggle off → Off
        /// 2) explicit Mode (AlwaysOn / Reduced / Off) overrides telemetry
        /// 3) Auto mode: soft-degrade to Reduced at 70% of threshold, Off above threshold
        /// </summary>
        public void EvaluateState()
        {
            lock (_evalLock)
            {
                AnimationLevel target;
                string reason;

                if (!UserEnabledAnimations)
                {
                    target = AnimationLevel.Off;
                    reason = "Отключены пользователем";
                }
                else if (Mode == PerformanceMode.Off)
                {
                    target = AnimationLevel.Off;
                    reason = "Режим «Выключено»";
                }
                else if (Mode == PerformanceMode.Reduced)
                {
                    target = AnimationLevel.Reduced;
                    reason = "Режим «Упрощённые»";
                }
                else if (Mode == PerformanceMode.AlwaysOn)
                {
                    target = AnimationLevel.Full;
                    reason = "Режим «Всегда»";
                }
                else
                {
                    // Auto: weigh all triggers
                    bool cpuCritical = AutoDisableOnHighCpu && CurrentCpuUsage > CpuThreshold;
                    bool cpuStressed = AutoDisableOnHighCpu && CurrentCpuUsage > CpuThreshold * 0.7f;
                    bool ramCritical = AutoDisableOnHighRam && CurrentRamUsage > RamThreshold;
                    bool batCritical = AutoDisableOnLowBattery && IsOnBattery && !IsCharging
                                       && CurrentBatteryLevel < BatteryThreshold;
                    bool batStressed = AutoDisableOnLowBattery && IsOnBattery && !IsCharging
                                       && CurrentBatteryLevel < BatteryThreshold * 1.5f;

                    if (cpuCritical)
                    {
                        target = AnimationLevel.Off;
                        reason = $"CPU {CurrentCpuUsage:F0}% — выше порога";
                    }
                    else if (ramCritical)
                    {
                        target = AnimationLevel.Off;
                        reason = $"ОЗУ {CurrentRamUsage:F0}% — выше порога";
                    }
                    else if (batCritical)
                    {
                        target = AnimationLevel.Off;
                        reason = $"Заряд {CurrentBatteryLevel:F0}% — низкий";
                    }
                    else if (cpuStressed || batStressed)
                    {
                        target = AnimationLevel.Reduced;
                        reason = cpuStressed
                            ? $"CPU {CurrentCpuUsage:F0}% — повышенная нагрузка"
                            : $"Заряд {CurrentBatteryLevel:F0}% — экономия энергии";
                    }
                    else
                    {
                        target = AnimationLevel.Full;
                        reason = "Нормальная работа";
                    }
                }

                LastTriggerReason = reason;
                if (_currentLevel == target) return;
                _currentLevel = target;
            }

            Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)(() => AnimationStateChanged?.Invoke(this, EventArgs.Empty)));
        }

        /// <summary>
        /// Returns a duration scaled by the current multiplier.
        /// Use from code-behind where XAML cannot reach the service.
        /// </summary>
        public Duration Scale(TimeSpan baseDuration)
        {
            double mul = DurationMultiplier;
            if (mul <= 0) return new Duration(TimeSpan.Zero);
            return new Duration(TimeSpan.FromMilliseconds(baseDuration.TotalMilliseconds * mul));
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
