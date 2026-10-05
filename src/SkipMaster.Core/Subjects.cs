namespace SkipMaster.Core;

public static class Subjects
{
    public static readonly IReadOnlyList<Subject> All = Enum.GetValues<Subject>();

    public static int Count => All.Count;

    public static string DisplayName(this Subject subject) => subject switch
    {
        Subject.Calculus => "Матанализ",
        Subject.LinearAlgebra => "Линейная алгебра",
        Subject.DiscreteMath => "Дискретная математика",
        Subject.Programming => "Программирование",
        Subject.Physics => "Физика",
        Subject.English => "Английский язык",
        _ => throw new ArgumentOutOfRangeException(nameof(subject), subject, null)
    };
}
