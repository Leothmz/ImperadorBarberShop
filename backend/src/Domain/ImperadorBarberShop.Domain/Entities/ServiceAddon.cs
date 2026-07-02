namespace ImperadorBarberShop.Domain.Entities;

public class ServiceAddon
{
    public Guid ParentServiceId { get; private set; }
    public Guid AddonServiceId { get; private set; }
    public Service AddonService { get; private set; } = null!;

    private ServiceAddon() { }

    public static ServiceAddon Create(Guid parentServiceId, Guid addonServiceId)
    {
        if (parentServiceId == addonServiceId)
            throw new ArgumentException("A service cannot be its own add-on.");
        return new ServiceAddon { ParentServiceId = parentServiceId, AddonServiceId = addonServiceId };
    }
}
