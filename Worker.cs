namespace GeoTabKLineImport;

using Geotab.Checkmate;
using Geotab.Checkmate.ObjectModel;
using Geotab.Checkmate.ObjectModel.Tachograph;

public class Worker(ILogger<Worker> logger, IConfiguration configuration, VehicleCache vehicleCache)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (stoppingToken.IsCancellationRequested is false)
        {
            var api = new API(
                configuration.GetValue<string>("GeoTabUser") ?? string.Empty,
                configuration.GetValue<string>("GeoTabPassword"),
                null,
                configuration.GetValue<string>("GeoTabDb") ?? string.Empty);

            var users = await api.CallAsync<IEnumerable<User>>(
                "Get",
                typeof(User),
                new
                {
                    search = new UserSearch()
                    {
                        UserSearchType = UserSearchType.Driver,
                    },
                },
                stoppingToken);

            if (users is null)
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                continue;
            }

            var drivers = users.
                Select(user => user as Driver).
                Where(driver => string.IsNullOrWhiteSpace(
                    driver!.Keys?.FirstOrDefault()?.SerialNumber) is false);

            foreach (var driver in drivers)
            {
                var activities = await api.CallAsync<IEnumerable<TachographDriverActivity>>(
                    "Get",
                    typeof(TachographDriverActivity),
                    new
                    {
                        search = new TachographDriverActivitySearch()
                        {
                            UserSearch = new UserSearch(driver!.Id),
                            Type = "STREAM",
                            FromDate = DateTime.UtcNow.AddDays(-7),
                            ToDate = DateTime.UtcNow,
                            Extrapolate = true,
                        },
                    },
                    stoppingToken);

                if (activities is null)
                    continue;

                activities = activities.
                    Where(activity => string.IsNullOrWhiteSpace(activity.Activity) is false &&
                        activity.Activity != "UNKNOWN").
                    Where(activity => activity.Device?.Id is not null);

                foreach (var activity in activities)
                {
                    var vehicle = await vehicleCache.GetAsync(api, activity.Device!.Id!);

                    if (vehicle is null)
                        continue;

                    var vin = vehicle.VehicleIdentificationNumber;
                    var timeStamp = activity.DateTime;
                    var activityActivity = activity.Activity;
                    var slot = activity.Slot;
                    var driverCardNumber = driver.Keys!.FirstOrDefault()!.SerialNumber;

                    logger.LogInformation(
                        "{vin} {timeStamp} {slot} {driverCardNumber} {activityActivity}",
                        vin,
                        timeStamp,
                        slot,
                        driverCardNumber,
                        activityActivity);
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}