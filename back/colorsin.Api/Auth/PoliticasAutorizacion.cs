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
    /// Operaciones con consecuencia economica o de cierre de documento:
    /// comprometer dinero con un proveedor, dar por recibida una compra, decidir
    /// que un traslado no se atiende, ajustar stock a mano.
    ///
    /// Administrador General y Gerente de Sucursal. Deja fuera al Operador, no
    /// por desconfianza sino porque son decisiones de quien responde por el
    /// resultado de la sede, no de quien ejecuta el dia a dia.
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

        return opciones;
    }
}
