using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
// using Microsoft.OpenApi.Models;
using System.Text;
// using Palms.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 2. Add controllers
builder.Services.AddControllers();

// 3. Add JWT Authentication
var jwtSecret = builder.Configuration["JwtSettings:Secret"];
var key = Encoding.ASCII.GetBytes(jwtSecret ?? "A_Very_Long_And_Secure_Secret_Key_For_Palms_Government_System_2026");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // Set true in Prod
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "id"
        };
    });

builder.Services.AddAuthorization();

// 4. Swagger configuration Removed temporarily

// 5. Dependency Injection Registration
// Repositories
builder.Services.AddSingleton<Palms.Api.Data.IDbConnectionFactory, Palms.Api.Data.SqlConnectionFactory>();
builder.Services.AddScoped<Palms.Api.Repositories.IUserRepository, Palms.Api.Repositories.UserRepository>();
builder.Services.AddScoped<Palms.Api.Repositories.IApplicantRepository, Palms.Api.Repositories.ApplicantRepository>();
builder.Services.AddScoped<Palms.Api.Repositories.IApplicationRepository, Palms.Api.Repositories.ApplicationRepository>();
builder.Services.AddScoped<Palms.Api.Repositories.ILicenseRepository, Palms.Api.Repositories.LicenseRepository>();

// Services
builder.Services.AddScoped<Palms.Api.Services.AuthService>();
builder.Services.AddScoped<Palms.Api.Services.OtpService>();
builder.Services.AddScoped<Palms.Api.Services.ApplicationWorkflowService>();
builder.Services.AddScoped<Palms.Api.Services.LicenseGeneratorService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // app.MapOpenApi();
}

app.UseCors();
// app.UseMiddleware<ExceptionMiddleware>(); // Global error handler
// app.UseHttpsRedirection();

// Serve static files from wwwroot/uploads and wwwroot/licenses
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
