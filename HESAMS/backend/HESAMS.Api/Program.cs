using System.Text;
using HESAMS.Api.Data;
using HESAMS.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---- Database ----
builder.Services.AddDbContext<HesamsDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// ---- JWT Authentication ----
var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt["Issuer"],
        ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
        ClockSkew = TimeSpan.FromSeconds(30)
    });

// ---- RBAC: permission-code policies (e.g. [Authorize(Policy = "equipment.update")]) ----
builder.Services.AddAuthorization(options =>
{
    foreach (var code in PermissionCatalog.All)
        options.AddPolicy(code, p => p.RequireClaim("perm", code));
});

// ---- Services ----
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AccessValidationService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<MaintenanceSchedulerService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "HESAMS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header, Name = "Authorization",
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference
            { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins("https://hesams.company.com", "http://localhost:3000")
     .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

/// <summary>Central catalog of permission codes used by RBAC policies.</summary>
public static class PermissionCatalog
{
    public static readonly string[] All =
    {
        "employees.read","employees.write","rfid.read","rfid.write",
        "equipment.read","equipment.write","equipment.unlock",
        "training.read","training.write","licenses.read","licenses.write",
        "authorizations.read","authorizations.write",
        "maintenance.read","maintenance.write","maintenance.unlock",
        "incidents.read","incidents.write","incidents.approve",
        "reports.read","reports.export","audit.read","users.admin","dashboard.read"
    };
}
