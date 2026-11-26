using FluentAssertions;
using LKvitai.MES.BuildingBlocks.Observability;
using LKvitai.MES.BuildingBlocks.WebUI.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Serilog;
using Xunit;

namespace LKvitai.MES.Tests.Shopfloor.Unit;

/// <summary>Shared Sentry setup (BuildingBlocks.Observability) and the browser snippet every WebUI layout renders.</summary>
public class SentryTests
{
    private const string Dsn = "https://public@o0.ingest.de.sentry.io/0";

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void Without_a_dsn_sentry_is_off_and_registers_nothing_but_its_settings()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Sentry:Dsn"] = " ";

        builder.AddCikadaSentry("shopfloor-webui");

        var services = builder.Services.BuildServiceProvider();
        services.GetRequiredService<CikadaSentrySettings>().Enabled.Should().BeFalse();
        services.GetService<IHub>().Should().BeNull();
        var logger = new LoggerConfiguration();
        logger.WriteToCikadaSentry(builder.Configuration).Should().BeSameAs(logger);
    }

    [Fact]
    public void Settings_default_to_privacy_safe_replay_and_no_tracing()
    {
        var settings = CikadaSentrySettings.From(Config(("Sentry:Dsn", Dsn), ("APP_VERSION", "v0.7.3-4-gabc")), "sales-webui");

        settings.Should().BeEquivalentTo(new CikadaSentrySettings(
            "sales-webui", Dsn, null, "v0.7.3-4-gabc", 0, 0.1, 1, true, true));
    }

    [Fact]
    public void Release_and_rates_come_from_configuration_and_bad_rates_fall_back()
    {
        var settings = CikadaSentrySettings.From(Config(
            ("Sentry:Dsn", Dsn),
            ("Sentry:Release", "v0.7.3"),
            ("RELEASE_TAG", "ignored"),
            ("Sentry:TracesSampleRate", "0.25"),
            ("Sentry:Replay:SessionSampleRate", "abc"),
            ("Sentry:Replay:OnErrorSampleRate", "7"),
            ("Sentry:Replay:MaskAllText", "false")), "portal-webui");

        settings.Release.Should().Be("v0.7.3");
        settings.TracesSampleRate.Should().Be(0.25);
        settings.ReplaySessionSampleRate.Should().Be(0.1);
        settings.ReplayOnErrorSampleRate.Should().Be(1);
        settings.ReplayMaskAllText.Should().BeFalse();
        settings.ReplayBlockAllMedia.Should().BeTrue();
    }

    [Fact]
    public void Browser_script_json_encodes_every_value_and_tags_module_and_user()
    {
        var settings = CikadaSentrySettings.From(Config(
            ("Sentry:Dsn", Dsn), ("Sentry:Environment", "test"), ("Sentry:Release", "abc123")), "finance-webui");

        var script = SentryBrowserScript.Build(settings, "jonas</script><script>alert(1)");

        script.Should().Contain("\"dsn\":\"https://public@o0.ingest.de.sentry.io/0\"")
            .And.Contain("\"environment\":\"test\"")
            .And.Contain("\"release\":\"abc123\"")
            .And.Contain("\"replaysSessionSampleRate\":0.1")
            .And.Contain("\"replaysOnErrorSampleRate\":1")
            .And.Contain("\"tags\":{\"module\":\"finance-webui\"}")
            .And.Contain("Sentry.replayIntegration({\"maskAllText\":true,\"maskAllInputs\":true,\"blockAllMedia\":true})")
            .And.Contain("Sentry.init(options);")
            .And.NotContain("</script")
            .And.NotContain("browserTracingIntegration")
            .And.NotContain("tracesSampleRate");
    }

    [Fact]
    public void Browser_script_adds_same_origin_tracing_only_when_enabled_and_no_user_when_anonymous()
    {
        var settings = CikadaSentrySettings.From(Config(("Sentry:Dsn", Dsn), ("Sentry:TracesSampleRate", "0.5")), "scanning-webui");

        var script = SentryBrowserScript.Build(settings, null);

        script.Should().Contain("\"tracesSampleRate\":0.5")
            .And.Contain("Sentry.browserTracingIntegration()")
            .And.Contain("options.tracePropagationTargets = [/^\\//];")
            .And.NotContain("\"user\"")
            .And.NotContain("\"environment\"");
    }
}
