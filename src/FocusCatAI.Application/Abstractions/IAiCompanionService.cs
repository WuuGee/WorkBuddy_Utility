using FocusCatAI.Domain.AI;

namespace FocusCatAI.Application.Abstractions;

public interface IAiHardwareProbe
{
    AiHardwareCapabilities Detect();
}

public interface ILocalModelScanner
{
    IReadOnlyList<string> ListInstalledModels();

    bool HasAnyModel();
}

public interface IAiCompanionService
{
    AiHardwareCapabilities Hardware { get; }

    CompanionReply GenerateReply(CompanionPromptContext context);
}
