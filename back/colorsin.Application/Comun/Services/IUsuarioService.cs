using Colorsin.Application.Comun.Auth;
using Colorsin.Application.Comun.DTOs;
using Colorsin.Domain.Comun;

namespace Colorsin.Application.Comun.Services;

/// <summary>Consultas sobre los usuarios del sistema.</summary>
public interface IUsuarioService
{
    /// <summary>
    /// Usuarios de toda la red, o solo los de una sede si se pasa
    /// <paramref name="sucursalId"/>.
    /// </summary>
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>El usuario con ese id, o <c>null</c> si no existe.</summary>
    Task<UsuarioDto?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Da de alta un usuario.
    ///
    /// LA REGLA DE QUIEN PUEDE CREAR A QUIEN SE COMPRUEBA AQUI, no en el
    /// endpoint. Es una regla de negocio -un gerente da de alta operadores de su
    /// sede y nada mas- y dejarla en la capa HTTP la ataria a que el proximo
    /// endpoint se acuerde de repetirla. Ver <see cref="ReglasCreacionUsuario"/>.
    ///
    /// No lanza excepciones por reglas de negocio. Revisa
    /// <see cref="ResultadoUsuario.Exito"/>.
    /// </summary>
    /// <param name="peticion">Datos del usuario nuevo, con su contrasena en claro.</param>
    /// <param name="creador">
    /// Quien esta dando de alta: su id, su rol y su sede. Sale del token, NUNCA
    /// del cuerpo de la peticion.
    /// </param>
    Task<ResultadoUsuario> CrearAsync(
        CrearUsuarioDto peticion,
        CreadorUsuario creador,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Quien da de alta, tal como lo afirma su token.
///
/// Va como un tipo propio y no como tres parametros sueltos porque los tres se
/// leen del mismo sitio y se usan juntos: separarlos invita a pasar el rol de
/// uno con la sede de otro, que es exactamente el error que rompe el
/// aislamiento.
/// </summary>
/// <param name="UsuarioId">Responsable, para la auditoria.</param>
/// <param name="Rol">Rol del creador.</param>
/// <param name="SucursalId">Sede del creador. Nula en el Administrador General.</param>
public readonly record struct CreadorUsuario(
    int UsuarioId,
    RolUsuario Rol,
    int? SucursalId);
