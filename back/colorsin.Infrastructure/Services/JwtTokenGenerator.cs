using System.Globalization;
using System.Security.Claims;
using System.Text;
using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Comun.Services;
using Colorsin.Domain.Comun;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Colorsin.Infrastructure.Services;

/// <inheritdoc cref="IJwtTokenGenerator"/>
///
/// USA JsonWebTokenHandler, no el antiguo JwtSecurityTokenHandler. La diferencia
/// que importa aqui no es el rendimiento sino que el viejo TRADUCE los nombres
/// de los claims al escribir: convierte <c>ClaimTypes.Role</c> en <c>role</c>,
/// <c>ClaimTypes.NameIdentifier</c> en <c>nameid</c>, y al leer los vuelve a
/// traducir. Mientras las dos puntas usen los mismos diccionarios funciona, pero
/// deja de hacerlo en cuanto una no los use, y el sintoma es un token que se
/// valida bien y aun asi no autoriza nada, porque el rol quedo bajo otro nombre.
/// Este escribe los claims tal cual se le dan. Ver la nota correspondiente en
/// Program.cs, donde la validacion se configura para leerlos igual.
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    // El manejador no guarda estado y es seguro entre hilos: uno para toda la
    // aplicacion.
    private static readonly JsonWebTokenHandler Manejador = new();

    private readonly JwtSettings _ajustes;
    private readonly SigningCredentials _credenciales;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _ajustes = JwtSettings.CargarDesde(configuration);

        // La clave y las credenciales se arman una vez y se reusan: no cambian
        // entre tokens, y derivarlas en cada emision seria trabajo repetido.
        _credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_ajustes.ClaveSecreta)),
            SecurityAlgorithms.HmacSha256);
    }

    public TokenGenerado Generar(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        // UTC, no hora local. Un JWT lleva los tiempos como segundos desde 1970
        // en UTC, y quien valide puede estar en otra zona: mezclar husos aqui
        // produce tokens que caducan cinco horas antes o despues de lo previsto.
        var emitidoEn = DateTime.UtcNow;
        var expiraEn = emitidoEn.AddMinutes(_ajustes.MinutosDeVigencia);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, usuario.Nombre),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Role, RolesColorsin.ParaClaim(usuario.Rol)),

            // Identificador unico de ESTE token. Todavia no se usa, porque no hay
            // lista de revocacion, pero sin el no se podria anadir despues: no
            // habria forma de nombrar un token concreto para invalidarlo.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Solo si tiene sede. El Administrador General no pertenece a ninguna y
        // su token sale sin el claim; ver la nota de ClaimsColorsin.SucursalId
        // sobre por que eso no es lo mismo que "ninguna sede".
        if (usuario.SucursalId is int sucursalId)
        {
            claims.Add(new Claim(
                ClaimsColorsin.SucursalId,
                sucursalId.ToString(CultureInfo.InvariantCulture)));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _ajustes.Issuer,
            Audience = _ajustes.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = emitidoEn,
            NotBefore = emitidoEn,
            Expires = expiraEn,
            SigningCredentials = _credenciales
        };

        return new TokenGenerado(Manejador.CreateToken(descriptor), expiraEn);
    }
}
