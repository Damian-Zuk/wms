namespace Wms.Application.HandlingUnits;

/// <summary>
/// Issues the next license-plate code (e.g. "HU-000123") for a handling unit
/// when the caller doesn't supply one.
/// </summary>
public interface IHandlingUnitCodeGenerator
{
    Task<string> NextCodeAsync(CancellationToken cancellationToken);
}
