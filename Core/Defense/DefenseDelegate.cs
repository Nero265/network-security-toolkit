namespace Core.Defense;

public delegate ValueTask<DefenseResult> DefenseDelegate(DefenseContext context, CancellationToken cancellationToken);