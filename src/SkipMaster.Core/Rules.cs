namespace SkipMaster.Core;

/// <summary>Правило, по которому преподаватель решает, спрашивать ли студента сегодня.</summary>
public interface ITeacherRule
{
    /// <param name="yesterday">Кого спросили вчера, индекс — (int)Subject.</param>
    bool ShouldAsk(IReadOnlyList<bool> yesterday);

    string Description { get; }
}

/// <summary>Правило 1: спросить с вероятностью 50%.</summary>
public sealed class RandomRule(Random random) : ITeacherRule
{
    public bool ShouldAsk(IReadOnlyList<bool> yesterday) => random.Next(2) == 0;

    public string Description => "случайно, 50%";
}

/// <summary>Правило 2: спросить, если вчера спросили по предмету A.</summary>
public sealed class FollowRule(Subject a) : ITeacherRule
{
    public Subject A => a;

    public bool ShouldAsk(IReadOnlyList<bool> yesterday) => yesterday[(int)a];

    public string Description => $"если вчера спросили по «{a.DisplayName()}»";
}

/// <summary>Правило 3: спросить, если вчера спросили ровно по одному из предметов A и Б.</summary>
public sealed class XorRule(Subject a, Subject b) : ITeacherRule
{
    public Subject A => a;
    public Subject B => b;

    public bool ShouldAsk(IReadOnlyList<bool> yesterday) => yesterday[(int)a] != yesterday[(int)b];

    public string Description => $"если вчера спросили ровно по одному из «{a.DisplayName()}» и «{b.DisplayName()}»";
}

public static class TeacherRuleFactory
{
    /// <summary>
    /// Случайное правило: тип равновероятно из трёх, A и Б — любые предметы (в том числе свой), A ≠ Б.
    /// </summary>
    public static ITeacherRule CreateRandom(Random random)
    {
        int kind = random.Next(3);
        var a = (Subject)random.Next(Subjects.Count);
        var b = (Subject)random.Next(Subjects.Count - 1);
        if (b >= a)
            b++;

        return kind switch
        {
            0 => new RandomRule(random),
            1 => new FollowRule(a),
            _ => new XorRule(a, b)
        };
    }

    public static ITeacherRule[] CreateForAllSubjects(Random random) =>
        Subjects.All.Select(_ => CreateRandom(random)).ToArray();
}
