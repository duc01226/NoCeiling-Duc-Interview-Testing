using WebApp_UnderTheHood;
using WebApp_UnderTheHood.Auth;
using WebApp_UnderTheHood.Authorization;
using WebApp_UnderTheHood.Authorization.PolicyRequirements;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
var environment = builder.Environment;

// Add services to the container.
builder.Services.AddRazorPages();

// WHY: <defaultScheme: DefaultAuthenticationSchemes.CookieScheme> Tell the authentication middleware from <app.UseAuthentication();> to which authentication scheme
// we want to use for the Authentication
builder.Services.AddAuthentication(defaultScheme: AppAuthenticationSchemes.CookieScheme)
    // WHY: The authenticationScheme name will be used to handle authentication for
    // HttpContext.SignInAsync(scheme: DefaultAuthenticationSchemes.CookieScheme) which have scheme = authenticationScheme
    // This register a cookie authentication handlers
    .AddCookie(authenticationScheme: AppAuthenticationSchemes.CookieScheme, options =>
    {
        options.Cookie.Name = AppAuthenticationSchemes.CookieScheme;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan =
            TimeSpan.FromMinutes(1); // Cookie will be expired no matter persisted or not (no matter IsPersistent = true/false)
    });

// Add Authorization Policies for policy based authorization. Ex: [Authorize(policy: AppAuthorizationPolicies.AdminOnly)]
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AppAuthorizationPolicies.MustBelongToHrDepartment,
        policy => policy.RequireClaim(AppAuthenticationClaims.HrDepartment.Type, AppAuthenticationClaims.HrDepartment.Value));

    options.AddPolicy(
        AppAuthorizationPolicies.AdminOnly,
        policy => policy.RequireClaim(AppAuthenticationClaims.Admin.Type, AppAuthenticationClaims.Admin.Value));

    options.AddPolicy(
        AppAuthorizationPolicies.HrManagerOnly,
        policy => policy
            .RequireClaim(AppAuthenticationClaims.HrDepartment.Type, AppAuthenticationClaims.HrDepartment.Value)
            .RequireClaim(AppAuthenticationClaims.Manager.Type, AppAuthenticationClaims.Manager.Value)
            .Requirements.Add(new HrManageProbationRequirement(HrManageProbationRequirement.DefaultMinimumProbationMonths)));
});

builder.Services.RegisterAllAppAuthorizationHandlers();

builder.Services.RegisterAllAppHelpers();

builder.Services.AddHttpClient(name: "OurWebApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7289/");
});

// Help store and reuse token in session
builder.Services.AddSession(options =>
{
    // WHY: A session cookie with the HttpOnly attribute is inaccessible to the JavaScript Document. We want our cookie to be safe
    options.Cookie.HttpOnly = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// WHY: Add AuthenticationMiddleware has responsible to call the authentication handlers
// which call IAuthenticationService.AuthenticateAsync (authenticate for the specified authentication scheme),
// which will read the http context header contain cookie and deserialize it into User context (ClaimsPrinciple) in the asp.net api controller/pages
app.UseAuthentication();

// WHY: Use the AuthorizationMiddleware. When a request come, after doing Authentication via UseAuthentication middleware
// the middleware use IAuthorizationService to authorize an endpoint based on the Authorize attribute information, the service
// will get the generic AuthorizationHandler to handle for simple cases (like role claim)
// or it will get a specific custom AuthorizationHandler for one or many custom requirements (1 Policy contain multiple requirements) to check that the requirement
// for policy authorization, if success then the request will continue be handled to the next middleware (to the pages/api)
app.UseAuthorization();

// Help store and reuse token in session
app.UseSession();

app.MapRazorPages();

app.Run();
