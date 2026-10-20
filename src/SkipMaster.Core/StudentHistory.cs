namespace SkipMaster.Core;

//хранит всё, но отдаёт только то, что студент реально видел
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
