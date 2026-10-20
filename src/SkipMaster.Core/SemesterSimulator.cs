namespace SkipMaster.Core;

//что было за день, массивы по индексу предмета
public sealed record DayOutcome(int Day, bool[] Attended, bool[] Asked, int TotalPleasure, Subject? ExpelledOn);

public sealed record SemesterResult(
    int TotalPleasure,
    int DaysInSemester,
    int DaysPlayed,
    int SkippedClasses,
    bool Expelled,
    int? ExpelledOnDay,
    Subject? ExpelledOn)
{
    public double AveragePerDay => (double)TotalPleasure / DaysInSemester;
}

//гоняет семестр: спросил стратегию, применил правила, посчитал
public sealed class SemesterSimulator
{
    public const int DefaultDays = 100;

    private readonly IReadOnlyList<ITeacherRule> _rules;
    private readonly int _days;

    public SemesterSimulator(IReadOnlyList<ITeacherRule> rules, int days = DefaultDays)
    {
        if (rules.Count != Subjects.Count)
            throw new ArgumentException($"Нужно ровно {Subjects.Count} правил — по одному на предмет.", nameof(rules));

        _rules = rules;
        _days = days;
    }

    public SemesterResult Run(ISkipStrategy strategy, Action<DayOutcome>? onDayCompleted = null)
    {
        var history = new StudentHistory();
        var yesterday = new bool[Subjects.Count]; //в первый день вчера никого не спрашивали
        int pleasure = 0;
        int skipped = 0;

        for (int day = 1; day <= _days; day++)
        {
            bool[] attend = strategy.DecideDay(day, history);
            if (attend is null || attend.Length != Subjects.Count)
                throw new InvalidOperationException($"Стратегия «{strategy.Name}» должна вернуть массив из {Subjects.Count} решений.");
            //преподы решают спрашивать ли
            var asked = new bool[Subjects.Count];
            for (int i = 0; i < asked.Length; i++)
                asked[i] = _rules[i].ShouldAsk(yesterday);
            //проверка на вылет
            for (int i = 0; i < asked.Length; i++)
            {
                if (attend[i] || !asked[i])
                    continue;

                //спросили на прогуле — вылет, всё сгорает
                var subject = (Subject)i;
                onDayCompleted?.Invoke(new DayOutcome(day, attend, asked, 0, subject));
                return new SemesterResult(0, _days, day, skipped, true, day, subject);
            }

            history.Record(day, attend, asked);
            int skippedToday = attend.Count(a => !a);
            skipped += skippedToday;
            pleasure += 1 + skippedToday; //пирожок + прогулы
            yesterday = asked;

            onDayCompleted?.Invoke(new DayOutcome(day, attend, asked, pleasure, null));
        }

        return new SemesterResult(pleasure, _days, _days, skipped, false, null, null);
    }
}
