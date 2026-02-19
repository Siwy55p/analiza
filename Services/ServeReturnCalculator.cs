using System.Globalization;
using STSAnaliza.Services.SportradarDtos;

namespace STSAnaliza.Services;

public sealed record ServeReturnMetrics(
    double? HoldPct,
    double? FirstWonPct,
    double? SecondWonPct,
    double? BreakPct,
    int BreakpointsWon,
    int TotalBreakpoints
);

public static class ServeReturnCalculator
{
    public struct Agg
    {
        public long HoldWon, HoldPlayed;
        public long FirstWon, FirstIn;
        public long SecondWon, SecondIn;
        public long BreakWon, BreakCh;
        public int MatchesUsed;
    }

    public static void AddMatch(ref Agg agg, CompetitorStatisticsDto me, CompetitorStatisticsDto opp)
    {
        // hold%: service_games_won / service_games_played
        // service_games_played = my_service_games_won + opponent_return_games_won
        // opponent_return_games_won = opp.games_won - opp.service_games_won
        if (me.ServiceGamesWon is int myHold &&
            opp.GamesWon is int oppGamesWon &&
            opp.ServiceGamesWon is int oppHold &&
            oppGamesWon >= oppHold)
        {
            var oppReturnGamesWon = oppGamesWon - oppHold;
            var myServicePlayed = myHold + oppReturnGamesWon;

            if (myServicePlayed > 0)
            {
                agg.HoldWon += myHold;
                agg.HoldPlayed += myServicePlayed;
            }
        }

        if (me.FirstServePointsWon is int fWon && me.FirstServeSuccessful is int fIn && fIn > 0)
        {
            agg.FirstWon += fWon;
            agg.FirstIn += fIn;
        }

        if (me.SecondServePointsWon is int sWon && me.SecondServeSuccessful is int sIn && sIn > 0)
        {
            agg.SecondWon += sWon;
            agg.SecondIn += sIn;
        }

        if (me.BreakpointsWon is int bWon && me.TotalBreakpoints is int bCh && bCh > 0)
        {
            agg.BreakWon += bWon;
            agg.BreakCh += bCh;
        }

        agg.MatchesUsed++;
    }

    public static ServeReturnMetrics Finalize(Agg agg)
    {
        double? P(long num, long den) => den > 0 ? (double)num / den : null;

        return new ServeReturnMetrics(
            HoldPct: P(agg.HoldWon, agg.HoldPlayed),
            FirstWonPct: P(agg.FirstWon, agg.FirstIn),
            SecondWonPct: P(agg.SecondWon, agg.SecondIn),
            BreakPct: P(agg.BreakWon, agg.BreakCh),
            BreakpointsWon: (int)agg.BreakWon,
            TotalBreakpoints: (int)agg.BreakCh
        );
    }

    public static (string Fill11_3_Service, string Fill11_4_Return) BuildPlaceholders(
        string playerAName, string playerBName,
        ServeReturnMetrics a, ServeReturnMetrics b)
    {
        string Pct(double? x) => x is null ? "brak" : (x.Value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        var fill11_3 =
            $"{playerAName}: hold {Pct(a.HoldPct)}, 1st won {Pct(a.FirstWonPct)}, 2nd won {Pct(a.SecondWonPct)}\n" +
            $"{playerBName}: hold {Pct(b.HoldPct)}, 1st won {Pct(b.FirstWonPct)}, 2nd won {Pct(b.SecondWonPct)}";

        string BreakLine(string name, ServeReturnMetrics m)
        {
            if (m.BreakPct is null || m.TotalBreakpoints <= 0)
                return $"{name}: break brak";

            return $"{name}: break {Pct(m.BreakPct)} ({m.BreakpointsWon}/{m.TotalBreakpoints})";
        }

        var fill11_4 = BreakLine(playerAName, a) + "\n" + BreakLine(playerBName, b);

        return (fill11_3, fill11_4);
    }
}
