import { useState } from 'react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { CampoArea, CampoNumero, CampoSelect } from '../ui/Campos';
import { CampoSede } from '../ui/CampoSede';
import { useSede } from '../../hooks/useSede';
import { useCatalogos } from '../../hooks/useCatalogos';
import { useEnvio } from '../../hooks/useEnvio';
import { registrarMovimiento } from '../../services/inventario';
import { MOTIVO_MOVIMIENTO, TIPO_MOVIMIENTO } from '../../models/inventario';
import type { ValorMotivoMovimiento, ValorTipoMovimiento } from '../../models/inventario';

/**
 * Compra, venta y transferencia NO se ofrecen aquí.
 *
 * Esos tres motivos los escribe el sistema cuando se recibe una compra, se
 * registra una venta o se despacha un traslado. Ofrecerlos en el formulario
 * manual permitiría inventar una entrada "por compra" sin orden que la respalde,
 * que es justo el rastro que el movimiento manual debería dejar visible.
 */
const MOTIVOS_MANUALES = [
  { valor: String(MOTIVO_MOVIMIENTO.ajuste), texto: 'Ajuste de inventario' },
  { valor: String(MOTIVO_MOVIMIENTO.merma), texto: 'Merma' },
  { valor: String(MOTIVO_MOVIMIENTO.devolucion), texto: 'Devolución' },
];

interface FormularioMovimientoProps {
  onCerrar: () => void;
  onRegistrado: () => void;
}

export function FormularioMovimiento({ onCerrar, onRegistrado }: FormularioMovimientoProps) {
  const { sedeActiva } = useSede();
  const { opcionesProducto, opcionesUnidad, unidadBaseDe } = useCatalogos();
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const [sucursalId, setSucursalId] = useState(sedeActiva === null ? '' : String(sedeActiva));
  const [productoId, setProductoId] = useState('');
  const [tipo, setTipo] = useState(String(TIPO_MOVIMIENTO.ingreso));
  const [motivo, setMotivo] = useState(String(MOTIVO_MOVIMIENTO.ajuste));
  const [cantidad, setCantidad] = useState('');
  const [unidadId, setUnidadId] = useState('');
  const [observaciones, setObservaciones] = useState('');
  const [validacion, setValidacion] = useState<string | null>(null);

  function elegirProducto(valor: string) {
    setProductoId(valor);
    const unidad = unidadBaseDe(Number(valor));
    setUnidadId(unidad === null ? '' : String(unidad));
  }

  function alGuardar() {
    setValidacion(null);

    if (!sucursalId || !productoId || !unidadId) {
      setValidacion('Completa sede, producto y unidad.');
      return;
    }
    const valorCantidad = Number(cantidad);
    if (!Number.isFinite(valorCantidad) || valorCantidad <= 0) {
      // El signo lo determina el tipo, no el número: es lo mismo que valida la
      // API, y decirlo aquí ahorra el viaje.
      setValidacion('La cantidad debe ser mayor que cero. El signo lo pone el tipo.');
      return;
    }

    void enviar(async () => {
      await registrarMovimiento({
        sucursalId: Number(sucursalId),
        productoId: Number(productoId),
        tipoMovimiento: Number(tipo) as ValorTipoMovimiento,
        motivo: Number(motivo) as ValorMotivoMovimiento,
        cantidad: valorCantidad,
        unidadId: Number(unidadId),
        observaciones: observaciones.trim() === '' ? null : observaciones.trim(),
      });
      onRegistrado();
      onCerrar();
    });
  }

  return (
    <Modal
      titulo="Registrar movimiento"
      descripcion="Entrada o salida sin documento detrás. Solo supervisión."
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
                <Spinner etiqueta="Registrando" />
                Registrando…
              </>
            ) : (
              'Registrar'
            )}
          </Boton>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {validacion ? <Alerta tipo="info">{validacion}</Alerta> : null}

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <CampoSede valor={sucursalId} onCambio={setSucursalId} disabled={enviando} />
          <CampoSelect
            etiqueta="Producto"
            valor={productoId}
            onCambio={elegirProducto}
            opciones={opcionesProducto}
            placeholder="Selecciona un producto"
            requerido
            disabled={enviando}
          />
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <CampoSelect
            etiqueta="Tipo"
            valor={tipo}
            onCambio={setTipo}
            disabled={enviando}
            opciones={[
              { valor: String(TIPO_MOVIMIENTO.ingreso), texto: 'Ingreso (suma)' },
              { valor: String(TIPO_MOVIMIENTO.retiro), texto: 'Retiro (resta)' },
            ]}
          />
          <CampoSelect
            etiqueta="Motivo"
            valor={motivo}
            onCambio={setMotivo}
            opciones={MOTIVOS_MANUALES}
            disabled={enviando}
            ayuda="Compra, venta y traslado los escribe el sistema en su propio flujo."
          />
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <CampoNumero
            etiqueta="Cantidad"
            valor={cantidad}
            onCambio={setCantidad}
            requerido
            disabled={enviando}
          />
          <CampoSelect
            etiqueta="Unidad"
            valor={unidadId}
            onCambio={setUnidadId}
            opciones={opcionesUnidad}
            placeholder="Elegir"
            requerido
            disabled={enviando}
            ayuda="El servidor convierte a la unidad base del producto."
          />
        </div>

        <CampoArea
          etiqueta="Observaciones"
          valor={observaciones}
          onCambio={setObservaciones}
          disabled={enviando}
          placeholder="Por qué se hace el ajuste"
        />
      </div>
    </Modal>
  );
}
