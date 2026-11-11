namespace Core.Defense;

public sealed class DefensePipelineBuilder
{
    private readonly List<IDefenseHandler> _handlers = [];

    public DefensePipelineBuilder Add(IDefenseHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers.Add(handler);
        return this;
    }

    public IDefensePipeline Build()
    {
        DefenseDelegate next = static (_, _) => ValueTask.FromResult(DefenseResult.Allowed);

        for (var i = _handlers.Count - 1; i >= 0; i--)
        {
            var handler = _handlers[i];
            var following = next;
            next = (context, ct) => handler.HandleAsync(context, following, ct);
        }

        return new DefensePipeline(next);
    }
}