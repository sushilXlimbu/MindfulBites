using System.Text;
using FoodJournalApp.Data;
using FoodJournalApp.Domain;
using FoodJournalApp.Rules;
using FoodJournalApp.Rules.Rules;
using FoodJournalApp.Security;
using FoodJournalApp.Vision;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// SQL Server via EF Core. Connection string comes from user-secrets / env var
// (ConnectionStrings__Default), not from appsettings.json.
var connectionString = SecretConfig.Require(builder.Configuration, "ConnectionStrings:Default");
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(connectionString));

// Identity, EF Core-backed, no cookie/UI pieces — this is a JWT-only API.
builder.Services.AddIdentityCore<ApplicationUser>(opt =>
    {
        opt.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = SecretConfig.Require(builder.Configuration, "Jwt:Key");
var jwtIssuer = jwtSection["Issuer"]!;
var jwtAudience = jwtSection["Audience"]!;
var jwtExpiryMinutes = int.Parse(jwtSection["ExpiryMinutes"] ?? "60");

builder.Services.AddSingleton(new JwtTokenService(jwtKey, jwtIssuer, jwtAudience, jwtExpiryMinutes));

builder.Services
    .AddAuthentication(opt =>
    {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// The vision seam. HttpClient is pooled by the factory. Swap this one line to change provider.
builder.Services.AddHttpClient<IVisionAnalyzer, ClaudeVisionAnalyzer>();

// Rules engine + every rule. Register new rules here; the engine picks them all up.
builder.Services.AddScoped<INudgeRule, FriedFoodFrequencyRule>();
builder.Services.AddScoped<INudgeRule, LateNightEatingRule>();
builder.Services.AddScoped<RulesEngine>();
// Shared nudge evaluation (lookback window + engine call) used by meals and nudges endpoints.
builder.Services.AddScoped<NudgeEvaluator>();

// Let the React dev server talk to the API during development.
builder.Services.AddCors(o => o.AddPolicy("dev", p => p
    .WithOrigins("http://localhost:5173")   // Vite default
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors("dev");
app.UseStaticFiles(); // serves wwwroot/uploads/* (meal photos) — public, no auth needed for <img> tags
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
