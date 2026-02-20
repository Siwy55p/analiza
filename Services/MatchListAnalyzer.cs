using STSAnaliza.Interfejs;
using STSAnaliza.Models;

namespace STSAnaliza.Services;

public sealed class MatchListAnalyzer : IMatchListAnalyzer
{
    private readonly MatchListPipeline _pipeline;
    private readonly IMatchPrefillBuilder _prefill;
    private readonly ISportradarRequestMeter _srMeter;

    // domyślny timeout per mecz – tak jak było w Form1
    private static readonly TimeSpan PerMatchTimeout = TimeSpan.FromMinutes(60);

    public MatchListAnalyzer(
        MatchListPipeline pipeline,
        IMatchPrefillBuilder prefill,
        ISportradarRequestMeter srMeter)
    {
        _pipeline = pipeline;
        _prefill = prefill;
        _srMeter = srMeter;
    }

    public async Task<string?> AnalyzeOneAsync(
        MatchListItem match,
        Func<CancellationToken, Task<string>> waitUserMessageAsync,
        Action<string> onChat,
        Action<int, int, string> onStep,
        Action<string> log,
        CancellationToken ct)
    {
        if (match is null) throw new ArgumentNullException(nameof(match));
        if (waitUserMessageAsync is null) throw new ArgumentNullException(nameof(waitUserMessageAsync));
        if (onChat is null) throw new ArgumentNullException(nameof(onChat));
        if (onStep is null) throw new ArgumentNullException(nameof(onStep));
        if (log is null) throw new ArgumentNullException(nameof(log));

        ct.ThrowIfCancellationRequested();

        var srStart = _srMeter.Snapshot();

        using var perMatchCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        perMatchCts.CancelAfter(PerMatchTimeout);
        var perCt = perMatchCts.Token;

        try
        {
            // Prefill (AUTO) – logowanie idzie do UI przez callback
            var prefill = await _prefill.BuildAsync(match, perCt, log);

            var res = await _pipeline.AnalyzeAsyncInteractive(
                match,
                waitUserMessageAsync: waitUserMessageAsync,
                onChat: onChat,
                onStep: onStep,
                prefilled: prefill.Prefilled,
                ct: perCt);

            return res;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // timeout per mecz
            log("  [PRZERWANO] Timeout lub anulowano.");
            return null;
        }
        catch (OperationCanceledException)
        {
            // globalny cancel -> przerywamy całą analizę
            throw;
        }
        catch (Exception ex)
        {
            log($"  [BŁĄD] {ex.Message}");
            return null;
        }
        finally
        {
            var delta = _srMeter.DeltaSince(srStart, topN: 5);
            log($"  [SR] {delta.ToLogLine()}");
        }
    }
}
