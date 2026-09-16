using Colorsin.Domain.Compras;
using Colorsin.Domain.Inventario;
using Colorsin.Domain.Transferencias;
using Colorsin.Domain.Ventas;

namespace Colorsin.Domain.Comun;

/// <summary>Sede fisica de Colorsin: la matriz o una sucursal de la red.</summary>
public class Sucursal
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string Ciudad { get; set; } = null!;
    public string? Direccion { get; set; }
    public RolRed RolRed { get; set; }
    public string? Descripcion { get; set; }

    // --- Navegacion ---
    public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    public ICollection<InventarioSucursal> Inventarios { get; set; } = new List<InventarioSucursal>();
    public ICollection<Lote> Lotes { get; set; } = new List<Lote>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
    public ICollection<OrdenCompra> OrdenesCompra { get; set; } = new List<OrdenCompra>();
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();

    /// <summary>Traslados que salen de esta sede.</summary>
    public ICollection<Transferencia> TransferenciasEnviadas { get; set; } = new List<Transferencia>();

    /// <summary>Traslados que llegan a esta sede.</summary>
    public ICollection<Transferencia> TransferenciasRecibidas { get; set; } = new List<Transferencia>();
}
