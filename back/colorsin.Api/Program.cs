using Colorsin.Application.Comun.Auditoria;
using Colorsin.Application.Comun.Repositories;
using Colorsin.Application.Comun.Services;
using Colorsin.Application.Inventario.Repositories;
using Colorsin.Application.Inventario.Services;
using Colorsin.Infrastructure.Auditoria;
using Colorsin.Infrastructure.Persistence;
using Colorsin.Infrastructure.Persistence.Repositories;
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
// La version del servidor va FIJA, no con ServerVersion.AutoDetect: esa
// variante abre una conexion durante el arranque para preguntarle su version
// al servidor, asi que la API no levanta si el contenedor esta apagado y
// paga una ida y vuelta extra en cada inicio.
//
// Valor tomado del servidor real: mysql 8.4.11 for Linux on x86_64
// (MySQL Community Server - GPL), la imagen mysql:8.4 del docker-compose.
// Si algun dia subes la imagen de MySQL, actualiza tambien esta constante.
// -----------------------------------------------------------------------------
const string MarcadorSinConfigurar = "__DEFINIR_EN_USER_SECRETS__";

// Version del contenedor MySQL declarado en docker-compose.yml (imagen mysql:8.4).
var versionServidor = new MySqlServerVersion(new Version(8, 4, 11));

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
        versionServidor,
        mySqlOptions => mySqlOptions.EnableRetryOnFailure());

    if (builder.Environment.IsDevelopment())
    {
        // Muestra los valores de los parametros en el log. Solo en desarrollo:
        // en produccion filtraria datos sensibles a los registros.
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

// -----------------------------------------------------------------------------
// Modulo Comun: sucursales, usuarios y unidades de medida.
//
// Todo va con lifetime Scoped, es decir una instancia por peticion HTTP, porque
// los repositorios dependen de AppDbContext y AddDbContext lo registra Scoped.
// Si un repositorio fuera Singleton se quedaria con el DbContext de la primera
// peticion (captive dependency): el contenedor lo detecta y la app no arranca.
// Transient tampoco: crearia varios repositorios por peticion sin ganar nada.
// -----------------------------------------------------------------------------
builder.Services.AddScoped<ISucursalRepository, SucursalRepository>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IUnidadMedidaRepository, UnidadMedidaRepository>();

builder.Services.AddScoped<ISucursalService, SucursalService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IUnidadMedidaService, UnidadMedidaService>();

// -----------------------------------------------------------------------------
// Auditoria: transversal a todos los modulos.
//
// Scoped y no Singleton por una razon de correccion, no de estilo: comparte el
// AppDbContext de la peticion con quien la llama, y es eso lo que hace que el
// evento se confirme en la MISMA transaccion que el cambio auditado.
// -----------------------------------------------------------------------------
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();

// -----------------------------------------------------------------------------
// Modulo Inventario: productos, existencias, lotes y libro mayor.
// -----------------------------------------------------------------------------
builder.Services.AddScoped<IProductoRepository, ProductoRepository>();
builder.Services.AddScoped<IInventarioRepository, InventarioRepository>();
builder.Services.AddScoped<IInventarioService, InventarioService>();

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
