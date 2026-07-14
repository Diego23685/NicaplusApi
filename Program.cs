using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NicaplusApi.Data;
using System.Text;
using Microsoft.OpenApi.Models;
using NicaplusApi.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Servicios Básicos
builder.Services.AddHttpContextAccessor();

builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();

builder.Services.AddHttpClient<IEmailService, EmailService>();

builder.Services.AddScoped<JwtService>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            ),

            ValidateIssuer = true,
            ValidateAudience = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"]
        };
    });

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

// 2. CORS Único (Para evitar conflictos)
// 2. CORS Ajustado para múltiples orígenes (Admin y Catálogo Público)
builder.Services.AddCors(options => 
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.WithOrigins(
                  "https://administration.nicaplusgaming.online", 
                  "https://www.nicaplusgaming.online" // ◄ Agregamos el catálogo de clientes
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 3. Swagger con Seguridad JWT
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Nicaplus ERP API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Escribe 'Bearer ' seguido de tu token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// 4. Middlewares (El orden importa)
app.UseCors("PermitirTodo"); // Activar CORS antes de autenticación

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nicaplus ERP API V1"));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();