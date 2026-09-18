using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Colorsin.Api.Auth;
using Colorsin.Api.Endpoints;
using Colorsin.Application.Comun.Auditoria;
using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Comun.Repositories;
using Colorsin.Application.Comun.Services;
using Colorsin.Application.Compras.Repositories;
using Colorsin.Application.Compras.Services;
using Colorsin.Application.Dashboard.Repositories;
using Colorsin.Application.Dashboard.Services;
using Colorsin.Application.Inventario.Repositories;
using Colorsin.Application.Inventario.Services;
using Colorsin.Application.Transferencias.Repositories;
using Colorsin.Application.Transferencias.Services;
using Colorsin.Application.Ventas.Repositories;
using Colorsin.Application.Ventas.Services;
using Colorsin.Infrastructure.Auditoria;
using Colorsin.Infrastructure.Persistence;
using Colorsin.Infrastructure.Persistence.Repositories;
using Colorsin.Infrastructure.Persistence.Repositories.Compras;
using Colorsin.Infrastructure.Persistence.Repositories.Dashboard;
using Colorsin.Infrastructure.Persistence.Repositories.Transferencias;
using Colorsin.Infrastructure.Persistence.Repositories.Ventas;
using Colorsin.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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

// -----------------------------------------------------------------------------
// Modulo Compras: proveedores y ordenes de compra.
//
// Scoped, como todo lo demas. Importa mas de lo que parece: ComprasService
// escribe en inventario y en auditoria, y solo comparten transaccion porque
// los tres comparten el AppDbContext de la peticion.
// -----------------------------------------------------------------------------
builder.Services.AddScoped<IProveedorRepository, ProveedorRepository>();
builder.Services.AddScoped<IOrdenCompraRepository, OrdenCompraRepository>();
builder.Services.AddScoped<IComprasService, ComprasService>();

// -----------------------------------------------------------------------------
// Modulo Ventas: clientes y ventas.
//
// Scoped, como todo lo demas, y aqui tambien importa por correccion:
// VentasService descuenta stock a traves de IInventarioRepository y solo
// comparten transaccion porque los dos usan el AppDbContext de la peticion.
// -----------------------------------------------------------------------------
builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
builder.Services.AddScoped<IVentaRepository, VentaRepository>();
builder.Services.AddScoped<IVentasService, VentasService>();

// -----------------------------------------------------------------------------
// Modulo Transferencias: traslados entre sedes, novedades y transportadoras.
//
// Scoped, como todo lo demas, y aqui tambien importa por correccion: el despacho
// y la recepcion descuentan y suman stock a traves de IInventarioRepository, y
// solo comparten transaccion porque los dos usan el AppDbContext de la peticion.
// -----------------------------------------------------------------------------
builder.Services.AddScoped<ITransportadoraRepository, TransportadoraRepository>();
builder.Services.AddScoped<ITransferenciaRepository, TransferenciaRepository>();
builder.Services.AddScoped<ITransferenciasService, TransferenciasService>();

