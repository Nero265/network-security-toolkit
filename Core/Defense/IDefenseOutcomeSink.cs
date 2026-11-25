namespace Core.Defense;
/// <summary>
/// Told how a login attempt ended, after the credentials were checked.
/// Handlers decide before the check, sinks learn the result after it.
/// </summary>
public interface IDefenseOutcomeSink
{
    ValueTask RecordFailureAsync(DefenseContext context, CancellationToken cancellationToken = default);
    ValueTask RecordSuccessAsync(DefenseContext context, CancellationToken cancellationToken = default);

}