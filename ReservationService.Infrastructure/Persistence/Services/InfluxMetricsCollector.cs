namespace ReservationService.Infrastructure.Services;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Hosting;

public class InfluxMetricsCollector : BackgroundService
{
    private readonly InfluxDBClient _client;
    private readonly string _bucket = "duaa";
    private readonly string _org = "test";
    private readonly string _serviceName = "reservationService";
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

    // State used to compute per-process CPU%
    private TimeSpan _prevCpu = TimeSpan.Zero;
    private DateTime _prevTime = DateTime.MinValue;
    private readonly Process _proc = Process.GetCurrentProcess();

    public InfluxMetricsCollector()
    {
        _client = InfluxDBClientFactory.Create(
            "http://localhost:8086",
            "WMc340bTMVpDXr3XuWCwKAgeObo1d_0lO9u2gC8Sw5yygX_ZwL3f3JUKf3OLP0Piym7Q-KM5rz7yfaJ37z-umA==".ToCharArray()
        );
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var writeApi = _client.GetWriteApiAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // --- CPU (per-process %) ---
                var now = DateTime.UtcNow;
                var cpuTime = _proc.TotalProcessorTime;

                double cpuPercent = double.NaN;
                if (_prevTime != DateTime.MinValue)
                {
                    var cpuDeltaMs = (cpuTime - _prevCpu).TotalMilliseconds;
                    var wallMs = (now - _prevTime).TotalMilliseconds;
                    if (wallMs > 0)
                    {
                        cpuPercent = Math.Max(0,
                            Math.Min(100.0 * cpuDeltaMs / (wallMs * Environment.ProcessorCount),
                                100.0 * Environment.ProcessorCount));
                    }
                }

                _prevCpu = cpuTime;
                _prevTime = now;

                // --- Memory (per-process) ---
                _proc.Refresh();
                long usedBytes = _proc.WorkingSet64; // physical memory in use
                long privateBytes = _proc.PrivateMemorySize64; // committed private bytes

                // Optional: percent of total system RAM (Windows-only)
                double? usedPercent = TryGetTotalRamBytes(out var totalRam)
                    ? (double)usedBytes / totalRam * 100.0
                    : (double?)null;

                // Build points
                var cpuPoint = PointData
                    .Measurement("cpu_usage")
                    .Tag("service", _serviceName)
                    .Field("usage_percent", Math.Round(double.IsNaN(cpuPercent) ? 0.0 : cpuPercent, 2))
                    .Timestamp(now, WritePrecision.Ns);

                var memPoint = PointData
                    .Measurement("mem_usage")
                    .Tag("service", _serviceName)
                    .Field("used_bytes", usedBytes)
                    .Field("private_bytes", privateBytes)
                    .Timestamp(now, WritePrecision.Ns);

                if (usedPercent.HasValue)
                    memPoint = memPoint.Field("used_percent", Math.Round(usedPercent.Value, 2));

                // Write
                await writeApi.WritePointAsync(cpuPoint, _bucket, _org, stoppingToken);
                await writeApi.WritePointAsync(memPoint, _bucket, _org, stoppingToken);
            }
            catch
            {
                // swallow one-off sampling errors; you can add logging here
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
            }
        }
    }

    // Windows-friendly total RAM; returns false on non-Windows or failure
    private static bool TryGetTotalRamBytes(out ulong total)
    {
        total = 0;
        try
        {
#if WINDOWS
                total = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
                return total > 0;
#else
            // On Linux/macOS you can parse /proc/meminfo or use other APIs if needed.
            return false;
#endif
        }
        catch
        {
            return false;
        }
    }

    public override void Dispose()
    {
        _client.Dispose();
        base.Dispose();
    }
}