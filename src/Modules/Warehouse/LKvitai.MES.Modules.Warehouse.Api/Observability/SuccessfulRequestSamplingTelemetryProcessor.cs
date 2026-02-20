using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Observability;

public sealed class SuccessfulRequestSamplingTelemetryProcessor : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;
    private readonly ApmOptions _options;

    public SuccessfulRequestSamplingTelemetryProcessor(
        ITelemetryProcessor next,
        IOptions<ApmOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public void Process(ITelemetry item)
    {
        if (!_options.Enabled)
        {
            _next.Process(item);
            return;
        }

        if (item is ExceptionTelemetry)
        {
            _next.Process(item);
            return;
        }

        if (item is RequestTelemetry requestTelemetry)
        {
            var isFailure = requestTelemetry.Success == false ||
                            int.TryParse(requestTelemetry.ResponseCode, out var code) && code >= 500;

            if (isFailure || ShouldKeepSuccessfulTelemetry())
            {
                _next.Process(item);
            }

            return;
        }

        _next.Process(item);
    }

    private bool ShouldKeepSuccessfulTelemetry()
    {
        if (_options.SuccessfulRequestSampleRate <= 0d)
        {
            return false;
        }

        if (_options.SuccessfulRequestSampleRate >= 1d)
        {
            return true;
        }

        return Random.Shared.NextDouble() <= _options.SuccessfulRequestSampleRate;
    }
}
