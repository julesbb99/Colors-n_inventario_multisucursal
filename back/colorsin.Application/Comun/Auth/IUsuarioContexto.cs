namespace Colorsin.Application.Comun.Auth;

/// <summary>
/// Quien esta haciendo la peticion, leido del token y no del cuerpo del JSON.
///
/// POR QUE ESTO EXISTE. Mientras el id del usuario llegue en el cuerpo de la
/// peticion, cualquiera puede escribir el que quiera: registrar una venta a
/// nombre de otro, firmar un traslado como el gerente, ensuciar la bitacora de
/// auditoria con responsables falsos. Y la bitacora es justo lo que no puede
/// mentir. El token lo firma el servidor; el cuerpo lo escribe el cliente.
///
/// La regla, entonces: el id del usuario para auditoria y trazabilidad sale
/// SIEMPRE de aqui. Nunca de un DTO.
///
/// QUE NO ES: no consulta la base. Todo sale de los claims del token, asi que
/// refleja como estaba el usuario cuando inicio sesion, no como esta ahora. Si
/// a alguien le cambian el rol o la sede, su token sigue diciendo lo anterior
/// hasta que caduque. Es el precio de no ir a la base en cada peticion, y se
/// acota con la vigencia del token.
/// </summary>
public interface IUsuarioContexto
{
    /// <summary>Si hay un token valido en la peticion.</summary>
    bool EstaAutenticado { get; }

    /// <summary>Id del usuario, o <c>null</c> si no hay token.</summary>
    int? UsuarioId { get; }

    /// <summary>
    /// Rol: 'AdminGeneral', 'GerenteSucursal' u 'Operador'. Las constantes
    /// estan en <see cref="RolesColorsin"/>.
    /// </summary>
    string? Rol { get; }

    /// <summary>
    /// Sede asignada, del claim <c>sucursal_id</c>.
    ///
    /// <c>null</c> en el Administrador General, que no pertenece a ninguna. OJO:
    /// para el no significa "ninguna sede" sino "todas"; ver
    /// <see cref="ResolverFiltroSucursal"/>.
    /// </summary>
    int? SucursalId { get; }

    /// <summary>Atajo de <c>Rol == RolesColorsin.AdminGeneral</c>.</summary>
    bool EsAdminGeneral { get; }

    /// <summary>
    /// Id del usuario para auditoria. Lanza si no hay token.
    ///
    /// Devuelve <c>int</c> y no <c>int?</c> a proposito: quien audita necesita un
    /// responsable si o si -la columna tiene clave foranea obligatoria- y un
    /// nulo ahi solo se puede resolver inventandose algo. Mejor fallar.
    /// </summary>
    int UsuarioIdRequerido();

    /// <summary>
    /// LA REGLA DE AISLAMIENTO ENTRE SEDES. Traduce lo que el cliente pidio a lo
    /// que de verdad se le va a consultar:
    ///
    ///   AdminGeneral
    ///     sin sucursalId  -> null, o sea toda la red
    ///     con sucursalId  -> esa sede, la que sea
    ///
    ///   GerenteSucursal / Operador
    ///     sin sucursalId  -> SU sede. No se le devuelve la red entera por
    ///                        omitir un parametro: omitirlo no puede ser la via
    ///                        para ver mas de lo que le toca.
    ///     con SU sede     -> su sede
    ///     con OTRA sede   -> <see cref="AccesoDenegadoException"/>, que sale
    ///                        como 403
    ///     sin sede propia -> <see cref="AccesoDenegadoException"/>: un usuario
    ///                        de sede sin sede asignada no puede ver nada, y
    ///                        tratarlo como administrador seria la peor lectura
    ///                        posible de ese dato faltante
    ///
    /// EL VALOR QUE DEVUELVE ES EL UNICO QUE DEBE LLEGAR A LA CONSULTA. Si algun
    /// dia alguien usa el parametro original en vez de este, el aislamiento se
    /// cae entero y sin ruido.
    /// </summary>
    int? ResolverFiltroSucursal(int? sucursalIdSolicitado);
}
