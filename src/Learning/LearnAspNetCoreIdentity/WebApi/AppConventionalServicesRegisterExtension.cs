using Microsoft.AspNetCore.Authorization;

namespace WebApi;

public static class AppConventionalServicesRegisterExtension
{
    public static IServiceCollection RegisterAllAppAuthorizationHandlers(this IServiceCollection serviceCollection)
    {
        foreach (var authorizationHandlerType in typeof(Program).Assembly.DefinedTypes
            .Where(predicate: p => p.IsAssignableTo(targetType: typeof(IAuthorizationHandler)) && p.IsClass && !p.IsAbstract))
            serviceCollection.AddSingleton(serviceType: typeof(IAuthorizationHandler), implementationType: authorizationHandlerType);

        return serviceCollection;
    }
}
