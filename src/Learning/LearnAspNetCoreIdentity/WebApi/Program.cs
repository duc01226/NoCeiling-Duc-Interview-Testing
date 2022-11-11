using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WebApi;
using WebApi.Auth;
using WebApi.Authorization;
using WebApi.Authorization.Requirements;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(authenticationScheme: JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            //ValidateIssuer = true, // Can turn this on when work with third parties
            //ValidIssuer = configuration["JWT:ValidIssuer"],
            ValidateIssuer = false,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:SecretAuthenticationKey"])),

            ValidateLifetime = true, // Validate is the token expired
            /*
             * What if we have two auth servers? How do you know that they will have the same time? What if they are in fact off by a few minutes.
             * One server will return that the token has expired and the other wont. If you are a small company this probably doesn't matter.
             * Now consider if you are a big search engine company with servers all around the world. Lets say its the fall of 2016 and daylight savings time kicks in.
             * Now you have some servers running on one time and others running at another.
             * Maybe just maybe some country decides to change when they start daylight savings time and boom a bunch of tokens get invalidated for no reason.
             */
            ClockSkew = TimeSpan.FromMinutes(5), // Go with validate lifetime, needed when you have multiple token provider servers

            // ValidateAudience = false // Can turn this on when work with third parties
            // ValidAudience = configuration["JWT:ValidAudience"],
            ValidateAudience = false,
        };
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
