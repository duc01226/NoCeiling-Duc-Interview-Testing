// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using IdentityServer4.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace PlatformExampleApp.Ids;

public class SeedData
{
    public static async Task EnsureSeedData(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddOperationalDbContext(
            options =>
            {
                options.ConfigureDbContext = db => db.UseSqlite(
                    connectionString,
                    sql => sql.MigrationsAssembly(typeof(SeedData).Assembly.FullName));
            });
        services.AddConfigurationDbContext(
            options =>
            {
                options.ConfigureDbContext = db => db.UseSqlite(
                    connectionString,
                    sql => sql.MigrationsAssembly(typeof(SeedData).Assembly.FullName));
            });

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            await scope.ServiceProvider.GetService<PersistedGrantDbContext>().Database.MigrateAsync();

            var context = scope.ServiceProvider.GetService<ConfigurationDbContext>();
            await context.Database.MigrateAsync();
            await EnsureSeedData(context);
        }
    }

    private static async Task EnsureSeedData(ConfigurationDbContext context)
    {
        if (!context.Clients.Any())
        {
            Log.Debug("Clients being populated");
            foreach (var client in Config.Clients.ToList())
                context.Clients.Add(client.ToEntity());

            await context.SaveChangesAsync();
        }
        else
        {
            Log.Debug("Clients already populated");
        }

        if (!context.IdentityResources.Any())
        {
            Log.Debug("IdentityResources being populated");
            foreach (var resource in Config.IdentityResources.ToList())
                context.IdentityResources.Add(resource.ToEntity());

            await context.SaveChangesAsync();
        }
        else
        {
            Log.Debug("IdentityResources already populated");
        }

        if (!context.ApiResources.Any())
        {
            Log.Debug("ApiScopes being populated");
            foreach (var resource in Config.ApiScopes.ToList())
                context.ApiScopes.Add(resource.ToEntity());

            await context.SaveChangesAsync();
        }
        else
        {
            Log.Debug("ApiScopes already populated");
        }
    }
}
