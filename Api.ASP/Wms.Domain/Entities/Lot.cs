using Wms.Domain.Primitives;
using Wms.Domain.ValueObjects;

namespace Wms.Domain.Entities;

public class Lot : Entity
{
    public LotNumber Number { get; private set; } = null!;
    public Guid ProductId { get; private set; }
    public DateTime? ManufacturedDate { get; private set; }
    public DateTime? ExpirationDate { get; private set; }

    private Lot() { }

    public Lot(LotNumber number, Guid productId, DateTime? manufacturedDate = null, DateTime? expirationDate = null)
    {
        if (expirationDate.HasValue && manufacturedDate.HasValue && expirationDate < manufacturedDate)
            throw new ArgumentException("Expiry date cannot be before manufactured date");

        Id = Guid.NewGuid();
        Number = number;
        ProductId = productId;
        ManufacturedDate = manufacturedDate;
        ExpirationDate = expirationDate;
    }

    public bool IsExpired()
    {
        return ExpirationDate.HasValue && ExpirationDate.Value.Date < DateTime.Today;
    }

    public bool IsExpiringSoon(int warningDays = 30)
    {
        return ExpirationDate.HasValue &&
               ExpirationDate.Value.Date <= DateTime.Today.AddDays(warningDays);
    }
}
