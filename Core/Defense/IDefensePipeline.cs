namespace Core.Defense;

public interface IDefensePipeline
{
    ValueTask<DefenseResult> EvaluateAsync(
        DefenseContext context,
        CancellationToken cancellationToken = default);
}