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
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace RunCat365
{
    struct NetworkInfo
    {
        internal float SentSpeed { get; set; }
        internal float ReceivedSpeed { get; set; }
    }

    internal static class NetworkInfoExtension
    {
        internal static List<string> GenerateIndicator(this NetworkInfo networkInfo)
        {
            return [
                TreeFormatter.CreateRoot($"{Strings.SystemInfo_Network}:"),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Sent}: {FormatSpeed(networkInfo.SentSpeed)}", false),
                TreeFormatter.CreateNode($"{Strings.SystemInfo_Received}: {FormatSpeed(networkInfo.ReceivedSpeed)}", true)
            ];
        }

        private static string FormatSpeed(float speedBytes)
        {
            return ((long)speedBytes).ToByteFormatted() + "/s";
        }
    }

    internal class NetworkRepository
    {
        private readonly NetworkInterface? networkInterface;
        private long lastSent;
        private long lastReceived;
        private long lastUpdateTimestamp;
        private NetworkInfo? networkInfo;

        internal bool IsAvailable => networkInterface is not null;

        internal NetworkRepository()
        {
            networkInterface = GetActiveNetworkInterface();
            if (networkInterface is null) return;
            var stats = networkInterface.GetIPStatistics();
            lastSent = stats.BytesSent;
            lastReceived = stats.BytesReceived;
            lastUpdateTimestamp = Stopwatch.GetTimestamp();
        }

        private static NetworkInterface? GetActiveNetworkInterface()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                return interfaces.FirstOrDefault(IsValidNetworkInterface);
            }
            catch (NetworkInformationException)
            {
                return null;
            }
        }

        private static bool IsValidNetworkInterface(NetworkInterface networkInterface)
        {
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback) return false;
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel) return false;
            if (networkInterface.OperationalStatus != OperationalStatus.Up) return false;

            var description = networkInterface.Description;
            if (description.Contains("vpn", StringComparison.OrdinalIgnoreCase)) return false;
            if (description.Contains("tap", StringComparison.OrdinalIgnoreCase)) return false;
            if (description.Contains("virtual", StringComparison.OrdinalIgnoreCase)) return false;
            if (description.Contains("tun", StringComparison.OrdinalIgnoreCase)) return false;

            return true;
        }

        internal void Update()
        {
            if (networkInterface is null) return;
            try
            {
                var stats = networkInterface.GetIPStatistics();
                var nowTimestamp = Stopwatch.GetTimestamp();
                var elapsedSec = Stopwatch.GetElapsedTime(lastUpdateTimestamp, nowTimestamp).TotalSeconds;
                if (elapsedSec > 0)
                {
                    var sentDelta = Math.Max(0L, stats.BytesSent - lastSent);
                    var receivedDelta = Math.Max(0L, stats.BytesReceived - lastReceived);
                    networkInfo = new NetworkInfo
                    {
                        SentSpeed = (float)(sentDelta / elapsedSec),
                        ReceivedSpeed = (float)(receivedDelta / elapsedSec)
                    };
                }
                lastSent = stats.BytesSent;
                lastReceived = stats.BytesReceived;
                lastUpdateTimestamp = nowTimestamp;
            }
            catch (NetworkInformationException)
            {
                networkInfo = null;
            }
        }

        internal NetworkInfo? Get()
        {
            return networkInfo;
        }
    }
}
