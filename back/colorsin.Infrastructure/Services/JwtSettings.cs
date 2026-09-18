using System.Text;
using Microsoft.Extensions.Configuration;

namespace Colorsin.Infrastructure.Services;

/// <summary>
/// La seccion <c>JwtSettings</c> de la configuracion, ya leida y validada.
///
/// SE LEE EN UN SOLO SITIO -<see cref="CargarDesde"/>- porque la necesitan dos:
/// el generador, para firmar, y el arranque de la API, para configurar la
/// validacion. Si cada uno la leyera por su cuenta, nada impediria que firmaran
/// con una clave y validaran con otra; el sintoma seria un 401 en todas las
/// peticiones sin ninguna pista del motivo.
///
/// LA VALIDACION ES AL ARRANQUE, no al emitir el primer token. Una clave
/// ausente o corta es un error de despliegue, y un error de despliegue debe
/// impedir que el servicio levante, no aparecer cuando alguien intenta entrar.
/// </summary>
/// <param name="Issuer">Quien emite el token. Debe coincidir al emitir y al validar.</param>
/// <param name="Audience">Para quien es el token. Misma exigencia.</param>
/// <param name="ClaveSecreta">
/// Clave de firma HMAC-SHA256. Config: <c>JwtSettings:SecretKey</c>.
/// </param>
/// <param name="MinutosDeVigencia">
/// Cuanto vale el token. Config: <c>JwtSettings:ExpirationInMinutes</c>.
/// </param>
public sealed record JwtSettings(
    string Issuer,
    string Audience,
    string ClaveSecreta,
    int MinutosDeVigencia)
{
    /// <summary>Nombre de la seccion en appsettings.</summary>
    public const string Seccion = "JwtSettings";

    /// <summary>
    /// Lo que trae el appsettings.json versionado. Si este valor llega hasta
    /// aqui es que nadie configuro la clave de verdad.
    /// </summary>
    public const string MarcadorSinConfigurar = "__DEFINIR_EN_USER_SECRETS__";

    /// <summary>
    /// Minimo de BYTES de la clave, no de caracteres.
    ///
    /// HMAC-SHA256 exige una clave de al menos 256 bits, y 256 bits son 32
    /// bytes. Contar caracteres solo coincide si son todos ASCII: una clave con
    /// enes o tildes ocupa mas bytes que caracteres, y una de 32 caracteres
    /// escrita en otro alfabeto podria no llegar. La libreria lanza igual, pero
    /// mucho despues y con un mensaje que no dice que hacer.
    /// </summary>
    public const int BytesMinimosClave = 32;

    private const int MinutosPorDefecto = 60;

    /// <summary>
    /// Lee y valida la seccion. Lanza <see cref="InvalidOperationException"/>
    /// con instrucciones si algo falta o no sirve.
    /// </summary>
    public static JwtSettings CargarDesde(IConfiguration configuration)
    {
        var seccion = configuration.GetSection(Seccion);

        var issuer = seccion["Issuer"];
        var audience = seccion["Audience"];
        var clave = seccion["SecretKey"];

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException(
                $"La seccion '{Seccion}' no esta completa: faltan 'Issuer' o 'Audience'.\n" +
                "Los dos van en appsettings.json y no son secretos.");
        }

        if (string.IsNullOrWhiteSpace(clave) ||
            clave.Contains(MarcadorSinConfigurar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{Seccion}:SecretKey' no esta configurada.\n" +
                "El valor de appsettings.json es solo un marcador: una clave de firma NO se\n" +
                "versiona, porque quien la tenga puede fabricar tokens de cualquier usuario\n" +
                "con cualquier rol.\n\n" +
                "Desde back/colorsin.Api, genera una y guardala fuera del repositorio:\n" +
                "  dotnet user-secrets set \"JwtSettings:SecretKey\" \"<64 bytes aleatorios en base64>\"\n\n" +
                "User Secrets solo se carga con ASPNETCORE_ENVIRONMENT=Development.\n" +
                "Fuera de desarrollo, usa la variable de entorno JwtSettings__SecretKey.");
        }

        var bytes = Encoding.UTF8.GetByteCount(clave);

        if (bytes < BytesMinimosClave)
        {
            throw new InvalidOperationException(
                $"'{Seccion}:SecretKey' es demasiado corta: {bytes} bytes, y HMAC-SHA256 " +
                $"exige al menos {BytesMinimosClave}.\n" +
                "No la alargues a mano con texto predecible: genera una aleatoria.");
        }

        // Ausente o ilegible cae al valor por defecto; cero o negativo NO, porque
        // significa un token ya caducado al nacer y eso no es un descuido que
        // convenga corregir en silencio.
        var minutosCrudos = seccion["ExpirationInMinutes"];
        var minutos = int.TryParse(minutosCrudos, out var valor) ? valor : MinutosPorDefecto;

        if (minutos <= 0)
        {
            throw new InvalidOperationException(
                $"'{Seccion}:ExpirationInMinutes' es {minutos}. Tiene que ser mayor que cero: " +
                "un token que nace caducado rechaza todas las peticiones.");
        }

        return new JwtSettings(issuer, audience, clave, minutos);
    }
}
