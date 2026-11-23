// Copyright 2025 Takuto Nakamura
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

using RunCat365.Properties;

namespace RunCat365
{
    struct GPUInfo
    {
        internal float Average { get; set; }
        internal float Maximum { get; set; }
    }

    internal static class GPUInfoExtension
    {
        internal static string GetDescription(this GPUInfo gpuInfo)
        {
            return $"{Strings.SystemInfo_GPU}: {gpuInfo.Maximum:f1}%";
        }

        internal static List<string> GenerateIndicator(this GPUInfo gpuInfo)
        {
            var resultLines = new List<string>
            {
                TreeFormatter.CreateRoot($"{Strings.SystemInfo_GPU}:"),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Average}: {gpuInfo.Average:f1}%", false),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Maximum}: {gpuInfo.Maximum:f1}%", true)
            };
            return resultLines;
        }
    }

    internal sealed class GPUPerformanceCounters : InstancedPerformanceCounters
    {
        private const string ENGINE_TYPE_FILTER = "engtype_3D";

        protected override string CategoryName => "GPU Engine";
        protected override string CounterName => "Utilization Percentage";

        protected override bool ShouldIncludeInstance(string instanceName)
        {
            return instanceName.Contains(ENGINE_TYPE_FILTER, StringComparison.Ordinal);
        }

        internal static GPUPerformanceCounters? TryCreate()
        {
            var instance = new GPUPerformanceCounters();
            return instance.TryInitialize() ? instance : null;
        }
    }

    internal class GPURepository
    {
        private const int GPU_INFO_LIST_LIMIT_SIZE = 5;
        private const int REFRESH_INTERVAL_TICKS = 30;
        private const long REFRESH_INTERVAL_MS = 30_000;

        private readonly GPUPerformanceCounters? counters;
        private readonly GPUInfo[] samples = new GPUInfo[GPU_INFO_LIST_LIMIT_SIZE];
        private int sampleIndex;
        private int sampleCount;
        private int ticksSinceLastRefresh;
        private long lastRefreshTickMs = Environment.TickCount64;

        internal bool IsAvailable => counters is not null;

        internal GPURepository()
        {
            counters = GPUPerformanceCounters.TryCreate();
        }

        internal void Update()
        {
            if (counters is null) return;

            ticksSinceLastRefresh += 1;
            var nowTickMs = Environment.TickCount64;
            if (REFRESH_INTERVAL_TICKS <= ticksSinceLastRefresh ||
                REFRESH_INTERVAL_MS <= nowTickMs - lastRefreshTickMs)
            {
                ticksSinceLastRefresh = 0;
                lastRefreshTickMs = nowTickMs;
                counters.RefreshInstances();
            }

            var values = counters.ReadValues();
            if (values.Count == 0) return;

            float sum = 0f;
            float max = float.MinValue;
            for (int i = 0; i < values.Count; i++)
            {
                var value = values[i];
                sum += value;
                if (value > max) max = value;
            }

            samples[sampleIndex] = new GPUInfo
            {
                Average = Math.Min(100, sum / values.Count),
                Maximum = Math.Min(100, max)
            };

            sampleIndex = (sampleIndex + 1) % GPU_INFO_LIST_LIMIT_SIZE;
            if (sampleCount < GPU_INFO_LIST_LIMIT_SIZE)
            {
                sampleCount++;
            }
        }

        internal GPUInfo? Get()
        {
            if (counters is null || sampleCount == 0) return null;

            float averageSum = 0f;
            float maximum = float.MinValue;
            for (int i = 0; i < sampleCount; i++)
            {
                averageSum += samples[i].Average;
                if (samples[i].Maximum > maximum)
                {
                    maximum = samples[i].Maximum;
                }
            }

            return new GPUInfo
            {
                Average = averageSum / sampleCount,
                Maximum = maximum
            };
        }

        internal void Close()
        {
            counters?.Close();
        }
    }
}
