using Colorsin.Domain.Compras;
using Colorsin.Domain.Comun;
using Colorsin.Domain.Inventario;
using Colorsin.Domain.Transferencias;
using Colorsin.Domain.Ventas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Colorsin.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core mapeado contra el esquema `colorsin_inventario` que ya
/// existe en MySQL. Todo el mapeo se hace por Fluent API: las entidades de
/// dominio son POCO puras y no conocen a EF.
///
/// IMPORTANTE sobre la precision decimal: cada columna se declara con la
/// precision REAL que tiene en MySQL, no con una unica precision global. Ver
/// la nota en <see cref="ConfigurarInventario"/> sobre FactorConversionLitros.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // --- Comun ---
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    // --- Inventario ---
    public DbSet<UnidadMedida> UnidadesMedida => Set<UnidadMedida>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<InventarioSucursal> InventarioSucursales => Set<InventarioSucursal>();
    public DbSet<Lote> Lotes => Set<Lote>();
    public DbSet<MovimientoInventario> MovimientosInventario => Set<MovimientoInventario>();

    // --- Compras ---
    public DbSet<Proveedor> Proveedores => Set<Proveedor>();
    public DbSet<ProductoProveedor> ProductoProveedores => Set<ProductoProveedor>();
    public DbSet<OrdenCompra> OrdenesCompra => Set<OrdenCompra>();
    public DbSet<OrdenCompraDetalle> OrdenCompraDetalles => Set<OrdenCompraDetalle>();

    // --- Ventas ---
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<VentaDetalle> VentaDetalles => Set<VentaDetalle>();

    // --- Transferencias ---
    public DbSet<Transportadora> Transportadoras => Set<Transportadora>();
    public DbSet<Transferencia> Transferencias => Set<Transferencia>();
    public DbSet<NovedadTransferencia> NovedadesTransferencia => Set<NovedadTransferencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Collation del esquema, para que las comparaciones de texto se
        // comporten igual desde EF que desde Workbench.
        modelBuilder.UseCollation("utf8mb4_0900_ai_ci");

        ConfigurarComun(modelBuilder);
        ConfigurarInventario(modelBuilder);
        ConfigurarCompras(modelBuilder);
        ConfigurarVentas(modelBuilder);
        ConfigurarTransferencias(modelBuilder);
    }

    // =========================================================================
    // Convertidores de ENUM
    //
    // Los ENUM cuyos nombres en C# coinciden con los valores de MySQL usan
    // HasConversion<string>(). Los dos que NO coinciden necesitan convertidor
    // explicito. Se escriben con operadores ternarios y no con expresiones
    // switch porque un arbol de expresiones no admite `switch`.
    // =========================================================================

    /// <summary>MySQL guarda 'Administrador General' y 'Gerente de Sucursal', con espacios.</summary>
    private static readonly ValueConverter<RolUsuario, string> RolUsuarioConverter = new(
        v => v == RolUsuario.AdministradorGeneral ? "Administrador General"
           : v == RolUsuario.GerenteDeSucursal ? "Gerente de Sucursal"
           : "Operador",
        v => v == "Administrador General" ? RolUsuario.AdministradorGeneral
           : v == "Gerente de Sucursal" ? RolUsuario.GerenteDeSucursal
           : RolUsuario.Operador);

    /// <summary>MySQL guarda 'urgente' y 'estandar', en minuscula.</summary>
    private static readonly ValueConverter<TipoServicio, string> TipoServicioConverter = new(
        v => v == TipoServicio.Urgente ? "urgente" : "estandar",
        v => v == "urgente" ? TipoServicio.Urgente : TipoServicio.Estandar);

    // =========================================================================
    // COMUN
    // =========================================================================
    private static void ConfigurarComun(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sucursal>(e =>
        {
            e.ToTable("sucursales");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            e.Property(x => x.Ciudad).HasColumnName("ciudad").HasMaxLength(80).IsRequired();
            e.Property(x => x.Direccion).HasColumnName("direccion").HasMaxLength(150);
            e.Property(x => x.RolRed).HasColumnName("rol_red").HasConversion<string>().IsRequired();
            e.Property(x => x.Descripcion).HasColumnName("descripcion").HasMaxLength(200);
        });

        modelBuilder.Entity<Usuario>(e =>
        {
            e.ToTable("usuarios");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            e.Property(x => x.Rol).HasColumnName("rol").HasConversion(RolUsuarioConverter).IsRequired();
            e.Property(x => x.SucursalId).HasColumnName("sucursal_id");

            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("uq_usuarios_email");

            // Si se elimina una sucursal, sus usuarios quedan sin sede (SET NULL),
            // no se borran: el Administrador General ya vive sin sede.
            e.HasOne(x => x.Sucursal)
             .WithMany(s => s.Usuarios)
             .HasForeignKey(x => x.SucursalId)
             .HasConstraintName("fk_usuarios_sucursal")
             .OnDelete(DeleteBehavior.SetNull);
        });
    }

    // =========================================================================
    // INVENTARIO
    // =========================================================================
    private static void ConfigurarInventario(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UnidadMedida>(e =>
        {
            e.ToTable("unidades_medida");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(50).IsRequired();
            e.Property(x => x.Simbolo).HasColumnName("simbolo").HasMaxLength(10).IsRequired();

            // OJO: 6 decimales, NO 2. Un galon son 3.785410 litros; con 2
            // decimales quedaria en 3.79 y toda conversion del sistema se
            // desviaria. La base tiene decimal(12,6) y aqui se respeta.
            e.Property(x => x.FactorConversionLitros)
             .HasColumnName("factor_conversion_litros")
             .HasPrecision(12, 6);
        });

        modelBuilder.Entity<Producto>(e =>
        {
            e.ToTable("productos");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            e.Property(x => x.Categoria).HasColumnName("categoria").HasMaxLength(50);
            e.Property(x => x.Descripcion).HasColumnName("descripcion").HasColumnType("text");
            e.Property(x => x.UnidadBaseId).HasColumnName("unidad_base_id");

            e.HasOne(x => x.UnidadBase)
             .WithMany(u => u.ProductosConUnidadBase)
             .HasForeignKey(x => x.UnidadBaseId)
             .HasConstraintName("fk_productos_unidad_base")
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventarioSucursal>(e =>
        {
            e.ToTable("inventario_sucursal");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.SucursalId).HasColumnName("sucursal_id").IsRequired();
            e.Property(x => x.ProductoId).HasColumnName("producto_id").IsRequired();
            e.Property(x => x.CantidadBase).HasColumnName("cantidad_base").HasPrecision(14, 4).HasDefaultValue(0m);
            e.Property(x => x.StockMinimo).HasColumnName("stock_minimo").HasPrecision(14, 4).HasDefaultValue(0m);
            e.Property(x => x.CostoPromedio).HasColumnName("costo_promedio").HasPrecision(14, 4).HasDefaultValue(0m);

            // Un solo saldo por pareja (sede, producto).
            e.HasIndex(x => new { x.SucursalId, x.ProductoId })
             .IsUnique()
             .HasDatabaseName("uq_inventario_sucursal_producto");

            e.HasOne(x => x.Sucursal)
             .WithMany(s => s.Inventarios)
             .HasForeignKey(x => x.SucursalId)
             .HasConstraintName("fk_inventario_sucursal")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Producto)
             .WithMany(p => p.Inventarios)
             .HasForeignKey(x => x.ProductoId)
             .HasConstraintName("fk_inventario_producto")
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Lote>(e =>
        {
            e.ToTable("lotes");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.ProductoId).HasColumnName("producto_id").IsRequired();
            e.Property(x => x.SucursalId).HasColumnName("sucursal_id").IsRequired();
            e.Property(x => x.NumeroLote).HasColumnName("numero_lote").HasMaxLength(50).IsRequired();
            e.Property(x => x.FechaVencimiento).HasColumnName("fecha_vencimiento").HasColumnType("date");
            e.Property(x => x.CantidadBase).HasColumnName("cantidad_base").HasPrecision(14, 4);
            e.Property(x => x.FechaIngreso).HasColumnName("fecha_ingreso").HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasIndex(x => x.FechaVencimiento).HasDatabaseName("idx_lotes_vencimiento");
            e.HasIndex(x => x.NumeroLote).HasDatabaseName("idx_lotes_numero");

            e.HasOne(x => x.Producto)
             .WithMany(p => p.Lotes)
             .HasForeignKey(x => x.ProductoId)
             .HasConstraintName("fk_lotes_producto")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Sucursal)
             .WithMany(s => s.Lotes)
             .HasForeignKey(x => x.SucursalId)
             .HasConstraintName("fk_lotes_sucursal")
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MovimientoInventario>(e =>
        {
            e.ToTable("movimientos_inventario");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.SucursalId).HasColumnName("sucursal_id").IsRequired();
            e.Property(x => x.ProductoId).HasColumnName("producto_id").IsRequired();
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
            e.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<string>();
            e.Property(x => x.Motivo).HasColumnName("motivo").HasConversion<string>();
            e.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(14, 4);
            e.Property(x => x.UnidadId).HasColumnName("unidad_id").IsRequired();
            e.Property(x => x.CantidadBase).HasColumnName("cantidad_base").HasPrecision(14, 4);
            e.Property(x => x.Fecha).HasColumnName("fecha").HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Indice principal: reconstruir el saldo de un producto en una sede.
            e.HasIndex(x => new { x.SucursalId, x.ProductoId, x.Fecha })
             .HasDatabaseName("idx_movinv_sucursal_producto_fecha");

            // Auditoria: TODAS las FK son Restrict. Nada debe poder borrar el
            // libro mayor en cascada.
            e.HasOne(x => x.Sucursal)
             .WithMany(s => s.Movimientos)
             .HasForeignKey(x => x.SucursalId)
             .HasConstraintName("fk_movinv_sucursal")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Producto)
             .WithMany(p => p.Movimientos)
             .HasForeignKey(x => x.ProductoId)
             .HasConstraintName("fk_movinv_producto")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Usuario)
             .WithMany(u => u.Movimientos)
             .HasForeignKey(x => x.UsuarioId)
             .HasConstraintName("fk_movinv_usuario")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Unidad)
             .WithMany(u => u.Movimientos)
             .HasForeignKey(x => x.UnidadId)
             .HasConstraintName("fk_movinv_unidad")
             .OnDelete(DeleteBehavior.Restrict);
        });
    }

    // =========================================================================
    // COMPRAS
    // =========================================================================
    private static void ConfigurarCompras(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Proveedor>(e =>
        {
            e.ToTable("proveedores");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            e.Property(x => x.Contacto).HasColumnName("contacto").HasMaxLength(100);
            e.Property(x => x.Telefono).HasColumnName("telefono").HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<ProductoProveedor>(e =>
        {
            e.ToTable("producto_proveedor");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.ProductoId).HasColumnName("producto_id").IsRequired();
            e.Property(x => x.ProveedorId).HasColumnName("proveedor_id").IsRequired();
            e.Property(x => x.PrecioReferencia).HasColumnName("precio_referencia").HasPrecision(12, 2);

            e.HasIndex(x => new { x.ProductoId, x.ProveedorId })
             .IsUnique()
             .HasDatabaseName("uq_producto_proveedor");

            // Tabla de asociacion pura: si desaparece cualquiera de los dos
            // extremos, el vinculo deja de existir.
            e.HasOne(x => x.Producto)
             .WithMany(p => p.Proveedores)
             .HasForeignKey(x => x.ProductoId)
             .HasConstraintName("fk_prodprov_producto")
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Proveedor)
             .WithMany(p => p.Productos)
             .HasForeignKey(x => x.ProveedorId)
             .HasConstraintName("fk_prodprov_proveedor")
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrdenCompra>(e =>
        {
            e.ToTable("ordenes_compra");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.ProveedorId).HasColumnName("proveedor_id").IsRequired();
            e.Property(x => x.SucursalId).HasColumnName("sucursal_id").IsRequired();
            e.Property(x => x.Fecha).HasColumnName("fecha").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Estado).HasColumnName("estado").HasConversion<string>();
            e.Property(x => x.PlazoPagoDias).HasColumnName("plazo_pago_dias");

            e.HasIndex(x => new { x.SucursalId, x.Fecha }).HasDatabaseName("idx_oc_sucursal_fecha");
            e.HasIndex(x => x.Estado).HasDatabaseName("idx_oc_estado");

            e.HasOne(x => x.Proveedor)
             .WithMany(p => p.OrdenesCompra)
             .HasForeignKey(x => x.ProveedorId)
             .HasConstraintName("fk_oc_proveedor")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Sucursal)
             .WithMany(s => s.OrdenesCompra)
             .HasForeignKey(x => x.SucursalId)
             .HasConstraintName("fk_oc_sucursal")
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrdenCompraDetalle>(e =>
        {
            e.ToTable("orden_compra_detalle");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.OrdenCompraId).HasColumnName("orden_compra_id").IsRequired();
            e.Property(x => x.ProductoId).HasColumnName("producto_id").IsRequired();
            e.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(14, 4);
            e.Property(x => x.UnidadId).HasColumnName("unidad_id").IsRequired();
            e.Property(x => x.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 2);
            e.Property(x => x.Descuento).HasColumnName("descuento").HasPrecision(5, 2).HasDefaultValue(0m);

            // El detalle pertenece a su encabezado: se borra con el.
            e.HasOne(x => x.OrdenCompra)
             .WithMany(o => o.Detalles)
             .HasForeignKey(x => x.OrdenCompraId)
             .HasConstraintName("fk_ocd_orden")
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Producto)
             .WithMany(p => p.LineasCompra)
             .HasForeignKey(x => x.ProductoId)
             .HasConstraintName("fk_ocd_producto")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Unidad)
             .WithMany(u => u.LineasCompra)
             .HasForeignKey(x => x.UnidadId)
             .HasConstraintName("fk_ocd_unidad")
             .OnDelete(DeleteBehavior.Restrict);
        });
    }

    // =========================================================================
    // VENTAS
    // =========================================================================
    private static void ConfigurarVentas(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Cliente>(e =>
        {
            e.ToTable("clientes");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.RazonSocial).HasColumnName("razon_social").HasMaxLength(150).IsRequired();
            e.Property(x => x.TipoPersona).HasColumnName("tipo_persona").HasConversion<string>().IsRequired();
            e.Property(x => x.Documento).HasColumnName("documento").HasMaxLength(20).IsRequired();
            e.Property(x => x.Telefono).HasColumnName("telefono").HasMaxLength(20);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(100);
            e.Property(x => x.Direccion).HasColumnName("direccion").HasMaxLength(150);

            e.HasIndex(x => x.Documento).IsUnique().HasDatabaseName("uq_clientes_documento");
        });

        modelBuilder.Entity<Venta>(e =>
        {
            e.ToTable("ventas");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.ClienteId).HasColumnName("cliente_id").IsRequired();
            e.Property(x => x.SucursalId).HasColumnName("sucursal_id").IsRequired();
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
            e.Property(x => x.Fecha).HasColumnName("fecha").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Total).HasColumnName("total").HasPrecision(14, 2);

            e.HasIndex(x => new { x.SucursalId, x.Fecha }).HasDatabaseName("idx_ventas_sucursal_fecha");

            e.HasOne(x => x.Cliente)
             .WithMany(c => c.Ventas)
             .HasForeignKey(x => x.ClienteId)
             .HasConstraintName("fk_ventas_cliente")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Sucursal)
             .WithMany(s => s.Ventas)
             .HasForeignKey(x => x.SucursalId)
             .HasConstraintName("fk_ventas_sucursal")
             .OnDelete(DeleteBehavior.Restrict);

            // El vendedor queda identificado: un empleado que se va se
            // desactiva, no se borra.
            e.HasOne(x => x.Usuario)
             .WithMany(u => u.Ventas)
             .HasForeignKey(x => x.UsuarioId)
             .HasConstraintName("fk_ventas_usuario")
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<VentaDetalle>(e =>
        {
            e.ToTable("venta_detalle");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.VentaId).HasColumnName("venta_id").IsRequired();
            e.Property(x => x.ProductoId).HasColumnName("producto_id").IsRequired();
            // La base define esta columna con 2 decimales, no 4 como el resto.
            e.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(12, 2);
            e.Property(x => x.UnidadId).HasColumnName("unidad_id").IsRequired();
            e.Property(x => x.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 2);
            e.Property(x => x.Descuento).HasColumnName("descuento").HasPrecision(5, 2).HasDefaultValue(0m);

            e.HasOne(x => x.Venta)
             .WithMany(v => v.Detalles)
             .HasForeignKey(x => x.VentaId)
             .HasConstraintName("fk_vd_venta")
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Producto)
             .WithMany(p => p.LineasVenta)
             .HasForeignKey(x => x.ProductoId)
             .HasConstraintName("fk_vd_producto")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Unidad)
             .WithMany(u => u.LineasVenta)
             .HasForeignKey(x => x.UnidadId)
             .HasConstraintName("fk_vd_unidad")
             .OnDelete(DeleteBehavior.Restrict);
        });
    }

    // =========================================================================
    // TRANSFERENCIAS
    // =========================================================================
    private static void ConfigurarTransferencias(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transportadora>(e =>
        {
            e.ToTable("transportadoras");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            e.Property(x => x.TipoServicio)
             .HasColumnName("tipo_servicio")
             .HasConversion(TipoServicioConverter)
             .IsRequired();
        });

        modelBuilder.Entity<Transferencia>(e =>
        {
            e.ToTable("transferencias");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.ProductoId).HasColumnName("producto_id").IsRequired();
            e.Property(x => x.SucursalOrigenId).HasColumnName("sucursal_origen_id").IsRequired();
            e.Property(x => x.SucursalDestinoId).HasColumnName("sucursal_destino_id").IsRequired();
            e.Property(x => x.TransportadoraId).HasColumnName("transportadora_id").IsRequired();
            e.Property(x => x.CantidadSolicitada).HasColumnName("cantidad_solicitada").HasPrecision(14, 4);
            e.Property(x => x.CantidadRecibida).HasColumnName("cantidad_recibida").HasPrecision(14, 4);
            e.Property(x => x.UnidadId).HasColumnName("unidad_id").IsRequired();
            e.Property(x => x.Estado).HasColumnName("estado").HasConversion<string>();
            e.Property(x => x.Urgencia).HasColumnName("urgencia").HasConversion<string>();
            e.Property(x => x.FechaSolicitud).HasColumnName("fecha_solicitud").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.FechaEstimadaLlegada).HasColumnName("fecha_estimada_llegada");

            e.HasIndex(x => new { x.SucursalDestinoId, x.Estado }).HasDatabaseName("idx_transf_destino_estado");

            e.HasOne(x => x.Producto)
             .WithMany(p => p.Transferencias)
             .HasForeignKey(x => x.ProductoId)
             .HasConstraintName("fk_transf_producto")
             .OnDelete(DeleteBehavior.Restrict);

            // Dos FK a la MISMA tabla: cada una necesita su propia navegacion.
            e.HasOne(x => x.SucursalOrigen)
             .WithMany(s => s.TransferenciasEnviadas)
             .HasForeignKey(x => x.SucursalOrigenId)
             .HasConstraintName("fk_transf_sucursal_origen")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.SucursalDestino)
             .WithMany(s => s.TransferenciasRecibidas)
             .HasForeignKey(x => x.SucursalDestinoId)
             .HasConstraintName("fk_transf_sucursal_destino")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Transportadora)
             .WithMany(t => t.Transferencias)
             .HasForeignKey(x => x.TransportadoraId)
             .HasConstraintName("fk_transf_transportadora")
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Unidad)
             .WithMany(u => u.Transferencias)
             .HasForeignKey(x => x.UnidadId)
             .HasConstraintName("fk_transf_unidad")
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NovedadTransferencia>(e =>
        {
            e.ToTable("novedades_transferencia");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.TransferenciaId).HasColumnName("transferencia_id").IsRequired();
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
            e.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<string>();
            e.Property(x => x.CantidadAfectada).HasColumnName("cantidad_afectada").HasPrecision(14, 4);
            e.Property(x => x.Observaciones).HasColumnName("observaciones").HasColumnType("text");
            e.Property(x => x.Fecha).HasColumnName("fecha").HasDefaultValueSql("CURRENT_TIMESTAMP");

            // La novedad pertenece al traslado, pero conserva a su reportante.
            e.HasOne(x => x.Transferencia)
             .WithMany(t => t.Novedades)
             .HasForeignKey(x => x.TransferenciaId)
             .HasConstraintName("fk_novtransf_transferencia")
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Usuario)
             .WithMany(u => u.NovedadesReportadas)
             .HasForeignKey(x => x.UsuarioId)
             .HasConstraintName("fk_novtransf_usuario")
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