// -----------------------------------------------------------------------------
// Modulo Dashboard: lecturas agregadas para la pantalla de inicio.
//
// Scoped como todo lo demas, aunque aqui el motivo es distinto: este modulo NO
// escribe, asi que no hay transaccion que compartir. Va Scoped porque depende
// del AppDbContext, que es Scoped; registrarlo Singleton se quedaria con el
// DbContext de la primera peticion y el contenedor lo rechaza al arrancar.
// -----------------------------------------------------------------------------
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// -----------------------------------------------------------------------------
// CORS: que navegadores pueden llamar a esta API desde otra pagina.
//
// Los origenes NO van en el codigo sino en configuracion
// (`Cors:OrigenesPermitidos`), por lo mismo que la cadena de conexion: cambian
// entre tu maquina, pruebas y produccion, y recompilar para cambiar un puerto es
// absurdo. appsettings.Development.json ya trae los puertos de desarrollo
// habituales; el appsettings.json versionado los deja VACIOS a proposito.
//
// NUNCA AllowAnyOrigin AQUI. Esta API todavia no tiene autenticacion, y estos
// endpoints entregan totales de facturacion, clientes con su NIT y el valor del
// inventario. Con el comodin, cualquier pagina que la victima abra podria leer
// todo eso desde su navegador. Lista blanca explicita o nada.
//
// Tampoco AllowCredentials: no hay cookies ni sesiones que enviar. Cuando
// llegue la autenticacion habra que anadirlo, y entonces el comodin de origenes
// pasa a ser directamente ilegal para el navegador.
// -----------------------------------------------------------------------------
// -----------------------------------------------------------------------------
// Autenticacion con JWT.
//
// Los ajustes se leen UNA vez, aqui, y el mismo objeto configura la validacion.
// El generador vuelve a leerlos por su cuenta desde IConfiguration, pero con la
// misma funcion: es lo que garantiza que se firme y se valide con la misma
// clave. Si esto se separara, el sintoma de una discrepancia seria un 401 en
// todas las peticiones sin ninguna pista del motivo.
//
// CargarDesde lanza si la clave falta, es el marcador del appsettings versionado
// o no llega a 32 bytes. Es deliberado que tumbe el arranque: una clave mal
// configurada es un error de despliegue, y vale mas que el servicio no levante a
// que levante emitiendo tokens que nadie puede validar.
// -----------------------------------------------------------------------------
var ajustesJwt = JwtSettings.CargarDesde(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        // Lo que se escribio es lo que se lee. Con la traduccion activada -que es
        // el valor por defecto, por compatibilidad- el manejador reescribe los
        // nombres cortos de los claims a las URI largas de ClaimTypes al
        // validar. Como el generador ya escribe las URI largas, traducir encima
        // solo anade una capa de sorpresas; ver la nota de JwtTokenGenerator.
        opciones.MapInboundClaims = false;

        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = ajustesJwt.Issuer,

            ValidateAudience = true,
            ValidAudience = ajustesJwt.Audience,

            // Lo unico que de verdad impide falsificar un token. Sin esto, la
            // API aceptaria cualquier JSON con la forma correcta.
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(ajustesJwt.ClaveSecreta)),

            ValidateLifetime = true,

            // Por defecto son CINCO MINUTOS de tolerancia, pensados para relojes
            // de servidores distintos. Aqui emisor y validador son el mismo
            // proceso, asi que ese margen solo alarga la vida real de cada token.
            ClockSkew = TimeSpan.FromSeconds(30),

            // Explicitos para que [Authorize(Roles = ...)] y User.Identity.Name
            // miren los mismos claims que escribe el generador, y no dependan de
            // los valores por defecto del manejador.
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

// Hace falta aparte de AddAuthentication: sin esto, UseAuthorization no tiene
// los servicios que necesita y falla al construir la aplicacion.
builder.Services.AddAuthorization();

// Singleton los dos, a diferencia del resto de servicios del proyecto: no
// dependen del AppDbContext ni de nada con alcance de peticion. El generador lee
// la configuracion y arma las credenciales de firma una sola vez; el hasher no
// guarda estado ninguno. Despues son inmutables y seguros entre hilos.
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

// Scoped, este si: comparte el AppDbContext de la peticion con el repositorio de
// usuarios y con la auditoria, que es lo que hace que el evento del inicio de
// sesion y la posible actualizacion del hash se confirmen en el mismo guardado.
builder.Services.AddScoped<IAuthService, AuthService>();

// -----------------------------------------------------------------------------
// Contexto del usuario: quien hace la peticion, leido del token.
//
// AddHttpContextAccessor es lo que permite a UsuarioContextoHttp llegar al
// usuario sin que los servicios tengan que recibirlo por parametro desde el
// endpoint. Scoped porque la respuesta cambia en cada peticion: un singleton
// devolveria el usuario de la primera para siempre.
// -----------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioContexto, UsuarioContextoHttp>();

// Traduce AccesoDenegadoException a un 403 con un mensaje generico, en vez de
// dejarla salir como un 500 con la traza -y el detalle interno- dentro.
builder.Services.AddExceptionHandler<AccesoDenegadoHandler>();
builder.Services.AddProblemDetails();

