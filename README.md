# Growatt to PVOutput

This service allows you to export data from the Growatt Server to PVOutput, while also adding temperature data to the status.

I copied the sources from [Ealse.Growatt.Api](https://github.com/ealse/GrowattApi) and modified the `DeviceStatus` property on the `Device` class, changing the type from `int` to `string`. This caused an serialization error while retrieving data.

# Run in Docker 
When running the docker container, the following environment variables can be provided:
| Variable | Description | Required |
| --- | --- | --- |
| `GROWATT_USERNAME` | Username for your Growatt account. | Yes |
| `GROWATT_PASSWORD` | Password for your Growatt account. | Yes |
| `PVOUTPUT_APIKEY`  | Your PVOutput API Key (on the bottom of [this](https://pvoutput.org/account.jsp) page). | Yes |
| `PVOUTPUT_APIKEY`  | Your PVOutput System ID. | Yes |
| `TZ` | Your local timezone, for reporting time to PVOutput in local time. | No (defaults to GMT) |
| `OWM_APIKEY` | Your Open Weather Map API key (generate [here](https://home.openweathermap.org/api_keys). | No, but required to send temperature |
| `OWM_LAT` | Latitude coordinate of your position. | No (but required if `OWM_APIKEY` is given). |
| `OWM_LONG` | Longitude coordinate of your position. | No (but required if `OWM_APIKEY` is given). |
| `OWM_UNITS` | Temperature units, accepts `metric' and `imperial`. | No (defaults to `metric`). |
| `SLEEP_INTERAL_SECONDS` | The interval in seconds on which the logic is running. | No (Defaults to 5 minutes (150 seconds)). |
