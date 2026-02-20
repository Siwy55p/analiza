using STSAnaliza.Models;

namespace STSAnaliza.Interfejs;

public interface IMatchPrefillBuilder
{
    Task<MatchPrefillResult> BuildAsync(MatchListItem match, CancellationToken ct, Action<string>? log = null);
}
