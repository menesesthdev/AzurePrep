using AzurePrep.Application.Abstractions;

namespace AzurePrep.Infrastructure.Time;

/// <summary>Relógio real baseado em UTC.</summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
