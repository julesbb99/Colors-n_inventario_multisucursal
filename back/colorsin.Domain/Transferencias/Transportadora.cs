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

    // --- Navegacion ---
    public ICollection<Transferencia> Transferencias { get; set; } = new List<Transferencia>();
}
