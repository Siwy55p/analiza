using STSAnaliza.Models;

namespace STSAnaliza.Interfejs;

/// <summary>
/// Licznik requestów do Sportradar (globalnie + możliwość delta per fragment pracy, np. per mecz).
/// Handler HTTP woła Track(...), UI/serwisy mogą brać Snapshot()/DeltaSince(...).
/// </summary>
public interface ISportradarRequestMeter
{
    SportradarRequestSnapshot Snapshot();

    SportradarRequestDelta DeltaSince(SportradarRequestSnapshot start, int topN = 5);

    void Track(string endpoint, int statusCode, long elapsedMs);
}
