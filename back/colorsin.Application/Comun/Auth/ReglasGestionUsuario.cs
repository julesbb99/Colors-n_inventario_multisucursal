using Colorsin.Domain.Comun;

namespace Colorsin.Application.Comun.Auth;

/// <summary>Por que no se puede cambiar el estado de ese perfil.</summary>
public enum ErrorGestionUsuario
{
    Ninguno = 0,

    /// <summary>Quien lo intenta no tiene rango para deshabilitar a nadie.</summary>
    RolSinPermiso,

    /// <summary>Tiene rango, pero no sobre ESE rol.</summary>
    RolDestinoNoPermitido,

    /// <summary>Un gerente solo gestiona a su propio equipo.</summary>
    SedeNoPermitida,

    /// <summary>Quien lo intenta no tiene sede asignada y su rol la exige.</summary>
    ActorSinSede,

    /// <summary>
    /// Se intenta deshabilitar la propia cuenta.
    ///
    /// Es la regla que mas agradece quien la encuentra: nadie que lo intente
    /// quiere quedarse fuera del sistema en ese mismo clic.
    /// </summary>
    NoSobreSiMismo,

    /// <summary>El perfil ya estaba en ese estado.</summary>
    SinCambio,

    /// <summary>El usuario no existe.</summary>
    NoEncontrado
}

/// <summary>
/// QUIEN PUEDE DESHABILITAR -Y REACTIVAR- A QUIEN.
///
///   AdministradorGeneral  gerentes y operadores, en cualquier sede
///   GerenteDeSucursal     operadores, SOLO de su propia sede
///   Operador              a nadie
///
/// Es la MISMA jerarquia de <see cref="ReglasCreacionUsuario"/>, y eso no es
/// casualidad: quien puede dar de alta a alguien es quien responde por esa
/// persona, asi que es quien tiene que poder darla de baja. Si las dos listas se
/// separaran, aparecerian cuentas que alguien creo y nadie puede cerrar.
///
/// DOS REGLAS QUE NO SON DE JERARQUIA:
///
/// 1. NADIE SE DESHABILITA A SI MISMO. No es una cuestion de rango -un
///    administrador tiene rango de sobra- sino de que el resultado es quedarse
///    fuera en el acto, sin forma de deshacerlo desde la aplicacion. Es el unico
///    clic del sistema que no se puede corregir con otro clic.
///
/// 2. NINGUN ADMINISTRADOR GENERAL SE DESHABILITA DESDE LA APLICACION, ni
///    siquiera por otro administrador. Por lo mismo que ninguno se crea desde
///    ella: son las cuentas que ven la red entera, y dos administradores
///    apagandose el uno al otro es un escenario sin salida. Se hace en la base,
///    que exige otro tipo de acceso y deja rastro fuera del sistema. La base
///    ademas impide apagar al ultimo que quede activo, con un trigger.
///
/// ESTA CLASE ES PURA: no consulta la base, no depende de HTTP y no conoce el
/// token. Recibe los datos y responde, igual que su hermana de creacion.
/// </summary>
public static class ReglasGestionUsuario
{
    /// <summary>Los roles cuyo estado puede cambiar <paramref name="rolActor"/>.</summary>
    public static IReadOnlyList<RolUsuario> RolesQuePuedeGestionar(RolUsuario rolActor) =>
        rolActor switch
        {
            RolUsuario.AdministradorGeneral =>
                [RolUsuario.GerenteDeSucursal, RolUsuario.Operador],

            RolUsuario.GerenteDeSucursal => [RolUsuario.Operador],

            _ => []
        };

    /// <summary>
    /// Si quien pide puede cambiar el estado de ese usuario.
    ///
    /// Vale para los DOS SENTIDOS -deshabilitar y reactivar- a proposito: quien
    /// puede apagar una cuenta tiene que poder volver a encenderla, o cada baja
    /// por error acabaria en la base de datos. Lo unico que cambia entre uno y
    /// otro es el mensaje, y ese lo pone el servicio.
    /// </summary>
    /// <param name="rolActor">Rol de quien pide, del token.</param>
    /// <param name="sedeActor">Sede de quien pide. Nula en el Administrador General.</param>
    /// <param name="actorId">Id de quien pide, para la regla de no tocarse a si mismo.</param>
    /// <param name="objetivoId">Id del usuario afectado.</param>
    /// <param name="rolObjetivo">Rol del usuario afectado.</param>
    /// <param name="sedeObjetivo">Sede del usuario afectado.</param>
    public static ResultadoGestion Validar(
        RolUsuario rolActor,
        int? sedeActor,
        int actorId,
        int objetivoId,
        RolUsuario rolObjetivo,
        int? sedeObjetivo)
    {
        // VA PRIMERO, antes que el rango. Un administrador pasaria todas las
        // demas comprobaciones sobre su propia cuenta, y el mensaje correcto no
        // es "no tienes permiso" sino "esto te deja fuera".
        if (actorId == objetivoId)
        {
            return ResultadoGestion.No(
                ErrorGestionUsuario.NoSobreSiMismo,
                "No puedes deshabilitar tu propia cuenta: te quedarias fuera del sistema en el " +
                "acto y no habria forma de volver a entrar desde la aplicacion. Si hay que " +
                "cerrarla, que lo haga otra persona con permiso.");
        }

        var gestionables = RolesQuePuedeGestionar(rolActor);

        if (gestionables.Count == 0)
        {
            return ResultadoGestion.No(
                ErrorGestionUsuario.RolSinPermiso,
                "Tu rol no puede habilitar ni deshabilitar perfiles.");
        }

        if (!gestionables.Contains(rolObjetivo))
        {
            return ResultadoGestion.No(
                ErrorGestionUsuario.RolDestinoNoPermitido,
                rolObjetivo == RolUsuario.AdministradorGeneral
                    ? "Un Administrador General no se deshabilita desde la aplicacion. Es una " +
                      "cuenta que ve la red entera; cerrarla es una operacion de base de datos."
                    : "Tu rol no alcanza para gestionar ese perfil. Un gerente solo gestiona a " +
                      "los operadores de su sede.");
        }

        if (rolActor == RolUsuario.AdministradorGeneral)
        {
            // No pertenece a ninguna sede, y eso significa TODAS.
            return ResultadoGestion.Ok();
        }

        if (sedeActor is not int propia)
        {
            return ResultadoGestion.No(
                ErrorGestionUsuario.ActorSinSede,
                "Tu usuario no tiene sede asignada, asi que no se puede determinar sobre que " +
                "equipo estarias actuando.");
        }

        if (sedeObjetivo != propia)
        {
            return ResultadoGestion.No(
                ErrorGestionUsuario.SedeNoPermitida,
                "Ese operador no es de tu sede. Cada gerente gestiona su propio equipo.");
        }

        return ResultadoGestion.Ok();
    }
}

/// <summary>Desenlace de la comprobacion.</summary>
/// <param name="Permitido">Si el cambio puede seguir.</param>
/// <param name="Error">Motivo del rechazo.</param>
/// <param name="Mensaje">Explicacion para una persona.</param>
public readonly record struct ResultadoGestion(
    bool Permitido,
    ErrorGestionUsuario Error,
    string Mensaje)
{
    public static ResultadoGestion Ok() =>
        new(true, ErrorGestionUsuario.Ninguno, string.Empty);

    public static ResultadoGestion No(ErrorGestionUsuario error, string mensaje) =>
        new(false, error, mensaje);
}
