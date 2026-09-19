using Colorsin.Application.Inventario.Repositories;
using Colorsin.Domain.Inventario;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories;

/// <inheritdoc cref="IInventarioRepository"/>
public sealed class InventarioRepository : IInventarioRepository
{
    private readonly AppDbContext _db;

    public InventarioRepository(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================================
    // CONSULTAS
    // =========================================================================

    public async Task<IReadOnlyList<InventarioSucursal>> ObtenerExistenciasAsync(
        int? sucursalId = null,
        bool incluirInactivas = false,
        CancellationToken cancellationToken = default)
    {
        var consulta = _db.InventarioSucursales
            .AsNoTracking()
            .Include(i => i.Sucursal)
            .Include(i => i.Producto)
                .ThenInclude(p => p.UnidadBase)
            .AsQueryable();

        if (sucursalId is int id)
        {
            consulta = consulta.Where(i => i.SucursalId == id);
        }

        if (!incluirInactivas)
        {
            consulta = consulta.Where(i => i.Activo);
        }

        return await consulta
            // Las activas primero cuando se piden las dos: una baja es la
            // excepcion y no tiene por que mezclarse con el listado del dia.
            .OrderByDescending(i => i.Activo)
            .ThenBy(i => i.Sucursal.Nombre)
            .ThenBy(i => i.Producto.Nombre)
            .ToListAsync(cancellationToken);
    }

    public Task<InventarioSucursal?> ObtenerExistenciaPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.InventarioSucursales
            // SIN AsNoTracking, a diferencia de las consultas de arriba: esta
            // entidad se modifica y se guarda, y sin seguimiento los cambios se
            // perderian en silencio al llamar a SaveChanges.
            .Include(i => i.Sucursal)
            .Include(i => i.Producto)
                .ThenInclude(p => p.UnidadBase)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<InventarioSucursal?> ObtenerSaldoAsync(
        int sucursalId,
        int productoId,
        CancellationToken cancellationToken = default) =>
        _db.InventarioSucursales
            .AsNoTracking()
            .Include(i => i.Sucursal)
            .Include(i => i.Producto)
                .ThenInclude(p => p.UnidadBase)
            .FirstOrDefaultAsync(
                i => i.SucursalId == sucursalId && i.ProductoId == productoId,
                cancellationToken);

    public async Task<InventarioSucursal?> ObtenerSaldoParaActualizarAsync(
        int sucursalId,
        int productoId,
        CancellationToken cancellationToken = default)
    {
        // Se materializa con ToListAsync y se toma el primero en memoria, en vez
        // de FirstOrDefaultAsync. Con FirstOrDefaultAsync, EF Core compone un
        // LIMIT 1 encima del SQL crudo y para eso lo encierra en una subconsulta,
        // donde el FOR UPDATE deja de aplicar sobre la tabla real. La clausula
        // WHERE cae sobre el indice unico (sucursal_id, producto_id), asi que
        // como mucho vuelve una fila.
        //
        // Sin AsNoTracking a proposito: esta fila se va a modificar.
        var filas = await _db.InventarioSucursales
            .FromSqlInterpolated(
                $@"SELECT * FROM inventario_sucursal
                   WHERE sucursal_id = {sucursalId} AND producto_id = {productoId}
                   FOR UPDATE")
            .ToListAsync(cancellationToken);

        return filas.FirstOrDefault();
    }

    public async Task<IReadOnlyList<Lote>> ObtenerLotesPorVencimientoAsync(
        int sucursalId,
        int productoId,
        CancellationToken cancellationToken = default) =>
        await _db.Lotes
            .AsNoTracking()
            .Include(l => l.Producto)
            .Where(l => l.SucursalId == sucursalId
                     && l.ProductoId == productoId
                     && l.CantidadBase > 0)
            // FEFO. El primer OrderBy es el que manda los lotes sin vencimiento
            // al final: MySQL ordena los NULL primero, y sin esta linea lo que
            // no caduca se despacharia antes que lo que si caduca. El bool se
            // traduce a 0/1, y el 0 (tiene fecha) va delante.
            .OrderBy(l => l.FechaVencimiento == null)
            .ThenBy(l => l.FechaVencimiento)
            // Entre lotes que vencen el mismo dia, sale primero el que llego
            // primero (FIFO).
            .ThenBy(l => l.FechaIngreso)
            .ToListAsync(cancellationToken);

    public async Task<Lote?> ObtenerLoteParaActualizarAsync(
        int loteId,
        CancellationToken cancellationToken = default)
    {
        // Mismo motivo que en ObtenerSaldoParaActualizarAsync para no usar
        // FirstOrDefaultAsync: el LIMIT 1 romperia el FOR UPDATE.
        var filas = await _db.Lotes
            .FromSqlInterpolated($"SELECT * FROM lotes WHERE id = {loteId} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return filas.FirstOrDefault();
    }

    public async Task<Lote?> ObtenerLotePorNumeroParaActualizarAsync(
        int sucursalId,
        int productoId,
        string numeroLote,
        CancellationToken cancellationToken = default)
    {
        // El FOR UPDATE no basta por si solo: bloquea filas que ya existen, y el
        // caso problematico es justo el contrario -que todavia no exista
        // ninguna-, donde dos recepciones simultaneas del mismo lote no se ven
        // entre si y las dos deciden crearlo.
        //
        // Quien cierra ese hueco es el indice unico
        // `ux_lotes_producto_sucursal_numero` sobre (producto_id, sucursal_id,
        // numero_lote), que existe desde la migracion
        // 20260917161220_LoteUnicoUsuarioOrdenYRecepcionParcial: la segunda
        // insercion falla y su transaccion se revierte entera.
        //
        // Se sigue ordenando por id por si la base viniera de antes de esa
        // migracion con filas duplicadas ya creadas: al menos siempre se le suma
        // a la misma.
        var filas = await _db.Lotes
            .FromSqlInterpolated(
                $@"SELECT * FROM lotes
                   WHERE sucursal_id = {sucursalId}
                     AND producto_id = {productoId}
                     AND numero_lote = {numeroLote}
                   ORDER BY id
                   FOR UPDATE")
            .ToListAsync(cancellationToken);

        return filas.FirstOrDefault();
    }

    public void AgregarLote(Lote lote) => _db.Lotes.Add(lote);

    public async Task<IReadOnlyList<InventarioSucursal>> ObtenerAlertasStockBajoAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default)
    {
        var consulta = _db.InventarioSucursales
            .AsNoTracking()
            .Include(i => i.Sucursal)
            .Include(i => i.Producto)
                .ThenInclude(p => p.UnidadBase)
            // El criterio de alerta. Va en la consulta y no en memoria para no
            // traer todo el inventario de la red y descartarlo aqui.
            //
            // `i.Activo` esta aqui y no como filtro aparte por lo mismo: avisar
            // de reponer un producto que la sede dio de baja es ruido, y ademas
            // InventarioSucursalDto.EnAlerta ya devuelve false para esas filas.
            // Si esta consulta no lo excluyera, el tablero contaria alertas que
            // la tabla de existencias pinta como normales.
            .Where(i => i.Activo && i.CantidadBase <= i.StockMinimo);

        if (sucursalId is int id)
        {
            consulta = consulta.Where(i => i.SucursalId == id);
        }

        return await consulta
            // Lo mas critico primero: la mayor diferencia contra el minimo.
            .OrderByDescending(i => i.StockMinimo - i.CantidadBase)
            .ThenBy(i => i.Producto.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MovimientoInventario>> ObtenerMovimientosAsync(
        int? sucursalId = null,
        int? productoId = null,
        int limite = 100,
        CancellationToken cancellationToken = default)
    {
        var consulta = _db.MovimientosInventario
            .AsNoTracking()
            .Include(m => m.Sucursal)
            .Include(m => m.Producto)
            .Include(m => m.Usuario)
            .Include(m => m.Unidad)
            .Include(m => m.Lote)
            .AsQueryable();

        if (sucursalId is int sid)
        {
            consulta = consulta.Where(m => m.SucursalId == sid);
        }

        if (productoId is int pid)
        {
            consulta = consulta.Where(m => m.ProductoId == pid);
        }

        return await consulta
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id)
            // Tope duro: el libro mayor crece sin limite y una consulta sin
            // LIMIT terminaria trayendo la tabla entera.
            .Take(Math.Clamp(limite, 1, 1000))
            .ToListAsync(cancellationToken);
    }

    // =========================================================================
    // ESCRITURA
    // =========================================================================

    public void ActualizarCantidadBase(InventarioSucursal saldo, decimal nuevaCantidadBase) =>
        saldo.CantidadBase = nuevaCantidadBase;

    public void AgregarSaldo(InventarioSucursal saldo) =>
        _db.InventarioSucursales.Add(saldo);

    public void ActualizarCantidadLote(Lote lote, decimal nuevaCantidadBase) =>
        lote.CantidadBase = nuevaCantidadBase;

    public void AgregarMovimiento(MovimientoInventario movimiento) =>
        _db.MovimientosInventario.Add(movimiento);

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

            var resultado = await operacion(ct);

            await transaccion.CommitAsync(ct);
            return resultado;
        }, cancellationToken);
    }
}
