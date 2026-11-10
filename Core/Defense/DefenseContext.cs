using System.Net;

namespace Core.Defense;

public sealed record DefenseContext(string Username, IPAddress? IpAddress);