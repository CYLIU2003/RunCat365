// Copyright 2020 Takuto Nakamura
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using Microsoft.Win32;
using RunCat365.Properties;
using System.Diagnostics;
using System.Globalization;
using FormsTimer = System.Windows.Forms.Timer;

namespace RunCat365
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
#if DEBUG
            var defaultCultureInfo = SupportedLanguage.English.GetDefaultCultureInfo();
#else
            var defaultCultureInfo = SupportedLanguageExtension.GetCurrentLanguage().GetDefaultCultureInfo();
#endif
            CultureInfo.CurrentUICulture = defaultCultureInfo;
            CultureInfo.CurrentCulture = defaultCultureInfo;

            using var procMutex = new Mutex(true, "_RUNCAT_MUTEX", out var result);
            if (!result) return;

            try
            {
                ApplicationConfiguration.Initialize();
                Application.SetColorMode(SystemColorMode.System);
                Application.Run(new RunCat365ApplicationContext());
            }
            finally
            {
                procMutex?.ReleaseMutex();
            }
        }
    }

    internal class RunCat365ApplicationContext : ApplicationContext
    {
        private const int FETCH_TIMER_DEFAULT_INTERVAL = 1000;
        private const int FETCH_COUNTER_SIZE = 5;
        private const int ANIMATE_TIMER_DEFAULT_INTERVAL = 200;
        private readonly CPURepository cpuRepository;
        private readonly GPURepository gpuRepository;
        private readonly MemoryRepository memoryRepository;
        private readonly TemperatureRepository temperatureRepository;
        private readonly StorageRepository storageRepository;
        private readonly NetworkRepository networkRepository;
        private readonly CustomRunnerRepository customRunnerRepository;
        private readonly LaunchAtStartupManager launchAtStartupManager;
        private readonly ContextMenuManager contextMenuManager;
        private readonly FormsTimer fetchTimer;
        private readonly FormsTimer animateTimer;
        private readonly Lock repositoryLock = new();
        private readonly List<string> systemInfoValues = new(24);
        private Runner runner = Runner.Cat;
        private Theme manualTheme = Theme.System;
        private Theme lastSystemTheme;
        private TemperatureUnit temperatureUnit = TemperatureUnit.System;
        private FPSMaxLimit fpsMaxLimit = FPSMaxLimit.FPS40;
        private SpeedSource speedSource = SpeedSource.CPU;
        private string? customRunnerName;
        private int fetchCounter = FETCH_COUNTER_SIZE;
        private int _fetchRunning = 0;
        private int _menuRefreshRunning = 0;
        private bool hasInitialGpuSample;
        private volatile bool isDisposed;

        private readonly record struct FetchSnapshot(
            string NotifyText,
            string? SystemInfoMenuText,
            int AnimateIntervalMs
        );

        public RunCat365ApplicationContext()
        {
            UserSettings.Default.Reload();
            _ = Enum.TryParse(UserSettings.Default.Runner, out runner);
            _ = Enum.TryParse(UserSettings.Default.Theme, out manualTheme);
            _ = Enum.TryParse(UserSettings.Default.TemperatureUnit, out temperatureUnit);
            _ = Enum.TryParse(UserSettings.Default.FPSMaxLimit, out fpsMaxLimit);
            _ = Enum.TryParse(UserSettings.Default.SpeedSource, out speedSource);
            customRunnerName = string.IsNullOrEmpty(UserSettings.Default.CustomRunnerName)
                ? null
                : UserSettings.Default.CustomRunnerName;

            SystemEvents.UserPreferenceChanged += new UserPreferenceChangedEventHandler(UserPreferenceChanged);

            cpuRepository = new CPURepository();
            gpuRepository = new GPURepository();
            memoryRepository = new MemoryRepository();
            temperatureRepository = new TemperatureRepository();
            storageRepository = new StorageRepository();
            networkRepository = new NetworkRepository();
            customRunnerRepository = new CustomRunnerRepository();
            launchAtStartupManager = new LaunchAtStartupManager();

            ResolveSpeedSource();
            lastSystemTheme = GetSystemTheme();

            contextMenuManager = new ContextMenuManager(
                () => runner,
                r => ChangeRunner(r),
                customRunnerRepository,
                () => customRunnerName,
                name => ApplyCustomRunner(name),
                deletedName => HandleCustomRunnerDeleted(deletedName),
                () => GetSystemTheme(),
                () => manualTheme,
                t => ChangeManualTheme(t),
                () => speedSource,
                s => ChangeSpeedSource(s),
                s => IsSpeedSourceAvailable(s),
                () => fpsMaxLimit,
                f => ChangeFPSMaxLimit(f),
                () => temperatureUnit,
                u => ChangeTemperatureUnit(u),
                () => launchAtStartupManager.GetStartup(),
                s => launchAtStartupManager.ToggleStartup(s),
                () => OpenProjectPage(),
                () => Application.Exit()
            );
            contextMenuManager.ContextMenuOpening += OnContextMenuOpening;

            if (customRunnerName is not null)
            {
                ApplyCustomRunner(customRunnerName);
            }

            animateTimer = new FormsTimer
            {
                Interval = ANIMATE_TIMER_DEFAULT_INTERVAL
            };
            animateTimer.Tick += new EventHandler(AnimationTick);
            animateTimer.Start();

            fetchTimer = new FormsTimer
            {
                Interval = FETCH_TIMER_DEFAULT_INTERVAL
            };
            fetchTimer.Tick += new EventHandler(FetchTick);
            fetchTimer.Start();

            ShowBalloonTipIfNeeded();
        }

        private static Theme GetSystemTheme()
        {
            var keyName = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            using var rKey = Registry.CurrentUser.OpenSubKey(keyName);
            if (rKey is null) return Theme.Light;
            var value = rKey.GetValue("SystemUsesLightTheme");
            if (value is null) return Theme.Light;
            return (int)value == 0 ? Theme.Dark : Theme.Light;
        }

        private bool IsSpeedSourceAvailable(SpeedSource speedSource)
        {
            return speedSource switch
            {
                SpeedSource.CPU => true,
                SpeedSource.GPU => gpuRepository.IsAvailable,
                SpeedSource.Memory => true,
                _ => false,
            };
        }

        private void ResolveSpeedSource()
        {
            if (!IsSpeedSourceAvailable(speedSource))
            {
                ChangeSpeedSource(SpeedSource.CPU);
            }
        }

        private void ShowBalloonTipIfNeeded()
        {
            if (!cpuRepository.IsAvailable)
            {
                contextMenuManager.ShowBalloonTip(BalloonTipType.CPUInfoUnavailable);
            }
            else if (UserSettings.Default.FirstLaunch)
            {
                contextMenuManager.ShowBalloonTip(BalloonTipType.AppLaunched);
                UserSettings.Default.FirstLaunch = false;
                UserSettings.Default.Save();
            }
        }

        private void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General) return;
            var systemTheme = GetSystemTheme();
            if (systemTheme == lastSystemTheme) return;
            lastSystemTheme = systemTheme;
            if (manualTheme != Theme.System) return;
            if (contextMenuManager.HasActiveCustomIcons)
            {
                contextMenuManager.RecolorActiveCustomIcons(systemTheme, manualTheme);
            }
            else
            {
                contextMenuManager.SetIcons(systemTheme, manualTheme, runner);
            }
        }

        private static void OpenProjectPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo()
                {
                    FileName = "https://github.com/runcat-dev/RunCat365",
                    UseShellExecute = true
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }
        }

        private void ChangeRunner(Runner r)
        {
            runner = r;
            customRunnerName = null;
            UserSettings.Default.Runner = runner.ToString();
            UserSettings.Default.CustomRunnerName = string.Empty;
            UserSettings.Default.Save();
        }

        private void ApplyCustomRunner(string name)
        {
            var frames = customRunnerRepository.LoadFrames(name);
            if (frames.Count == 0) return;
            customRunnerName = name;
            UserSettings.Default.CustomRunnerName = name;
            UserSettings.Default.Save();
            contextMenuManager.SetCustomIcons(frames, GetSystemTheme(), manualTheme);
            foreach (var frame in frames) frame.Dispose();
        }

        private void RevertToBuiltInRunner()
        {
            customRunnerName = null;
            UserSettings.Default.CustomRunnerName = string.Empty;
            UserSettings.Default.Save();
            contextMenuManager.SetIcons(GetSystemTheme(), manualTheme, runner);
        }

        private void HandleCustomRunnerDeleted(string deletedName)
        {
            if (!string.Equals(customRunnerName, deletedName, StringComparison.OrdinalIgnoreCase)) return;
            RevertToBuiltInRunner();
        }

        private void ChangeManualTheme(Theme t)
        {
            manualTheme = t;
            UserSettings.Default.Theme = manualTheme.ToString();
            UserSettings.Default.Save();
        }

        private void ChangeTemperatureUnit(TemperatureUnit u)
        {
            temperatureUnit = u;
            fetchCounter = FETCH_COUNTER_SIZE;
            UserSettings.Default.TemperatureUnit = temperatureUnit.ToString();
            UserSettings.Default.Save();
        }

        private void ChangeSpeedSource(SpeedSource s)
        {
            speedSource = s;
            fetchCounter = FETCH_COUNTER_SIZE;
            UserSettings.Default.SpeedSource = speedSource.ToString();
            UserSettings.Default.Save();
        }

        private void ChangeFPSMaxLimit(FPSMaxLimit f)
        {
            fpsMaxLimit = f;
            fetchCounter = FETCH_COUNTER_SIZE;
            UserSettings.Default.FPSMaxLimit = fpsMaxLimit.ToString();
            UserSettings.Default.Save();
        }

        private void AnimationTick(object? sender, EventArgs e)
        {
            contextMenuManager.AdvanceFrame();
        }

        private static string GetInfoDescription(
            SpeedSource currentSpeedSource,
            TemperatureUnit currentTemperatureUnit,
            CPUInfo cpuInfo,
            GPUInfo? gpuInfo,
            MemoryInfo memoryInfo,
            TemperatureInfo? temperatureInfo
        )
        {
            var baseDescription = currentSpeedSource switch
            {
                SpeedSource.CPU => cpuInfo.GetDescription(),
                SpeedSource.GPU => gpuInfo?.GetDescription() ?? "",
                SpeedSource.Memory => memoryInfo.GetDescription(),
                _ => "",
            };

            var temperatureDescription = temperatureInfo?.GetDescription(currentTemperatureUnit) ?? "";
            return string.IsNullOrEmpty(temperatureDescription) ? baseDescription : $"{baseDescription}\n{temperatureDescription}";
        }

        private static int CalculateInterval(
            SpeedSource currentSpeedSource,
            FPSMaxLimit currentFpsMaxLimit,
            CPUInfo cpuInfo,
            GPUInfo? gpuInfo,
            MemoryInfo memoryInfo
        )
        {
            var load = currentSpeedSource switch
            {
                SpeedSource.CPU => cpuInfo.Total,
                SpeedSource.GPU => gpuInfo?.Maximum ?? 0f,
                SpeedSource.Memory => memoryInfo.MemoryLoad,
                _ => 0f,
            };
            var speed = (float)Math.Max(1.0f, (load / 5.0f) * currentFpsMaxLimit.GetRate());
            return (int)(500.0f / speed);
        }

        private string BuildSystemInfoMenuText(
            TemperatureUnit currentTemperatureUnit,
            CPUInfo cpuInfo,
            GPUInfo? gpuInfo,
            MemoryInfo memoryInfo,
            TemperatureInfo? temperatureInfo,
            List<StorageInfo> storageInfo,
            NetworkInfo? networkInfo
        )
        {
            systemInfoValues.Clear();
            systemInfoValues.AddRange(cpuInfo.GenerateIndicator());
            if (gpuInfo.HasValue)
            {
                systemInfoValues.AddRange(gpuInfo.Value.GenerateIndicator());
            }
            systemInfoValues.AddRange(memoryInfo.GenerateIndicator());
            if (temperatureInfo.HasValue)
            {
                systemInfoValues.AddRange(temperatureInfo.Value.GenerateIndicator(currentTemperatureUnit));
            }
            systemInfoValues.AddRange(storageInfo.GenerateIndicator());
            if (networkInfo.HasValue)
            {
                systemInfoValues.AddRange(networkInfo.Value.GenerateIndicator());
            }
            return string.Join('\n', systemInfoValues);
        }

        private FetchSnapshot? BuildFetchSnapshot(
            SpeedSource currentSpeedSource,
            TemperatureUnit currentTemperatureUnit,
            FPSMaxLimit currentFpsMaxLimit,
            bool isMenuOpen,
            bool doHeavySlice,
            bool needsCpuTick,
            bool needsGpuTick
        )
        {
            lock (repositoryLock)
            {
                if (isDisposed) return null;

                if (needsCpuTick)
                {
                    cpuRepository.Update();
                }
                if (needsGpuTick)
                {
                    gpuRepository.Update();
                    hasInitialGpuSample = true;
                }

                if (!doHeavySlice) return null;

                if (gpuRepository.IsAvailable && !needsGpuTick && !hasInitialGpuSample)
                {
                    gpuRepository.Update();
                    hasInitialGpuSample = true;
                }

                temperatureRepository.Update();
                if (isMenuOpen || currentSpeedSource == SpeedSource.Memory)
                {
                    memoryRepository.Update();
                }
                if (isMenuOpen)
                {
                    storageRepository.Update();
                    networkRepository.Update();
                }

                var cpuInfo = cpuRepository.Get();
                var gpuInfo = gpuRepository.Get();
                var memoryInfo = memoryRepository.Get();
                var temperatureInfo = temperatureRepository.Get();

                var notifyText = GetInfoDescription(
                    currentSpeedSource,
                    currentTemperatureUnit,
                    cpuInfo,
                    gpuInfo,
                    memoryInfo,
                    temperatureInfo
                );

                string? menuText = isMenuOpen
                    ? BuildSystemInfoMenuText(
                        currentTemperatureUnit,
                        cpuInfo,
                        gpuInfo,
                        memoryInfo,
                        temperatureInfo,
                        storageRepository.Get(),
                        networkRepository.Get()
                    )
                    : null;

                var interval = CalculateInterval(
                    currentSpeedSource,
                    currentFpsMaxLimit,
                    cpuInfo,
                    gpuInfo,
                    memoryInfo
                );

                return new FetchSnapshot(notifyText, menuText, interval);
            }
        }

        private async void OnContextMenuOpening(object? sender, EventArgs e)
        {
            if (isDisposed) return;
            if (Interlocked.Exchange(ref _menuRefreshRunning, 1) == 1) return;
            try
            {
                var currentSpeedSource = speedSource;
                var currentTemperatureUnit = temperatureUnit;
                var currentFpsMaxLimit = fpsMaxLimit;
                var needsCpuSample = currentSpeedSource != SpeedSource.CPU;
                var needsGpuSample = gpuRepository.IsAvailable && currentSpeedSource != SpeedSource.GPU;
                var snapshot = await Task.Run(() => BuildFetchSnapshot(
                    currentSpeedSource,
                    currentTemperatureUnit,
                    currentFpsMaxLimit,
                    isMenuOpen: true,
                    doHeavySlice: true,
                    needsCpuTick: needsCpuSample,
                    needsGpuTick: needsGpuSample
                ));

                if (!isDisposed && contextMenuManager.IsContextMenuOpen &&
                    snapshot?.SystemInfoMenuText is string menuText)
                {
                    contextMenuManager.SetSystemInfoMenuText(menuText);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ContextMenu refresh failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _menuRefreshRunning, 0);
            }
        }

        private async void FetchTick(object? sender, EventArgs e)
        {
            if (isDisposed) return;
            if (Interlocked.Exchange(ref _fetchRunning, 1) == 1) return;
            try
            {
                var currentSpeedSource = speedSource;
                var currentTemperatureUnit = temperatureUnit;
                var currentFpsMaxLimit = fpsMaxLimit;
                var isMenuOpen = contextMenuManager.IsContextMenuOpen;

                fetchCounter += 1;
                var doHeavySlice = fetchCounter >= FETCH_COUNTER_SIZE;
                if (doHeavySlice)
                {
                    fetchCounter = 0;
                }

                var needsCpuTick = isMenuOpen || currentSpeedSource == SpeedSource.CPU;
                var needsGpuTick = gpuRepository.IsAvailable && (isMenuOpen || currentSpeedSource == SpeedSource.GPU);

                if (!doHeavySlice && !needsCpuTick && !needsGpuTick)
                {
                    return;
                }

                var snapshot = await Task.Run(() => BuildFetchSnapshot(
                    currentSpeedSource,
                    currentTemperatureUnit,
                    currentFpsMaxLimit,
                    isMenuOpen,
                    doHeavySlice,
                    needsCpuTick,
                    needsGpuTick
                ));

                if (isDisposed || snapshot is null) return;

                contextMenuManager.SetNotifyIconText(snapshot.Value.NotifyText);
                if (snapshot.Value.SystemInfoMenuText is not null)
                {
                    contextMenuManager.SetSystemInfoMenuText(snapshot.Value.SystemInfoMenuText);
                }

                var clampedInterval = Math.Clamp(snapshot.Value.AnimateIntervalMs, 25, 1000);
                if (animateTimer.Interval != clampedInterval)
                {
                    animateTimer.Stop();
                    animateTimer.Interval = clampedInterval;
                    animateTimer.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FetchTick failed: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _fetchRunning, 0);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                isDisposed = true;
                SystemEvents.UserPreferenceChanged -= UserPreferenceChanged;
                if (contextMenuManager is not null)
                {
                    contextMenuManager.ContextMenuOpening -= OnContextMenuOpening;
                }

                animateTimer?.Stop();
                animateTimer?.Dispose();
                fetchTimer?.Stop();
                fetchTimer?.Dispose();

                lock (repositoryLock)
                {
                    cpuRepository?.Close();
                    gpuRepository?.Close();
                    temperatureRepository?.Close();
                }

                contextMenuManager?.HideNotifyIcon();
                contextMenuManager?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
