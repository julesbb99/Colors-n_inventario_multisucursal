namespace Colorsin.Domain.Transferencias;

/// <summary>Empresa de transporte usada para despachos entre sedes y a clientes.</summary>
public class Transportadora
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public TipoServicio TipoServicio { get; set; }

    // --- Navegacion ---
    public ICollection<Transferencia> Transferencias { get; set; } = new List<Transferencia>();
}
