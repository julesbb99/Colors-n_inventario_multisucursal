using Colorsin.Application.Dashboard.DTOs;
using Colorsin.Application.Dashboard.Repositories;
using Colorsin.Application.Inventario.DTOs;
using Colorsin.Domain.Compras;
using Microsoft.EntityFrameworkCore;

namespace Colorsin.Infrastructure.Persistence.Repositories.Dashboard;

/// <inheritdoc cref="IDashboardRepository"/>
///
/// FORMA DE LAS CONSULTAS AGRUPADAS, y esto se verifico leyendo el SQL que
/// genera EF Core, no suponiendolo:
///
///   1. Primero un <c>Select</c> plano a un tipo anonimo, y despues el
///      <c>GroupBy</c>. Agrupar la entidad y navegar dentro de los agregados
///      dice lo mismo en C#, pero deja la traduccion en manos de EF en el punto
///      donde es mas fragil.
///   2. La clave del <c>GroupBy</c> lleva SOLO la clave foranea, nunca las
///      columnas descriptivas de la tabla relacionada. Esas se resuelven en una
///      segunda consulta por clave primaria. Meterlas en la clave hace que EF
///      correlacione las subconsultas por texto sin indice.
///
/// Lo que NO se puede evitar desde LINQ: un agregado que necesita un JOIN mas
/// alla de la tabla agrupada -toda conversion de unidades lo necesita- sale
/// siempre como subconsulta correlacionada. Con las dos reglas de arriba la
/// correlacion cae sobre una columna indexada y cuesta del orden de una pasada
/// extra; sin ellas, sobre texto y arrastrando JOIN de mas.
///
/// Las consultas cuyos agregados solo tocan la tabla raiz -ventas por dia, top
/// de clientes- salen en una sola pasada y no necesitan nada de esto.
///
/// EL ORDEN DE Take Y Select. Cuando no hay agrupacion, <c>Take</c> va ANTES
/// del <c>Select</c> para que el LIMIT entre en la consulta y no se proyecten
/// filas que se van a descartar.
public sealed class DashboardRepository : IDashboardRepository
{
    private readonly AppDbContext _db;

    public DashboardRepository(AppDbContext db)
    {
        _db = db;
    }

    // =========================================================================
    // COMUN
    // =========================================================================

    public Task<string?> ObtenerNombreSucursalAsync(
        int sucursalId,
        CancellationToken cancellationToken = default) =>
        _db.Sucursales
            .AsNoTracking()
            .Where(s => s.Id == sucursalId)
            .Select(s => (string?)s.Nombre)
            .FirstOrDefaultAsync(cancellationToken);

    // =========================================================================
    // VENTAS
    // =========================================================================

    public async Task<ResumenVentas> ObtenerResumenVentasAsync(
        int? sucursalId,
        DateTime desdeInclusivo,
        DateTime hastaExclusivo,
        CancellationToken cancellationToken = default)
    {
        var consulta = FiltrarVentas(sucursalId, desdeInclusivo, hastaExclusivo);

        var cantidad = await consulta.CountAsync(cancellationToken);

        // SumAsync sobre decimal? devuelve null cuando no hay filas, porque el
        // SUM de MySQL sobre un conjunto vacio es NULL. Con decimal a secas, EF
        // tendria que desenvolver ese null y revienta.
        var total = await consulta
            .Select(v => v.Total)
            .SumAsync(cancellationToken) ?? 0m;

        return new ResumenVentas(cantidad, total);
    }

    public async Task<IReadOnlyList<VentasPorDiaDto>> ObtenerVentasPorDiaAsync(
        int? sucursalId,
        DateTime desdeInclusivo,
        DateTime hastaExclusivo,
        CancellationToken cancellationToken = default)
    {
        // Sin verificar `Fecha != null`: el filtro de rango ya las descarta.
        // En SQL, cualquier comparacion contra NULL da NULL, que no es verdadero,
        // asi que una venta sin fecha nunca entra al rango.
        var filas = await FiltrarVentas(sucursalId, desdeInclusivo, hastaExclusivo)
            .Select(v => new { Dia = v.Fecha!.Value.Date, v.Total })
            .GroupBy(v => v.Dia)
            .Select(g => new
            {
                Dia = g.Key,
                Cantidad = g.Count(),
                Total = g.Sum(x => x.Total)
            })
            .OrderBy(x => x.Dia)
            .ToListAsync(cancellationToken);

        return filas
            .Select(f => new VentasPorDiaDto(
                DateOnly.FromDateTime(f.Dia),
                f.Cantidad,
                f.Total ?? 0m))
            .ToList();
    }

