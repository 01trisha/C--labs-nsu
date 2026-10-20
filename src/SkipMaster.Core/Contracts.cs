namespace SkipMaster.Core;

//контракты из условия конкурса, не трогаем

public enum Subject
{
    Calculus,           //матан
    LinearAlgebra,      //линал
    DiscreteMath,       //дискретка
    Programming,        //прога
    Physics,            //физика
    English             //английский
}

public interface ISkipStrategy
{
    string Name { get; }

    //true — иду на пару, false — прогуливаю
    bool[] DecideDay(int day, IReadOnlyStudentHistory history);
}

public interface IReadOnlyStudentHistory
{
    //был ли на паре в этот день
    bool Attended(int day, Subject subject);

    //спросили ли — видно только если был на паре, иначе null
    bool? WasAsked(int day, Subject subject);
}
