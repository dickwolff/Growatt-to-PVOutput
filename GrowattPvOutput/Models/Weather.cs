namespace GrowattPvOutput.Models
{
    /// <summary>
    /// Open Weather Map: Weather model.
    /// </summary>
    public class Weather
    {
        public Main Main { get; set; }
    }

    public class Main
    {
        public decimal Temp { get; set; }
    }
}