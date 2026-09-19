using System.Globalization;
using Colorsin.Application.Comun.Auditoria;
using Colorsin.Application.Comun.Services;
using Colorsin.Application.Compras.DTOs;
using Colorsin.Application.Compras.Mapping;
using Colorsin.Application.Compras.Repositories;
using Colorsin.Application.Inventario;
using Colorsin.Application.Inventario.Repositories;
using Colorsin.Domain.Compras;
using Colorsin.Domain.Inventario;

namespace Colorsin.Application.Compras.Services;

/// <inheritdoc cref="IComprasService"/>
public sealed class ComprasService : IComprasService
{
    /// <summary>Nombre del modulo en los eventos de auditoria.</summary>
    private const string Modulo = "Compras";

    /// <summary>
    /// Estados desde los que todavia puede llegar mercancia.
    ///
    /// 'Confirmada' entra porque un pedido que el proveedor ya acuso es
    /// justamente el que esta por llegar, y 'ParcialmenteRecibida' porque una
    /// orden a medias sigue esperando entregas. 'Recibida' queda fuera para que
    /// una confirmacion repetida no duplique el stock, y 'Cancelada' porque lo
    /// cancelado no llega.
    /// </summary>
    private static readonly EstadoOrdenCompra[] EstadosQuePermitenRecepcion =
    [
        EstadoOrdenCompra.Pendiente,
        EstadoOrdenCompra.Confirmada,
        EstadoOrdenCompra.ParcialmenteRecibida
    ];

    private readonly IOrdenCompraRepository _ordenes;
    private readonly IProveedorRepository _proveedores;
    private readonly IInventarioRepository _inventario;
    private readonly IProductoRepository _productos;
    private readonly IUnidadMedidaService _unidades;
    private readonly IAuditoriaService _auditoria;

    public ComprasService(
        IOrdenCompraRepository ordenes,
        IProveedorRepository proveedores,
        IInventarioRepository inventario,
        IProductoRepository productos,
        IUnidadMedidaService unidades,
        IAuditoriaService auditoria)
    {
        _productos = productos;
        _ordenes = ordenes;
        _proveedores = proveedores;
        _inventario = inventario;
        _unidades = unidades;
        _auditoria = auditoria;
    }

    // =========================================================================
    // CONSULTAS
    // =========================================================================

    public async Task<IReadOnlyList<ProveedorDto>> ObtenerProveedoresAsync(
        bool incluirInactivos,
        CancellationToken cancellationToken = default)
    {
        var proveedores = await _proveedores.ObtenerTodosAsync(
            incluirInactivos, cancellationToken);

        return proveedores.Select(p => p.ToDto(contarProductos: true)).ToList();
    }

