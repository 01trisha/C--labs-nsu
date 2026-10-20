namespace SkipMaster.Core;

//как препод решает, спросить сегодня или нет
public interface ITeacherRule
{
    //yesterday[i] — спросили ли вчера по предмету i
    bool ShouldAsk(IReadOnlyList<bool> yesterday);

    string Description { get; }
}

//правило 1: монетка 50/50
public sealed class RandomRule(Random random) : ITeacherRule
{
    public bool ShouldAsk(IReadOnlyList<bool> yesterday) => random.Next(2) == 0;

    public string Description => "случайно, 50%";
}

//правило 2: спрашивает, если вчера спросили по a
public sealed class FollowRule(Subject a) : ITeacherRule
{
    public Subject A => a;

    public bool ShouldAsk(IReadOnlyList<bool> yesterday) => yesterday[(int)a];

    public string Description => $"если вчера спросили по «{a.DisplayName()}»";
}

//правило 3: спрашивает, если вчера спросили ровно по одному из a и б
public sealed class XorRule(Subject a, Subject b) : ITeacherRule
{
    public Subject A => a;
    public Subject B => b;

    public bool ShouldAsk(IReadOnlyList<bool> yesterday) => yesterday[(int)a] != yesterday[(int)b];

    public string Description => $"если вчера спросили ровно по одному из «{a.DisplayName()}» и «{b.DisplayName()}»";
}

public static class TeacherRuleFactory
{
    //случайный тип правила, a и б любые, но разные
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
