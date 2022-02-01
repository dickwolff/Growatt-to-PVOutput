using Ealse.Growatt.Api;
using GrowattPvOutput.Models;
using Newtonsoft.Json;
using PVOutput.Net;
using PVOutput.Net.Builders;
using PVOutput.Net.Objects;

namespace GrowattPvOutput.HostedServices
{
    public class GrowattListenerService : BackgroundService
    {
        private Session _growattClient;
        private PVOutputClient _pVOutputClient;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Validate Growatt variables and create client.
            ValidateGrowattEnvironmentVariables();
            var gwUsername = Environment.GetEnvironmentVariable("GROWATT_USERNAME");
            var gwPassword = Environment.GetEnvironmentVariable("GROWATT_PASSWORD");

            _growattClient = new Session(gwUsername, gwPassword);

            // Validate PVOutput variables and create client.
            ValidatePvOutputEnvironmentVariables();
            var pvOutputApiKey = Environment.GetEnvironmentVariable("PVOUTPUT_APIKEY");
            var pvOutputSystemId = int.Parse(Environment.GetEnvironmentVariable("PVOUTPUT_SYSTEMID")!);

            _pVOutputClient = new PVOutputClient(pvOutputApiKey, pvOutputSystemId);

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

        private static void ValidatePvOutputEnvironmentVariables()
        {
            if (string.IsNullOrWhiteSpace("PVOUTPUT_APIKEY"))
            {
                throw new ArgumentException("PVOutput API Key not provided!");
            }

            if (string.IsNullOrWhiteSpace("PVOUTPUT_SYSTEMID"))
            {
                throw new ArgumentException("PVOutput System ID not provided!");
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

            // Verwerk de data naar PVO model.
            var builder = new StatusPostBuilder<IStatusPost>();
            builder
                .SetTimeStamp(DateTime.Now)
                .SetGeneration(decimal.ToInt32(powerTotalW), powerNow);

            // Check if Open Weather Map variables were provided. If so, get temperature.
            if (HasOpenWeatherMapVariablesProvided())
            {
                // Haal de temperatuur op.
                var temp = await GetWeatherAsync();

                Log($"\t\tGot temperature {temp} C");

                builder = builder.SetTemperature(temp);
            }
            
            // Build model and send to PVOutput.
            await _pVOutputClient.Status.AddStatusAsync(builder.Build());

            Log("\t\tSent status to PVOutput..");
        }

        private bool HasOpenWeatherMapVariablesProvided() =>
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OWM_APIKEY")) &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OWM_LAT")) &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OWM_LONG"));

        private async Task<decimal> GetWeatherAsync()
        {
            var apiKey = Environment.GetEnvironmentVariable("OWM_APIKEY");
            var lat = Environment.GetEnvironmentVariable("OWM_LAT");
            var @long = Environment.GetEnvironmentVariable("OWM_LONG");
            var units = Environment.GetEnvironmentVariable("OWM_UNITS") ?? "metric";

            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetStringAsync($"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={@long}&units={units}&appid={apiKey}");
                var obj = JsonConvert.DeserializeObject<Weather>(response);
                return obj!.Main.Temp;
            }
        }

        private static void Log(string message)
        {
            Console.WriteLine($"[{DateTimeOffset.Now:dd-MM-yyyy HH:mm:ss}]: {message}");
        }
    }
}