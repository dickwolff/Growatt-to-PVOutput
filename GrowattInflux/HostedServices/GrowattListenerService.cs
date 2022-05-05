using Ealse.Growatt.Api;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;

namespace GrowattInflux.HostedServices
{
    public class GrowattListenerService : BackgroundService
    {
        private Session _growattClient;
        private InfluxDBClient _influxDbClient;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Validate Growatt variables and create client.
            ValidateGrowattEnvironmentVariables();
            var gwUsername = Environment.GetEnvironmentVariable("GROWATT_USERNAME");
            var gwPassword = Environment.GetEnvironmentVariable("GROWATT_PASSWORD");

            _growattClient = new Session(gwUsername, gwPassword);

            // Validate InfluxDB variables and create client.
            ValidateInfluxDbEnvironmentVariables();
            var idbUrl = Environment.GetEnvironmentVariable("INFLUX_URL");            
            var idbToken = Environment.GetEnvironmentVariable("INFLUX_TOKEN");
            _influxDbClient = InfluxDBClientFactory.Create(idbUrl, idbToken);

            // Get sleep interval, or fallback to 60 seconds.            
            GetSleepTimeout(out var sleepIntervalSeconds, out var timeout);

            while (true)
            {
                try
                {
                    // Run the job.
                    await RunAsync();
                }
                catch (Exception ex)
                {
                    Log($"Error! {ex.Message}");
                    Log(ex.StackTrace);
                    Log("--------------------------------");
                }

                // Wait n seconds before running again.
                Log($"Going to sleep for {sleepIntervalSeconds} seconds.");
                Thread.Sleep(timeout);
            }
        }

        private static void GetSleepTimeout(out string sleepIntervalSeconds, out int timeout)
        {
            sleepIntervalSeconds = Environment.GetEnvironmentVariable("SLEEP_INTERVAL_SECONDS") ?? "60";
            var timespan = TimeSpan.FromSeconds(int.Parse(sleepIntervalSeconds));
            timeout = int.Parse($"{timespan.TotalMilliseconds}");
        }

        private static void ValidateGrowattEnvironmentVariables()
        {
            if (string.IsNullOrWhiteSpace("GROWATT_USERNAME"))
            {
                throw new ArgumentException("Growatt Username not provided!");
            }

            if (string.IsNullOrWhiteSpace("GROWATT_PASSWORD"))
            {
                throw new ArgumentException("Growatt Password not provided!");
            }
        }

        private static void ValidateInfluxDbEnvironmentVariables()
        {
            if (string.IsNullOrWhiteSpace("INFLUX_URL"))
            {
                throw new ArgumentException("Influx DB URL not provided!");
            }

            if (string.IsNullOrWhiteSpace("INFLUX_TOKEN"))
            {
                throw new ArgumentException("Influx DB token not provided!");
            }

            if (string.IsNullOrWhiteSpace("INFLUX_ORGANIZATION"))
            {
                throw new ArgumentException("Influx DB organization not provided!");
            }

            if (string.IsNullOrWhiteSpace("INFLUX_DATABASE"))
            {
                throw new ArgumentException("Influx DB database not provided!");
            }
        }

        private async Task RunAsync()
        {
            Log("\t\tStarting run..");

            // Haal de data op.          
            var plants = await _growattClient.GetPlantList();
            var plantId = plants.Data.FirstOrDefault()?.PlantId;
            var devices = await _growattClient.GetInverterSerialNumbers(plantId);
            var device = devices.DeviceList.FirstOrDefault();

            // Als de data null is, dan heeft verwerking geen zin.
            if (device == null)
            {
                Log("\t\tData is empty, skipping run..");
                return;
            }

            // Lees de data lokaal uit.
            var powerNow = Convert.ToInt32(decimal.Parse(device.Power));
            var powerTotalKwh = decimal.Parse(device.EToday);
            var powerTotalW = powerTotalKwh * 1000;
            Log($"\t\tGot power {powerNow}w ({powerTotalW}w total)");

            // Convert to Influx DB model.
            using (var writeApi = _influxDbClient.GetWriteApi())
            {
                var idbOrganization = Environment.GetEnvironmentVariable("INFLUX_ORGANIZATION");
                var idbDatabase = Environment.GetEnvironmentVariable("INFLUX_DATABASE");

                writeApi.WritePoint(
                    PointData.Measurement("power")
                    .Field("power_now", powerNow)
                    .Field("power_todayTotalKwh", powerTotalKwh)
                    .Field("power_todayTotalW", powerTotalW)
                    .Timestamp(DateTime.UtcNow, WritePrecision.Ns),
                    idbDatabase,
                    idbOrganization);                
            }

            Log("\t\tSent status to InfluxDB..");
        }

        private static void Log(string message)
        {
            Console.WriteLine($"[{DateTimeOffset.Now:dd-MM-yyyy HH:mm:ss}]: {message}");
        }
    }
}