namespace Colorsin.Domain.Transferencias;

/// <summary>Empresa de transporte usada para despachos entre sedes y a clientes.</summary>
public class Transportadora
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public TipoServicio TipoServicio { get; set; }

    /// <summary>
    /// Dias que tarda en entregar. Es lo que convierte la etiqueta del servicio
    /// en una fecha.
    /// </summary>
    /// <remarks>
    /// VA POR TRANSPORTADORA Y NO POR <see cref="TipoServicio"/> porque 'urgente'
    /// y 'estandar' son etiquetas comerciales, no plazos: dos empresas urgentes
    /// pueden tardar 1 y 2 dias. Con la columna, el plazo se corrige con un
    /// UPDATE y no recompilando.
    ///
    /// Siempre mayor que cero; lo refuerza el CHECK
    /// <c>chk_transportadoras_dias_entrega</c>. Un traslado que llega el mismo
    /// dia que sale no necesita transportadora.
    /// </remarks>
    public byte DiasEntrega { get; set; }

    /// <summary>
    /// <c>false</c> si esta retirada: deja de ofrecerse al despachar y conserva
    /// su historia.
    /// </summary>
    /// <remarks>
    /// Baja LOGICA y no borrado, por la misma razon que en proveedores:
    /// <c>transferencias.transportadora_id</c> la referencia, y borrarla de
    /// verdad fallaria justo con las que llevan anos de traslados. Ese dato es
    /// el que hace falta el dia que se reclama un faltante.
    ///
    /// Es reversible: se vuelve a contratar a una empresa que se habia dejado de
    /// usar, y reactivarla conserva su historia en vez de crear un duplicado.
    /// </remarks>
    public bool Activo { get; set; } = true;

    // --- Navegacion ---
    public ICollection<Transferencia> Transferencias { get; set; } = new List<Transferencia>();
}
