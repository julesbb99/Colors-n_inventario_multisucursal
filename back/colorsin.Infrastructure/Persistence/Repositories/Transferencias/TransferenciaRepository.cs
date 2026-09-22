using Colorsin.Application.Transferencias.Repositories;
using Colorsin.Domain.Inventario;
using Colorsin.Domain.Transferencias;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories.Transferencias;

/// <inheritdoc cref="ITransferenciaRepository"/>
public sealed class TransferenciaRepository : ITransferenciaRepository
{
    private readonly AppDbContext _db;

    public TransferenciaRepository(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================================
    // CONSULTAS
    // =========================================================================

    public async Task<IReadOnlyList<Transferencia>> ObtenerAsync(
        int? sucursalOrigenId = null,
        int? sucursalDestinoId = null,
        EstadoTransferencia? estado = null,
        int limite = 100,
        CancellationToken cancellationToken = default)
    {
        // SIN MOVIMIENTOS pero CON NOVEDADES, y la asimetria es deliberada.
        //
        // Los movimientos son el libro mayor: un traslado puede tener uno por
        // lote de salida y otro por lote de entrada, y cargarlos para cien
        // filas trae cientos de renglones que el listado no pinta.
        //
        // Las novedades si las pinta: la columna de la tabla muestra el TIPO y
        // la cantidad afectada de cada una, no un contador, y ademas hay que
        // saber cuales siguen abiertas para ofrecer el cierre. Son pocas por
        // traslado -lo normal es ninguna o una- asi que el join cuesta poco y
        // la alternativa seria una consulta por fila.
        var consulta = _db.Transferencias
            .AsNoTracking()
            .Include(t => t.Producto)
                // La unidad BASE del producto, que no es la del traslado: es la
                // de las cantidades de los lotes. Sin esto el DTO la manda nula
                // y la pantalla rotularia litros con el simbolo del traslado.
                .ThenInclude(p => p.UnidadBase)
            .Include(t => t.SucursalOrigen)
            .Include(t => t.SucursalDestino)
            .Include(t => t.Transportadora)
            .Include(t => t.Unidad)
            .Include(t => t.Usuario)
            .Include(t => t.Novedades)
                .ThenInclude(n => n.Usuario)
            // Y quien la cerro, que no es la misma persona: entre reportar y
            // resolver pasan dias. Sin este Include el nombre volveria vacio, y
            // vacio es indistinguible de "todavia no se ha cerrado".
            .Include(t => t.Novedades)
                .ThenInclude(n => n.UsuarioCierre)
            .AsQueryable();

        if (sucursalOrigenId is int origen)
        {
            consulta = consulta.Where(t => t.SucursalOrigenId == origen);
        }

        if (sucursalDestinoId is int destino)
        {
            consulta = consulta.Where(t => t.SucursalDestinoId == destino);
        }

        if (estado is EstadoTransferencia e)
        {
            consulta = consulta.Where(t => t.Estado == e);
        }

        return await consulta
            .OrderByDescending(t => t.FechaSolicitud)
            .ThenByDescending(t => t.Id)
            // Tope duro: la tabla crece sin limite y una consulta sin LIMIT
            // terminaria trayendola entera.
            .Take(Math.Clamp(limite, 1, 1000))
            .ToListAsync(cancellationToken);
    }

    public Task<Transferencia?> ObtenerPorIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        _db.Transferencias
            .AsNoTracking()
            .Include(t => t.Producto)
                .ThenInclude(p => p.UnidadBase)
            .Include(t => t.SucursalOrigen)
            .Include(t => t.SucursalDestino)
            .Include(t => t.Transportadora)
            .Include(t => t.Unidad)
            .Include(t => t.Usuario)
            .Include(t => t.Novedades)
                .ThenInclude(n => n.Usuario)
            .Include(t => t.Novedades)
                .ThenInclude(n => n.UsuarioCierre)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Transferencia>> ObtenerParaReporteAsync(
        DateTime? desde = null,
        DateTime? hasta = null,
        int? sucursalId = null,
        CancellationToken cancellationToken = default)
    {
        // SIN TOPE DE FILAS, al contrario que el listado, y a proposito: un
        // informe recortado a las cien mas recientes daria porcentajes de una
        // muestra arbitraria y los presentaria como los del periodo. Lo que
        // acota aqui es el periodo, que quien consulta elige.
        //
        // El filtro -periodo y sede por cualquiera de sus dos lados- se comparte
        // con el detalle y su conteo: ver FiltrarParaReporte.
        return await FiltrarParaReporte(desde, hasta, sucursalId)
            .Include(t => t.SucursalOrigen)
            .Include(t => t.SucursalDestino)
            .Include(t => t.Novedades)
            .OrderBy(t => t.FechaSolicitud)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transferencia>> ObtenerDetalleParaReporteAsync(
        DateTime? desde = null,
        DateTime? hasta = null,
        int? sucursalId = null,
        int limite = 200,
        CancellationToken cancellationToken = default) =>
        await FiltrarParaReporte(desde, hasta, sucursalId)
            .Include(t => t.Producto)
                .ThenInclude(p => p.UnidadBase)
            .Include(t => t.SucursalOrigen)
            .Include(t => t.SucursalDestino)
            .Include(t => t.Transportadora)
            .Include(t => t.Unidad)
            .Include(t => t.Novedades)
            // Del mas reciente al mas antiguo: es el orden en que se buscan las
            // cosas en un informe -lo de ayer antes que lo del mes pasado- y es
            // el contrario del agregado, que va cronologico porque alimenta
            // sumas y no una lista que alguien recorre con la vista.
            .OrderByDescending(t => t.FechaSolicitud)
            .ThenByDescending(t => t.Id)
            .Take(Math.Clamp(limite, 1, 1000))
            .ToListAsync(cancellationToken);

    public Task<int> ContarParaReporteAsync(
        DateTime? desde = null,
        DateTime? hasta = null,
        int? sucursalId = null,
        CancellationToken cancellationToken = default) =>
        // Sin Include ni Take: un COUNT no trae filas, asi que cargar las
        // navegaciones aqui seria trabajo para descartarlo.
        FiltrarParaReporte(desde, hasta, sucursalId).CountAsync(cancellationToken);

    /// <summary>
    /// El filtro que comparten las tres consultas del informe: periodo por
    /// fecha de solicitud y sede por CUALQUIERA de sus dos lados.
    ///
    /// Va en un solo sitio a proposito: si el listado y su conteo filtraran
    /// distinto, el aviso de "hay mas" saldria cuando no debe.
    /// </summary>
    private IQueryable<Transferencia> FiltrarParaReporte(
        DateTime? desde,
        DateTime? hasta,
        int? sucursalId)
    {
        var consulta = _db.Transferencias.AsNoTracking().AsQueryable();

        if (desde is DateTime d)
        {
            consulta = consulta.Where(t => t.FechaSolicitud >= d);
        }

        if (hasta is DateTime h)
        {
            consulta = consulta.Where(t => t.FechaSolicitud <= h);
        }

        // Una sede participa por sus dos lados: lo que despacha y lo que recibe.
        if (sucursalId is int s)
        {
            consulta = consulta.Where(
                t => t.SucursalOrigenId == s || t.SucursalDestinoId == s);
        }

        return consulta;
    }

    public Task<NovedadTransferencia?> ObtenerNovedadParaOperarAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        // Con seguimiento: esta fila se cierra. No lleva FOR UPDATE porque el
        // traslado si lo lleva -se bloquea en la misma transaccion- y es ese el
        // que decide si se cierra o no.
        _db.NovedadesTransferencia
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public Task<int> ContarNovedadesAbiertasAsync(
        int transferenciaId,
        int? exceptoNovedadId = null,
        CancellationToken cancellationToken = default)
    {
        var consulta = _db.NovedadesTransferencia
            .AsNoTracking()
            .Where(n => n.TransferenciaId == transferenciaId
                     && n.Estado == EstadoNovedad.Abierta);

        // Se saca en una variable antes del Where: EF no traduce el acceso a
        // `.Value` de un nullable capturado dentro del arbol de expresion.
        if (exceptoNovedadId is int excepto)
        {
            consulta = consulta.Where(n => n.Id != excepto);
        }

        return consulta.CountAsync(cancellationToken);
    }

    public async Task<Transferencia?> ObtenerParaOperarAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        // Se materializa con ToListAsync en vez de FirstOrDefaultAsync por lo
        // mismo que en los otros repositorios: FirstOrDefaultAsync compone un
        // LIMIT 1 y para ello encierra el SQL crudo en una subconsulta, donde el
        // FOR UPDATE deja de aplicar sobre la tabla real. El WHERE cae sobre la
        // clave primaria, asi que vuelve una fila como maximo.
        //
        // Sin AsNoTracking: el estado de este traslado se va a modificar.
        var filas = await _db.Transferencias
            .FromSqlInterpolated($"SELECT * FROM transferencias WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);

        return filas.FirstOrDefault();
    }

    public async Task<IReadOnlyList<MovimientoInventario>> ObtenerMovimientosDespachoAsync(
        int transferenciaId,
        CancellationToken cancellationToken = default) =>
        await _db.MovimientosInventario
            .AsNoTracking()
            .Include(m => m.Lote)
            .Include(m => m.Sucursal)
            .Where(m => m.TransferenciaId == transferenciaId
                     && m.Tipo == TipoMovimiento.Retiro)
            // El mismo orden FEFO en que salieron: primero el lote que vencia
            // antes. Asi, si llega menos de lo despachado, se completa antes el
            // lote mas urgente. Los movimientos sin lote van al final, igual que
            // en la consulta FEFO de inventario.
            .OrderBy(m => m.Lote == null)
            .ThenBy(m => m.Lote!.FechaVencimiento == null)
            .ThenBy(m => m.Lote!.FechaVencimiento)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MovimientoInventario>> ObtenerMovimientosAsync(
        int transferenciaId,
        CancellationToken cancellationToken = default) =>
        await _db.MovimientosInventario
            .AsNoTracking()
            .Include(m => m.Lote)
            .Include(m => m.Sucursal)
            .Where(m => m.TransferenciaId == transferenciaId)
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LoteDeTransferencia>> ObtenerLotesDeTransferenciasAsync(
        IReadOnlyCollection<int> transferenciaIds,
        CancellationToken cancellationToken = default)
    {
        // Sin ids no hay nada que preguntar, y un IN () vacio es SQL invalido en
        // algunos proveedores: se corta antes de ir a la base.
        if (transferenciaIds.Count == 0)
        {
            return [];
        }

        // SOLO LOS RETIROS. Son los movimientos del despacho, que dicen que
        // salio de verdad del origen. El destino recrea esos mismos numeros al
        // recibir, asi que incluir los ingresos daria la misma lista con el
        // doble de filas.
        //
        // Se agrupa por lote porque FEFO puede partir una cantidad en dos
        // movimientos del MISMO lote -pasa si el reparto se recalcula- y la
        // tabla tiene que mostrar un lote una vez, con su total.
        var filas = await _db.MovimientosInventario
            .AsNoTracking()
            .Where(m => m.TransferenciaId != null
                     && transferenciaIds.Contains(m.TransferenciaId.Value)
                     && m.Tipo == TipoMovimiento.Retiro)
            .GroupBy(m => new
            {
                TransferenciaId = m.TransferenciaId!.Value,
                m.LoteId,
                // El numero y el vencimiento entran en la clave y no como
                // agregado: son constantes dentro de un lote, y sacarlos con un
                // Max() obligaria a EF a una subconsulta correlacionada por
                // cada grupo.
                NumeroLote = m.Lote != null ? m.Lote.NumeroLote : null,
                FechaVencimiento = m.Lote != null ? m.Lote.FechaVencimiento : null
            })
            .Select(g => new
            {
                g.Key.TransferenciaId,
                g.Key.LoteId,
                g.Key.NumeroLote,
                g.Key.FechaVencimiento,
                CantidadBase = g.Sum(m => m.CantidadBase)
            })
            .ToListAsync(cancellationToken);

        return filas
            // FEFO: el que vence antes primero, igual que salio del estante. Los
            // que no caducan al final, para que no encabecen la lista por tener
            // la fecha nula.
            .OrderBy(f => f.FechaVencimiento is null)
            .ThenBy(f => f.FechaVencimiento)
            .ThenBy(f => f.LoteId)
            .Select(f => new LoteDeTransferencia(
                f.TransferenciaId,
                f.LoteId,
                f.NumeroLote,
                f.FechaVencimiento,
                f.CantidadBase ?? 0m))
            .ToList();
    }

    // =========================================================================
    // ESCRITURA
    // =========================================================================

    public void AgregarTransferencia(Transferencia transferencia) =>
        _db.Transferencias.Add(transferencia);

    public void AgregarNovedad(NovedadTransferencia novedad) =>
        _db.NovedadesTransferencia.Add(novedad);

    public Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    public async Task<T> EjecutarEnTransaccionAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken cancellationToken = default)
    {
        // La estrategia de ejecucion es obligatoria aqui: con EnableRetryOnFailure
        // activo, EF Core lanza una excepcion si se llama a BeginTransaction por
        // fuera de ella. La estrategia necesita envolver toda la operacion para
        // poder repetirla completa ante un fallo transitorio de red.
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
