namespace STSAnaliza
{
    public static class MatchListStepDefaults
    {
        public static List<StepDefinition> Create()
        {
            return new List<StepDefinition>
            {
                new StepDefinition
                {
                    Order = 1,
                    Enabled = true,
                    KursBuch = false,
                    Title = "1) P_est bez kursów (wstępnie)",
                    Prompt =
@"Uzupełnij WYŁĄCZNIE sekcję 'P_est' w tekście.
Nie używaj kursów (ignoruj je, jeśli są).
Zwróć CAŁY tekst wynikowy."
                },
                new StepDefinition
                {
                    Order = 2,
                    Enabled = true,
                    KursBuch = false,
                    Title = "2) Dane o zawodniczkach (forma/staty/kontuzje)",
                    Prompt =
@"Uzupełnij WYŁĄCZNIE sekcję 'Dane' / 'Forma/Statystyki/Kontuzje'.
Nie używaj kursów.
Zwróć CAŁY tekst wynikowy."
                },
                new StepDefinition
                {
                    Order = 3,
                    Enabled = true,
                    KursBuch = false,
                    Title = "3) Typ: kto wygra + % (na podstawie danych)",
                    Prompt =
@"Uzupełnij WYŁĄCZNIE sekcję 'Typ'.
Podaj zwycięzcę i procent (Twoje P_est po danych).
Nie używaj kursów.
Zwróć CAŁY tekst wynikowy."
                },
                new StepDefinition
                {
                    Order = 4,
                    Enabled = true,
                    KursBuch = true,
                    Title = "4) Porównanie do kursów (P_imp / edge)",
                    Prompt =
@"Uzupełnij WYŁĄCZNIE sekcję 'Kursy -> P_imp -> Edge'.
Policz:
P_imp = 1/kurs
Edge = P_est - P_imp
Zwróć CAŁY tekst wynikowy."
                },
                new StepDefinition
                {
                    Order = 5,
                    Enabled = true,
                    KursBuch = true,
                    Title = "5) Podsumowanie",
                    Prompt =
@"Uzupełnij WYŁĄCZNIE sekcję 'Podsumowanie' (2-4 zdania).
Możesz odnieść się do edge.
Zwróć CAŁY tekst wynikowy."
                }
            };
        }
    }
}
