using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApp_UnderTheHood.Auth;
using WebApp_UnderTheHood.Dtos;
using WebApp_UnderTheHood.Helpers;

namespace WebApp_UnderTheHood.Pages;

[Authorize(Policy = "HrManagerOnly")]
public class HrManagerModel : PageModel
{
    private readonly InvokeApiHelper invokeApiHelper;

    public HrManagerModel(InvokeApiHelper invokeApiHelper)
    {
        this.invokeApiHelper = invokeApiHelper;
    }

    [BindProperty]
    public List<WeatherForecastDto> WeatherForecastItems { get; set; } = new List<WeatherForecastDto>();

    public async Task OnGetAsync()
    {
        WeatherForecastItems =
            await invokeApiHelper.InvokeEndpoint<List<WeatherForecastDto>>(
                httpClientName: "OurWebApi",
                endpointUrl: "WeatherForecast",
                credential: new Credential
                {
                    UserName = "admin",
                    Password = "password"
                }) ??
            new List<WeatherForecastDto>();
    }
}
