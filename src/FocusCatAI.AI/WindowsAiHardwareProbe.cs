using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.AI;

namespace FocusCatAI.AI;

/// <summary>Best-effort, non-throwing hardware hints. CPU is always available; GPU/NPU come from environment hints and driver files.</summary>
public sealed class WindowsAiHardwareProbe : IAiHardwareProbe
{
    public AiHardwareCapabilities Detect()
    {
        var gpu = false;
        var npu = false;
        var cpuName = "CPU";

        try
        {
            cpuName = $"{Environment.ProcessorCount}-core CPU";
            var arch = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? string.Empty;
            var hint = Environment.GetEnvironmentVariable("WORKBUDDY_AI_HINT") ?? string.Empty;

            // Snapdragon X / Ryzen AI / Core Ultra identifiers commonly accompany an NPU.
            npu = hint.Contains("npu", StringComparison.OrdinalIgnoreCase)
                || arch.Contains("Snapdragon", StringComparison.OrdinalIgnoreCase)
                || arch.Contains("Qualcomm", StringComparison.OrdinalIgnoreCase);
            gpu = hint.Contains("gpu", StringComparison.OrdinalIgnoreCase);

            if (OperatingSystem.IsWindows())
            {
                var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
                if (!string.IsNullOrEmpty(system))
                {
                    gpu |= File.Exists(Path.Combine(system, "DirectML.dll")) || File.Exists(Path.Combine(system, "d3d12.dll"));
                }
            }
        }
        catch (Exception)
        {
            // Hints only; fall back to what was detected.
        }

        var summary = $"{cpuName}{(gpu ? ", GPU" : string.Empty)}{(npu ? ", NPU" : string.Empty)}";
        return new AiHardwareCapabilities(true, gpu, npu, summary);
    }
}
