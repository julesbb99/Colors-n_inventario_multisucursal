import { useState } from 'react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { aNumero, aTexto, CampoNumero, CampoSelect } from '../ui/Campos';
import { CampoSede } from '../ui/CampoSede';
import { useSede } from '../../hooks/useSede';
import { useCatalogos } from '../../hooks/useCatalogos';
import { useEnvio } from '../../hooks/useEnvio';
import { actualizarExistencia, crearExistencia } from '../../services/inventario';
import type { ExistenciaDto } from '../../models/inventario';

interface FormularioExistenciaProps {
  /**
   * La fila que se edita, o `null` para dar de alta una nueva.
   *
   * Un solo componente para los dos casos porque comparten el único campo que
   * de verdad se digita -el mínimo- y separarlos habría duplicado el formulario
   * entero para cambiar dos rótulos.
   */
  existencia: ExistenciaDto | null;
  /**
   * Sede con la que arranca el formulario en un alta.
   *
   * La manda la pantalla y no se lee del contexto porque Existencias tiene su
   * propio filtro de sede: si el listado está puesto en Manizales, el alta
   * empieza ahí. Nula significa "sin sugerencia", y entonces hay que elegirla.
   */
  sedeSugerida?: number | null;
  onCerrar: () => void;
  onGuardado: () => void;
}

/**
 * Alta y edición de una existencia.
 *
 * NO PIDE CANTIDAD NI COSTO, y no es un olvido: el saldo solo se mueve por el
 * libro mayor -un ingreso, una recepción de compra, un traslado- porque cada una
 * de esas vías deja un asiento que explica de dónde salió la mercancía. Digitar
 * aquí un saldo inicial sería la única forma de cambiar el inventario sin
 * rastro. El costo promedio lo recalcula cada entrada.
 *
 * La sede va bloqueada para gerentes y operadores, igual que en el resto de
 * formularios: la API responde 403 a cualquier otra.
 */
export function FormularioExistencia({
  existencia,
  sedeSugerida = null,
  onCerrar,
  onGuardado,
}: FormularioExistenciaProps) {
  const { sedeActiva } = useSede();
  const { opcionesProducto } = useCatalogos();
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const esEdicion = existencia !== null;

  // Para un gerente u operador `sedeActiva` ES su sede, así que sirve de
  // respaldo cuando la pantalla no sugiere ninguna.
  const inicial = sedeSugerida ?? sedeActiva;

  const [productoId, setProductoId] = useState(
    existencia ? String(existencia.productoId) : '',
  );
  const [sucursalId, setSucursalId] = useState(
    existencia ? String(existencia.sucursalId) : inicial === null ? '' : String(inicial),
  );
  const [stockMinimo, setStockMinimo] = useState(existencia ? aTexto(existencia.stockMinimo) : '0');
  const [validacion, setValidacion] = useState<string | null>(null);

  function alGuardar() {
    setValidacion(null);

    const minimo = aNumero(stockMinimo);
    if (minimo === null || minimo < 0) {
      setValidacion('El mínimo de reposición tiene que ser un número de cero en adelante.');
      return;
    }

    if (!esEdicion) {
      if (!productoId) {
        setValidacion('Elige el producto que se va a manejar en la sede.');
        return;
      }
      if (!sucursalId) {
        setValidacion('Elige la sede.');
        return;
      }
    }

    void enviar(async () => {
      if (existencia) {
        await actualizarExistencia(existencia.id, { stockMinimo: minimo });
      } else {
        await crearExistencia({
          sucursalId: Number(sucursalId),
          productoId: Number(productoId),
          stockMinimo: minimo,
        });
      }
      onGuardado();
      onCerrar();
    });
  }

  return (
    <Modal
      titulo={esEdicion ? 'Editar existencia' : 'Habilitar producto en una sede'}
      descripcion={
        esEdicion
          ? `${existencia.productoNombre} — ${existencia.sucursalNombre}. Solo se cambia el mínimo de reposición.`
          : 'La existencia nace con saldo cero; la mercancía entra después con un ingreso, una compra o un traslado.'
      }
      ocupado={enviando}
      onCerrar={onCerrar}
      pie={
        <>
          <Boton variante="secundaria" onClick={onCerrar} disabled={enviando}>
            Cancelar
          </Boton>
          <Boton onClick={alGuardar} disabled={enviando}>
            {enviando ? (
              <>
                <Spinner etiqueta="Guardando" />
                Guardando…
              </>
            ) : esEdicion ? (
              'Guardar cambios'
            ) : (
              'Habilitar producto'
            )}
          </Boton>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {validacion ? <Alerta tipo="info">{validacion}</Alerta> : null}

        {esEdicion ? null : (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <CampoSelect
              etiqueta="Producto"
              valor={productoId}
              onCambio={setProductoId}
              opciones={opcionesProducto}
              requerido
              disabled={enviando}
              placeholder="Selecciona un producto"
              ayuda="Del catálogo de la red."
            />
            <CampoSede valor={sucursalId} onCambio={setSucursalId} disabled={enviando} />
          </div>
        )}

        <CampoNumero
          etiqueta="Mínimo de reposición"
          valor={stockMinimo}
          onCambio={setStockMinimo}
          disabled={enviando}
          ayuda={
            'En la unidad base del producto. La alerta salta cuando el saldo baja de aquí; ' +
            'con cero solo avisa al agotarse.'
          }
        />

        {esEdicion ? null : (
          <Alerta tipo="info">
            Si esa sede ya manejaba el producto y lo tenía deshabilitado, esto lo vuelve a
            habilitar con el saldo que tenía.
          </Alerta>
        )}
      </div>
    </Modal>
  );
}
