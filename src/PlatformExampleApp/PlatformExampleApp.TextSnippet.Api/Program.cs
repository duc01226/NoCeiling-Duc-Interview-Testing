using Easy.Platform.AspNetCore;
using Easy.Platform.AspNetCore.Extensions;
using Easy.Platform.Common;
using Easy.Platform.Common.DependencyInjection;
using Easy.Platform.Common.JsonSerialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using PlatformExampleApp.TextSnippet.Api;
using Serilog;

var configuration = PlatformConfigurationBuilder.GetConfigurationBuilder().Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Logger.Information("Starting web host");

    // CONFIG APP BUILDER
    var builder = WebApplication.CreateBuilder(args);

    ConfigureServices(builder.Services);

    builder.Host.UseSerilog();

    builder.WebHost.UseConfiguration(configuration);

    // BUILD AND CONFIG APP
    var webApplication = builder.Build();

    ConfigureRequestPipeline(webApplication);

    // RUN APP
    BeforeRunInit(webApplication);

    webApplication.Run();
}
catch (Exception e)
{
    Log.Logger.Error(e, "Start web host failed");
    throw;
}

static void ConfigureServices(IServiceCollection services)
{
    services.AddControllers()
        .AddJsonOptions(options => PlatformJsonSerializer.ConfigOptionsByCurrentOptions(options.JsonSerializerOptions));
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen(
        options =>
        {
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo
                {
                    Title = "TextSnippet HTTP API",
                    Version = "v1"
                });
            options.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Description =
                        "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
            options.AddSecurityRequirement(
                new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header
                        },
                        new List<string>()
                    }
                });

            // Fix bug: Swashbuckle.AspNetCore.SwaggerGen.SwaggerGeneratorException: Failed to generate Operation for action Can't use schemaId The same schemaId is already used for type
            // References: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1607#issuecomment-607170559
            options.CustomSchemaIds(type => type.ToString());
        });

    services.RegisterModule<TextSnippetApiAspNetCoreModule>(); // Register module into service collection
}


static void ConfigureRequestPipeline(WebApplication app)
{
    if (PlatformEnvironment.IsDevelopment) app.UseDeveloperExceptionPage();

    app.UseSwagger();
    app.UseSwaggerUI();

    // Reference middleware orders: https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-5.0#middleware-order

    app.UseGlobalExceptionHandlerMiddleware();
    app.UseRequestIdGeneratorMiddleware();

    app.UseRouting();

    /*
     * With endpoint routing, the CORS middleware must be configured to execute between the calls to UseRouting and UseEndpoints.
     * Incorrect configuration will cause the middleware to stop functioning correctly.
     */
    app.UseDefaultCorsPolicy();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseEndpoints(endpoints => endpoints.MapControllers());

    app.UseDefaultResponseHealthCheckForEmptyRootPath();
}

static void BeforeRunInit(WebApplication webApplication)
{
    // Init module to start running init for all other modules and this module itself
    webApplication.InitPlatformAspNetCoreModule<TextSnippetApiAspNetCoreModule>();
}
