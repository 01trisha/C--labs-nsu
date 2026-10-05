using SkipMaster.Core;

namespace SkipMaster.Strategy;

/// <summary>
/// Байесовская стратегия «Мастер прогулов».
/// Для каждого преподавателя держит 22 гипотезы о его правиле, отбрасывает те, что противоречат
/// истории, и прогуливает пару, только когда вероятность вопроса настолько мала, что
/// риск потерять всё накопленное не перевешивает +1 к удовольствию. Подробно — docs/strategy.md.
/// </summary>
public sealed class BayesianSkipStrategy : ISkipStrategy
{
    private const int SubjectCount = 6;
    private const int SemesterDays = 100;
    private const double RandomRulePrior = 1.0 / 3;
    // Оценка среднего удовольствия за будущий день. Это неподвижная точка самой стратегии:
    // в среднем она набирает ≈346 за семестр, то есть ≈3.5 в день. Подробнее — docs/strategy.md.
    private const double ExpectedDailyPleasure = 3.5;

    /// <summary>Детерминированная гипотеза: «как вчера A» (B &lt; 0) или «вчера A xor B».</summary>
    private readonly record struct Hypothesis(int A, int B, double Prior)
    {
        public bool Predict(bool[] yesterday) => B < 0 ? yesterday[A] : yesterday[A] != yesterday[B];
    }

    // 6 гипотез правила 2 и 15 гипотез правила 3 (xor симметричен, поэтому пары неупорядоченные).
    // Априорно каждый тип правила равновероятен, A и Б — равновероятны.
    private static readonly Hypothesis[] Hypotheses = BuildHypotheses();

    private readonly bool[][] _alive = new bool[SubjectCount][];
    private bool[] _yesterday = new bool[SubjectCount];
    private int? _lastDay;
    private int _daysObserved;
    private int _pleasure;

    public BayesianSkipStrategy() => Reset();

    public string Name => "Bayesian Skipper";

    public bool[] DecideDay(int day, IReadOnlyStudentHistory history)
    {
        // Номер дня не вырос — значит, начался новый семестр (раннер переиспользует экземпляр).
        if (_lastDay is not int lastDay || day <= lastDay)
            Reset();
        else
            Observe(lastDay, history);

        _lastDay = day;
        return ChooseAttendance();
    }

    private void Reset()
    {
        for (int i = 0; i < SubjectCount; i++)
            _alive[i] = Enumerable.Repeat(true, Hypotheses.Length).ToArray();

        _yesterday = new bool[SubjectCount];
        _lastDay = null;
        _daysObserved = 0;
        _pleasure = 0;
    }

    /// <summary>
    /// Восстанавливает, кого спросили в прошлый день. Если был на паре — исход виден в истории.
    /// Если прогулял — раз нас всё ещё спрашивают о решении, значит, не вылетели, то есть не спросили.
    /// Поэтому полное состояние каждого дня известно всегда.
    /// </summary>
    private void Observe(int day, IReadOnlyStudentHistory history)
    {
        var asked = new bool[SubjectCount];
        int skipped = 0;
        for (int i = 0; i < SubjectCount; i++)
        {
            if (history.Attended(day, (Subject)i))
                asked[i] = history.WasAsked(day, (Subject)i) == true;
            else
                skipped++;
        }

        for (int teacher = 0; teacher < SubjectCount; teacher++)
        {
            for (int h = 0; h < Hypotheses.Length; h++)
            {
                if (_alive[teacher][h] && Hypotheses[h].Predict(_yesterday) != asked[teacher])
                    _alive[teacher][h] = false;
            }
        }

        _daysObserved++;
        _pleasure += 1 + skipped;
        _yesterday = asked;
    }

    private bool[] ChooseAttendance()
    {
        var risk = new double[SubjectCount];
        for (int i = 0; i < SubjectCount; i++)
            risk[i] = AskProbability(i);

        // На кону: уже накопленное + сегодняшний пирожок + ожидаемое за оставшиеся дни.
        int daysLeftAfterToday = Math.Max(0, SemesterDays - _daysObserved - 1);
        double stake = _pleasure + 1 + daysLeftAfterToday * ExpectedDailyPleasure;

        // Прогуливаем k самых безопасных пар, где k максимизирует ожидание:
        // P(ни на одной из k не спросят) * (stake + k).
        int[] order = Enumerable.Range(0, SubjectCount).OrderBy(i => risk[i]).ToArray();
        int bestSkips = 0;
        double bestValue = stake;
        double survival = 1;
        for (int k = 1; k <= SubjectCount; k++)
        {
            survival *= 1 - risk[order[k - 1]];
            double value = survival * (stake + k);
            if (value > bestValue)
            {
                bestValue = value;
                bestSkips = k;
            }
        }

        var attend = Enumerable.Repeat(true, SubjectCount).ToArray();
        for (int k = 0; k < bestSkips; k++)
            attend[order[k]] = false;
        return attend;
    }

    /// <summary>
    /// Апостериорная вероятность, что преподаватель спросит сегодня.
    /// Случайное правило объясняет любую историю из n дней с вероятностью 2^-n,
    /// любая выжившая детерминированная гипотеза — с вероятностью 1.
    /// </summary>
    private double AskProbability(int teacher)
    {
        double randomWeight = RandomRulePrior * Math.Pow(0.5, _daysObserved);
        double total = randomWeight;
        double asked = randomWeight * 0.5;

        for (int h = 0; h < Hypotheses.Length; h++)
        {
            if (!_alive[teacher][h])
                continue;

            total += Hypotheses[h].Prior;
            if (Hypotheses[h].Predict(_yesterday))
                asked += Hypotheses[h].Prior;
        }

        return asked / total;
    }

    private static Hypothesis[] BuildHypotheses()
    {
        var result = new List<Hypothesis>();
        for (int a = 0; a < SubjectCount; a++)
            result.Add(new Hypothesis(a, -1, 1.0 / 3 / SubjectCount));

        int pairs = SubjectCount * (SubjectCount - 1) / 2;
        for (int a = 0; a < SubjectCount; a++)
            for (int b = a + 1; b < SubjectCount; b++)
                result.Add(new Hypothesis(a, b, 1.0 / 3 / pairs));

        return result.ToArray();
    }
}
