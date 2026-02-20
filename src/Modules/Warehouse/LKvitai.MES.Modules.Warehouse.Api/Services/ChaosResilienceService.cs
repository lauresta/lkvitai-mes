using Microsoft.AspNetCore.Http;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public enum ChaosScenario
{
    None = 0,
    DatabaseFailure = 1,
    RedisFailure = 2,
    NetworkPartition = 3,
    HighLatency = 4,
    MessageQueueFailure = 5
}

public sealed class ChaosInjectionOptions
{
    public bool Enabled { get; set; }
    public ChaosScenario Scenario { get; set; } = ChaosScenario.None;
    public int InjectLatencyMs { get; set; } = 500;
}

public sealed record ChaosExecutionResult<T>(
    int StatusCode,
    bool IsSuccess,
    bool IsDegraded,
    bool CircuitOpen,
    int RetryAttempts,
    T? Value,
    string Message);

public interface IChaosResilienceService
{
    Task<ChaosExecutionResult<T>> ExecuteAsync<T>(
        ChaosScenario scenario,
        Func<CancellationToken, Task<T>> operation,
        Func<CancellationToken, Task<T>>? fallback = null,
        ChaosInjectionOptions? injection = null,
        CancellationToken cancellationToken = default);

    Task<ChaosExecutionResult<T>> ExecuteTransactionalAsync<T>(
        ChaosScenario scenario,
        Func<CancellationToken, Task<T>> operation,
        Func<CancellationToken, Task> rollback,
        ChaosInjectionOptions? injection = null,
        CancellationToken cancellationToken = default);
}

public sealed class ChaosDependencyException : Exception
{
    public ChaosDependencyException(string message) : base(message)
    {
    }
}

public sealed class ChaosResilienceService : IChaosResilienceService
{
    private readonly object _sync = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _circuitOpenUntil;

    public async Task<ChaosExecutionResult<T>> ExecuteAsync<T>(
        ChaosScenario scenario,
        Func<CancellationToken, Task<T>> operation,
        Func<CancellationToken, Task<T>>? fallback = null,
        ChaosInjectionOptions? injection = null,
        CancellationToken cancellationToken = default)
    {
        if (IsCircuitOpen())
        {
            if (scenario == ChaosScenario.RedisFailure && fallback is not null)
            {
                var fallbackValue = await fallback(cancellationToken);
                return new ChaosExecutionResult<T>(
                    StatusCodes.Status200OK,
                    true,
                    true,
                    true,
                    0,
                    fallbackValue,
                    "Circuit open. Graceful degradation fallback used.");
            }

            return new ChaosExecutionResult<T>(
                StatusCodes.Status503ServiceUnavailable,
                false,
                false,
                true,
                0,
                default,
                "Circuit breaker is open.");
        }

        var retryAttempts = 0;
        var maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await ApplyLatencyIfConfiguredAsync(injection, cancellationToken);
                ThrowIfFaultInjected(scenario, injection);

                var value = await operation(cancellationToken);
                ResetCircuit();

                return new ChaosExecutionResult<T>(
                    StatusCodes.Status200OK,
                    true,
                    false,
                    false,
                    retryAttempts,
                    value,
                    "Success");
            }
            catch (ChaosDependencyException ex) when (scenario == ChaosScenario.RedisFailure && fallback is not null)
            {
                var fallbackValue = await fallback(cancellationToken);
                return new ChaosExecutionResult<T>(
                    StatusCodes.Status200OK,
                    true,
                    true,
                    false,
                    retryAttempts,
                    fallbackValue,
                    $"Graceful degradation applied: {ex.Message}");
            }
            catch (ChaosDependencyException ex)
            {
                retryAttempts++;

                if (attempt < maxAttempts)
                {
                    var backoff = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1));
                    await Task.Delay(backoff, cancellationToken);
                    continue;
                }

                RegisterFailure();

                return new ChaosExecutionResult<T>(
                    StatusCodes.Status503ServiceUnavailable,
                    false,
                    false,
                    IsCircuitOpen(),
                    retryAttempts,
                    default,
                    ex.Message);
            }
        }

        RegisterFailure();
        return new ChaosExecutionResult<T>(
            StatusCodes.Status503ServiceUnavailable,
            false,
            false,
            IsCircuitOpen(),
            retryAttempts,
            default,
            "Dependency failure.");
    }

    public async Task<ChaosExecutionResult<T>> ExecuteTransactionalAsync<T>(
        ChaosScenario scenario,
        Func<CancellationToken, Task<T>> operation,
        Func<CancellationToken, Task> rollback,
        ChaosInjectionOptions? injection = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(
            scenario,
            operation,
            fallback: null,
            injection,
            cancellationToken);

        if (!result.IsSuccess)
        {
            await rollback(cancellationToken);
        }

        return result;
    }

    private static async Task ApplyLatencyIfConfiguredAsync(ChaosInjectionOptions? injection, CancellationToken cancellationToken)
    {
        if (injection?.Enabled == true && injection.Scenario == ChaosScenario.HighLatency)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, injection.InjectLatencyMs)), cancellationToken);
        }
    }

    private static void ThrowIfFaultInjected(ChaosScenario scenario, ChaosInjectionOptions? injection)
    {
        if (injection?.Enabled != true || injection.Scenario != scenario)
        {
            return;
        }

        throw scenario switch
        {
            ChaosScenario.DatabaseFailure => new ChaosDependencyException("Database connection failed"),
            ChaosScenario.RedisFailure => new ChaosDependencyException("Redis cache unavailable"),
            ChaosScenario.NetworkPartition => new ChaosDependencyException("Network partition detected"),
            ChaosScenario.MessageQueueFailure => new ChaosDependencyException("Message queue unavailable"),
            _ => new ChaosDependencyException("Chaos fault injected")
        };
    }

    private void RegisterFailure()
    {
        lock (_sync)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= 3)
            {
                _circuitOpenUntil = DateTimeOffset.UtcNow.AddSeconds(30);
            }
        }
    }

    private void ResetCircuit()
    {
        lock (_sync)
        {
            _consecutiveFailures = 0;
            _circuitOpenUntil = null;
        }
    }

    private bool IsCircuitOpen()
    {
        lock (_sync)
        {
            if (_circuitOpenUntil is null)
            {
                return false;
            }

            if (_circuitOpenUntil <= DateTimeOffset.UtcNow)
            {
                _consecutiveFailures = 0;
                _circuitOpenUntil = null;
                return false;
            }

            return true;
        }
    }
}
