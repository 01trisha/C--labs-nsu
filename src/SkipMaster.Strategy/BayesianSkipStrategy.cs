using SkipMaster.Core;

namespace SkipMaster.Strategy;

//угадываем правило каждого препода и прогуливаем, только когда риск того стоит
public sealed class BayesianSkipStrategy : ISkipStrategy
{
    private const int SubjectCount = 6;
    private const int SemesterDays = 100;
    private const double RandomRulePrior = 1.0 / 3;
    //примерно столько стратегия набирает в день, см. docs/strategy.md
    private const double ExpectedDailyPleasure = 3.5;

    //b < 0 — «как вчера a», иначе «a xor b»
    private readonly record struct Hypothesis(int A, int B, double Prior)
    {
        public bool Predict(bool[] yesterday) => B < 0 ? yesterday[A] : yesterday[A] != yesterday[B];
    }

    //6 вариантов правила 2 и 15 пар для правила 3
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
        //день не вырос — значит, новый семестр
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

    //на прогуле точно не спросили, раз мы ещё не вылетели
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

        //сколько потеряем при вылете
        int daysLeftAfterToday = Math.Max(0, SemesterDays - _daysObserved - 1);
        double stake = _pleasure + 1 + daysLeftAfterToday * ExpectedDailyPleasure;

        //ищем, сколько самых безопасных пар выгодно прогулять
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

    //шанс, что препод спросит сегодня, по выжившим гипотезам
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
