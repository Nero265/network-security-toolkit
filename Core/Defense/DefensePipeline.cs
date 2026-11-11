namespace Core.Defense;

internal sealed class DefensePipeline(DefenseDelegate entry): IDefensePipeline
{
    public ValueTask<DefenseResult> EvaluateAsync(DefenseContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return entry(context, cancellationToken);
    }
}