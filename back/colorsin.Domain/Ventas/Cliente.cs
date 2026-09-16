namespace Colorsin.Domain.Ventas;

/// <summary>Cliente de Colorsin, persona natural o juridica.</summary>
public class Cliente
{
    public int Id { get; set; }
    public string RazonSocial { get; set; } = null!;
    public TipoPersona TipoPersona { get; set; }

    /// <summary>Cedula o NIT. Unico en toda la base.</summary>
    public string Documento { get; set; } = null!;

    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }

    // --- Navegacion ---
    public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
}