// -----------------------------------------------------------------------------
// Limitacion de peticiones al inicio de sesion.
//
// POR QUE SOLO AHI. Es el unico endpoint al que se puede llamar sin token, y por
// tanto el unico donde alguien puede probar combinaciones sin identificarse. Los
// 500 ms que cuesta BCrypt encarecen el ataque pero no lo impiden: sin limite,
// una tarde entera de intentos sigue siendo gratis.
//
// VENTANA FIJA de 5 intentos por minuto y por IP. Fija y no deslizante porque es
// la que se puede explicar sin ambiguedad -"cinco por minuto"- y porque el peor
// caso de la ventana fija, diez intentos a caballo de dos ventanas, es
// irrelevante frente a los millones que busca un ataque por fuerza bruta.
//
// SIN COLA (QueueLimit = 0): el sexto intento se rechaza en el acto en vez de
// esperar turno. Encolarlos ataria hilos del servidor a peticiones que de todas
// formas van a fallar, que es precisamente lo que busca quien ataca.
//
// LO QUE NO CUBRE: la particion es por IP, asi que un ataque repartido entre
// muchas IP pasa por debajo, y varias personas detras de la misma salida a
// internet comparten cupo. Ver la nota de la cabecera X-Forwarded-For abajo.
// -----------------------------------------------------------------------------
builder.Services.AddRateLimiter(opciones =>
{
    // Por defecto un rechazo sale como 503 ("servicio no disponible"), que dice
    // que el servidor tiene un problema. No lo tiene: esta negando a proposito.
    // 429 es el codigo que significa "vas demasiado rapido".
    opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opciones.AddPolicy(PoliticasRateLimit.Login, contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            // La IP como clave de particion. Si no se puede determinar -pasa con
            // algunas conexiones locales- todas esas peticiones caen en la misma
            // particion "desconocida", que es el lado prudente del error: comparten
            // cupo en vez de quedarse sin limite.
            partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    // Retry-After: sin ella, el cliente no sabe cuanto esperar y reintenta a
    // ciegas, sumando peticiones al problema.
    opciones.OnRejected = async (contexto, cancelacion) =>
    {
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
        {
            contexto.HttpContext.Response.Headers.RetryAfter =
                ((int)espera.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        contexto.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimit")
            .LogWarning(
                "Limite de intentos alcanzado en {Ruta} desde {Ip}.",
                contexto.HttpContext.Request.Path,
                contexto.HttpContext.Connection.RemoteIpAddress);

        await contexto.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                title = "Demasiados intentos",
                status = StatusCodes.Status429TooManyRequests,
                detail = "Has superado el limite de intentos. Espera un momento y vuelve a intentarlo."
            },
            cancelacion);
    };
});

const string PoliticaCors = "FrontendColorsin";

var origenesPermitidos = (builder.Configuration
        .GetSection("Cors:OrigenesPermitidos")
        .Get<string[]>() ?? [])
    .Where(origen => !string.IsNullOrWhiteSpace(origen))
    // La barra final es el error clasico de CORS: WithOrigins compara el texto
    // tal cual, y "http://localhost:5173/" no coincide nunca con el Origin que
    // manda el navegador, que va sin barra. Falla en silencio.
    .Select(origen => origen.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

if (origenesPermitidos.Length > 0)
{
    builder.Services.AddCors(opciones =>
        opciones.AddPolicy(PoliticaCors, politica => politica
            .WithOrigins(origenesPermitidos)
            .AllowAnyHeader()
            .AllowAnyMethod()));
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Primero de todo, para que envuelva al resto: lo que lance cualquier middleware
// posterior pasa por aqui. Es lo que convierte AccesoDenegadoException en 403.
app.UseExceptionHandler();

app.UseHttpsRedirection();

// Explicito, aunque WebApplication lo insertaria solo. Hace falta ANTES del
// limitador y de la autorizacion porque los dos deciden mirando los metadatos
// del endpoint -que politica de limite lleva, si exige token-, y esos metadatos
// no existen hasta que el enrutamiento resuelve a que endpoint va la peticion.
app.UseRouting();

// -----------------------------------------------------------------------------
// CORS, despues de UseHttpsRedirection y antes de las rutas.
//
// Si no hay origenes configurados no se registra ninguna politica y la API
// queda sin cabeceras CORS. Eso NO es un fallo: el navegador bloquea por
// defecto, que es el comportamiento seguro. Se avisa en el log para que nadie
// pierda una tarde depurando por que el frontend recibe un error de CORS.
//
// OJO CON LA REDIRECCION A HTTPS. Si la API escucha en https y el frontend la
// llama por http, la peticion de sondeo (el OPTIONS previo) recibe un 307 y el
// navegador no lo sigue: da error de CORS aunque el origen este permitido. Con
// el perfil `http` de launchSettings no pasa, porque no hay puerto https
// configurado y UseHttpsRedirection no hace nada. Con el perfil `https`, apunta
// el frontend a https://localhost:7055.
// -----------------------------------------------------------------------------
if (origenesPermitidos.Length > 0)
{
    app.UseCors(PoliticaCors);
    app.Logger.LogInformation(
        "CORS habilitado para {Cantidad} origen(es): {Origenes}",
        origenesPermitidos.Length,
        string.Join(", ", origenesPermitidos));
}
else
{
    app.Logger.LogWarning(
        "CORS deshabilitado: no hay origenes en 'Cors:OrigenesPermitidos'. " +
        "Una pagina servida desde otro origen no podra llamar a esta API.");
}

// -----------------------------------------------------------------------------
// Autenticacion y autorizacion, en este orden y despues de CORS.
//
// El orden no es una convencion, es una dependencia:
//
//   UseCors           primero, porque el sondeo previo (OPTIONS) lo manda el
//                     navegador SIN cabecera Authorization. Si la autenticacion
//                     fuera antes, ese sondeo se respondria 401 y el navegador
//                     ni siquiera llegaria a mandar la peticion real: daria
//                     error de CORS por lo que en realidad es un 401.
//   UseAuthentication luego, que es quien lee el token y arma el usuario.
//   UseAuthorization  al final, porque decide con ese usuario ya armado. Al
//                     reves siempre veria un anonimo y rechazaria todo.
//
// Registrarlos no cierra nada por si solo: un endpoint sigue siendo publico
// mientras no lleve [Authorize] o .RequireAuthorization(). De momento lo son
// todos.
// -----------------------------------------------------------------------------
// El limitador va DESPUES de CORS y ANTES de la autenticacion.
//
//   Despues de CORS, porque si no un rechazo 429 saldria sin cabeceras CORS y el
//   navegador se lo mostraria al usuario como un error de CORS, escondiendo el
//   mensaje de "demasiados intentos" que si le sirve.
//
//   Antes de la autenticacion, para descartar el exceso cuanto antes: comprobar
//   una contrasena cuesta medio segundo, y quien abusa no deberia poder gastarlo.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

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
.WithName("HealthDb")
// Tambien exige token, aunque no sea un "grupo de endpoints" como los otros.
// No es solo un pulso de vida: devuelve cuantas sedes, productos y lotes hay, y
// eso es informacion del negocio que no tiene por que estar abierta.
//
// Si algun dia hace falta un pulso para un monitor externo, lo correcto es un
// endpoint aparte que responda solo si la base contesta, sin ninguna cifra, y
// ese si puede ir publico.
.RequireAuthorization();

// -----------------------------------------------------------------------------
// Endpoints del tablero: /api/dashboard/*
//
// Van en su propio archivo y no aqui abajo porque Program.cs ya hace bastante
// -configuracion, cadena de conexion, seis modulos de inyeccion- y mezclar las
// rutas de cada modulo lo volveria imposible de leer. Cuando los demas modulos
// expongan HTTP, cada uno traera su propio Map*Endpoints y esta seccion sera una
// lista de llamadas.
// -----------------------------------------------------------------------------
app.MapAuthEndpoints();
app.MapDashboardEndpoints();

app.Run();
