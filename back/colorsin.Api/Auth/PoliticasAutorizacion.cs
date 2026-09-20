using Colorsin.Application.Comun.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Colorsin.Api.Auth;

/// <summary>
/// Politicas de autorizacion por rol.
///
/// POR QUE POLITICAS CON NOMBRE y no <c>RequireRole(...)</c> suelto en cada
/// endpoint. La lista de roles de una regla se repetiria en ocho sitios, y el
/// dia que cambie habria que acertar en los ocho; peor aun, el nombre de la
/// regla es lo que explica POR QUE ese endpoint esta restringido, y una lista de
/// roles no lo dice. <c>Supervision</c> se lee; <c>RequireRole("AdminGeneral",
/// "GerenteSucursal")</c> hay que interpretarlo.
///
/// EL EJE DE ROLES ES INDEPENDIENTE DEL EJE DE SEDE. Son dos preguntas
/// distintas y las dos tienen que responderse:
///
///   rol   QUE clase de operacion puedes hacer          -> este archivo, 401/403
///   sede  SOBRE QUE sede puedes hacerla                -> IUsuarioContexto, 403
///
/// Un gerente de Armenia pasa la politica de supervision y aun asi no puede
/// confirmar una compra de Cali. Las dos comprobaciones son necesarias y ninguna
/// sustituye a la otra.
/// </summary>
public static class PoliticasAutorizacion
{
    /// <summary>
    /// Operaciones que DECIDEN sobre el inventario sin un hecho externo que las
    /// respalde: ajustar stock a mano, registrar una merma, decidir que un
    /// traslado no se atiende.
    ///
    /// Administrador General y Gerente de Sucursal. Deja fuera al Operador, no
    /// por desconfianza sino porque son decisiones de quien responde por el
    /// resultado de la sede, no de quien ejecuta el dia a dia.
    ///
    /// NO incluye recibir una compra, aunque lo parezca: eso tiene su propia
    /// politica, <see cref="RecepcionMercancia"/>, y alli esta el porque.
    /// </summary>
    public const string Supervision = "supervision";

    /// <summary>
    /// Decisiones de alcance de RED, no de sede: dar de alta un proveedor,
    /// cambiar su lista de precios, retirarlo.
    ///
    /// Solo Administrador General. El motivo no es jerarquia por si misma: el
    /// catalogo de proveedores y sus precios son COMPARTIDOS por las tres sedes,
    /// asi que un cambio ahi lo hereda todo el mundo. Un gerente responde por su
    /// sede, y esto se le escapa del alcance: si pudiera retocar el precio de
    /// referencia, estaria moviendo el criterio con el que compran las otras dos.
    ///
    /// Notese que esto NO es supervision con un rol menos. Es otro eje: aqui no
    /// hay sede sobre la que comprobar nada, porque el dato no es de ninguna.
    /// </summary>
    public const string SoloAdminGeneral = "solo-admin-general";

    /// <summary>
    /// Registrar que llego mercancia de una compra.
    ///
    /// LOS TRES ROLES, y por eso NO es <see cref="Supervision"/>. Recibir es
    /// trabajo de bodega: quien abre las cajas y cuenta lo que llego es el
    /// operador, y era justo el unico que no podia anotarlo. Obligar a que lo
    /// tecleara un gerente no anadia control -el gerente no estuvo en la
    /// descarga- y solo conseguia que el stock quedara desactualizado hasta que
    /// alguien con rango pasara por el sistema.
    ///
    /// LO QUE NO SE AMPLIA. Es una politica aparte y no un rol mas en
    /// <see cref="Supervision"/>, porque esa cubre ademas los ajustes manuales de
    /// stock, las mermas y las decisiones sobre traslados. Recibir es ANOTAR UN
    /// HECHO -llegaron 250 litros-; un ajuste es corregir la realidad a mano sin
    /// nada que lo respalde, y eso sigue cerrado al operador. Meter al operador
    /// en Supervision le habria abierto las siete rutas de inventario de paso.
    ///
    /// EL EJE DE SEDE NO SE TOCA: el endpoint sigue llamando a
    /// <c>ExigirAccesoASucursal</c>, asi que un operador de Cali no puede
    /// ingresar mercancia en la bodega de Armenia.
    ///
    /// Se enumeran los tres roles en vez de pedir solo autenticacion para que un
    /// rol que se agregue manana -un auditor de solo lectura, por ejemplo- no
    /// herede el permiso por omision.
    /// </summary>
    public const string RecepcionMercancia = "recepcion-mercancia";

    /// <summary>
    /// Registra las politicas. Se llama desde el arranque, junto a
    /// <c>AddAuthorization</c>.
    /// </summary>
    public static AuthorizationOptions AgregarPoliticasColorsin(this AuthorizationOptions opciones)
    {
        // La lista de roles sale de RolesColorsin, no se escribe aqui: hay una
        // comprobacion mas que depende del contenido de la peticion -la novedad
        // critica- y tiene que usar exactamente la misma.
        opciones.AddPolicy(Supervision, politica => politica
            .RequireAuthenticatedUser()
            .RequireRole(RolesColorsin.Supervision));

        opciones.AddPolicy(SoloAdminGeneral, politica => politica
            .RequireAuthenticatedUser()
            .RequireRole(RolesColorsin.AdminGeneral));

        opciones.AddPolicy(RecepcionMercancia, politica => politica
            .RequireAuthenticatedUser()
            .RequireRole(
                RolesColorsin.AdminGeneral,
                RolesColorsin.GerenteSucursal,
                RolesColorsin.Operador));

        return opciones;
    }
}
