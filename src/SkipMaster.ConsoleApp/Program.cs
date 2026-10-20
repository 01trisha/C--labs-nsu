using SkipMaster.Core;
using SkipMaster.Strategy;

//лаба 1, запуск: dotnet run --project src/SkipMaster.ConsoleApp -- [--seed n] [--verbose]

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
            Console.Error.WriteLine($"unknown arg: {args[i]}");
            Console.Error.WriteLine("using: SkipMaster.ConsoleApp [--seed n] [--verbose]");
            return 1;
    }
}

var random = new Random(seed);
ITeacherRule[] rules = TeacherRuleFactory.CreateForAllSubjects(random);
ISkipStrategy strategy = new BayesianSkipStrategy();

Console.WriteLine($"seed: {seed}");
Console.WriteLine($"стратегия: {strategy.Name}");
Console.WriteLine("правила преподавателей (студент их не знает):");
foreach (var subject in Subjects.All)
    Console.WriteLine($"  {subject.DisplayName(),-22} {rules[(int)subject].Description}");
Console.WriteLine();

Action<DayOutcome>? log = verbose ? PrintDay : null;
SemesterResult result = new SemesterSimulator(rules).Run(strategy, log);

if (verbose)
    Console.WriteLine();

if (result.Expelled)
    Console.WriteLine($"ВЫЛЕТ на дне {result.ExpelledOnDay}: спросили по «{result.ExpelledOn!.Value.DisplayName()}» на прогуле");

Console.WriteLine($"прогуляно пар: {result.SkippedClasses}");
Console.WriteLine($"общее удовольствие: {result.TotalPleasure}");
Console.WriteLine($"среднее удовольствие/день: {result.AveragePerDay:F2}");
return 0;

static void PrintDay(DayOutcome day)
{
    //. был   ? был и спросили   - прогулял   ! прогулял и спросили
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
