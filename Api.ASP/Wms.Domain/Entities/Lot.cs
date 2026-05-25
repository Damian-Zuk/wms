using Wms.Domain.Errors;
using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;
using Wms.Shared.Common;

namespace Wms.Domain.Entities;

public class Lot : Entity
{
    public LotNumber Number { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public DateOnly? ManufactureDate { get; private set; }
    public DateOnly? ExpirationDate { get; private set; }

    private Lot() { }

    public Lot(LotNumber number, Guid productId, DateOnly? manufactureDate = null, DateOnly? expirationDate = null)
    {
        if (expirationDate.HasValue && manufactureDate.HasValue 
            && expirationDate < manufactureDate)
            throw new ArgumentException("Expiry date cannot be before manufacture date");

        Id = Guid.NewGuid();
        Number = number;
        ProductId = productId;
        ManufactureDate = manufactureDate;
        ExpirationDate = expirationDate;
    }

    public Result UpdateDates(DateOnly? manufactureDate, DateOnly? expirationDate)
    {
        if (expirationDate.HasValue && manufactureDate.HasValue
            && expirationDate < manufactureDate)
            return LotErrors.InvalidDates;

        ManufactureDate = manufactureDate;
        ExpirationDate = expirationDate;
        return Result.Success();
    }

    public bool IsExpired()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        return ExpirationDate.HasValue && ExpirationDate.Value < today;
    }

    public bool IsExpiringSoon(int warningDays = 30)
    {
        var threshold = DateOnly.FromDateTime(DateTime.Today).AddDays(warningDays);
        return ExpirationDate.HasValue && ExpirationDate.Value <= threshold;
    }
}
