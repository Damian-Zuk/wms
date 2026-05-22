namespace Wms.Domain.Entities;

public class ProductPreferredLocation
{
    public Guid ProductId { get; private set; }
    public Guid LocationId { get; private set; }
    public int Sequence { get; private set; }

    private ProductPreferredLocation() { }

    public ProductPreferredLocation(Guid productId, Guid locationId, int sequence)
    {
        ProductId = productId;
        LocationId = locationId;
        Sequence = sequence;
    }
}
