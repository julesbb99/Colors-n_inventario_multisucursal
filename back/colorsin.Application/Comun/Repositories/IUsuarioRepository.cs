using Colorsin.Domain.Comun;

namespace Colorsin.Application.Comun.Repositories;

/// <summary>Acceso a los usuarios del sistema.</summary>
public interface IUsuarioRepository
{
    /// <summary>
    /// Todos los usuarios, ordenados por nombre.
    /// Trae cargada la sede (<c>Usuario.Sucursal</c>) para que el mapeo a DTO
    /// no dispare una consulta por fila.
    /// </summary>
    /// <param name="incluirInactivos">
    /// <c>false</c> deja fuera los perfiles deshabilitados, que es lo que quiere
    /// cualquier desplegable de asignacion: no se le asigna trabajo a alguien
    /// que no puede entrar.
    /// </param>
    Task<IReadOnlyList<Usuario>> ObtenerTodosAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Usuarios de una sede concreta. Sirve para que un Gerente de Sucursal
    /// vea solo su equipo y no toda la red.
    /// </summary>
    Task<IReadOnlyList<Usuario>> ObtenerPorSucursalAsync(
        int sucursalId,
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default);

    /// <summary>El usuario con ese id, o <c>null</c> si no existe. Incluye la sede.</summary>
    Task<Usuario?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Como <see cref="ObtenerPorIdAsync"/>, pero RASTREADO para poder
    /// modificarlo. Incluye tambien los deshabilitados: reactivar uno exige
    /// poder encontrarlo.
    /// </summary>
    Task<Usuario?> ObtenerParaActualizarAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// El usuario con ese correo, o <c>null</c>. Incluye la sede.
    ///
    /// ES EL METODO DEL INICIO DE SESION, y por eso se diferencia de los demas
    /// en dos cosas:
    ///
    ///   1. La entidad viene RASTREADA, no con AsNoTracking, para que se pueda
    ///      actualizar el hash sin volver a consultar cuando toque subir el
    ///      costo. El resto de lecturas de este repositorio no rastrean.
    ///   2. El <c>PasswordHash</c> viene cargado, que es lo que hace falta para
    ///      comprobar la contrasena. Nunca debe salir de la capa de aplicacion:
    ///      quien devuelva usuarios hacia afuera usa <c>UsuarioDto</c>, que no
    ///      lo tiene.
    ///
    /// La comparacion NO distingue mayusculas, pero no porque aqui se normalice
    /// nada: la columna usa la intercalacion utf8mb4_0900_ai_ci, que ya compara
    /// asi. Meter un ToLower() en la consulta solo conseguiria que MySQL dejara
    /// de usar el indice unico uq_usuarios_email.
    /// </summary>
    Task<Usuario?> ObtenerPorEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma lo que haya preparado: el hash actualizado, el evento de
    /// auditoria del inicio de sesion, o los dos en la misma transaccion.
    /// </summary>
    /// <summary>
    /// Crea un usuario. NO guarda: deja el cambio preparado.
    ///
    /// Quien confirma es el servicio, para que el evento de auditoria del alta
    /// entre en el mismo guardado que el usuario. Si se confirmaran por separado
    /// podria quedar una cuenta creada sin rastro de quien la creo, que es justo
    /// lo que la bitacora existe para impedir.
    /// </summary>
    void Agregar(Usuario usuario);

    Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default);
}
