# Growatt to PVOutput

This service allows you to export data from the Growatt Server to PVOutput, while also adding temperature data to the status.

I copied the sources from [Ealse.Growatt.Api](https://github.com/ealse/GrowattApi) and modified the `DeviceStatus` property on the `Device` class, changing the type from `int` to `string`. This caused an serialization error while retrieving data.
