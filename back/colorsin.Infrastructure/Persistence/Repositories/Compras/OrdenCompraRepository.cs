using Colorsin.Application.Compras.Repositories;
using Colorsin.Domain.Compras;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories.Compras;

/// <inheritdoc cref="IOrdenCompraRepository"/>
public sealed class OrdenCompraRepository : IOrdenCompraRepository
{
    private readonly AppDbContext _db;

    public OrdenCompraRepository(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================================
    // CONSULTAS
    // =========================================================================

    public async Task<IReadOnlyList<OrdenCompra>> ObtenerAsync(
        int? sucursalId = null,
        int? proveedorId = null,
        EstadoOrdenCompra? estado = null,
        int limite = 100,
        CancellationToken cancellationToken = default)
    {
        // CON el detalle, aunque el listado no devuelva las lineas: hacen falta
        // para calcular el total y cuantas lineas siguen esperando mercancia,
        // que son dos columnas de ese listado. Sin este Include el total salia
        // en cero para todas las ordenes.
        //
        // No son "ciento una consultas": EF Core resuelve un Include de
        // coleccion en UNA sola consulta con JOIN. Lo que si hace es repetir el
        // encabezado por cada linea, y por eso NO se incluyen aqui Producto ni
        // Unidad de cada linea: el listado no los necesita -solo suma importes-
        // y multiplicarian el ancho de las filas sin aportar nada.
        var consulta = _db.OrdenesCompra
            .AsNoTracking()
            .Include(o => o.Proveedor)
            .Include(o => o.Sucursal)
            .Include(o => o.Usuario)
            .Include(o => o.Detalles)
            .AsQueryable();

        if (sucursalId is int sid)
        {
            consulta = consulta.Where(o => o.SucursalId == sid);
        }

        if (proveedorId is int pid)
        {
            consulta = consulta.Where(o => o.ProveedorId == pid);
        }

        if (estado is EstadoOrdenCompra e)
        {
            consulta = consulta.Where(o => o.Estado == e);
        }

        return await consulta
            .OrderByDescending(o => o.Fecha)
            .ThenByDescending(o => o.Id)
            // Tope duro: la tabla crece sin limite y una consulta sin LIMIT
            // terminaria trayendola entera.
            .Take(Math.Clamp(limite, 1, 1000))
            .ToListAsync(cancellationToken);
    }

    public Task<OrdenCompra?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.OrdenesCompra
            .AsNoTracking()
            .Include(o => o.Proveedor)
            .Include(o => o.Sucursal)
            .Include(o => o.Usuario)
            .Include(o => o.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(o => o.Detalles)
                .ThenInclude(d => d.Unidad)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<OrdenCompra?> ObtenerParaRecibirAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Paso 1: el encabezado, con la fila bloqueada.
        //
        // Se materializa con ToListAsync en vez de FirstOrDefaultAsync por lo
        // mismo que en el repositorio de inventario: FirstOrDefaultAsync compone
        // un LIMIT 1 y para ello encierra el SQL crudo en una subconsulta, donde
        // el FOR UPDATE deja de aplicar sobre la tabla real. El WHERE cae sobre
        // la clave primaria, asi que vuelve una fila como maximo.
        //
        // Sin AsNoTracking: el estado de esta orden se va a modificar.
        var filas = await _db.OrdenesCompra
            .FromSqlInterpolated($"SELECT * FROM ordenes_compra WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var orden = filas.FirstOrDefault();
        if (orden is null)
        {
            return null;
        }

        // Paso 2: el detalle, en una consulta aparte.
        //
        // No se puede encadenar Include() sobre el FromSql de arriba: eso seria
        // componer, y volveria a romper el FOR UPDATE. Con carga explicita el
        // bloqueo del encabezado se mantiene y las lineas quedan rastreadas.
        //
        // Producto viene incluido porque la recepcion necesita su UnidadBaseId
        // para convertir lo recibido a unidad base.
        await _db.Entry(orden)
            .Collection(o => o.Detalles)
            .Query()
            .Include(d => d.Producto)
            .Include(d => d.Unidad)
            .LoadAsync(cancellationToken);

        return orden;
    }

    // =========================================================================
    // ESCRITURA
    // =========================================================================

    // EF inserta el encabezado y sus lineas en cascada: basta con agregar la
    // raiz, y las claves foraneas se rellenan solas con el id que asigne MySQL.
    public void AgregarOrden(OrdenCompra orden) => _db.OrdenesCompra.Add(orden);

    public void ActualizarEstado(OrdenCompra orden, EstadoOrdenCompra estado) =>
        orden.Estado = estado;

    public void ActualizarCantidadRecibida(OrdenCompraDetalle detalle, decimal cantidadRecibida) =>
        detalle.CantidadRecibida = cantidadRecibida;

    public Task<OrdenCompra?> ObtenerParaEditarAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.OrdenesCompra
            // Con seguimiento: esta orden se modifica y se guarda.
            .Include(o => o.Detalles)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public void QuitarDetalles(IEnumerable<OrdenCompraDetalle> detalles) =>
        _db.OrdenCompraDetalles.RemoveRange(detalles);

    public void AgregarDetalle(OrdenCompraDetalle detalle) =>
        _db.OrdenCompraDetalles.Add(detalle);

    public Task<OrdenCompraDetalle?> ObtenerUltimaLineaConPrecioAsync(
        int productoId,
        int proveedorId,
        CancellationToken cancellationToken = default) =>
        _db.OrdenCompraDetalles
            .AsNoTracking()
            .Include(d => d.Unidad)
            .Include(d => d.OrdenCompra)
            .Where(d =>
                d.ProductoId == productoId &&
                d.OrdenCompra.ProveedorId == proveedorId &&
                d.PrecioUnitario != null &&
                d.OrdenCompra.Estado != EstadoOrdenCompra.Cancelada)
            // Por fecha de la orden y, a igualdad, por id: dos ordenes del mismo
            // dia tienen la misma `fecha` si se crearon en el mismo segundo, y
            // sin el desempate el "ultimo precio" seria el que MySQL devolviera
            // primero, que no esta definido.
            .OrderByDescending(d => d.OrdenCompra.Fecha)
            .ThenByDescending(d => d.OrdenCompraId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
