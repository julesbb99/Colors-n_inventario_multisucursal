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
///
/// INDICES: todos llevan HasDatabaseName() explicito, incluidos los de clave
/// foranea que EF crearia solo. No es cosmetico. EF los nombra
/// IX_&lt;tabla&gt;_&lt;columna&gt;, pero el esquema real se creo con los scripts de
/// infra/mysql/init, que usan el prefijo idx_ y nombres abreviados
/// (idx_movinv_producto, idx_ocd_orden...). Mientras el modelo tuvo los nombres
/// de EF y la base los otros, cada migracion que tocaba un indice generaba un
/// DROP de algo inexistente, que falla con el error 1091 y deja la migracion a
/// medias. Al declararlos, el modelo describe el esquema que de verdad hay.
///
/// Van declarados tambien los indices que no salen de ninguna relacion
/// (idx_movinv_fecha, idx_ventas_fecha, idx_transf_estado...): EF no los
/// deduce, y sin declararlos el modelo describiria solo una parte del esquema.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // --- Comun ---
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<EventoAuditoria> EventosAuditoria => Set<EventoAuditoria>();

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
    // Tipos ENUM fisicos
    //
    // EF Core no tiene concepto de ENUM: HasConversion<string>() por si solo
    // produce longtext (o varchar(255) si la columna esta indexada). Declarar
    // el tipo explicito mantiene el modelo fiel al esquema real y conserva la
    // validacion que da el propio ENUM de MySQL.
    //
    // El orden de los valores debe coincidir con el del DDL: MySQL guarda el
    // indice ordinal, no el texto.
    // =========================================================================
    private const string EnumRolRed = "enum('Matriz','Sucursal')";
    private const string EnumRolUsuario = "enum('Administrador General','Gerente de Sucursal','Operador')";
    private const string EnumTipoPersona = "enum('Natural','Juridica')";
    private const string EnumTipoServicio = "enum('urgente','estandar')";
    // Esta lista debe coincidir valor por valor, y en el mismo orden, con el
    // enum EstadoOrdenCompra del dominio: si se separan, guardar un estado que
    // falte aqui falla con el error 1265 de MySQL ("Data truncated for column
    // 'estado'").
    private const string EnumEstadoOrdenCompra =
        "enum('Pendiente','Confirmada','ParcialmenteRecibida','Recibida','Cancelada')";
    // Debe coincidir valor por valor, y en el mismo orden, con el enum
    // EstadoTransferencia del dominio.
    private const string EnumEstadoTransferencia =
        "enum('Solicitada','EnTransito','Completada','RecibidaParcial'," +
        "'Cerrada','Rechazada','Cancelada')";
    private const string EnumUrgencia = "enum('Baja','Media','Alta')";
    private const string EnumTipoNovedad = "enum('Faltante','Averia','Sobrante','Retraso')";
    private const string EnumTipoMovimiento = "enum('Ingreso','Retiro')";
    private const string EnumMotivoMovimiento =
        "enum('Compra','Venta','Ajuste','Transferencia','Merma','Devolucion')";

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
            e.Property(x => x.RolRed).HasColumnName("rol_red")
             .HasConversion<string>().HasColumnType(EnumRolRed).IsRequired();
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
            e.Property(x => x.Rol).HasColumnName("rol")
             .HasConversion(RolUsuarioConverter).HasColumnType(EnumRolUsuario).IsRequired();
            e.Property(x => x.SucursalId).HasColumnName("sucursal_id");

            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("uq_usuarios_email");

            // Nombre explicito del indice de la clave foranea. EF lo crearia
            // igual, pero llamandolo IX_usuarios_sucursal_id, mientras que la
            // base -construida con los scripts de infra/mysql/init- lo tiene
            // como idx_usuarios_sucursal. Ver la nota de INDICES arriba.
            e.HasIndex(x => x.SucursalId).HasDatabaseName("idx_usuarios_sucursal");

            // Si se elimina una sucursal, sus usuarios quedan sin sede (SET NULL),
            // no se borran: el Administrador General ya vive sin sede.
            e.HasOne(x => x.Sucursal)
             .WithMany(s => s.Usuarios)
             .HasForeignKey(x => x.SucursalId)
             .HasConstraintName("fk_usuarios_sucursal")
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EventoAuditoria>(e =>
        {
            e.ToTable("auditoria_eventos");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Modulo).HasColumnName("modulo").HasMaxLength(50).IsRequired();
            e.Property(x => x.Accion).HasColumnName("accion").HasMaxLength(80).IsRequired();
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
            e.Property(x => x.Detalle).HasColumnName("detalle").HasMaxLength(1000);

            // ValueGeneratedOnAdd para que EF omita la columna en el INSERT y la
            // llene CURRENT_TIMESTAMP. Sin eso mandaria el valor por defecto de
            // DateTime (0001-01-01), que MySQL rechaza.
            e.Property(x => x.Fecha).HasColumnName("fecha")
             .HasColumnType("datetime")
             .HasDefaultValueSql("CURRENT_TIMESTAMP")
             .ValueGeneratedOnAdd();

            // Consulta tipica: que paso en tal modulo entre tales fechas.
            e.HasIndex(x => new { x.Modulo, x.Fecha }).HasDatabaseName("idx_auditoria_modulo_fecha");
            e.HasIndex(x => x.UsuarioId).HasDatabaseName("idx_auditoria_usuario");

            // Restrict, como el resto de la auditoria: borrar un usuario no
            // puede llevarse por delante el rastro de lo que hizo.
            e.HasOne(x => x.Usuario)
             .WithMany()
             .HasForeignKey(x => x.UsuarioId)
             .HasConstraintName("fk_auditoria_usuario")
             .OnDelete(DeleteBehavior.Restrict);
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
            // El precio nulo es "sin fijar"; el cero solo puede ser un error de
            // digitacion, y colarlo haria que la pantalla rellenara las ventas
            // con cero pesos.
            e.ToTable("productos", t => t.HasCheckConstraint(
                "chk_productos_precio_venta",
                "`precio_venta` IS NULL OR `precio_venta` > 0"));
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            e.Property(x => x.Categoria).HasColumnName("categoria").HasMaxLength(50);
            e.Property(x => x.Descripcion).HasColumnName("descripcion").HasColumnType("text");
            e.Property(x => x.UnidadBaseId).HasColumnName("unidad_base_id");
            e.Property(x => x.PrecioVenta).HasColumnName("precio_venta").HasPrecision(12, 2);

            e.HasIndex(x => x.UnidadBaseId).HasDatabaseName("idx_productos_unidad_base");

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
            e.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);

            // Indice de la migracion 12: el listado filtra por sede y por activas.
            e.HasIndex(x => new { x.SucursalId, x.Activo })
                .HasDatabaseName("idx_inventario_sucursal_activo");

            // Un solo saldo por pareja (sede, producto).
            e.HasIndex(x => new { x.SucursalId, x.ProductoId })
             .IsUnique()
             .HasDatabaseName("uq_inventario_sucursal_producto");

            e.HasIndex(x => x.ProductoId).HasDatabaseName("idx_inventario_producto");

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
            // datetime sin precision fraccionaria: Pomelo mapearia DateTime a
            // datetime(6), y MySQL rechaza DEFAULT CURRENT_TIMESTAMP sobre esa
            // precision. La base fisica usa datetime a secas.
            e.Property(x => x.FechaIngreso).HasColumnName("fecha_ingreso")
             .HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasIndex(x => x.FechaVencimiento).HasDatabaseName("idx_lotes_vencimiento");
            e.HasIndex(x => x.NumeroLote).HasDatabaseName("idx_lotes_numero");
            e.HasIndex(x => x.SucursalId).HasDatabaseName("idx_lotes_sucursal");

            // Compuesto (producto, sucursal): es el filtro de la consulta FEFO.
            // Su columna inicial cubre ademas la clave foranea de producto_id,
            // asi que EF no crea un indice aparte para ella. La base ya lo
            // tenia; sin declararlo aqui, el modelo y el esquema no coinciden.
            e.HasIndex(x => new { x.ProductoId, x.SucursalId })
             .HasDatabaseName("idx_lotes_producto_sucursal");

            // Un numero de lote del fabricante identifica UN lote dentro de una
            // sede: si vuelve a llegar, se le suma cantidad, no se crea otra
            // fila. Sin este indice la regla la aplicaba solo el codigo, y dos
            // recepciones simultaneas del mismo lote no se ven entre si (ambas
            // consultan antes de que la otra inserte) y terminan duplicando la
            // fila, lo que rompe el orden FEFO y el cuadre por lote.
            //
            // El motor no tiene ese punto ciego: la segunda inserta y falla con
            // el error 1062, y su transaccion se revierte entera.
            e.HasIndex(x => new { x.ProductoId, x.SucursalId, x.NumeroLote })
             .IsUnique()
             .HasDatabaseName("ux_lotes_producto_sucursal_numero");

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
            e.ToTable("movimientos_inventario", t =>
            {
                // La cantidad se guarda siempre positiva: el signo lo da `tipo`.
                t.HasCheckConstraint("chk_movinv_cantidad", "`cantidad` IS NULL OR `cantidad` > 0");
                t.HasCheckConstraint("chk_movinv_cantidad_base", "`cantidad_base` IS NULL OR `cantidad_base` > 0");
            });
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.SucursalId).HasColumnName("sucursal_id").IsRequired();
            e.Property(x => x.ProductoId).HasColumnName("producto_id").IsRequired();
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
            e.Property(x => x.Tipo).HasColumnName("tipo")
             .HasConversion<string>().HasColumnType(EnumTipoMovimiento);
            e.Property(x => x.Motivo).HasColumnName("motivo")
             .HasConversion<string>().HasColumnType(EnumMotivoMovimiento);
            e.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(14, 4);
            e.Property(x => x.UnidadId).HasColumnName("unidad_id").IsRequired();
            e.Property(x => x.CantidadBase).HasColumnName("cantidad_base").HasPrecision(14, 4);
            e.Property(x => x.LoteId).HasColumnName("lote_id");
            e.Property(x => x.TransferenciaId).HasColumnName("transferencia_id");
            e.Property(x => x.Observaciones).HasColumnName("observaciones").HasMaxLength(255);
            e.Property(x => x.Fecha).HasColumnName("fecha")
             .HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP")
             .ValueGeneratedOnAdd();

            // Indice principal: reconstruir el saldo de un producto en una sede.
            e.HasIndex(x => new { x.SucursalId, x.ProductoId, x.Fecha })
             .HasDatabaseName("idx_movinv_sucursal_producto_fecha");

            // Nombrado a mano: si no, EF lo llamaria
            // IX_movimientos_inventario_lote_id y desentonaria con los idx_ del
            // resto del esquema.
            e.HasIndex(x => x.LoteId).HasDatabaseName("idx_movinv_lote");
            // La recepcion de un traslado consulta por aqui los movimientos que
            // genero su despacho, para saber de que lotes salio la mercancia.
            e.HasIndex(x => x.TransferenciaId).HasDatabaseName("idx_movinv_transferencia");
            e.HasIndex(x => x.ProductoId).HasDatabaseName("idx_movinv_producto");
            e.HasIndex(x => x.UsuarioId).HasDatabaseName("idx_movinv_usuario");
            e.HasIndex(x => x.UnidadId).HasDatabaseName("idx_movinv_unidad");

            // Estos dos no vienen de ninguna clave foranea: estaban en el DDL
            // para las consultas por rango de fechas y por motivo del
            // movimiento. Se declaran para que el modelo describa el esquema
            // completo y no solo la parte que EF deduce de las relaciones.
            e.HasIndex(x => x.Fecha).HasDatabaseName("idx_movinv_fecha");
            e.HasIndex(x => x.Motivo).HasDatabaseName("idx_movinv_motivo");

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

            // Opcional: no todo movimiento se imputa a un lote. Sin coleccion
            // inversa en Lote, que no hace falta para ninguna consulta actual.
            e.HasOne(x => x.Lote)
             .WithMany()
             .HasForeignKey(x => x.LoteId)
             .HasConstraintName("fk_movinv_lote")
             .OnDelete(DeleteBehavior.Restrict);

            // Restrict, como el resto del libro mayor: borrar un traslado no
            // puede llevarse por delante los movimientos que genero.
            e.HasOne(x => x.Transferencia)
             .WithMany()
             .HasForeignKey(x => x.TransferenciaId)
             .HasConstraintName("fk_movinv_transferencia")
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
            e.Property(x => x.Activo).HasColumnName("activo").HasDefaultValue(true);
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

            e.HasIndex(x => x.ProveedorId).HasDatabaseName("idx_prodprov_proveedor");

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
            e.ToTable("ordenes_compra", t => t.HasCheckConstraint(
                "chk_oc_plazo_pago",
                "`plazo_pago_dias` IS NULL OR `plazo_pago_dias` >= 0"));
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.ProveedorId).HasColumnName("proveedor_id").IsRequired();
            e.Property(x => x.SucursalId).HasColumnName("sucursal_id").IsRequired();
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
            e.Property(x => x.Fecha).HasColumnName("fecha")
             .HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP")
             .ValueGeneratedOnAdd();
            e.Property(x => x.Estado).HasColumnName("estado")
             .HasConversion<string>().HasColumnType(EnumEstadoOrdenCompra);
            e.Property(x => x.PlazoPagoDias).HasColumnName("plazo_pago_dias");

            e.HasIndex(x => new { x.SucursalId, x.Fecha }).HasDatabaseName("idx_oc_sucursal_fecha");
            e.HasIndex(x => x.Estado).HasDatabaseName("idx_oc_estado");
            e.HasIndex(x => x.UsuarioId).HasDatabaseName("idx_oc_usuario");
            e.HasIndex(x => x.ProveedorId).HasDatabaseName("idx_oc_proveedor");

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

            // Restrict: un usuario con ordenes a su nombre no se puede borrar.
            // Es el mismo criterio del libro mayor y de la auditoria; dejar
            // huerfana una orden de compra borraria el rastro de quien la pidio.
            // Sin coleccion inversa en Usuario: ninguna consulta la necesita.
            e.HasOne(x => x.Usuario)
             .WithMany()
             .HasForeignKey(x => x.UsuarioId)
             .HasConstraintName("fk_oc_usuario")
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrdenCompraDetalle>(e =>
        {
            e.ToTable("orden_compra_detalle", t =>
            {
                t.HasCheckConstraint("chk_ocd_cantidad", "`cantidad` IS NULL OR `cantidad` > 0");
                t.HasCheckConstraint("chk_ocd_precio", "`precio_unitario` IS NULL OR `precio_unitario` >= 0");
                t.HasCheckConstraint("chk_ocd_descuento", "`descuento` >= 0 AND `descuento` <= 100");

                // La invariante de la recepcion parcial, impuesta por el motor:
                // lo recibido nunca es negativo ni supera lo pedido. El servicio
                // ya lo valida, pero eso solo cubre lo que pasa por el servicio;
                // esto cubre tambien un UPDATE hecho a mano.
                t.HasCheckConstraint(
                    "chk_ocd_cantidad_recibida",
                    "`cantidad_recibida` >= 0 AND " +
                    "(`cantidad` IS NULL OR `cantidad_recibida` <= `cantidad`)");
            });
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.OrdenCompraId).HasColumnName("orden_compra_id").IsRequired();
            e.Property(x => x.ProductoId).HasColumnName("producto_id").IsRequired();
            e.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(14, 4);

            // Misma escala que `cantidad`, con la que se compara en cada
            // recepcion: (14,4) las dos. Con escalas distintas habria un rango
            // de cantidades pedidas que no se podrian completar nunca.
            e.Property(x => x.CantidadRecibida).HasColumnName("cantidad_recibida")
             .HasPrecision(14, 4).HasDefaultValue(0m);

            e.Property(x => x.UnidadId).HasColumnName("unidad_id").IsRequired();
            e.Property(x => x.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 2);
            e.Property(x => x.Descuento).HasColumnName("descuento").HasPrecision(5, 2).HasDefaultValue(0m);

            e.HasIndex(x => x.OrdenCompraId).HasDatabaseName("idx_ocd_orden");
            e.HasIndex(x => x.ProductoId).HasDatabaseName("idx_ocd_producto");
            e.HasIndex(x => x.UnidadId).HasDatabaseName("idx_ocd_unidad");

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
            e.Property(x => x.TipoPersona).HasColumnName("tipo_persona")
             .HasConversion<string>().HasColumnType(EnumTipoPersona).IsRequired();
            e.Property(x => x.Documento).HasColumnName("documento").HasMaxLength(20).IsRequired();
            e.Property(x => x.Telefono).HasColumnName("telefono").HasMaxLength(20);
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(100);
            e.Property(x => x.Direccion).HasColumnName("direccion").HasMaxLength(150);

            e.HasIndex(x => x.Documento).IsUnique().HasDatabaseName("uq_clientes_documento");
        });

        modelBuilder.Entity<Venta>(e =>
        {
            // Un total negativo indicaria una devolucion, que va por otro documento.
            e.ToTable("ventas", t => t.HasCheckConstraint(
                "chk_ventas_total",
                "`total` IS NULL OR `total` >= 0"));
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.ClienteId).HasColumnName("cliente_id").IsRequired();
            e.Property(x => x.SucursalId).HasColumnName("sucursal_id").IsRequired();
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
            e.Property(x => x.Fecha).HasColumnName("fecha")
             .HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.Total).HasColumnName("total").HasPrecision(14, 2);

            e.HasIndex(x => new { x.SucursalId, x.Fecha }).HasDatabaseName("idx_ventas_sucursal_fecha");
            e.HasIndex(x => x.ClienteId).HasDatabaseName("idx_ventas_cliente");
            e.HasIndex(x => x.UsuarioId).HasDatabaseName("idx_ventas_usuario");
            e.HasIndex(x => x.Fecha).HasDatabaseName("idx_ventas_fecha");

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
            e.ToTable("venta_detalle", t =>
            {
                t.HasCheckConstraint("chk_vd_cantidad", "`cantidad` IS NULL OR `cantidad` > 0");
                t.HasCheckConstraint("chk_vd_precio", "`precio_unitario` IS NULL OR `precio_unitario` >= 0");
                t.HasCheckConstraint("chk_vd_descuento", "`descuento` >= 0 AND `descuento` <= 100");
            });
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.VentaId).HasColumnName("venta_id").IsRequired();
            e.Property(x => x.ProductoId).HasColumnName("producto_id").IsRequired();
            // La base define esta columna con 2 decimales, no 4 como el resto.
            e.Property(x => x.Cantidad).HasColumnName("cantidad").HasPrecision(12, 2);
            e.Property(x => x.UnidadId).HasColumnName("unidad_id").IsRequired();
            e.Property(x => x.PrecioUnitario).HasColumnName("precio_unitario").HasPrecision(12, 2);
            e.Property(x => x.Descuento).HasColumnName("descuento").HasPrecision(5, 2).HasDefaultValue(0m);

            e.HasIndex(x => x.VentaId).HasDatabaseName("idx_vd_venta");
            e.HasIndex(x => x.ProductoId).HasDatabaseName("idx_vd_producto");
            e.HasIndex(x => x.UnidadId).HasDatabaseName("idx_vd_unidad");

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
            // Un plazo de cero dias no describe a nadie que necesite camion.
            e.ToTable("transportadoras", t => t.HasCheckConstraint(
                "chk_transportadoras_dias_entrega",
                "`dias_entrega` >= 1"));
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.Nombre).HasColumnName("nombre").HasMaxLength(100).IsRequired();
            e.Property(x => x.TipoServicio)
             .HasColumnName("tipo_servicio")
             .HasConversion(TipoServicioConverter)
             .HasColumnType(EnumTipoServicio)
             .IsRequired();
            e.Property(x => x.DiasEntrega)
             .HasColumnName("dias_entrega")
             .HasColumnType("tinyint unsigned")
             .IsRequired();
            e.Property(x => x.Activo).HasColumnName("activo").IsRequired();
        });

        modelBuilder.Entity<Transferencia>(e =>
        {
            e.ToTable("transferencias", t =>
            {
                // Una sede no se transfiere mercancia a si misma.
                t.HasCheckConstraint("chk_transf_sedes_distintas",
                    "`sucursal_origen_id` <> `sucursal_destino_id`");
                t.HasCheckConstraint("chk_transf_cantidades",
                    "(`cantidad_solicitada` IS NULL OR `cantidad_solicitada` > 0) " +
                    "AND (`cantidad_recibida` IS NULL OR `cantidad_recibida` >= 0)");
            });
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.ProductoId).HasColumnName("producto_id").IsRequired();
            e.Property(x => x.SucursalOrigenId).HasColumnName("sucursal_origen_id").IsRequired();
            e.Property(x => x.SucursalDestinoId).HasColumnName("sucursal_destino_id").IsRequired();
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
            // Nullable: al solicitar el traslado todavia no se sabe quien lo va
            // a mover. Se asigna al despachar, junto con la guia.
            e.Property(x => x.TransportadoraId).HasColumnName("transportadora_id");
            e.Property(x => x.Guia).HasColumnName("guia").HasMaxLength(50);
            e.Property(x => x.CantidadSolicitada).HasColumnName("cantidad_solicitada").HasPrecision(14, 4);
            e.Property(x => x.CantidadRecibida).HasColumnName("cantidad_recibida").HasPrecision(14, 4);
            e.Property(x => x.UnidadId).HasColumnName("unidad_id").IsRequired();
            e.Property(x => x.Estado).HasColumnName("estado")
             .HasConversion<string>().HasColumnType(EnumEstadoTransferencia);
            e.Property(x => x.Urgencia).HasColumnName("urgencia")
             .HasConversion<string>().HasColumnType(EnumUrgencia);
            e.Property(x => x.FechaSolicitud).HasColumnName("fecha_solicitud")
             .HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");
            e.Property(x => x.FechaEstimadaLlegada).HasColumnName("fecha_estimada_llegada")
             .HasColumnType("datetime");

            e.HasIndex(x => new { x.SucursalDestinoId, x.Estado }).HasDatabaseName("idx_transf_destino_estado");
            e.HasIndex(x => x.ProductoId).HasDatabaseName("idx_transf_producto");
            e.HasIndex(x => x.SucursalOrigenId).HasDatabaseName("idx_transf_origen");
            e.HasIndex(x => x.TransportadoraId).HasDatabaseName("idx_transf_transportadora");
            e.HasIndex(x => x.UnidadId).HasDatabaseName("idx_transf_unidad");
            e.HasIndex(x => x.Estado).HasDatabaseName("idx_transf_estado");
            e.HasIndex(x => x.UsuarioId).HasDatabaseName("idx_transf_usuario");

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

            // Restrict, como en ordenes_compra y en el libro mayor: un usuario
            // con traslados a su nombre no se puede borrar, o se perderia el
            // rastro de quien pidio que. Sin coleccion inversa en Usuario:
            // ninguna consulta la necesita.
            e.HasOne(x => x.Usuario)
             .WithMany()
             .HasForeignKey(x => x.UsuarioId)
             .HasConstraintName("fk_transf_usuario")
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NovedadTransferencia>(e =>
        {
            e.ToTable("novedades_transferencia", t => t.HasCheckConstraint(
                "chk_novtransf_cantidad",
                "`cantidad_afectada` IS NULL OR `cantidad_afectada` >= 0"));
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.TransferenciaId).HasColumnName("transferencia_id").IsRequired();
            e.Property(x => x.UsuarioId).HasColumnName("usuario_id").IsRequired();
            e.Property(x => x.Tipo).HasColumnName("tipo")
             .HasConversion<string>().HasColumnType(EnumTipoNovedad);
            e.Property(x => x.CantidadAfectada).HasColumnName("cantidad_afectada").HasPrecision(14, 4);
            e.Property(x => x.Observaciones).HasColumnName("observaciones").HasColumnType("text");
            e.Property(x => x.Fecha).HasColumnName("fecha")
             .HasColumnType("datetime").HasDefaultValueSql("CURRENT_TIMESTAMP");

            e.HasIndex(x => x.TransferenciaId).HasDatabaseName("idx_novtransf_transferencia");
            e.HasIndex(x => x.UsuarioId).HasDatabaseName("idx_novtransf_usuario");
            e.HasIndex(x => x.Tipo).HasDatabaseName("idx_novtransf_tipo");

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
