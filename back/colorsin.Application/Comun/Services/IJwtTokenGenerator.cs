using Colorsin.Application.Comun.Auth;
using Colorsin.Domain.Comun;

namespace Colorsin.Application.Comun.Services;

/// <summary>Token recien emitido, con el momento en que caduca.</summary>
/// <param name="Token">El JWT firmado, listo para <c>Authorization: Bearer</c>.</param>
/// <param name="Expiracion">Cuando deja de valer, en UTC.</param>
public sealed record TokenGenerado(string Token, DateTime Expiracion);

/// <summary>
/// Emite el token con el que un usuario ya autenticado se identifica en las
/// siguientes peticiones.
///
/// SOLO EMITE. No comprueba contrasenas ni decide si alguien puede entrar: para
/// cuando se llama a este servicio, esa decision ya se tomo. Separarlo importa
/// porque son dos responsabilidades con riesgos distintos, y mezclarlas hace
/// facil emitir un token por un camino que no verifico nada.
///
/// FIRMA CON HMAC-SHA256 y una clave simetrica: la misma clave firma y valida.
/// Eso sirve mientras quien emite y quien valida son el mismo servicio, que es
/// el caso. El dia que un tercero deba validar tokens sin poder emitirlos, hay
/// que pasar a un algoritmo asimetrico (RS256): con la clave simetrica,
/// cualquiera que pueda validar puede tambien falsificar.
///
/// QUE LLEVA EL TOKEN:
///
///   ClaimTypes.NameIdentifier   usuario.Id
///   ClaimTypes.Name             usuario.Nombre
///   ClaimTypes.Email            usuario.Email
///   ClaimTypes.Role             el rol, traducido por
///                               <see cref="RolesColorsin.ParaClaim"/>
///   sucursal_id                 usuario.SucursalId, SOLO si tiene sede
///   jti                         identificador unico de este token
///
/// El correo va ademas del nombre porque son cosas distintas: el nombre es para
/// mostrar y no es unico; el correo es con lo que se inicio sesion. Meter uno
/// en el lugar del otro obliga despues a adivinar cual es cual.
///
/// NADA DE ESTO ES SECRETO. El contenido de un JWT va firmado, no cifrado:
/// cualquiera que tenga el token puede leer los claims. La firma garantiza que
/// no se alteraron, no que nadie los vea. Por eso aqui no va nada que no pueda
/// leer el propio usuario.
/// </summary>
public interface IJwtTokenGenerator
{
    /// <summary>
    /// Emite un token para <paramref name="usuario"/>.
    ///
    /// Lanza si el rol no tiene equivalencia en <see cref="RolesColorsin"/>.
    /// </summary>
    TokenGenerado Generar(Usuario usuario);
}
