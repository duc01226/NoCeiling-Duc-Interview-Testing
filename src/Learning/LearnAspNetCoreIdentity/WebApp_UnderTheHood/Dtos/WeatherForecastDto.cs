using WebApp_UnderTheHood.Dtos.Abstract;

namespace WebApp_UnderTheHood.Dtos;

public class WeatherForecastDto : Dto
{
    public DateTime Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF { get; set; }

    public string? Summary { get; set; }
}
