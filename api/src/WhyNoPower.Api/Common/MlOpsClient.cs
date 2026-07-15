namespace WhyNoPower.Api.Common;

public interface IMlOpsClient
{
    Task<bool> PingHealthAsync(CancellationToken ct = default);
}

/// <summary>
/// Ops-only surface (retrain / recompute / health) — never on the user request path
/// (system-design.md §3, principle 2; ADR-007 candidate). Resilience (timeout/retry/circuit
/// breaker) is added via Polly in Program.cs's HttpClient registration, not here.
/// </summary>
public class MlOpsClient : IMlOpsClient
{
    private readonly HttpClient _http;
    public MlOpsClient(HttpClient http) => _http = http;

    public async Task<bool> PingHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("/healthz", ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
