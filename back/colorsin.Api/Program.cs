using Colorsin.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// -----------------------------------------------------------------------------
// Base de datos: MySQL 8.4 en el contenedor local (puerto 3307 del host).
//
// La cadena REAL no esta en appsettings.json (que se versiona) sino en User
// Secrets, fuera del repositorio. appsettings.json solo guarda la plantilla,
// con el marcador de posicion que se valida abajo.
//
// ServerVersion.AutoDetect abre una conexion al arrancar para preguntarle su
// version al servidor. Es comodo en desarrollo, pero exige que el contenedor
// este arriba antes que la API; si no, la aplicacion falla al iniciar.
// En despliegue conviene fijarla: new MySqlServerVersion(new Version(8, 4, 0)).
// -----------------------------------------------------------------------------
const string MarcadorSinConfigurar = "__DEFINIR_EN_USER_SECRETS__";

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString) ||
    connectionString.Contains(MarcadorSinConfigurar, StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "La cadena de conexion 'DefaultConnection' no esta configurada.\n" +
        "El valor de appsettings.json es solo una plantilla y no lleva contrasena.\n\n" +
        "Desde back/colorsin.Api ejecuta:\n" +
        "  dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" " +
        "\"Server=localhost;Port=3307;Database=colorsin_inventario;Uid=colorsin;Pwd=TU_CLAVE;\"\n\n" +
        "Recuerda que User Secrets solo se carga con ASPNETCORE_ENVIRONMENT=Development. " +
        "Fuera de desarrollo, define ConnectionStrings__DefaultConnection como variable de entorno.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure());

    if (builder.Environment.IsDevelopment())
    {
        // Muestra los valores de los parametros en el log. Solo en desarrollo:
        // en produccion filtraria datos sensibles a los registros.
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Verificacion rapida de que la API alcanza la base y el mapeo responde.
app.MapGet("/health/db", async (AppDbContext db) =>
{
    var puedeConectar = await db.Database.CanConnectAsync();
    if (!puedeConectar)
    {
        return Results.Problem("No hay conexion con MySQL.", statusCode: 503);
    }

    return Results.Ok(new
    {
        conectado = true,
        sucursales = await db.Sucursales.CountAsync(),
        productos = await db.Productos.CountAsync(),
        unidades = await db.UnidadesMedida.CountAsync(),
        inventario = await db.InventarioSucursales.CountAsync(),
        lotes = await db.Lotes.CountAsync()
    });
})
.WithName("HealthDb");

app.Run();
