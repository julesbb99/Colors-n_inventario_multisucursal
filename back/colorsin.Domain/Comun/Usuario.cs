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

    // --- Navegacion ---
    public Sucursal? Sucursal { get; set; }
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
    public ICollection<NovedadTransferencia> NovedadesReportadas { get; set; } = new List<NovedadTransferencia>();
}
