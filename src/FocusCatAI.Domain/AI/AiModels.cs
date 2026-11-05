namespace FocusCatAI.Domain.AI;

public enum AiDevicePreference
{
    Auto,
    Cpu,
    Gpu,
    Npu
}

public enum AiExecutionDevice
{
    Cpu,
    Gpu,
    Npu
}

public sealed record AiHardwareCapabilities(bool CpuAvailable, bool GpuAvailable, bool NpuAvailable, string Summary);

public sealed record CompanionPromptContext(string Prompt, string PetId, int CompanionLevel, int HourOfDay);

public sealed record CompanionReply(string Text, AiExecutionDevice DeviceUsed, bool UsedLocalModel, string ModelName);

public static class AiDeviceResolver
{
    public static AiExecutionDevice Resolve(AiDevicePreference pref, AiHardwareCapabilities caps) => pref switch
    {
        AiDevicePreference.Npu when caps.NpuAvailable => AiExecutionDevice.Npu,
        AiDevicePreference.Gpu when caps.GpuAvailable => AiExecutionDevice.Gpu,
        AiDevicePreference.Auto when caps.NpuAvailable => AiExecutionDevice.Npu,
        AiDevicePreference.Auto when caps.GpuAvailable => AiExecutionDevice.Gpu,
        _ => AiExecutionDevice.Cpu
    };
}