    public async Task<ProveedorDto?> ObtenerProveedorPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var proveedor = await _proveedores.ObtenerPorIdAsync(id, cancellationToken);
        return proveedor?.ToDto(contarProductos: true);
    }

    public async Task<IReadOnlyList<OrdenCompraDto>> ObtenerOrdenesAsync(
        int? sucursalId = null,
        int? proveedorId = null,
        EstadoOrdenCompra? estado = null,
        int limite = 100,
        CancellationToken cancellationToken = default)
    {
        var ordenes = await _ordenes.ObtenerAsync(
            sucursalId, proveedorId, estado, limite, cancellationToken);

        return ordenes.Select(o => o.ToDto(incluirDetalle: false)).ToList();
    }

    public async Task<OrdenCompraDto?> ObtenerOrdenPorIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var orden = await _ordenes.ObtenerPorIdAsync(id, cancellationToken);
        return orden?.ToDto();
    }

    // =========================================================================
    // CREAR ORDEN
    // =========================================================================

    public async Task<ResultadoOrdenCompra> CrearOrdenAsync(
        CrearOrdenCompraDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        // --- 1 y 2. Forma y proveedor -------------------------------------------
        // Compartidas con ActualizarOrdenAsync: si estuvieran duplicadas, editar
        // una orden acabaria admitiendo lo que crearla rechaza.
        if (await ValidarOrdenAsync(peticion, usuarioId, cancellationToken) is { } rechazo)
        {
            return rechazo;
        }

        // --- 3. Crear, siempre en Pendiente -------------------------------------
        var orden = new OrdenCompra
        {
            ProveedorId = peticion.ProveedorId,
            SucursalId = peticion.SucursalId,
            UsuarioId = usuarioId,
            // El estado NO lo elige quien llama: una orden recien creada no
            // puede nacer 'Recibida' y saltarse el ingreso al stock.
            Estado = EstadoOrdenCompra.Pendiente,
            PlazoPagoDias = peticion.PlazoPagoDias
            // Fecha la pone la base con CURRENT_TIMESTAMP.
        };

        foreach (var linea in peticion.Lineas)
        {
            orden.Detalles.Add(new OrdenCompraDetalle
            {
                ProductoId = linea.ProductoId,
                Cantidad = linea.Cantidad,
                // Nada recibido todavia: el stock no se mueve al crear la orden.
                CantidadRecibida = 0m,
                UnidadId = linea.UnidadId,
                PrecioUnitario = linea.PrecioUnitario,
                Descuento = linea.Descuento
            });
        }

        // Encabezado, lineas y auditoria en una transaccion: una orden a medias
        // no sirve para nada.
        return await _inventario.EjecutarEnTransaccionAsync(async ct =>
        {
            _ordenes.AgregarOrden(orden);
            await _ordenes.GuardarCambiosAsync(ct);

            var total = orden.ToDto().Total;

            await _auditoria.RegistrarEventoAsync(
                Modulo,
                "CrearOrdenCompra",
                usuarioId,
                $"orden={orden.Id} | proveedor={peticion.ProveedorId} | " +
                $"sucursal={peticion.SucursalId} | lineas={peticion.Lineas.Count} | " +
                $"total={Num(total)} | estado=Pendiente",
                ct);

            await _ordenes.GuardarCambiosAsync(ct);

            return ResultadoOrdenCompra.Ok(orden.Id, total);
        }, cancellationToken);
    }

    /// <summary>
    /// Validaciones comunes al alta y a la edicion de una orden. Devuelve el
    /// rechazo, o <c>null</c> si todo esta bien.
    /// </summary>
    private async Task<ResultadoOrdenCompra?> ValidarOrdenAsync(
        CrearOrdenCompraDto peticion,
        int usuarioId,
        CancellationToken cancellationToken)
    {
        // --- 1. Validaciones de forma ------------------------------------------
        if (peticion.Lineas is null || peticion.Lineas.Count == 0)
        {
            return ResultadoOrdenCompra.Fallo(
                ErrorCompra.OrdenSinLineas,
                "La orden debe tener al menos una linea.");
        }

        if (usuarioId <= 0)
        {
            return ResultadoOrdenCompra.Fallo(
                ErrorCompra.UsuarioNoIndicado,
                "Hay que indicar el usuario que crea la orden; " +
                $"llego {usuarioId}.");
        }

        if (peticion.PlazoPagoDias is < 0)
        {
            return ResultadoOrdenCompra.Fallo(
                ErrorCompra.PlazoPagoInvalido,
                $"El plazo de pago no puede ser negativo; llego {peticion.PlazoPagoDias}.");
        }

        // Estas tres las exigen tambien los CHECK de la base. Validarlas aqui
        // es lo que permite senalar cual linea falla; MySQL solo diria que se
        // violo `chk_ocd_cantidad`, sin decir donde.
        for (var i = 0; i < peticion.Lineas.Count; i++)
        {
            var linea = peticion.Lineas[i];

            if (linea.Cantidad <= 0m)
            {
                return ResultadoOrdenCompra.Fallo(
                    ErrorCompra.CantidadInvalida,
                    $"Linea {i + 1}: la cantidad debe ser mayor que cero; llego {Num(linea.Cantidad)}.");
            }

            if (linea.PrecioUnitario is < 0m)
            {
                return ResultadoOrdenCompra.Fallo(
                    ErrorCompra.PrecioInvalido,
                    $"Linea {i + 1}: el precio unitario no puede ser negativo; " +
                    $"llego {Num(linea.PrecioUnitario.Value)}.");
            }

            if (linea.Descuento is < 0m or > 100m)
            {
                return ResultadoOrdenCompra.Fallo(
                    ErrorCompra.DescuentoInvalido,
                    $"Linea {i + 1}: el descuento es un porcentaje entre 0 y 100; " +
                    $"llego {Num(linea.Descuento)}.");
            }
        }

        // --- 2. El proveedor debe existir ---------------------------------------
        // La FK lo impondria igual, pero como DbUpdateException al guardar, que
        // llega al cliente como error 500 en vez de como un rechazo con motivo.
        if (!await _proveedores.ExisteAsync(peticion.ProveedorId, cancellationToken))
        {
            return ResultadoOrdenCompra.Fallo(
                ErrorCompra.ProveedorNoEncontrado,
                $"No existe el proveedor {peticion.ProveedorId}.");
        }

        return null;
    }

    // =========================================================================
    // CONFIRMAR RECEPCION: la unica operacion de compras que mueve stock
    // =========================================================================

    public async Task<ResultadoRecepcion> ConfirmarRecepcionAsync(
        ConfirmarRecepcionDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId <= 0)
        {
            return ResultadoRecepcion.Fallo(
                ErrorCompra.UsuarioNoIndicado,
                $"Hay que indicar el usuario que recibe; llego {usuarioId}.");
        }

        var lineas = peticion.Lineas ?? [];

        // La misma linea dos veces en una entrega es ambiguo: no hay forma de
        // saber cual cantidad y cual lote valen. Se detecta antes de abrir la
        // transaccion.
        var duplicada = lineas.GroupBy(l => l.DetalleId).FirstOrDefault(g => g.Count() > 1);
        if (duplicada is not null)
        {
            return ResultadoRecepcion.Fallo(
                ErrorCompra.LoteDuplicadoEnPeticion,
                $"La linea {duplicada.Key} aparece {duplicada.Count()} veces en la entrega. " +
                "Cada linea puede figurar como mucho una vez.");
        }

        foreach (var linea in lineas)
        {
            if (linea.Cantidad is <= 0m)
            {
                return ResultadoRecepcion.Fallo(
                    ErrorCompra.CantidadInvalida,
                    $"Linea {linea.DetalleId}: la cantidad recibida debe ser mayor que cero; " +
                    $"llego {Num(linea.Cantidad!.Value)}. Para no recibir nada de una linea, " +
                    "basta con omitirla.");
            }
        }

        return await _inventario.EjecutarEnTransaccionAsync(
            ct => RecibirEnTransaccionAsync(peticion, usuarioId, lineas, ct),
            cancellationToken);
    }

    private async Task<ResultadoRecepcion> RecibirEnTransaccionAsync(
        ConfirmarRecepcionDto peticion,
        int usuarioId,
        IReadOnlyList<LineaRecepcionDto> lineas,
        CancellationToken cancellationToken)
    {
        // --- 1. Orden, con la fila del encabezado bloqueada ----------------------
        var orden = await _ordenes.ObtenerParaRecibirAsync(peticion.OrdenCompraId, cancellationToken);

        if (orden is null)
        {
            return ResultadoRecepcion.Fallo(
                ErrorCompra.OrdenNoEncontrada,
                $"No existe la orden de compra {peticion.OrdenCompraId}.");
        }

        // El bloqueo de arriba mas esta comprobacion son lo que impide duplicar
        // el stock: una segunda confirmacion simultanea espera, y al entrar ya
        // lee 'Recibida'.
        if (orden.Estado is null || !EstadosQuePermitenRecepcion.Contains(orden.Estado.Value))
        {
            return ResultadoRecepcion.Fallo(
                ErrorCompra.EstadoNoPermiteRecepcion,
                $"La orden {orden.Id} esta en estado '{orden.Estado?.ToString() ?? "sin estado"}' " +
                "y solo admite entregas una orden Pendiente, Confirmada o " +
                "ParcialmenteRecibida.");
        }

        if (orden.Detalles.Count == 0)
        {
            return ResultadoRecepcion.Fallo(
                ErrorCompra.OrdenSinLineas,
                $"La orden {orden.Id} no tiene lineas que recibir.");
        }

        // --- 2. Decidir que se recibe y cuanto ------------------------------------
        var (aRecibir, rechazo) = ResolverEntrega(orden, lineas);
        if (rechazo is not null)
        {
            return rechazo;
        }

        if (aRecibir!.Count == 0)
        {
            return ResultadoRecepcion.Fallo(
                ErrorCompra.NadaPorRecibir,
                $"La entrega no aporta nada a la orden {orden.Id}: no hay lineas con " +
                "cantidad pendiente.");
        }

        // --- 3. Convertir TODAS las lineas antes de tocar nada --------------------
        // Se hace en una pasada aparte a proposito: si la linea 3 no se puede
        // convertir, es mejor rechazar la entrega entera antes de haber
        // ingresado las lineas 1 y 2. La transaccion lo revertiria igual, pero
        // asi el rechazo es limpio y no depende del rollback.
        var convertidas = new List<(EntregaLinea Entrega, decimal CantidadBase)>();

        foreach (var entrega in aRecibir)
        {
            var producto = entrega.Detalle.Producto;

            if (producto is null)
            {
                return ResultadoRecepcion.Fallo(
                    ErrorCompra.ProductoNoEncontrado,
                    $"La linea {entrega.Detalle.Id} apunta al producto " +
                    $"{entrega.Detalle.ProductoId}, que no existe.");
            }

            if (producto.UnidadBaseId is null)
            {
                return ResultadoRecepcion.Fallo(
                    ErrorCompra.ProductoSinUnidadBase,
                    $"El producto '{producto.Nombre}' no tiene unidad base definida, " +
                    "asi que no hay unidad a la cual convertir lo recibido.");
            }

            var conversion = await ConvertirAsync(
                entrega.Cantidad, entrega.Detalle.UnidadId,
                producto.UnidadBaseId.Value, cancellationToken);

            if (conversion.Error is ErrorCompra error)
            {
                return ResultadoRecepcion.Fallo(error, conversion.Mensaje!);
            }

            convertidas.Add((entrega, conversion.CantidadBase));
        }

        // --- 4. Aplicar. Desde aqui ya no hay rechazos ---------------------------
        var resultado = new List<LineaRecibidaDto>(convertidas.Count);

        foreach (var (entrega, cantidadBase) in convertidas)
        {
            var detalle = entrega.Detalle;

            var saldoResultante = await IngresarAlSaldoAsync(
                orden.SucursalId, detalle.ProductoId, cantidadBase, cancellationToken);

            var (loteId, loteCreado) = string.IsNullOrWhiteSpace(entrega.NumeroLote)
                ? (null, (bool?)null)
                : await RegistrarLoteAsync(
                    orden.SucursalId, detalle.ProductoId, cantidadBase,
                    entrega.NumeroLote, entrega.FechaVencimiento, cancellationToken);

            var movimiento = new MovimientoInventario
            {
                SucursalId = orden.SucursalId,
                ProductoId = detalle.ProductoId,
                UsuarioId = usuarioId,
                Tipo = TipoMovimiento.Ingreso,
                Motivo = MotivoMovimiento.Compra,
                // Lo de ESTA entrega, no lo pedido en la linea. En una entrega
                // parcial son distintos, y el libro mayor tiene que reflejar lo
                // que entro fisicamente.
                Cantidad = entrega.Cantidad,
                UnidadId = detalle.UnidadId,
                CantidadBase = cantidadBase,
                LoteId = loteId,
                // La orden queda referida en el texto porque
                // `movimientos_inventario` no tiene columna para el documento
                // de origen. Es una limitacion conocida: buscar los movimientos
                // de una orden obliga a filtrar por texto.
                Observaciones = ConstruirObservacion(orden.Id, peticion.Observaciones)
                // Fecha la pone la base con CURRENT_TIMESTAMP.
            };
            _inventario.AgregarMovimiento(movimiento);

            // Acumula sobre lo ya recibido en entregas anteriores.
            var recibidoTotal = detalle.CantidadRecibida + entrega.Cantidad;
            _ordenes.ActualizarCantidadRecibida(detalle, recibidoTotal);

            // Se guarda por linea para que MySQL asigne el id del movimiento,
            // que va en la respuesta. Sigue todo dentro de la misma transaccion.
            await _ordenes.GuardarCambiosAsync(cancellationToken);

            resultado.Add(new LineaRecibidaDto(
                detalle.Id,
                detalle.ProductoId,
                // El bucle de conversion ya rechazo cualquier linea sin
                // producto, asi que aqui no puede ser nulo.
                detalle.Producto!.Nombre,
                entrega.Cantidad,
                recibidoTotal,
                detalle.Cantidad ?? 0m,
                Math.Max(0m, (detalle.Cantidad ?? 0m) - recibidoTotal),
                detalle.UnidadId,
                cantidadBase,
                saldoResultante,
                movimiento.Id,
                loteId,
                loteCreado));
        }

        // --- 5. Estado resultante -------------------------------------------------
        // Se mira la orden ENTERA, no solo las lineas de esta entrega: una
        // linea que no vino en este camion sigue pendiente igual.
        var todoCompleto = orden.Detalles.All(EstaCompleta);
        var estadoNuevo = todoCompleto
            ? EstadoOrdenCompra.Recibida
            : EstadoOrdenCompra.ParcialmenteRecibida;

        _ordenes.ActualizarEstado(orden, estadoNuevo);
        await _ordenes.GuardarCambiosAsync(cancellationToken);

        // --- 6. Auditoria, en la MISMA transaccion --------------------------------
        await _auditoria.RegistrarEventoAsync(
            Modulo,
            todoCompleto ? "ConfirmarRecepcionCompra" : "RecepcionParcialCompra",
            usuarioId,
            ConstruirDetalleAuditoria(orden, estadoNuevo, resultado),
            cancellationToken);

        await _ordenes.GuardarCambiosAsync(cancellationToken);

        return ResultadoRecepcion.Ok(orden.Id, estadoNuevo, resultado);
    }

    /// <summary>Una linea de la orden con lo que le llega en esta entrega.</summary>
    private sealed record EntregaLinea(
        OrdenCompraDetalle Detalle,
        decimal Cantidad,
        string? NumeroLote,
        DateOnly? FechaVencimiento);

    /// <summary>
    /// Cruza lo que pide la peticion con lo que falta en la orden y devuelve
    /// que lineas se reciben y por cuanto.
    ///
    /// Sin lineas en la peticion se entiende "llego todo lo que faltaba", que es
    /// el caso de la entrega completa. Con lineas, solo se reciben esas: las que
    /// no aparezcan quedan pendientes.
    /// </summary>
    private static (List<EntregaLinea>? Entregas, ResultadoRecepcion? Rechazo) ResolverEntrega(
        OrdenCompra orden,
        IReadOnlyList<LineaRecepcionDto> lineas)
    {
        var entregas = new List<EntregaLinea>();

        if (lineas.Count == 0)
        {
            foreach (var detalle in orden.Detalles)
            {
                var pendiente = Pendiente(detalle);
                if (pendiente > 0m)
                {
                    entregas.Add(new EntregaLinea(detalle, pendiente, null, null));
                }
            }

            return (entregas, null);
        }

        var detallesPorId = orden.Detalles.ToDictionary(d => d.Id);

        foreach (var linea in lineas)
        {
            if (!detallesPorId.TryGetValue(linea.DetalleId, out var detalle))
            {
                return (null, ResultadoRecepcion.Fallo(
                    ErrorCompra.LineaNoPertenece,
                    $"La linea {linea.DetalleId} no pertenece a la orden {orden.Id}."));
            }

            var pendiente = Pendiente(detalle);

            if (pendiente <= 0m)
            {
                return (null, ResultadoRecepcion.Fallo(
                    ErrorCompra.CantidadRecibidaExcedeSolicitada,
                    $"La linea {detalle.Id} ya esta completa: se pidieron " +
                    $"{Num(detalle.Cantidad ?? 0m)} y ya se recibieron " +
                    $"{Num(detalle.CantidadRecibida)}."));
            }

            // Sin cantidad explicita se entiende "todo lo que falta de esta
            // linea", que es lo que se quiere en la ultima entrega.
            var cantidad = linea.Cantidad ?? pendiente;

            if (cantidad > pendiente)
            {
                return (null, ResultadoRecepcion.Fallo(
                    ErrorCompra.CantidadRecibidaExcedeSolicitada,
                    $"La linea {detalle.Id} admite {Num(pendiente)} mas " +
                    $"(pedido {Num(detalle.Cantidad ?? 0m)}, recibido " +
                    $"{Num(detalle.CantidadRecibida)}), y la entrega trae " +
                    $"{Num(cantidad)}."));
            }

            entregas.Add(new EntregaLinea(
                detalle, cantidad, linea.NumeroLote, linea.FechaVencimiento));
        }

        return (entregas, null);
    }

    /// <summary>Cuanto falta por recibir de una linea. Nunca negativo.</summary>
    private static decimal Pendiente(OrdenCompraDetalle detalle) =>
        Math.Max(0m, (detalle.Cantidad ?? 0m) - detalle.CantidadRecibida);

    /// <summary>
    /// Si una linea ya no espera nada mas.
    ///
    /// Una linea con `cantidad` nula se da por completa: no hay cantidad pedida
    /// contra la cual medir, y dejarla pendiente para siempre impediria cerrar
    /// la orden.
    /// </summary>
    private static bool EstaCompleta(OrdenCompraDetalle detalle) =>
        detalle.CantidadRecibida >= (detalle.Cantidad ?? 0m);

    // =========================================================================
    // AUXILIARES DE LA RECEPCION
    // =========================================================================

    /// <summary>
    /// Sube el saldo de la sede y devuelve como quedo.
    ///
    /// Un ingreso nunca se rechaza por falta de stock, asi que no hay validacion
    /// que hacer; pero si hace falta el bloqueo de fila, porque otra operacion
    /// puede estar retirando del mismo saldo al mismo tiempo.
    /// </summary>
    private async Task<decimal> IngresarAlSaldoAsync(
        int sucursalId,
        int productoId,
        decimal cantidadBase,
        CancellationToken cancellationToken)
    {
        var saldo = await _inventario.ObtenerSaldoParaActualizarAsync(
            sucursalId, productoId, cancellationToken);

        if (saldo is null)
        {
            // Primera compra de este producto en esta sede.
            _inventario.AgregarSaldo(new InventarioSucursal
            {
                SucursalId = sucursalId,
                ProductoId = productoId,
                CantidadBase = cantidadBase,
                StockMinimo = 0m,
                CostoPromedio = 0m
            });
            return cantidadBase;
        }

        var saldoResultante = saldo.CantidadBase + cantidadBase;
        _inventario.ActualizarCantidadBase(saldo, saldoResultante);
        return saldoResultante;
    }

    /// <summary>
    /// Registra el lote recibido, o le suma la cantidad si la sede ya lo tenia.
    ///
    /// Sumar y no crear otro es lo correcto: un mismo numero de lote del
    /// fabricante es el mismo lote, llegue en uno o en tres despachos, y
    /// duplicar la fila romperia el orden FEFO y el cuadre por lote.
    /// </summary>
    /// <returns>El id del lote y si se creo en esta entrega.</returns>
    private async Task<(int? LoteId, bool? Creado)> RegistrarLoteAsync(
        int sucursalId,
        int productoId,
        decimal cantidadBase,
        string numeroLote,
        DateOnly? fechaVencimiento,
        CancellationToken cancellationToken)
    {
        var existente = await _inventario.ObtenerLotePorNumeroParaActualizarAsync(
            sucursalId, productoId, numeroLote, cancellationToken);

        if (existente is not null)
        {
            _inventario.ActualizarCantidadLote(
                existente, (existente.CantidadBase ?? 0m) + cantidadBase);

            // La fecha de vencimiento NO se pisa. Si el lote ya existe con una
            // fecha y llega otra distinta, lo mas probable es un error de
            // digitacion, y sobrescribirla en silencio podria alargar la vida
            // util de un producto vencido.
            return (existente.Id, false);
        }

        var nuevo = new Lote
        {
            ProductoId = productoId,
            SucursalId = sucursalId,
            NumeroLote = numeroLote,
            FechaVencimiento = fechaVencimiento,
            CantidadBase = cantidadBase
            // FechaIngreso la pone la base con CURRENT_TIMESTAMP.
        };
        _inventario.AgregarLote(nuevo);

        // Hace falta guardar ya: el movimiento de esta misma linea lleva
        // `lote_id`, y hasta que MySQL no asigne el id no hay valor que poner.
        await _ordenes.GuardarCambiosAsync(cancellationToken);

        return (nuevo.Id, true);
    }

    private readonly record struct ConversionCompra(
        decimal CantidadBase,
        ErrorCompra? Error,
        string? Mensaje);

    /// <summary>
    /// Busca los factores y delega en <see cref="ConversorUnidades"/>, la misma
    /// formula que usa el modulo de Inventario. Aqui solo se traduce el
    /// resultado a los codigos de error de Compras.
    /// </summary>
    private async Task<ConversionCompra> ConvertirAsync(
        decimal cantidad,
        int unidadId,
        int unidadBaseId,
        CancellationToken cancellationToken)
    {
        decimal? factorUnidad = null;
        decimal? factorUnidadBase = null;

        if (unidadId != unidadBaseId)
        {
            var origen = await _unidades.ObtenerFactorConversionLitrosAsync(unidadId, cancellationToken);
            if (!origen.UnidadExiste)
            {
                return new ConversionCompra(0m, ErrorCompra.UnidadNoEncontrada,
                    $"No existe la unidad de compra {unidadId}.");
            }

            var destino = await _unidades.ObtenerFactorConversionLitrosAsync(unidadBaseId, cancellationToken);
            if (!destino.UnidadExiste)
            {
                return new ConversionCompra(0m, ErrorCompra.UnidadNoEncontrada,
                    $"No existe la unidad base {unidadBaseId} que declara el producto.");
            }

            factorUnidad = origen.FactorLitros;
            factorUnidadBase = destino.FactorLitros;
        }

        var conversion = ConversorUnidades.ABaseDelProducto(
            cantidad, unidadId, factorUnidad, unidadBaseId, factorUnidadBase);

        return conversion.Estado switch
        {
            EstadoConversion.Ok =>
                new ConversionCompra(conversion.CantidadBase, null, null),

            EstadoConversion.SinFactor =>
                new ConversionCompra(0m, ErrorCompra.ConversionImposible,
                    $"No hay conversion entre la unidad de compra {unidadId} y la unidad " +
                    $"base {unidadBaseId}: alguna de las dos no tiene factor a litros."),

            _ =>
                new ConversionCompra(0m, ErrorCompra.CantidadBaseCero,
                    $"Convertida a unidad base, la cantidad {Num(cantidad)} se redondea a " +
                    $"cero con {ConversorUnidades.DecimalesCantidadBase} decimales.")
        };
    }

    private static string ConstruirObservacion(int ordenId, string? observaciones) =>
        string.IsNullOrWhiteSpace(observaciones)
            ? $"Recepcion OC-{ordenId}"
            : $"Recepcion OC-{ordenId} | {observaciones}";

    private static string ConstruirDetalleAuditoria(
        OrdenCompra orden,
        EstadoOrdenCompra estadoNuevo,
        IReadOnlyList<LineaRecibidaDto> lineas)
    {
        var pendientes = orden.Detalles.Count(d => !EstaCompleta(d));

        var detalle =
            $"orden={orden.Id} | proveedor={orden.ProveedorId} | " +
            $"sucursal={orden.SucursalId} | estado->{estadoNuevo} | " +
            $"lineasEnEstaEntrega={lineas.Count} | lineasPendientes={pendientes}";

        foreach (var linea in lineas)
        {
            detalle +=
                $" || producto={linea.ProductoId} " +
                // Queda constancia de lo recibido ahora y del acumulado contra
                // lo pedido: sin eso, una bitacora de tres entregas parciales no
                // permite reconstruir cuanto faltaba en cada momento.
                $"recibidoAhora={Num(linea.CantidadRecibidaAhora)} " +
                $"acumulado={Num(linea.CantidadRecibidaTotal)}/{Num(linea.CantidadSolicitada)} " +
                $"cantidadBase=+{Num(linea.CantidadBaseIngresada)} " +
                $"saldo={Num(linea.SaldoResultante)} " +
                $"mov={linea.MovimientoId}";

            if (linea.LoteId is int loteId)
            {
                detalle += $" lote={loteId}({(linea.LoteCreado == true ? "nuevo" : "existente")})";
            }
        }

        return detalle;
    }

    // =========================================================================
    // EDICION Y RETIRO DE ORDENES: solo mientras sean un borrador
    // =========================================================================

    public async Task<ResultadoOrdenCompra> ActualizarOrdenAsync(
        int id,
        CrearOrdenCompraDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (await ValidarOrdenAsync(peticion, usuarioId, cancellationToken) is { } rechazo)
        {
            return rechazo;
        }

        var orden = await _ordenes.ObtenerParaEditarAsync(id, cancellationToken);
        if (orden is null)
        {
            return ResultadoOrdenCompra.Fallo(
                ErrorCompra.OrdenNoEncontrada,
                $"No existe la orden {id}.");
        }

        if (orden.Estado != EstadoOrdenCompra.Pendiente)
        {
            return ResultadoOrdenCompra.Fallo(
                ErrorCompra.EstadoNoPermiteEdicion,
                $"La orden {id} esta en estado '{orden.Estado}' y ya no se puede editar. " +
                "Solo se modifica mientras siga Pendiente: a partir de ahi hay un compromiso " +
                "con el proveedor y, si entro mercancia, movimientos en el libro mayor que " +
                "citan esta orden.");
        }

        var antes = orden.ToDto().Total;

        return await _inventario.EjecutarEnTransaccionAsync(async ct =>
        {
            orden.ProveedorId = peticion.ProveedorId;
            orden.PlazoPagoDias = peticion.PlazoPagoDias;

            // REEMPLAZO COMPLETO de las lineas. Se borran y se vuelven a crear en
            // vez de intentar casarlas una a una: las lineas de un borrador no
            // tienen identidad estable -nadie las ha citado todavia- y el
            // emparejamiento por posicion daria cambios silenciosos en cuanto
            // alguien inserte una linea en medio.
            //
            // Es seguro porque la orden esta Pendiente: CantidadRecibida es cero
            // en todas, asi que no se pierde ningun dato de recepcion.
            _ordenes.QuitarDetalles(orden.Detalles.ToList());
            orden.Detalles.Clear();

            foreach (var linea in peticion.Lineas)
            {
                _ordenes.AgregarDetalle(new OrdenCompraDetalle
                {
                    OrdenCompraId = orden.Id,
                    ProductoId = linea.ProductoId,
                    Cantidad = linea.Cantidad,
                    CantidadRecibida = 0m,
                    UnidadId = linea.UnidadId,
                    PrecioUnitario = linea.PrecioUnitario,
                    Descuento = linea.Descuento
                });
            }

            await _ordenes.GuardarCambiosAsync(ct);

            // Se relee para calcular el total con las lineas ya guardadas: la
            // coleccion en memoria se acaba de vaciar y rellenar por separado.
            var guardada = await _ordenes.ObtenerPorIdAsync(orden.Id, ct);
            var total = guardada?.ToDto().Total ?? 0m;

            await _auditoria.RegistrarEventoAsync(
                Modulo,
                "ActualizarOrdenCompra",
                usuarioId,
                $"orden={orden.Id} | proveedor={peticion.ProveedorId} | " +
                $"lineas={peticion.Lineas.Count} | total={Num(antes)} -> {Num(total)}",
                ct);

            await _ordenes.GuardarCambiosAsync(ct);

            return new ResultadoOrdenCompra(
                true, ErrorCompra.Ninguno, "Orden actualizada.", orden.Id, total);
        }, cancellationToken);
    }

    public async Task<ResultadoOrdenCompra> CancelarOrdenAsync(
        int id,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var orden = await _ordenes.ObtenerParaEditarAsync(id, cancellationToken);
        if (orden is null)
        {
            return ResultadoOrdenCompra.Fallo(
                ErrorCompra.OrdenNoEncontrada,
                $"No existe la orden {id}.");
        }

        if (orden.Estado != EstadoOrdenCompra.Pendiente)
        {
            var explicacion = orden.Estado is EstadoOrdenCompra.Cancelada
                ? "Esa orden ya estaba cancelada."
                : $"La orden {id} esta en estado '{orden.Estado}' y ya no se puede retirar. " +
                  "Una vez confirmada o recibida, la orden es el documento que respalda lo que " +
                  "entro a la bodega.";

            return ResultadoOrdenCompra.Fallo(ErrorCompra.EstadoNoPermiteEdicion, explicacion);
        }

        var total = orden.ToDto().Total;

        return await _inventario.EjecutarEnTransaccionAsync(async ct =>
        {
            _ordenes.ActualizarEstado(orden, EstadoOrdenCompra.Cancelada);

            await _auditoria.RegistrarEventoAsync(
                Modulo,
                "CancelarOrdenCompra",
                usuarioId,
                $"orden={orden.Id} | proveedor={orden.ProveedorId} | " +
                $"sucursal={orden.SucursalId} | total={Num(total)} | Pendiente -> Cancelada",
                ct);

            await _ordenes.GuardarCambiosAsync(ct);

            return new ResultadoOrdenCompra(
                true,
                ErrorCompra.Ninguno,
                "Orden cancelada. No se borro: queda registrada como Cancelada con su detalle.",
                orden.Id,
                total);
        }, cancellationToken);
    }

    // =========================================================================
    // PRECIOS DE REFERENCIA
    // =========================================================================

    public async Task<PrecioReferenciaDto?> ObtenerPrecioReferenciaAsync(
        int productoId,
        int proveedorId,
        CancellationToken cancellationToken = default)
    {
        var producto = await _productos.ObtenerPorIdAsync(productoId, cancellationToken);
        if (producto is null)
        {
            return null;
        }

        var proveedor = await _proveedores.ObtenerPorIdAsync(proveedorId, cancellationToken);
        if (proveedor is null)
        {
            return null;
        }

        var precio = await _proveedores.ObtenerPrecioAsync(
            productoId, proveedorId, cancellationToken);

        var ultima = await _ordenes.ObtenerUltimaLineaConPrecioAsync(
            productoId, proveedorId, cancellationToken);

        return new PrecioReferenciaDto(
            producto.Id,
            producto.Nombre,
            proveedor.Id,
            proveedor.Nombre,
            producto.UnidadBase?.Simbolo,
            precio?.PrecioReferencia,
            ultima is null
                ? null
                : new UltimaCompraDto(
                    ultima.OrdenCompraId,
                    ultima.OrdenCompra.Fecha ?? default,
                    ultima.Cantidad,
                    ultima.UnidadId,
                    ultima.Unidad?.Simbolo,
                    ultima.PrecioUnitario,
                    ultima.Descuento,
                    ultima.OrdenCompra.Estado?.ToString() ?? string.Empty));
    }

    // =========================================================================
    // PROVEEDORES
    // =========================================================================

    public async Task<ResultadoProveedor> CrearProveedorAsync(
        GuardarProveedorDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var nombre = peticion.Nombre?.Trim() ?? string.Empty;
        var telefono = peticion.Telefono?.Trim() ?? string.Empty;

        if (nombre.Length == 0 || telefono.Length == 0)
        {
            return ResultadoProveedor.Fallo(
                ErrorCompra.DatosProveedorIncompletos,
                "El nombre y el telefono del proveedor son obligatorios.");
        }

        if (await _proveedores.ExisteNombreAsync(nombre, null, cancellationToken))
        {
            return ResultadoProveedor.Fallo(
                ErrorCompra.ProveedorDuplicado,
                $"Ya hay un proveedor llamado '{nombre}'. Si esta retirado, reactivalo en vez " +
                "de crear otro: asi conserva su lista de precios y su historial de ordenes.");
        }

        var proveedor = new Proveedor
        {
            Nombre = nombre,
            Contacto = string.IsNullOrWhiteSpace(peticion.Contacto)
                ? null
                : peticion.Contacto.Trim(),
            Telefono = telefono,
            Activo = true
        };

        _proveedores.Agregar(proveedor);
        await _proveedores.GuardarCambiosAsync(cancellationToken);

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "CrearProveedor",
            usuarioId,
            $"proveedor={proveedor.Id} ('{nombre}') | telefono={telefono}",
            cancellationToken);

        await _proveedores.GuardarCambiosAsync(cancellationToken);

        return ResultadoProveedor.Ok(
            proveedor.ToDto(contarProductos: true), $"Proveedor '{nombre}' creado.");
    }

    public async Task<ResultadoProveedor> ActualizarProveedorAsync(
        int id,
        GuardarProveedorDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        var nombre = peticion.Nombre?.Trim() ?? string.Empty;
        var telefono = peticion.Telefono?.Trim() ?? string.Empty;

        if (nombre.Length == 0 || telefono.Length == 0)
        {
            return ResultadoProveedor.Fallo(
                ErrorCompra.DatosProveedorIncompletos,
                "El nombre y el telefono del proveedor son obligatorios.");
        }

        var proveedor = await _proveedores.ObtenerParaEditarAsync(id, cancellationToken);
        if (proveedor is null)
        {
            return ResultadoProveedor.Fallo(
                ErrorCompra.ProveedorNoEncontrado,
                $"No existe el proveedor {id}.");
        }

        // `excluyendoId` es lo que permite guardar sin cambiar el nombre: sin el,
        // el proveedor chocaria consigo mismo.
        if (await _proveedores.ExisteNombreAsync(nombre, id, cancellationToken))
        {
            return ResultadoProveedor.Fallo(
                ErrorCompra.ProveedorDuplicado,
                $"Ya hay otro proveedor llamado '{nombre}'.");
        }

        var antes = $"'{proveedor.Nombre}' contacto='{proveedor.Contacto}' tel={proveedor.Telefono}";

        proveedor.Nombre = nombre;
        proveedor.Contacto = string.IsNullOrWhiteSpace(peticion.Contacto)
            ? null
            : peticion.Contacto.Trim();
        proveedor.Telefono = telefono;

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "ActualizarProveedor",
            usuarioId,
            $"proveedor={id} | {antes} -> '{nombre}' contacto='{proveedor.Contacto}' tel={telefono}",
            cancellationToken);

        await _proveedores.GuardarCambiosAsync(cancellationToken);

        return ResultadoProveedor.Ok(
            proveedor.ToDto(contarProductos: true), "Proveedor actualizado.");
    }

    public Task<ResultadoProveedor> DesactivarProveedorAsync(
        int id,
        int usuarioId,
        CancellationToken cancellationToken = default) =>
        CambiarEstadoProveedorAsync(id, activo: false, usuarioId, cancellationToken);

    public Task<ResultadoProveedor> ReactivarProveedorAsync(
        int id,
        int usuarioId,
        CancellationToken cancellationToken = default) =>
        CambiarEstadoProveedorAsync(id, activo: true, usuarioId, cancellationToken);

    private async Task<ResultadoProveedor> CambiarEstadoProveedorAsync(
        int id,
        bool activo,
        int usuarioId,
        CancellationToken cancellationToken)
    {
        var proveedor = await _proveedores.ObtenerParaEditarAsync(id, cancellationToken);
        if (proveedor is null)
        {
            return ResultadoProveedor.Fallo(
                ErrorCompra.ProveedorNoEncontrado,
                $"No existe el proveedor {id}.");
        }

        if (proveedor.Activo == activo)
        {
            return ResultadoProveedor.Fallo(
                ErrorCompra.ProveedorEstadoSinCambio,
                activo
                    ? "Ese proveedor ya estaba activo."
                    : "Ese proveedor ya estaba retirado.");
        }

        proveedor.Activo = activo;

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            activo ? "ReactivarProveedor" : "DesactivarProveedor",
            usuarioId,
            $"proveedor={id} ('{proveedor.Nombre}')",
            cancellationToken);

        await _proveedores.GuardarCambiosAsync(cancellationToken);

        return ResultadoProveedor.Ok(
            proveedor.ToDto(contarProductos: true),
            activo
                ? $"'{proveedor.Nombre}' vuelve al catalogo."
                : $"'{proveedor.Nombre}' queda retirado. Sus ordenes y su lista de precios " +
                  "siguen intactas; solo deja de ofrecerse en ordenes nuevas.");
    }

    public async Task<ResultadoProveedor> GuardarPrecioReferenciaAsync(
        int proveedorId,
        int productoId,
        GuardarPrecioReferenciaDto peticion,
        int usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (peticion.PrecioReferencia is < 0m)
        {
            return ResultadoProveedor.Fallo(
                ErrorCompra.PrecioInvalido,
                "El precio de referencia no puede ser negativo.");
        }

        var proveedor = await _proveedores.ObtenerParaEditarAsync(proveedorId, cancellationToken);
        if (proveedor is null)
        {
            return ResultadoProveedor.Fallo(
                ErrorCompra.ProveedorNoEncontrado,
                $"No existe el proveedor {proveedorId}.");
        }

        var producto = await _productos.ObtenerPorIdAsync(productoId, cancellationToken);
        if (producto is null)
        {
            return ResultadoProveedor.Fallo(
                ErrorCompra.ProductoNoEncontrado,
                $"No existe el producto {productoId}.");
        }

        var precio = await _proveedores.ObtenerPrecioAsync(
            productoId, proveedorId, cancellationToken);

        string detalle;

        if (peticion.PrecioReferencia is null)
        {
            if (precio is null)
            {
                return ResultadoProveedor.Fallo(
                    ErrorCompra.PrecioNoEncontrado,
                    $"'{producto.Nombre}' no estaba en la lista de '{proveedor.Nombre}'.");
            }

            // Quitar la entrada NO toca ninguna orden: las lineas historicas
            // guardan su propio precio, no una referencia a esta tabla.
            _proveedores.QuitarPrecio(precio);
            detalle = $"producto={productoId} ('{producto.Nombre}') | precio retirado de la lista";
        }
        else if (precio is null)
        {
            _proveedores.AgregarPrecio(new ProductoProveedor
            {
                ProductoId = productoId,
                ProveedorId = proveedorId,
                PrecioReferencia = peticion.PrecioReferencia
            });
            detalle = $"producto={productoId} ('{producto.Nombre}') | " +
                      $"precio nuevo={Num(peticion.PrecioReferencia.Value)}";
        }
        else
        {
            var anterior = precio.PrecioReferencia;
            precio.PrecioReferencia = peticion.PrecioReferencia;
            detalle = $"producto={productoId} ('{producto.Nombre}') | precio=" +
                      $"{(anterior is null ? "sin precio" : Num(anterior.Value))} -> " +
                      $"{Num(peticion.PrecioReferencia.Value)}";
        }

        await _auditoria.RegistrarEventoAsync(
            Modulo,
            "GuardarPrecioReferencia",
            usuarioId,
            $"proveedor={proveedorId} ('{proveedor.Nombre}') | {detalle} | " +
            $"unidad base={producto.UnidadBase?.Simbolo}",
            cancellationToken);

        await _proveedores.GuardarCambiosAsync(cancellationToken);

        var recargado = await _proveedores.ObtenerPorIdAsync(proveedorId, cancellationToken);

        return ResultadoProveedor.Ok(
            (recargado ?? proveedor).ToDto(contarProductos: true),
            "Lista de precios actualizada.");
    }

    /// <summary>
    /// Formatea con punto decimal siempre, para que la bitacora no cambie de
    /// formato segun la configuracion regional del servidor.
    /// </summary>
    private static string Num(decimal valor) =>
        valor.ToString("0.####", CultureInfo.InvariantCulture);
}
