using System;
using System.Collections.Generic;
using System.Linq;

namespace SkipMaster.Strategy;

/// <summary>
/// Байесовская стратегия «Мастер прогулов».
/// <para>
/// Для каждого преподавателя держит 22 гипотезы о его правиле: случайное 50%, «как вчера A» (6 штук)
/// и «вчера A xor Б» (15 неупорядоченных пар). После каждого дня вычёркивает детерминированные гипотезы,
/// которые противоречат истории; правдоподобие случайного правила за n дней равно 2^-n.
/// </para>
/// <para>
/// Полное состояние каждого прошедшего дня известно: на посещённой паре исход виден в истории,
/// а прогулянная пара, раз студент всё ещё в игре, точно прошла без вопроса.
/// </para>
/// <para>
/// Пару прогуливает, только когда вероятность вопроса так мала, что риск потерять всё накопленное
/// не перевешивает +1 к удовольствию: выбирает k самых безопасных пар, максимизируя
/// P(ни на одной не спросят) * (на кону + k).
/// </para>
/// </summary>
public sealed class BayesianSkipStrategy : ISkipStrategy
{
    private const int SubjectCount = 6;
    private const int SemesterDays = 100;
    private const double RandomRulePrior = 1.0 / 3;
    // Оценка среднего удовольствия за будущий день. Это неподвижная точка самой стратегии:
    // в среднем она набирает ≈346 за семестр, то есть ≈3.5 в день.
    private const double ExpectedDailyPleasure = 3.5;

    // Детерминированные гипотезы: «как вчера A» (B < 0) или «вчера A xor B».
    // Априорно каждый тип правила равновероятен, A и Б — равновероятны.
    private static readonly int[] HypothesisA;
    private static readonly int[] HypothesisB;
    private static readonly double[] HypothesisPrior;

    private readonly bool[][] _alive = new bool[SubjectCount][];
    private bool[] _yesterday = new bool[SubjectCount];
    private int? _lastDay;
    private int _daysObserved;
    private int _pleasure;

    static BayesianSkipStrategy()
    {
        var a = new List<int>();
        var b = new List<int>();
        var prior = new List<double>();

        for (int i = 0; i < SubjectCount; i++)
        {
            a.Add(i);
            b.Add(-1);
            prior.Add(1.0 / 3 / SubjectCount);
        }

        int pairs = SubjectCount * (SubjectCount - 1) / 2;
        for (int i = 0; i < SubjectCount; i++)
        {
            for (int j = i + 1; j < SubjectCount; j++)
            {
                a.Add(i);
                b.Add(j);
                prior.Add(1.0 / 3 / pairs);
            }
        }

        HypothesisA = a.ToArray();
        HypothesisB = b.ToArray();
        HypothesisPrior = prior.ToArray();
    }

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
            _alive[i] = Enumerable.Repeat(true, HypothesisPrior.Length).ToArray();

        _yesterday = new bool[SubjectCount];
        _lastDay = null;
        _daysObserved = 0;
        _pleasure = 0;
    }

    /// <summary>
    /// Восстанавливает, кого спросили в прошлый день. Если был на паре — исход виден в истории.
    /// Если прогулял — раз нас всё ещё спрашивают о решении, значит, не вылетели, то есть не спросили.
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
            for (int h = 0; h < HypothesisPrior.Length; h++)
            {
                if (_alive[teacher][h] && Predict(h, _yesterday) != asked[teacher])
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

        for (int h = 0; h < HypothesisPrior.Length; h++)
        {
            if (!_alive[teacher][h])
                continue;

            total += HypothesisPrior[h];
            if (Predict(h, _yesterday))
                asked += HypothesisPrior[h];
        }

        return asked / total;
    }

    private static bool Predict(int hypothesis, bool[] yesterday)
    {
        int a = HypothesisA[hypothesis];
        int b = HypothesisB[hypothesis];
        return b < 0 ? yesterday[a] : yesterday[a] != yesterday[b];
    }
}
