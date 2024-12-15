namespace GeoTabKLineImport;

using Geotab.Checkmate;
using Geotab.Checkmate.ObjectModel;
using Geotab.Checkmate.ObjectModel.Tachograph;
using Nelibur.ObjectMapper;

public class Worker(ILogger<Worker> logger, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var api = new API(
                configuration.GetValue<string>("GeoTabUser"),
                configuration.GetValue<string>("GeoTabPassword"),
                null,
                configuration.GetValue<string>("GeoTabDb"));

            var drivers = (await api.CallAsync<IEnumerable<Geotab.Checkmate.ObjectModel.User>>(
                "Get",
                typeof(Geotab.Checkmate.ObjectModel.User),
                new
                {
                    search = new UserSearch()
                    {
                        UserSearchType = UserSearchType.Driver,
                    },
                })).
                Select(user => user as Driver).
                Where(driver => string.IsNullOrWhiteSpace(driver?.Keys?.FirstOrDefault()?.SerialNumber) is false);

            foreach (var driver in drivers)
            {
                var activities = (await api.CallAsync<IEnumerable<TachographDriverActivity>>(
                    "Get",
                    typeof(TachographDriverActivity),
                    new
                    {
                        search = new TachographDriverActivitySearch()
                        {
                            UserSearch = new UserSearch(driver.Id),
                            Type = "STREAM",
                            FromDate = DateTime.UtcNow.AddDays(-14),
                            ToDate = DateTime.UtcNow,
                            Extrapolate = true,
                        },
                    })).
                    Where(activity => string.IsNullOrWhiteSpace(activity.Activity) is false &&
                        activity.Activity != "UNKNOWN").
                    Where(activity => activity.Device?.Id is not null);

                foreach (var activity in activities)
                {
                    var device = (await api.CallAsync<IEnumerable<Device>>(
                        "Get",
                        typeof(Device),
                        new
                        {
                            search = new DeviceSearch(activity.Device.Id),
                        })).
                        First();

                    TinyMapper.Bind<Go9, Vehicle>();
                    TinyMapper.Bind<Go9B, Vehicle>();
                    var vehicle = TinyMapper.Map<Vehicle>(device);

                    var vin = vehicle.VehicleIdentificationNumber;
                    var timeStamp = activity.DateTime;
                    var activityActivity = activity.Activity;
                    var slot = activity.Slot;
                    var driverCardNumber = driver.Keys.FirstOrDefault()?.SerialNumber;
                }
            }

            await Task.Delay(TimeSpan.MaxValue, stoppingToken);
        }
    }
}
