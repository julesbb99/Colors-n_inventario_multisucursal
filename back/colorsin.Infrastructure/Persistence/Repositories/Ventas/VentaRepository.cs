using Colorsin.Application.Ventas.Repositories;
using Colorsin.Domain.Ventas;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories.Ventas;

/// <inheritdoc cref="IVentaRepository"/>
public sealed class VentaRepository : IVentaRepository
{
    private readonly AppDbContext _db;

    public VentaRepository(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================================
    // CONSULTAS
    // =========================================================================

    public async Task<IReadOnlyList<Venta>> ObtenerAsync(
        int? sucursalId = null,
        int? clienteId = null,
        DateTime? desde = null,
        DateTime? hasta = null,
        int limite = 100,
        CancellationToken cancellationToken = default)
    {
        // Sin Include del detalle: este metodo alimenta listados, y cargar las
        // lineas de cien ventas convertiria una consulta en ciento una.
        var consulta = _db.Ventas
            .AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Sucursal)
            .Include(v => v.Usuario)
            .AsQueryable();

        if (sucursalId is int sid)
        {
            consulta = consulta.Where(v => v.SucursalId == sid);
        }

        if (clienteId is int cid)
        {
            consulta = consulta.Where(v => v.ClienteId == cid);
        }

        if (desde is DateTime d)
        {
            consulta = consulta.Where(v => v.Fecha >= d);
        }

        if (hasta is DateTime h)
        {
            consulta = consulta.Where(v => v.Fecha <= h);
        }

        return await consulta
            .OrderByDescending(v => v.Fecha)
            .ThenByDescending(v => v.Id)
            // Tope duro: la tabla crece sin limite y una consulta sin LIMIT
            // terminaria trayendola entera.
            .Take(Math.Clamp(limite, 1, 1000))
            .ToListAsync(cancellationToken);
    }

    public Task<Venta?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Ventas
            .AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Sucursal)
            .Include(v => v.Usuario)
            .Include(v => v.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(v => v.Detalles)
                .ThenInclude(d => d.Unidad)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<VentaDetalle?> ObtenerUltimaVentaDeProductoAsync(
        int productoId,
        int? sucursalId = null,
        CancellationToken cancellationToken = default)
    {
        var consulta = _db.VentaDetalles
            .AsNoTracking()
            .Include(d => d.Venta)
            .Include(d => d.Unidad)
            // Una linea sin precio no sirve de referencia: diria "la ultima vez
            // se cobro nada". Es el caso que dejaba la venta sin precio antes de
            // que existiera la lista.
            .Where(d => d.ProductoId == productoId && d.PrecioUnitario != null)
            .AsQueryable();

        if (sucursalId is int sid)
        {
            consulta = consulta.Where(d => d.Venta.SucursalId == sid);
        }

        return await consulta
            // Por fecha de la venta, y el id desempata: dos ventas del mismo
            // segundo son perfectamente posibles en un mostrador.
            .OrderByDescending(d => d.Venta.Fecha)
            .ThenByDescending(d => d.VentaId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    // =========================================================================
    // ESCRITURA
    // =========================================================================

    // EF inserta el encabezado y sus lineas en cascada: basta con agregar la
    // raiz, y las claves foraneas se rellenan solas con el id que asigne MySQL.
    public void AgregarVenta(Venta venta) => _db.Ventas.Add(venta);

    public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<T> EjecutarEnTransaccionAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken cancellationToken = default)
    {
        // La estrategia de ejecucion es obligatoria aqui: con
        // EnableRetryOnFailure activo, EF Core lanza una excepcion si se llama
        // a BeginTransaction por fuera de ella. La estrategia necesita envolver
        // toda la operacion para poder repetirla completa ante un fallo
        // transitorio de red.
        var estrategia = _db.Database.CreateExecutionStrategy();

        return await estrategia.ExecuteAsync(async ct =>
        {
            await using var transaccion = await _db.Database.BeginTransactionAsync(ct);

            // Si `operacion` lanza (incluidas las reglas de negocio de ventas,
            // que son excepciones), el await using revierte la transaccion
            // antes de propagar. Por eso no hace falta try/catch aqui.
            var resultado = await operacion(ct);

            await transaccion.CommitAsync(ct);
            return resultado;
        }, cancellationToken);
    }
}
