using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NicaplusApi.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuración de Servicios Básicos e Inyecciones
builder.Services.AddHttpContextAccessor(); // Necesario para capturar auditorías

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))
    ));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("TU_CLAVE_SECRETA_SUPER_LARGA_DE_AL_MENOS_32_CARACTERES")),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

builder.Services.AddEndpointsApiExplorer();

// 2. Configuración Avanzada de Swagger con Candado JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Nicaplus ERP API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Autenticación JWT usando el esquema Bearer. Escribe 'Bearer ' seguido de tu token.\r\n\r\nEjemplo: \"Bearer eyJhbGciOi...\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            },
            new List<string>()
        }
    });
});

builder.Services.AddCors(options => options.AddPolicy("NicaplusCors", p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

// --- CONSTRUCCIÓN DE LA APP ---
var app = builder.Build();

// 3. ACTIVAR EL MOTOR VISUAL DE SWAGGER (¡Esto era lo que faltaba!)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Nicaplus ERP API V1");
    // Opcional: Si quieres que Swagger se abra directo en la raíz de localhost:5139/ en vez de /swagger
    // c.RoutePrefix = string.Empty; 
});

// 4. Middlewares de Flujo y Seguridad
app.UseCors("NicaplusCors");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();