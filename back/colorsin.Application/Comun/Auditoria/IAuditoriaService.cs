namespace Colorsin.Application.Comun.Auditoria;

/// <summary>
/// Servicio transversal de trazabilidad. Lo usan todos los modulos para dejar
/// constancia de los cambios criticos.
///
/// Solo anexa: no hay metodos para editar ni borrar eventos. Una bitacora que
/// se puede reescribir no sirve como bitacora.
///
/// Esa garantia llega hasta donde llega la aplicacion. Quien tenga acceso
/// directo a MySQL todavia puede modificar la tabla; reforzarlo en la base
/// requiere un cambio de configuracion del servidor, explicado en la migracion
/// AuditoriaYTrazabilidadLote.
/// </summary>
public interface IAuditoriaService
{
    /// <summary>
    /// Deja constancia de un evento.
    ///
    /// IMPORTANTE: solo lo deja PREPARADO, no lo confirma. El evento se
    /// persiste cuando quien llama guarda su unidad de trabajo, de modo que
    /// entra en la MISMA transaccion que el cambio auditado. Es a proposito:
    /// si la operacion se revierte, su rastro tambien, y nunca queda un evento
    /// describiendo algo que no ocurrio. Al reves tampoco: no hay forma de que
    /// el cambio se confirme y la auditoria se pierda.
    /// </summary>
    /// <param name="modulo">Modulo de origen: 'Inventario', 'Compras', 'Ventas'.</param>
    /// <param name="accion">Que se hizo: 'RegistrarMovimiento', 'AjustarLote'.</param>
    /// <param name="usuarioId">Responsable. Debe existir en `usuarios` o la transaccion falla.</param>
    /// <param name="detalle">Contexto del cambio: ids, cantidades, saldo resultante.</param>
    Task RegistrarEventoAsync(
        string modulo,
        string accion,
        int usuarioId,
        string detalle,
        CancellationToken cancellationToken = default);
}
