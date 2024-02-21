namespace FileManager_FlatData.Data
{
    public class WeatherForecastService
    {
        private readonly HttpClient httpClient;
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        public WeatherForecastService(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public WeatherForecast[] GetForecastAsync(DateOnly startDate)
        {
            var data = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = startDate.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            }).ToArray();
            return data;
        }
    }
}
