using System.Globalization;
using System.Security.Claims;

namespace Colorsin.Application.Comun.Auth;

/// <summary>
/// Los claims propios de Colorsin y como leerlos.
///
/// Los estandar -id, nombre, correo, rol- viajan con los tipos de
/// <see cref="ClaimTypes"/> y se leen con las herramientas de siempre. El unico
/// propio es <see cref="SucursalId"/>, y esta aqui junto con su lector para que
/// ningun endpoint tenga que acordarse del nombre de la clave ni de convertir
/// el texto a numero por su cuenta.
/// </summary>
public static class ClaimsColorsin
{
    /// <summary>
    /// Sede a la que pertenece quien presenta el token. Es la clave con la que
    /// se filtran las consultas por sede.
    ///
    /// NO VIAJA SIEMPRE: el Administrador General no pertenece a ninguna sede
    /// (<c>usuarios.sucursal_id</c> es nulo para el) y su token sale sin este
    /// claim. Un claim con cadena vacia seria peor: obligaria a distinguir
    /// "sin sede" de "sede no informada" en cada lectura.
    /// </summary>
    public const string SucursalId = "sucursal_id";

    /// <summary>
    /// Sede del token, o <c>null</c> si no la lleva -caso del Administrador
    /// General- o si el valor no es un numero.
    ///
    /// OJO AL USARLO: <c>null</c> significa "sin sede asignada", que para el
    /// Administrador General quiere decir TODA la red, no NINGUNA. Quien filtre
    /// por sede debe decidir ese caso a proposito; devolver cero resultados
    /// seria interpretar el nulo al reves.
    /// </summary>
    public static int? LeerSucursalId(this ClaimsPrincipal principal)
    {
        // FindFirst y no FindFirstValue: ese ultimo es una extension de
        // ASP.NET Core, y esta capa no depende de ASP.NET Core ni debe hacerlo.
        var valor = principal.FindFirst(SucursalId)?.Value;

        return int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }

    /// <summary>
    /// Id del usuario del token, o <c>null</c> si falta o no es un numero.
    ///
    /// Sale de <see cref="ClaimTypes.NameIdentifier"/>. Es el valor que se pasa
    /// como <c>usuarioId</c> a los servicios que exigen trazabilidad, y por eso
    /// conviene leerlo del token y nunca del cuerpo de la peticion: el cuerpo lo
    /// escribe el cliente y puede decir cualquier cosa.
    /// </summary>
    public static int? LeerUsuarioId(this ClaimsPrincipal principal)
    {
        var valor = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }
}
