using Colorsin.Domain.Transferencias;

namespace Colorsin.Application.Transferencias.Repositories;

/// <summary>Acceso al catalogo de transportadoras.</summary>
public interface ITransportadoraRepository
{
    /// <summary>
    /// Transportadoras del catalogo, ordenadas por nombre.
    /// </summary>
    /// <param name="incluirRetiradas">
    /// <c>false</c> por defecto: las retiradas no salen, que es lo que quiere el
    /// selector del despacho. Se pone en <c>true</c> solo donde hay que verlas a
    /// proposito, que hoy es la pantalla que las administra -sin ella la baja
    /// seria irreversible desde la interfaz-.
    /// </param>
    Task<IReadOnlyList<Transportadora>> ObtenerTodasAsync(
        bool incluirRetiradas = false,
        CancellationToken cancellationToken = default);

    /// <summary>La transportadora con ese id, o <c>null</c> si no existe.</summary>
    Task<Transportadora?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Si existe una transportadora con ese id, sin traer la fila. Se usa al
    /// despachar, donde solo hace falta saber si esta o no.
    /// </summary>
    Task<bool> ExisteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// La transportadora con ese id CON SEGUIMIENTO, para poder modificarla.
    ///
    /// Va aparte de <see cref="ObtenerPorIdAsync"/>, que usa <c>AsNoTracking</c>:
    /// sobre una entidad sin seguimiento, cambiar una propiedad y llamar a
    /// <c>SaveChanges</c> no guarda nada y tampoco avisa.
    /// </summary>
    Task<Transportadora?> ObtenerParaActualizarAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Si ya hay una transportadora con ese nombre, ignorando mayusculas y
    /// espacios de los extremos. <paramref name="exceptoId"/> permite excluir la
    /// que se esta editando, que si puede conservar su propio nombre.
    ///
    /// Evita el catalogo con "Redetrans" y "redetrans " como dos empresas
    /// distintas, que es como una lista corta se vuelve inservible.
    /// </summary>
    Task<bool> ExisteNombreAsync(
        string nombre,
        int? exceptoId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Deja preparada el alta. No guarda.</summary>
    void Agregar(Transportadora transportadora);

    /// <summary>Confirma en la base lo preparado. Devuelve las filas afectadas.</summary>
    Task<int> GuardarCambiosAsync(CancellationToken cancellationToken = default);
}
