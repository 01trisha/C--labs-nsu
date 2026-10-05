namespace SkipMaster.Core;

/// <summary>
/// История студента. Хранит полную правду о днях, но наружу отдаёт только то,
/// что студент мог увидеть: исход пары известен, только если он на ней был.
/// </summary>
public sealed class StudentHistory : IReadOnlyStudentHistory
{
    private readonly Dictionary<int, (bool[] Attended, bool[] Asked)> _days = new();

    public void Record(int day, bool[] attended, bool[] asked) =>
        _days[day] = ((bool[])attended.Clone(), (bool[])asked.Clone());

    public bool Attended(int day, Subject subject) =>
        _days.TryGetValue(day, out var record) && record.Attended[(int)subject];

    public bool? WasAsked(int day, Subject subject) =>
        _days.TryGetValue(day, out var record) && record.Attended[(int)subject]
            ? record.Asked[(int)subject]
            : null;
}
