// Контракты конкурса стратегий — дословно из task.md, в глобальном пространстве имён, как в условии.
// Заменяют контракты общего раннера; менять нельзя.

public enum Subject
{
    Calculus,           // Матанализ
    LinearAlgebra,      // Линейная алгебра
    DiscreteMath,       // Дискретная математика
    Programming,        // Программирование
    Physics,            // Физика
    English             // Английский язык
}

public interface ISkipStrategy
{
    string Name { get; }

    /// <summary>
    /// Решение на день: для каждого предмета — идти на пару (true) или прогулять (false).
    /// Вызывается один раз в начале каждого дня, до того как станут известны сегодняшние исходы.
    /// </summary>
    bool[] DecideDay(int day, IReadOnlyStudentHistory history);
}

public interface IReadOnlyStudentHistory
{
    /// <summary>Был ли студент на паре по предмету в указанный день.</summary>
    bool Attended(int day, Subject subject);

    /// <summary>
    /// Спросили ли по предмету в указанный день — известно только если Attended(day, subject) == true.
    /// Если информации еще нет то null
    /// </summary>
    bool? WasAsked(int day, Subject subject);
}
