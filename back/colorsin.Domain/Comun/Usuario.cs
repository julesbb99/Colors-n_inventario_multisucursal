using Colorsin.Domain.Inventario;
using Colorsin.Domain.Transferencias;
using Colorsin.Domain.Ventas;

namespace Colorsin.Domain.Comun;

/// <summary>
/// Usuario del sistema. <see cref="SucursalId"/> es nulo a proposito: el
/// Administrador General no pertenece a una sede concreta.
/// </summary>
public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string Email { get; set; } = null!;

    /// <summary>Hash de la contrasena (BCrypt/Argon2). Nunca texto plano.</summary>
    public string PasswordHash { get; set; } = null!;

    public RolUsuario Rol { get; set; }
    public int? SucursalId { get; set; }

    /// <summary>
    /// <c>false</c> si el perfil esta deshabilitado: NO PUEDE INICIAR SESION.
    ///
    /// NO ES UN BORRADO, y no podria serlo: seis tablas referencian al usuario
    /// con ON DELETE RESTRICT -ventas, movimientos, traslados, ordenes de
    /// compra, novedades y la bitacora- porque ninguna de ellas puede quedarse
    /// sin responsable. Lo que se quiere al "eliminar" a alguien es que deje de
    /// entrar; su historia se queda donde esta.
    ///
    /// OJO CON LOS TOKENS YA EMITIDOS: deshabilitar no invalida el que la
    /// persona tenga en el navegador, porque el contexto de usuario se lee del
    /// token y no de la base. Deja de entrar cuando ese token caduca.
    /// </summary>
    public bool Activo { get; set; } = true;

    // --- Navegacion ---
    public Sucursal? Sucursal { get; set; }
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
    public ICollection<NovedadTransferencia> NovedadesReportadas { get; set; } = new List<NovedadTransferencia>();
}
