// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PlatformExampleApp.Ids.Quickstart.Home;

[SecurityHeaders]
[AllowAnonymous]
public class HomeController : Controller
{
    private readonly IWebHostEnvironment environment;
    private readonly IIdentityServerInteractionService interaction;
    private readonly ILogger<HomeController> logger;

    public HomeController(
        IIdentityServerInteractionService interaction,
        IWebHostEnvironment environment,
        ILogger<HomeController> logger)
    {
        this.interaction = interaction;
        this.environment = environment;
        this.logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        if (environment.IsDevelopment())
            // only show in development
            return View();

        logger.LogInformation("Homepage is disabled in production. Returning 404.");
        return NotFound();
    }

    /// <summary>
    /// Shows the error page
    /// </summary>
    public async Task<IActionResult> Error(string errorId)
    {
        var vm = new ErrorViewModel();

        // retrieve error details from identityserver
        var message = await interaction.GetErrorContextAsync(errorId);
        if (message != null)
        {
            vm.Error = message;

            if (!environment.IsDevelopment())
                // only show in development
                message.ErrorDescription = null;
        }

        return View("Error", vm);
    }
}
