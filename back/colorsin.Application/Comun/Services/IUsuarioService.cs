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
    /// <param name="incluirInactivos">
    /// <c>false</c> -lo normal- deja fuera los perfiles deshabilitados. La
    /// pantalla que los administra pide los dos, para poder reactivarlos.
    /// </param>
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(
        int? sucursalId = null,
        bool incluirInactivos = false,
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

    /// <summary>
    /// Deshabilita o reactiva un perfil.
    ///
    /// DESHABILITAR NO BORRA. El usuario sigue en la base con toda su historia
    /// -sus ventas, sus movimientos, su rastro en la bitacora- porque esas filas
    /// no pueden quedarse sin responsable: seis tablas lo referencian con
    /// ON DELETE RESTRICT. Lo que cambia es que deja de poder iniciar sesion.
    ///
    /// LA JERARQUIA SE COMPRUEBA AQUI, igual que en el alta, y es la misma:
    /// ver <see cref="ReglasGestionUsuario"/>. Mas dos reglas propias: nadie se
    /// deshabilita a si mismo, y ningun Administrador General se deshabilita
    /// desde la aplicacion.
    ///
    /// OJO CON LOS TOKENS YA EMITIDOS: quien tenga sesion abierta sigue
    /// entrando hasta que su token caduque, porque el contexto de usuario se lee
    /// del token y no de la base en cada peticion.
    /// </summary>
    /// <param name="id">Usuario afectado.</param>
    /// <param name="activo"><c>false</c> deshabilita, <c>true</c> reactiva.</param>
    /// <param name="actor">
    /// Quien lo pide: su id, su rol y su sede. Sale del token, NUNCA del cuerpo.
    /// </param>
    Task<ResultadoUsuario> CambiarEstadoAsync(
        int id,
        bool activo,
        CreadorUsuario actor,
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
