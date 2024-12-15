namespace GeoTabKLineImport;

using Geotab.Checkmate;
using Geotab.Checkmate.ObjectModel;
using Microsoft.Extensions.Caching.Memory;
using Nelibur.ObjectMapper;

public class VehicleCache(IMemoryCache memory)
{
    public async Task<Vehicle?> GetAsync(API api, Id id)
    {
        if (memory.TryGetValue<Vehicle>(id, out var vehicle))
            return vehicle;

        var devices = await api.CallAsync<IEnumerable<Device>>(
            "Get",
            typeof(Device),
            new
            {
                search = new DeviceSearch(id),
            });

        if (devices is null)
            return null;

        var device = devices.FirstOrDefault();

        if (device is null)
            return null;

        TinyMapper.Bind<Go9, Vehicle>();
        TinyMapper.Bind<Go9B, Vehicle>();
        // ...

        vehicle = TinyMapper.Map<Vehicle>(device);

        if (string.IsNullOrWhiteSpace(vehicle.VehicleIdentificationNumber))
            return null;

        memory.Set(id, vehicle);

        return vehicle;
    }
}