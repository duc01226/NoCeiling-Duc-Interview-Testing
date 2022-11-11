using Microsoft.AspNetCore.Authorization;
using WebApp_UnderTheHood.Helpers.Abstract;

namespace WebApp_UnderTheHood;

public static class AppConventionalServicesRegisterExtension
{
    public static IServiceCollection RegisterAllAppAuthorizationHandlers(this IServiceCollection serviceCollection)
    {
        foreach (var authorizationHandlerType in typeof(Program).Assembly.DefinedTypes
            .Where(p => p.IsAssignableTo(typeof(IAuthorizationHandler)) && p.IsClass && !p.IsAbstract))
            serviceCollection.AddSingleton(serviceType: typeof(IAuthorizationHandler), implementationType: authorizationHandlerType);

        return serviceCollection;
    }

    public static IServiceCollection RegisterAllAppHelpers(this IServiceCollection serviceCollection)
    {
        foreach (var authorizationHandlerType in typeof(Program).Assembly.DefinedTypes
            .Where(p => p.IsAssignableTo(typeof(Helper)) && p.IsClass && !p.IsAbstract))
            serviceCollection.AddTransient(serviceType: authorizationHandlerType, implementationType: authorizationHandlerType);

        return serviceCollection;
    }
}
