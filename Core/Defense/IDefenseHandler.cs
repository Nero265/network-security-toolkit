namespace Core.Defense;

public interface IDefenseHandler
{
    ValueTask<DefenseResult> HandleAsync(
        DefenseContext context,
        DefenseDelegate next,
        CancellationToken cancellationToken = default);
}