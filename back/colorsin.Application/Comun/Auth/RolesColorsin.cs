using Colorsin.Domain.Comun;

namespace Colorsin.Application.Comun.Auth;

/// <summary>
/// Los roles tal como viajan DENTRO del token, y la unica traduccion autorizada
/// desde <see cref="RolUsuario"/>.
///
/// EL PROBLEMA QUE RESUELVE ESTA CLASE. El mismo rol se escribe de tres formas
/// distintas en el sistema:
///
///   ENUM de MySQL       'Administrador General'   (con espacios)
///   enum del dominio     AdministradorGeneral
///   claim del token      AdminGeneral
///
/// Las tres son legitimas -la base ya estaba escrita asi, y un claim con
/// espacios es fragil en <c>[Authorize(Roles = "...")]</c>, que separa por
/// comas- pero tres grafias sueltas es exactamente como una regla de
/// autorizacion termina sin coincidir con nada y dejando pasar o bloqueando a
/// quien no debe, en silencio.
///
/// Por eso: la traduccion vive SOLO en <see cref="ParaClaim"/>, y las cadenas
/// solo existen como constantes. Un atributo se escribe
/// <c>[Authorize(Roles = RolesColorsin.AdminGeneral)]</c>, nunca con la cadena
/// literal, y asi el compilador se encarga de que coincidan.
/// </summary>
public static class RolesColorsin
{
    /// <summary>No pertenece a ninguna sede: ve toda la red.</summary>
    public const string AdminGeneral = "AdminGeneral";

    /// <summary>Responsable de una sede.</summary>
    public const string GerenteSucursal = "GerenteSucursal";

    /// <summary>Opera el dia a dia de una sede.</summary>
    public const string Operador = "Operador";

    /// <summary>
    /// El rol del dominio, como cadena para el claim.
    ///
    /// Lanza si aparece un rol sin equivalencia, en vez de devolver algo por
    /// defecto. Es deliberado: si algun dia se agrega un rol al enum y se olvida
    /// esta lista, el fallo tiene que ser ruidoso al emitir el token. La
    /// alternativa -un rol vacio o un rol equivocado- produce un token valido
    /// que falla la autorizacion mas adelante, donde ya no se ve la causa.
    /// </summary>
    public static string ParaClaim(RolUsuario rol) => rol switch
    {
        RolUsuario.AdministradorGeneral => AdminGeneral,
        RolUsuario.GerenteDeSucursal => GerenteSucursal,
        RolUsuario.Operador => Operador,
        _ => throw new ArgumentOutOfRangeException(
            nameof(rol), rol,
            "Rol sin equivalencia en el token. Agregalo a RolesColorsin.ParaClaim.")
    };
}