    public async Task<IReadOnlyList<ProductoMasVendidoDto>> ObtenerProductosMasVendidosAsync(
        int? sucursalId,
        DateTime desdeInclusivo,
        DateTime hastaExclusivo,
        int top,
        CancellationToken cancellationToken = default)
    {
        var lineas = _db.VentaDetalles
            .AsNoTracking()
            .Where(d => d.Venta.Fecha >= desdeInclusivo && d.Venta.Fecha < hastaExclusivo);

        if (sucursalId is int id)
        {
            lineas = lineas.Where(d => d.Venta.SucursalId == id);
        }

        // Se agrupa SOLO por producto_id; el nombre, la categoria y el simbolo se
        // resuelven despues, en una segunda consulta por clave primaria.
        //
        // POR QUE, leyendo el SQL que sale de verdad. EF Core no sabe meter en la
        // consulta agrupada un agregado que necesita un JOIN mas alla de la tabla
        // raiz, y la suma de CantidadBase necesita dos, a `unidades_medida`: lo
        // saca a una subconsulta CORRELACIONADA. Eso no se evita con esta forma y
        // sigue ahi. Lo que si evita es que la correlacion sea por `nombre`,
        // `categoria` y `simbolo` -texto, sin indice- y que la subconsulta
        // arrastre tambien el JOIN a `productos` del agrupamiento.
        //
        // Como queda: la correlacion es por `producto_id`, que esta indexado, y
        // el costo es del orden de una pasada extra sobre las mismas filas, no
        // una por grupo. Si algun dia pesa, lo que toca es escribir esta consulta
        // en SQL crudo; en LINQ no hay forma de plegarla.
        var planas = lineas.Select(d => new
        {
            d.ProductoId,

            // La misma formula de ConversorUnidades.ABaseDelProducto, escrita a
            // mano porque aquel es un metodo estatico de C# y esto tiene que
            // viajar a SQL: dentro de un arbol de expresiones no se puede llamar.
            //
            // El primer caso no es un atajo de rendimiento, es correccion: si la
            // linea ya viene en la unidad base, la cantidad pasa tal cual. Sin
            // ese caso, un producto que se lleva en kilogramos (sin factor a
            // litros) daria NULL y desapareceria del ranking.
            //
            // Lo que NO hace, a diferencia del camino de escritura, es redondear
            // cada linea a 4 decimales antes de sumar. Para ordenar un ranking la
            // diferencia es irrelevante, y redondear fila por fila en SQL costaria
            // mas de lo que aporta.
            CantidadBase = d.UnidadId == d.Producto.UnidadBaseId
                ? d.Cantidad
                : (d.Unidad.FactorConversionLitros == null
                        || d.Producto.UnidadBase!.FactorConversionLitros == null
                        || d.Producto.UnidadBase.FactorConversionLitros.Value == 0m
                    ? (decimal?)null
                    : d.Cantidad
                        * d.Unidad.FactorConversionLitros.Value
                        / d.Producto.UnidadBase.FactorConversionLitros.Value),

            // Descuento es un PORCENTAJE de 0 a 100, no un importe.
            Neto = d.Cantidad * d.PrecioUnitario * (1m - d.Descuento / 100m)
        });

        var filas = await planas
            .GroupBy(x => x.ProductoId)
            .Select(g => new
            {
                ProductoId = g.Key,
                Lineas = g.Count(),
                CantidadBase = g.Sum(x => x.CantidadBase),
                TotalFacturado = g.Sum(x => x.Neto)
            })
            .OrderByDescending(x => x.TotalFacturado)
            // Desempata por id y no por nombre, que ya no esta en la consulta.
            // Lo que importa es que el orden sea estable entre llamadas.
            .ThenBy(x => x.ProductoId)
            .Take(top)
            .ToListAsync(cancellationToken);

        var descripciones = await DescribirProductosAsync(
            filas.Select(f => f.ProductoId), cancellationToken);

        return filas
            .Select(f =>
            {
                descripciones.TryGetValue(f.ProductoId, out var d);
                return new ProductoMasVendidoDto(
                    f.ProductoId,
                    d.Nombre ?? string.Empty,
                    d.Categoria,
                    f.Lineas,
                    f.CantidadBase ?? 0m,
                    d.UnidadBaseSimbolo,
                    f.TotalFacturado ?? 0m);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ClienteTopDto>> ObtenerTopClientesAsync(
        int? sucursalId,
        DateTime desdeInclusivo,
        DateTime hastaExclusivo,
        int top,
        CancellationToken cancellationToken = default)
    {
        var planas = FiltrarVentas(sucursalId, desdeInclusivo, hastaExclusivo)
            .Select(v => new
            {
                v.ClienteId,
                v.Cliente.RazonSocial,
                v.Cliente.Documento,
                v.Total,
                v.Fecha
            });

        var filas = await planas
            .GroupBy(x => new { x.ClienteId, x.RazonSocial, x.Documento })
            .Select(g => new
            {
                g.Key.ClienteId,
                g.Key.RazonSocial,
                g.Key.Documento,
                Cantidad = g.Count(),
                Total = g.Sum(x => x.Total),
                // La mas reciente DENTRO del rango, no de toda su historia.
                Ultima = g.Max(x => x.Fecha)
            })
            .OrderByDescending(x => x.Total)
            .ThenBy(x => x.RazonSocial)
            .Take(top)
            .ToListAsync(cancellationToken);

        return filas
            .Select(f => new ClienteTopDto(
                f.ClienteId,
                f.RazonSocial,
                f.Documento,
                f.Cantidad,
                f.Total ?? 0m,
                f.Ultima))
            .ToList();
    }

    // =========================================================================
    // INVENTARIO
    // =========================================================================

    public async Task<IReadOnlyList<StockPorSucursalDto>> ObtenerStockPorSucursalAsync(
        int? sucursalId,
        CancellationToken cancellationToken = default)
    {
        var saldos = _db.InventarioSucursales.AsNoTracking();

        if (sucursalId is int id)
        {
            saldos = saldos.Where(i => i.SucursalId == id);
        }

        // Se agrupa SOLO por sucursal_id; el nombre y la ciudad se resuelven
        // despues. Mismo motivo que en ObtenerProductosMasVendidosAsync, donde
        // esta la explicacion larga: los litros y los saldos sin convertir
        // necesitan JOIN a `productos` y `unidades_medida`, asi que EF Core los
        // saca a subconsultas correlacionadas y eso no se puede evitar desde
        // LINQ. Con la clave reducida al id, la correlacion cae sobre
        // `sucursal_id`, indexado, y no sobre `nombre` y `ciudad`, y la
        // subconsulta deja de arrastrar el JOIN a `sucursales`.
        var planas = saldos.Select(i => new
        {
            i.SucursalId,

            // `cantidad_base` NO esta en litros: esta en la unidad base de CADA
            // producto. Sumar la columna a secas mezclaria unidades distintas.
            // Queda NULL lo que no tiene como convertirse (producto sin unidad
            // base, o unidad base sin factor, como el kilogramo), y el SUM de SQL
            // ignora los NULL: por eso hay que contarlos aparte, abajo.
            Litros = i.Producto.UnidadBaseId == null
                     || i.Producto.UnidadBase!.FactorConversionLitros == null
                ? (decimal?)null
                : i.CantidadBase * i.Producto.UnidadBase.FactorConversionLitros.Value,

            SinConversion = i.Producto.UnidadBaseId == null
                            || i.Producto.UnidadBase!.FactorConversionLitros == null ? 1 : 0,

            ConSaldo = i.CantidadBase > 0 ? 1 : 0,

            // El criterio de alerta del modulo de inventario, sin cambiarlo.
            EnAlerta = i.CantidadBase <= i.StockMinimo ? 1 : 0,

            // Esta suma SI es segura sin convertir: el costo promedio ya esta
            // por unidad base de cada producto, asi que cada termino queda en
            // pesos antes de sumarse.
            Valor = i.CantidadBase * i.CostoPromedio
        });

        var filas = await planas
            .GroupBy(x => x.SucursalId)
            .Select(g => new
            {
                SucursalId = g.Key,
                ProductosConSaldo = g.Sum(x => x.ConSaldo),
                ProductosEnAlerta = g.Sum(x => x.EnAlerta),
                Litros = g.Sum(x => x.Litros),
                SinConversion = g.Sum(x => x.SinConversion),
                Valor = g.Sum(x => x.Valor)
            })
            .ToListAsync(cancellationToken);

        var ids = filas.Select(f => f.SucursalId).ToList();

        var sedes = await _db.Sucursales
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.Nombre, s.Ciudad })
            .ToListAsync(cancellationToken);

        var porId = sedes.ToDictionary(s => s.Id);

        return filas
            .Select(f =>
            {
                porId.TryGetValue(f.SucursalId, out var sede);
                return new StockPorSucursalDto(
                    f.SucursalId,
                    sede?.Nombre ?? string.Empty,
                    sede?.Ciudad ?? string.Empty,
                    f.ProductosConSaldo,
                    f.ProductosEnAlerta,
                    f.Litros ?? 0m,
                    f.SinConversion,
                    f.Valor);
            })
            // Se ordena en memoria: son tantas filas como sedes tenga la red, y
            // ordenar por los litros en SQL obligaba a repetir la suma completa
            // en el ORDER BY.
            .OrderByDescending(d => d.SaldoLitros)
            .ThenBy(d => d.SucursalNombre, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<LotePorVencerDto>> ObtenerLotesProximosAVencerAsync(
        int? sucursalId,
        DateOnly hoy,
        DateOnly limiteVencimiento,
        int top,
        CancellationToken cancellationToken = default)
    {
        var lotes = _db.Lotes
            .AsNoTracking()
            // Los agotados no interesan: un lote en cero ya no se puede despachar
            // y su vencimiento da igual. Los que no caducan tampoco entran.
            .Where(l => l.CantidadBase > 0
                     && l.FechaVencimiento != null
                     && l.FechaVencimiento <= limiteVencimiento);

        if (sucursalId is int id)
        {
            lotes = lotes.Where(l => l.SucursalId == id);
        }

        var filas = await lotes
            // FEFO. Aqui no hace falta el truco de mandar los NULL al final que
            // lleva la consulta de inventario: el filtro ya dejo fuera los lotes
            // sin vencimiento.
            .OrderBy(l => l.FechaVencimiento)
            .ThenBy(l => l.FechaIngreso)
            .ThenBy(l => l.Id)
            .Take(top)
            .Select(l => new
            {
                LoteId = l.Id,
                l.ProductoId,
                ProductoNombre = l.Producto.Nombre,
                l.SucursalId,
                SucursalNombre = l.Sucursal.Nombre,
                l.NumeroLote,
                l.FechaVencimiento,
                l.CantidadBase,
                UnidadBaseSimbolo = l.Producto.UnidadBase!.Simbolo
            })
            .ToListAsync(cancellationToken);

        return filas
            .Select(f =>
            {
                // El filtro garantiza que no es nulo.
                var vence = f.FechaVencimiento!.Value;

                // DayNumber no se traduce a SQL, y tampoco hace falta: son las
                // filas que ya vinieron, como mucho `top`.
                var dias = vence.DayNumber - hoy.DayNumber;

                return new LotePorVencerDto(
                    f.LoteId,
                    f.ProductoId,
                    f.ProductoNombre,
                    f.SucursalId,
                    f.SucursalNombre,
                    f.NumeroLote,
                    vence,
                    dias,
                    f.CantidadBase ?? 0m,
                    f.UnidadBaseSimbolo,
                    dias < 0);
            })
            .ToList();
    }

    public Task<int> ContarLotesProximosAVencerAsync(
        int? sucursalId,
        DateOnly limiteVencimiento,
        CancellationToken cancellationToken = default)
    {
        // El MISMO criterio que ObtenerLotesProximosAVencerAsync, palabra por
        // palabra, y sin Take: el conteo y la lista tienen que responder a la
        // misma pregunta o el resumen dira una cosa y el detalle otra.
        var lotes = _db.Lotes
            .AsNoTracking()
            .Where(l => l.CantidadBase > 0
                     && l.FechaVencimiento != null
                     && l.FechaVencimiento <= limiteVencimiento);

        if (sucursalId is int id)
        {
            lotes = lotes.Where(l => l.SucursalId == id);
        }

        return lotes.CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MovimientoInventarioDto>> ObtenerMovimientosRecientesAsync(
        int? sucursalId,
        int top,
        CancellationToken cancellationToken = default)
    {
        var movimientos = _db.MovimientosInventario.AsNoTracking();

        if (sucursalId is int id)
        {
            movimientos = movimientos.Where(m => m.SucursalId == id);
        }

        var filas = await movimientos
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id)
            .Take(top)
            .Select(m => new
            {
                m.Id,
                m.SucursalId,
                SucursalNombre = m.Sucursal.Nombre,
                m.ProductoId,
                ProductoNombre = m.Producto.Nombre,
                m.UsuarioId,
                UsuarioNombre = m.Usuario.Nombre,
                m.Tipo,
                m.Motivo,
                m.Cantidad,
                m.UnidadId,
                UnidadSimbolo = m.Unidad.Simbolo,
                m.CantidadBase,
                m.LoteId,
                // Lote es opcional: el LEFT JOIN deja NULL cuando el movimiento
                // no se imputo a ninguno.
                NumeroLote = (string?)m.Lote!.NumeroLote,
                m.Observaciones,
                m.Fecha
            })
            .ToListAsync(cancellationToken);

        // ToString() sobre los enums se hace aqui y no en la proyeccion: EF los
        // guarda como texto con un convertidor, pero traducir la llamada no esta
        // garantizado. Sobre `top` filas ya materializadas no cuesta nada.
        return filas
            .Select(f => new MovimientoInventarioDto(
                f.Id,
                f.SucursalId,
                f.SucursalNombre,
                f.ProductoId,
                f.ProductoNombre,
                f.UsuarioId,
                f.UsuarioNombre,
                f.Tipo?.ToString(),
                f.Motivo?.ToString(),
                f.Cantidad,
                f.UnidadId,
                f.UnidadSimbolo,
                f.CantidadBase,
                f.LoteId,
                f.NumeroLote,
                f.Observaciones,
                f.Fecha))
            .ToList();
    }

    // =========================================================================
    // TRANSFERENCIAS
    // =========================================================================

    public async Task<IReadOnlyList<ConteoPorEstado>> ObtenerConteoTransferenciasAsync(
        int? sucursalId,
        CancellationToken cancellationToken = default)
    {
        var transferencias = _db.Transferencias.AsNoTracking();

        if (sucursalId is int id)
        {
            // Origen O destino: los dos lados tienen algo pendiente con el
            // traslado, uno despacharlo y el otro recibirlo.
            transferencias = transferencias.Where(
                t => t.SucursalOrigenId == id || t.SucursalDestinoId == id);
        }

        var filas = await transferencias
            .Select(t => new { t.Estado })
            .GroupBy(x => x.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(cancellationToken);

        return filas
            .Select(f => new ConteoPorEstado(f.Estado, f.Cantidad))
            .ToList();
    }

    public async Task<IReadOnlyList<NovedadRecienteDto>> ObtenerNovedadesRecientesAsync(
        int? sucursalId,
        int top,
        CancellationToken cancellationToken = default)
    {
        var novedades = _db.NovedadesTransferencia.AsNoTracking();

        if (sucursalId is int id)
        {
            novedades = novedades.Where(
                n => n.Transferencia.SucursalOrigenId == id
                  || n.Transferencia.SucursalDestinoId == id);
        }

        var filas = await novedades
            .OrderByDescending(n => n.Fecha)
            .ThenByDescending(n => n.Id)
            .Take(top)
            .Select(n => new
            {
                n.Id,
                n.TransferenciaId,
                n.Transferencia.ProductoId,
                ProductoNombre = n.Transferencia.Producto.Nombre,
                n.Transferencia.SucursalOrigenId,
                SucursalOrigenNombre = n.Transferencia.SucursalOrigen.Nombre,
                n.Transferencia.SucursalDestinoId,
                SucursalDestinoNombre = n.Transferencia.SucursalDestino.Nombre,
                n.Tipo,
                n.CantidadAfectada,
                n.Observaciones,
                n.UsuarioId,
                UsuarioNombre = n.Usuario.Nombre,
                n.Fecha
            })
            .ToListAsync(cancellationToken);

        return filas
            .Select(f => new NovedadRecienteDto(
                f.Id,
                f.TransferenciaId,
                f.ProductoId,
                f.ProductoNombre,
                f.SucursalOrigenId,
                f.SucursalOrigenNombre,
                f.SucursalDestinoId,
                f.SucursalDestinoNombre,
                f.Tipo?.ToString(),
                f.CantidadAfectada,
                f.Observaciones,
                f.UsuarioId,
                f.UsuarioNombre,
                f.Fecha))
            .ToList();
    }

    // =========================================================================
    // APOYO
    // =========================================================================

    /// <summary>
    /// Nombre, categoria y simbolo de la unidad base de unos productos.
    ///
    /// Va en una consulta aparte del ranking para no meter esas tres columnas en
    /// la clave del GROUP BY; ver la nota de
    /// <see cref="ObtenerProductosMasVendidosAsync"/>. Son como mucho `top`
    /// filas, buscadas por clave primaria.
    /// </summary>
    private async Task<Dictionary<int, (string? Nombre, string? Categoria, string? UnidadBaseSimbolo)>>
        DescribirProductosAsync(
            IEnumerable<int> productoIds,
            CancellationToken cancellationToken)
    {
        var ids = productoIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<int, (string?, string?, string?)>();
        }

        var filas = await _db.Productos
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Nombre,
                p.Categoria,
                Simbolo = p.UnidadBase!.Simbolo
            })
            .ToListAsync(cancellationToken);

        return filas.ToDictionary(
            p => p.Id,
            p => (Nombre: (string?)p.Nombre,
                  Categoria: p.Categoria,
                  UnidadBaseSimbolo: (string?)p.Simbolo));
    }

    public async Task<FaltanteRecepcion> ObtenerFaltanteRecepcionAsync(
        int? sucursalId,
        CancellationToken cancellationToken = default)
    {
        // Solo 'ParcialmenteRecibida'. Una orden Pendiente no ha recibido nada,
        // asi que no "falto" nada: esta entera por llegar, que es otra cosa. El
        // faltante nace justo cuando una entrega llega corta.
        var consulta = _db.OrdenesCompra
            .AsNoTracking()
            .Where(o => o.Estado == EstadoOrdenCompra.ParcialmenteRecibida);

        if (sucursalId is int id)
        {
            consulta = consulta.Where(o => o.SucursalId == id);
        }

        var ordenes = await consulta.CountAsync(cancellationToken);

        // Se suma en el servidor, no trayendo las lineas: son todas las lineas
        // abiertas de la red y solo hace falta un numero.
        //
        // `?? 0` en cantidad y precio porque las dos columnas admiten nulo. Una
        // linea sin precio aporta cero al valor, que es lo correcto: no se sabe
        // cuanto vale lo que falta, y suponerlo seria inventar la cifra.
        var valor = await consulta
            .SelectMany(o => o.Detalles)
            .Select(d =>
                ((d.Cantidad ?? 0m) - d.CantidadRecibida) *
                (d.PrecioUnitario ?? 0m) *
                (1m - d.Descuento / 100m))
            .SumAsync(cancellationToken);

        // Nunca negativo: si una linea quedara con mas recibido que pedido -lo
        // impide un CHECK, pero el dato viene de la base- restaria del total y
        // disimularia el faltante de otra.
        return new FaltanteRecepcion(ordenes, Math.Max(0m, valor));
    }

    /// <summary>
    /// Ventas de un rango, filtradas por sede si se indica. Es la base de los
    /// cuatro metodos de ventas, para que el criterio de rango y de sede se
    /// escriba una sola vez.
    /// </summary>
    private IQueryable<Domain.Ventas.Venta> FiltrarVentas(
        int? sucursalId,
        DateTime desdeInclusivo,
        DateTime hastaExclusivo)
    {
        var consulta = _db.Ventas
            .AsNoTracking()
            .Where(v => v.Fecha >= desdeInclusivo && v.Fecha < hastaExclusivo);

        return sucursalId is int id
            ? consulta.Where(v => v.SucursalId == id)
            : consulta;
    }
}
