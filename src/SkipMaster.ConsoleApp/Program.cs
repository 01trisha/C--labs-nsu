using SkipMaster.Core;
using SkipMaster.Strategy;

// Лаба 1: один семестр (100 дней) для одной стратегии.
// Запуск: dotnet run --project src/SkipMaster.ConsoleApp -- [--seed N] [--verbose]

int seed = Random.Shared.Next();
bool verbose = false;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--seed" when i + 1 < args.Length && int.TryParse(args[i + 1], out int parsed):
            seed = parsed;
            i++;
            break;
        case "--verbose" or "-v":
            verbose = true;
            break;
        default:
            Console.Error.WriteLine($"Неизвестный аргумент: {args[i]}");
            Console.Error.WriteLine("Использование: SkipMaster.ConsoleApp [--seed N] [--verbose]");
            return 1;
    }
}

var random = new Random(seed);
ITeacherRule[] rules = TeacherRuleFactory.CreateForAllSubjects(random);
ISkipStrategy strategy = new BayesianSkipStrategy();

Console.WriteLine($"Seed: {seed}");
Console.WriteLine($"Стратегия: {strategy.Name}");
Console.WriteLine("Правила преподавателей (студент их не знает):");
foreach (var subject in Subjects.All)
    Console.WriteLine($"  {subject.DisplayName(),-22} {rules[(int)subject].Description}");
Console.WriteLine();

Action<DayOutcome>? log = verbose ? PrintDay : null;
SemesterResult result = new SemesterSimulator(rules).Run(strategy, log);

if (verbose)
    Console.WriteLine();

if (result.Expelled)
    Console.WriteLine($"ВЫЛЕТ на дне {result.ExpelledOnDay}: спросили по «{result.ExpelledOn!.Value.DisplayName()}» на прогуле.");

Console.WriteLine($"Прогуляно пар:             {result.SkippedClasses}");
Console.WriteLine($"Общее удовольствие:        {result.TotalPleasure}");
Console.WriteLine($"Среднее удовольствие/день: {result.AveragePerDay:F2}");
return 0;

static void PrintDay(DayOutcome day)
{
    // По символу на предмет в порядке enum Subject: '.' — был, не спросили; '?' — был, спросили;
    // '-' — прогулял, не спросили; '!' — прогулял, спросили (вылет).
    var marks = new char[Subjects.Count];
    for (int i = 0; i < marks.Length; i++)
        marks[i] = (day.Attended[i], day.Asked[i]) switch
        {
            (true, false) => '.',
            (true, true) => '?',
            (false, false) => '-',
            (false, true) => '!'
        };

    Console.WriteLine($"День {day.Day,3}: [{new string(marks)}]  удовольствие {day.TotalPleasure,3}");
}
