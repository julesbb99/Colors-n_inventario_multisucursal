using System.Globalization;
using Colorsin.Application.Comun.Auditoria;
using Colorsin.Application.Comun.Services;
using Colorsin.Application.Inventario.DTOs;
using Colorsin.Application.Inventario.Mapping;
using Colorsin.Application.Inventario.Repositories;
using Colorsin.Domain.Inventario;

namespace Colorsin.Application.Inventario.Services;

/// <inheritdoc cref="IInventarioService"/>
public sealed class InventarioService : IInventarioService
{
    /// <summary>Nombre del modulo en los eventos de auditoria.</summary>
    private const string Modulo = "Inventario";

    private readonly IInventarioRepository _inventario;
    private readonly IUnidadMedidaService _unidades;
    private readonly IProductoRepository _productos;
    private readonly IAuditoriaService _auditoria;

    public InventarioService(
        IInventarioRepository inventario,
        IUnidadMedidaService unidades,
        IProductoRepository productos,
        IAuditoriaService auditoria)
    {
        _inventario = inventario;
        _unidades = unidades;
        _productos = productos;
        _auditoria = auditoria;
    }

    // =========================================================================
    // CONSULTAS
    // =========================================================================

    public async Task<IReadOnlyList<InventarioSucursalDto>> ObtenerExistenciasAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default)
    {
        var saldos = await _inventario.ObtenerExistenciasAsync(sucursalId, cancellationToken);
        return saldos.Select(s => s.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<LoteDto>> ObtenerLotesPorVencimientoAsync(
        int sucursalId,
        int productoId,
        CancellationToken cancellationToken = default)
    {
        var lotes = await _inventario.ObtenerLotesPorVencimientoAsync(
            sucursalId, productoId, cancellationToken);

        // Una sola lectura del reloj para toda la lista: si se leyera por lote,
        // una consulta lanzada justo a medianoche daria dias distintos para
        // lotes con la misma fecha.
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        return lotes.Select(l => l.ToDto(hoy)).ToList();
    }

    public async Task<IReadOnlyList<MovimientoInventarioDto>> ObtenerMovimientosAsync(
        int? sucursalId = null,
        int? productoId = null,
        int limite = 100,
        CancellationToken cancellationToken = default)
    {
        var movimientos = await _inventario.ObtenerMovimientosAsync(
            sucursalId, productoId, limite, cancellationToken);
        return movimientos.Select(m => m.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<StockAlertaDto>> ObtenerAlertasStockBajoAsync(
        int? sucursalId = null,
        CancellationToken cancellationToken = default)
    {
        var saldos = await _inventario.ObtenerAlertasStockBajoAsync(sucursalId, cancellationToken);
        return saldos.Select(s => s.ToAlertaDto()).ToList();
    }

    // =========================================================================
    // REGISTRO DE MOVIMIENTOS
    // =========================================================================

    public async Task<ResultadoMovimiento> RegistrarMovimientoAsync(
        RegistrarMovimientoDto peticion,
        CancellationToken cancellationToken = default)
    {
        // --- 1. Validaciones que no tocan la base ------------------------------
        if (peticion.Cantidad <= 0)
        {
            return ResultadoMovimiento.Fallo(
                ErrorMovimiento.CantidadInvalida,
                $"La cantidad debe ser mayor que cero; llego {Num(peticion.Cantidad)}. " +
                "El signo lo determina el tipo de movimiento, no el numero.");
        }

        // --- 2. Producto y su unidad base --------------------------------------
        var producto = await _productos.ObtenerPorIdAsync(peticion.ProductoId, cancellationToken);
        if (producto is null)
        {
            return ResultadoMovimiento.Fallo(
                ErrorMovimiento.ProductoNoEncontrado,
                $"No existe el producto {peticion.ProductoId}.");
        }

        if (producto.UnidadBaseId is null)
        {
            return ResultadoMovimiento.Fallo(
                ErrorMovimiento.ProductoSinUnidadBase,
                $"El producto '{producto.Nombre}' no tiene unidad base definida, " +
                "asi que no hay unidad a la cual convertir la cantidad.");
        }

        // --- 3. Conversion a unidad base ---------------------------------------
        var conversion = await ConvertirAUnidadBaseAsync(
            peticion.Cantidad, peticion.UnidadId, producto.UnidadBaseId.Value, cancellationToken);

        if (!conversion.Exito)
        {
            return conversion.Resultado!;
        }

        var cantidadBase = conversion.CantidadBase;

        // --- 4. Escritura, toda dentro de una transaccion -----------------------
        // El bloqueo de las filas de saldo y de lote vive mientras la
        // transaccion este abierta; por eso la lectura del saldo va adentro.
        //
        // Dentro de la transaccion NO se prepara ningun cambio hasta que todas
        // las validaciones pasan: asi una regla de negocio incumplida sale con
        // la transaccion limpia, sin nada que revertir.
        return await _inventario.EjecutarEnTransaccionAsync(
            ct => RegistrarEnTransaccionAsync(peticion, producto, cantidadBase, ct),
            cancellationToken);
    }

    private async Task<ResultadoMovimiento> RegistrarEnTransaccionAsync(
        RegistrarMovimientoDto peticion,
        Producto producto,
        decimal cantidadBase,
        CancellationToken cancellationToken)
    {
        var esIngreso = peticion.TipoMovimiento == TipoMovimiento.Ingreso;

        var saldo = await _inventario.ObtenerSaldoParaActualizarAsync(
            peticion.SucursalId, peticion.ProductoId, cancellationToken);

        // Sin fila de saldo el disponible es cero. Para un retiro eso ya es
        // motivo de rechazo; para un ingreso, la fila se crea mas abajo.
        var disponible = saldo?.CantidadBase ?? 0m;

        if (saldo is null && !esIngreso)
        {
            return ResultadoMovimiento.Fallo(
                ErrorMovimiento.SaldoNoEncontrado,
                $"La sede {peticion.SucursalId} no tiene saldo registrado del producto " +
                $"'{producto.Nombre}'; no se puede retirar de lo que nunca entro.");
        }

        // --- 5. Lote, si el movimiento se imputa a uno --------------------------
        Lote? lote = null;
        if (peticion.LoteId is int loteId)
        {
            lote = await _inventario.ObtenerLoteParaActualizarAsync(loteId, cancellationToken);

            if (lote is null)
            {
                return ResultadoMovimiento.Fallo(
                    ErrorMovimiento.LoteNoEncontrado, $"No existe el lote {loteId}.");
            }

            // Sin esta validacion se podria descontar del lote de otra sede o de
            // otro producto, y el saldo por lote dejaria de cuadrar con el
            // consolidado sin que nada avisara.
            if (lote.ProductoId != peticion.ProductoId || lote.SucursalId != peticion.SucursalId)
            {
                return ResultadoMovimiento.Fallo(
                    ErrorMovimiento.LoteNoCorresponde,
                    $"El lote '{lote.NumeroLote}' pertenece al producto {lote.ProductoId} " +
                    $"en la sede {lote.SucursalId}, pero el movimiento es del producto " +
                    $"{peticion.ProductoId} en la sede {peticion.SucursalId}.");
            }

            var cantidadLote = lote.CantidadBase ?? 0m;
            if (!esIngreso && cantidadLote < cantidadBase)
            {
                return ResultadoMovimiento.Fallo(
                    ErrorMovimiento.StockLoteInsuficiente,
                    $"El lote '{lote.NumeroLote}' tiene {Num(cantidadLote)} y se " +
                    $"intentan retirar {Num(cantidadBase)}.");
            }
        }

        // --- 6. Regla de negocio: un retiro no puede dejar el saldo negativo ----
        if (!esIngreso && disponible < cantidadBase)
        {
            return ResultadoMovimiento.Fallo(
                ErrorMovimiento.StockInsuficiente,
                $"Stock insuficiente de '{producto.Nombre}' en la sede {peticion.SucursalId}: " +
                $"hay {Num(disponible)} y se intentan retirar {Num(cantidadBase)}.");
        }

        // --- 7. Aplicar. Desde aqui ya no hay rechazos ---------------------------
        var delta = esIngreso ? cantidadBase : -cantidadBase;
        var saldoResultante = disponible + delta;

        if (saldo is null)
        {
            // Primer ingreso de este producto en esta sede: se abre la fila. El
            // indice unico (sucursal, producto) impide que dos ingresos
            // simultaneos creen dos filas; el segundo falla y se revierte.
            _inventario.AgregarSaldo(new InventarioSucursal
            {
                SucursalId = peticion.SucursalId,
                ProductoId = peticion.ProductoId,
                CantidadBase = saldoResultante,
                StockMinimo = 0m,
                CostoPromedio = 0m
            });
        }
        else
        {
            _inventario.ActualizarCantidadBase(saldo, saldoResultante);
        }

        if (lote is not null)
        {
            _inventario.ActualizarCantidadLote(lote, (lote.CantidadBase ?? 0m) + delta);
        }

        var movimiento = new MovimientoInventario
        {
            SucursalId = peticion.SucursalId,
            ProductoId = peticion.ProductoId,
            UsuarioId = peticion.UsuarioId,
            Tipo = peticion.TipoMovimiento,
            Motivo = peticion.Motivo,
            Cantidad = peticion.Cantidad,
            UnidadId = peticion.UnidadId,
            CantidadBase = cantidadBase,
            LoteId = peticion.LoteId,
            Observaciones = peticion.Observaciones
            // Fecha la pone la base con CURRENT_TIMESTAMP: en un libro mayor
            // todas las marcas de tiempo deben venir del mismo reloj, no del
            // de cada maquina que ejecute la API.
        };
        _inventario.AgregarMovimiento(movimiento);

        // Se guarda aqui para que MySQL asigne el id del movimiento, que hace
        // falta en el detalle de la auditoria. Sigue dentro de la transaccion.
        await _inventario.GuardarCambiosAsync(cancellationToken);

        // --- 8. Auditoria, en la MISMA transaccion ------------------------------
        await _auditoria.RegistrarEventoAsync(
            Modulo,
            lote is null ? "RegistrarMovimiento" : "RegistrarMovimientoConLote",
            peticion.UsuarioId,
            ConstruirDetalle(peticion, producto.Nombre, cantidadBase, saldoResultante, lote),
            cancellationToken);

        await _inventario.GuardarCambiosAsync(cancellationToken);

        return ResultadoMovimiento.Ok(movimiento.Id, cantidadBase, saldoResultante);
    }

    // =========================================================================
    // CONVERSION DE UNIDADES
    // =========================================================================

    private readonly record struct ResultadoConversion(
        bool Exito,
        decimal CantidadBase,
        ResultadoMovimiento? Resultado);

    /// <summary>
    /// Busca los factores de las dos unidades y delega la cuenta en
    /// <see cref="ConversorUnidades"/>, que es donde vive la formula.
    ///
    /// Aqui solo queda lo que necesita la base de datos (buscar los factores) y
    /// la traduccion del resultado a los codigos de error de este modulo.
    /// Compras hace lo mismo con sus propios codigos, sobre la misma formula.
    /// </summary>
    private async Task<ResultadoConversion> ConvertirAUnidadBaseAsync(
        decimal cantidad,
        int unidadId,
        int unidadBaseId,
        CancellationToken cancellationToken)
    {
        decimal? factorUnidad = null;
        decimal? factorUnidadBase = null;

        // Si las unidades coinciden no hace falta consultar nada: el conversor
        // devuelve la cantidad tal cual.
        if (unidadId != unidadBaseId)
        {
            var origen = await _unidades.ObtenerFactorConversionLitrosAsync(unidadId, cancellationToken);
            if (!origen.UnidadExiste)
            {
                return new ResultadoConversion(false, 0m, ResultadoMovimiento.Fallo(
                    ErrorMovimiento.UnidadNoEncontrada,
                    $"No existe la unidad de medida {unidadId}."));
            }

            var destino = await _unidades.ObtenerFactorConversionLitrosAsync(unidadBaseId, cancellationToken);
            if (!destino.UnidadExiste)
            {
                return new ResultadoConversion(false, 0m, ResultadoMovimiento.Fallo(
                    ErrorMovimiento.UnidadNoEncontrada,
                    $"No existe la unidad base {unidadBaseId} que declara el producto."));
            }

            factorUnidad = origen.FactorLitros;
            factorUnidadBase = destino.FactorLitros;
        }

        var conversion = ConversorUnidades.ABaseDelProducto(
            cantidad, unidadId, factorUnidad, unidadBaseId, factorUnidadBase);

        return conversion.Estado switch
        {
            EstadoConversion.Ok =>
                new ResultadoConversion(true, conversion.CantidadBase, null),

            EstadoConversion.SinFactor =>
                new ResultadoConversion(false, 0m, ResultadoMovimiento.Fallo(
                    ErrorMovimiento.ConversionImposible,
                    $"No hay conversion entre la unidad {unidadId} y la unidad base " +
                    $"{unidadBaseId}: alguna de las dos no tiene factor a litros.")),

            _ =>
                new ResultadoConversion(false, 0m, ResultadoMovimiento.Fallo(
                    ErrorMovimiento.CantidadBaseCero,
                    $"Convertida a unidad base, la cantidad {Num(cantidad)} se redondea a cero " +
                    $"con {ConversorUnidades.DecimalesCantidadBase} decimales. " +
                    "Registra una cantidad mayor."))
        };
    }

    // =========================================================================
    // AUXILIARES
    // =========================================================================

    private static string ConstruirDetalle(
        RegistrarMovimientoDto peticion,
        string nombreProducto,
        decimal cantidadBase,
        decimal saldoResultante,
        Lote? lote)
    {
        var detalle =
            $"{peticion.TipoMovimiento}/{peticion.Motivo} | " +
            $"sucursal={peticion.SucursalId} | " +
            $"producto={peticion.ProductoId} ('{nombreProducto}') | " +
            $"cantidad={Num(peticion.Cantidad)} unidad={peticion.UnidadId} | " +
            $"cantidadBase={Num(cantidadBase)} | " +
            $"saldoResultante={Num(saldoResultante)}";

        if (lote is not null)
        {
            detalle += $" | lote={lote.Id} ('{lote.NumeroLote}')";
        }

        if (!string.IsNullOrWhiteSpace(peticion.Observaciones))
        {
            detalle += $" | obs={peticion.Observaciones}";
        }

        return detalle;
    }

    /// <summary>
    /// Formatea con punto decimal siempre.
    ///
    /// Sin esto, el detalle de auditoria saldria con coma en un servidor con
    /// configuracion regional colombiana y con punto en otro, y la misma
    /// bitacora quedaria con dos formatos segun donde se ejecute.
    /// </summary>
    private static string Num(decimal valor) =>
        valor.ToString("0.####", CultureInfo.InvariantCulture);
}
