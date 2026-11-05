using System.Text.Json;
using FluentAssertions;
using FocusCatAI.AI;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Settings;
using FocusCatAI.Domain.AI;
using FocusCatAI.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FocusCatAI.Application.Tests.AI;

public sealed class AiCompanionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wb-ai-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private static AiHardwareCapabilities Caps(bool gpu, bool npu) => new(true, gpu, npu, "x");

    [Theory]
    [InlineData(AiDevicePreference.Auto, true, true, AiExecutionDevice.Npu)]
    [InlineData(AiDevicePreference.Auto, true, false, AiExecutionDevice.Gpu)]
    [InlineData(AiDevicePreference.Auto, false, false, AiExecutionDevice.Cpu)]
    [InlineData(AiDevicePreference.Npu, true, false, AiExecutionDevice.Cpu)]
    [InlineData(AiDevicePreference.Gpu, false, true, AiExecutionDevice.Cpu)]
    [InlineData(AiDevicePreference.Gpu, true, true, AiExecutionDevice.Gpu)]
    [InlineData(AiDevicePreference.Npu, true, true, AiExecutionDevice.Npu)]
    [InlineData(AiDevicePreference.Cpu, true, true, AiExecutionDevice.Cpu)]
    public void ResolverPicksDevice(AiDevicePreference pref, bool gpu, bool npu, AiExecutionDevice expected) =>
        AiDeviceResolver.Resolve(pref, Caps(gpu, npu)).Should().Be(expected);

    [Fact]
    public void InvalidDeviceStringDefaultsToAuto()
    {
        var s = JsonSerializer.Deserialize<AppSettings>("{\"Ai\":{\"Enabled\":false,\"Device\":\"Quantum\"}}");
        s!.Ai.Device.Should().Be(AiDevicePreference.Auto);
        s.Ai.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ValidDeviceStringIsRead()
    {
        var s = JsonSerializer.Deserialize<AppSettings>("{\"Ai\":{\"Device\":\"npu\"}}");
        s!.Ai.Device.Should().Be(AiDevicePreference.Npu);
    }

    [Fact]
    public void MissingAiSectionUsesDefaults()
    {
        var s = JsonSerializer.Deserialize<AppSettings>("{}");
        s!.Ai.Should().Be(new AiSettings());
    }

    [Fact]
    public void SanitizeHandlesNullAi()
    {
        var s = new AppSettings { Ai = null! };
        SettingsLimits.Sanitize(s).Ai.Should().NotBeNull();
    }

    private FileSystemLocalModelScanner Scanner()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.ModelsDirectory.Returns(_dir);
        return new FileSystemLocalModelScanner(paths);
    }

    [Fact]
    public void EmptyDirectoryHasNoModelsAndIsCreated()
    {
        var scanner = Scanner();
        scanner.ListInstalledModels().Should().BeEmpty();
        scanner.HasAnyModel().Should().BeFalse();
        Directory.Exists(_dir).Should().BeTrue();
    }

    [Fact]
    public void OnnxFileAndModelFolderAreDetected()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "phi"));
        File.WriteAllText(Path.Combine(_dir, "tiny.onnx"), "x");
        File.WriteAllText(Path.Combine(_dir, "phi", "model.json"), "{}");
        var scanner = Scanner();
        scanner.ListInstalledModels().Should().BeEquivalentTo("tiny", "phi");
        scanner.HasAnyModel().Should().BeTrue();
    }

    [Fact]
    public void InvalidPathDoesNotThrow()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.ModelsDirectory.Returns("::invalid\0path");
        var act = () => new FileSystemLocalModelScanner(paths).ListInstalledModels();
        act.Should().NotThrow();
    }

    private static LocalAiCompanionService Service(AiDevicePreference pref, bool gpu, bool npu, params string[] models)
    {
        var probe = Substitute.For<IAiHardwareProbe>();
        probe.Detect().Returns(Caps(gpu, npu));
        var scanner = Substitute.For<ILocalModelScanner>();
        scanner.ListInstalledModels().Returns(models);
        scanner.HasAnyModel().Returns(models.Length > 0);
        var settings = Substitute.For<ISettingsService>();
        settings.Current.Returns(new AppSettings { Ai = new AiSettings(true, pref) });
        return new LocalAiCompanionService(probe, scanner, settings);
    }

    [Theory]
    [InlineData("cat")]
    [InlineData("tuxedo")]
    [InlineData("ginger")]
    public void RepliesAreShortAndNonEmptyForAllIntents(string pet)
    {
        var svc = Service(AiDevicePreference.Auto, false, false);
        var prompts = new[] { "I have a bug in my code", "I'm so tired", "hello", "help me focus", "I need motivation", "what's up" };
        foreach (var prompt in prompts)
        {
            foreach (var hour in new[] { 3, 9, 15, 23 })
            {
                var reply = svc.GenerateReply(new CompanionPromptContext(prompt, pet, 3, hour));
                reply.Text.Should().NotBeNullOrWhiteSpace();
                reply.Text.Length.Should().BeLessThanOrEqualTo(110);
                reply.DeviceUsed.Should().Be(AiExecutionDevice.Cpu);
            }
        }
    }

    [Fact]
    public void PetsSoundDifferent()
    {
        var svc = Service(AiDevicePreference.Auto, false, false);
        var texts = new[] { "cat", "tuxedo", "ginger" }
            .Select(p => svc.GenerateReply(new CompanionPromptContext("debug this bug", p, 1, 12)).Text)
            .Distinct().ToList();
        texts.Should().HaveCount(3);
    }

    [Fact]
    public void ReplyReportsDeviceAndModel()
    {
        var svc = Service(AiDevicePreference.Auto, true, true, "tiny");
        var reply = svc.GenerateReply(new CompanionPromptContext("hi", "cat", 1, 10));
        reply.DeviceUsed.Should().Be(AiExecutionDevice.Npu);
        reply.UsedLocalModel.Should().BeTrue();
        reply.ModelName.Should().Be("tiny");
    }

    [Fact]
    public void UnknownPetFallsBackToGentleReply()
    {
        var reply = Service(AiDevicePreference.Cpu, true, true).GenerateReply(new CompanionPromptContext("hmm", "zzz", 1, 10));
        reply.Text.Should().NotBeNullOrWhiteSpace();
        reply.UsedLocalModel.Should().BeFalse();
    }

    [Fact]
    public void ProbeAlwaysHasCpu()
    {
        new WindowsAiHardwareProbe().Detect().CpuAvailable.Should().BeTrue();
    }

    [Fact]
    public void OpenModelsFolderDoesNotThrowOnBadPath()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.ModelsDirectory.Returns("::invalid\0path");
        var launcher = new ShellLauncher(paths, NullLogger<ShellLauncher>.Instance);
        var act = () => launcher.OpenModelsFolder();
        act.Should().NotThrow();
    }
}
