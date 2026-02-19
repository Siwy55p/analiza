using System.Collections.Generic;

namespace STSAnaliza
{
    /// <summary>
    /// Fabryka domyślnej konfiguracji kroków dla pipeline analizującego listę meczów (zakładka 2).
    /// </summary>
    /// <remarks>
    /// Domyślne kroki są zapisywane do pliku JSON przy pierwszym uruchomieniu (gdy brak konfiguracji),
    /// a następnie mogą być edytowane przez użytkownika w oknie opcji kroków.
    /// </remarks>
    public static class MatchListStepDefaults
    {
        /// <summary>
        /// Tworzy listę domyślnych kroków dla zakładki 2.
        /// </summary>
        /// <returns>Lista kroków w domyślnej kolejności wykonywania.</returns>
        /// <remarks>
        /// Pole <see cref="StepDefinition.RequiresMarkets"/> w tym pipeline oznacza:
        /// - <see langword="false"/>: krok nie powinien widzieć kursów (anti-anchoring),
        /// - <see langword="true"/>: krok może korzystać z kursów (np. do wyliczenia P_imp/Edge).
        /// </remarks>
        public static List<StepDefinition> Create()
        {
            return new List<StepDefinition>
            {
                new StepDefinition
                {
                    Order = 1,
                    Enabled = true,
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
