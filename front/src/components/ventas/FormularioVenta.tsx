import { useMemo, useState } from 'react';
import { Plus, Trash2, UserPlus } from 'lucide-react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { aNumero, aTexto, CampoNumero, CampoSelect, CampoArea } from '../ui/Campos';
import { CampoSede } from '../ui/CampoSede';
import { useSede } from '../../hooks/useSede';
import { useCatalogos } from '../../hooks/useCatalogos';
import { useConsulta } from '../../hooks/useConsulta';
import { useEnvio } from '../../hooks/useEnvio';
import { obtenerClientes, obtenerPrecioVenta, registrarVenta } from '../../services/ventas';
import type { ClienteDto, CrearLineaVentaDto, PrecioVentaDto } from '../../models/ventas';
import { formatearCOP } from '../../utils/formato';
import { convertirCosto, factorPorSimbolo } from '../../utils/unidades';
import { FormularioCliente } from './FormularioCliente';
import { PanelPreciosVenta } from './PanelPreciosVenta';

/**
 * De dónde salió el precio que hay en la línea.
 *
 * EL ORDEN VA DE LO MÁS ESPECÍFICO A LO MÁS GENERAL, y cada escalón es peor que
 * el anterior:
 *
 *   lista   lo que fijó la administración. Es el único que es una DECISIÓN.
 *   ultima  lo que se cobró la última vez. Fue un precio real, pero de una
 *           venta concreta que pudo llevar su propio criterio.
 *   costo   lo que el producto ha costado en bodega. NO es un precio de venta:
 *           cobrarlo tal cual es vender SIN MARGEN.
 *
 * Por eso se guarda de dónde vino y se dice en pantalla. Aceptar una cifra
 * creyendo que está pactada cuando es el costo de bodega es justo el error que
 * esto tiene que evitar.
 */
type OrigenPrecio = 'lista' | 'ultima' | 'costo';

interface LineaBorrador {
  clave: number;
  productoId: string;
  cantidad: string;
  unidadId: string;
  precioUnitario: string;
  descuento: string;
  /**
   * De dónde salió el precio, o `null` si lo escribió una persona.
   *
   * Hace falta para saber cuándo se puede RECALCULAR al cambiar de unidad. Un
   * precio escrito a mano no se toca: quien lo escribió sabía en qué unidad
   * estaba, y convertírselo por detrás sería cambiarle la venta sin avisar. Uno
   * que puso la pantalla sí, porque su única razón de ser es reflejar la cifra
   * de referencia en la unidad que se esté usando.
   */
  origenPrecio: OrigenPrecio | null;
  /**
   * Ese mismo precio, POR UNIDAD BASE del producto.
   *
   * Se guarda la cifra canónica y no solo la convertida: al cambiar de unidad
   * se vuelve a derivar de aquí, y así convertir dos veces (a galón y de vuelta
   * a litro) no arrastra el redondeo de cada paso.
   */
  precioBase: number | null;
}

function lineaVacia(clave: number): LineaBorrador {
  return {
    clave,
    productoId: '',
    cantidad: '',
    unidadId: '',
    precioUnitario: '',
    descuento: '',
    origenPrecio: null,
    precioBase: null,
  };
}


const SIN_CLIENTES: ClienteDto[] = [];

interface FormularioVentaProps {
  onCerrar: () => void;
  /** Se llama tras registrar, para que la pantalla recargue la lista. */
  onRegistrada: () => void;
}

export function FormularioVenta({ onCerrar, onRegistrada }: FormularioVentaProps) {
  const { sedeActiva } = useSede();
  const { opcionesProducto, opcionesUnidad, unidadBaseDe, unidades } = useCatalogos();
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const {
    datos: clientes,
    recargar: recargarClientes,
  } = useConsulta(() => obtenerClientes(), [], SIN_CLIENTES);

  const [clienteId, setClienteId] = useState('');
  const [clienteNuevoAbierto, setClienteNuevoAbierto] = useState(false);
  const [sucursalId, setSucursalId] = useState(sedeActiva === null ? '' : String(sedeActiva));
  const [observaciones, setObservaciones] = useState('');
  const [lineas, setLineas] = useState<LineaBorrador[]>([lineaVacia(0)]);
  const [siguienteClave, setSiguienteClave] = useState(1);
  const [validacion, setValidacion] = useState<string | null>(null);

  function actualizarLinea(clave: number, cambios: Partial<LineaBorrador>) {
    setLineas((previas) =>
      previas.map((linea) => (linea.clave === clave ? { ...linea, ...cambios } : linea)),
    );
  }

  /** El factor a litros de una unidad, o nulo si no es de volumen. */
  function factorDe(unidadId: number | null): number | null {
    if (unidadId === null) {
      return null;
    }
    return unidades.find((u) => u.id === unidadId)?.factorConversionLitros ?? null;
  }

  /**
   * Un precio POR UNIDAD BASE, expresado en otra unidad.
   *
   * LA CONVERSIÓN VA AL REVÉS QUE LA DE CANTIDAD: 100 L son 26,4 gal -menos
   * número, porque el galón es más grande- pero si el litro vale $22.500, el
   * galón vale $85.172, MÁS. Invertir esta división es el error clásico y el
   * resultado sigue pareciendo razonable, que es lo que lo hace peligroso.
   *
   * Nulo cuando alguna de las dos unidades no es de volumen: ahí no hay nada
   * que convertir, y es mejor dejar el campo vacío que rellenarlo con una cifra
   * inventada.
   */
  function enUnidad(precioBase: number, unidadBaseId: number, unidadId: number): number | null {
    // A 2 decimales SIEMPRE, incluso cuando no hay conversión: `precioBase`
    // puede venir ya de una división -normalizar la última venta de galón a
    // litro da 13.208,608842899448- y volcar eso al campo llena la casilla de
    // decimales que la columna `venta_detalle.precio_unitario` ni siquiera
    // guarda.
    const redondear = (valor: number) => Math.round(valor * 100) / 100;

    if (unidadBaseId === unidadId) {
      return redondear(precioBase);
    }

    const factorBase = factorDe(unidadBaseId);
    const factorDestino = factorDe(unidadId);
    if (factorBase === null || factorBase <= 0 || factorDestino === null) {
      return null;
    }

    return redondear(convertirCosto(precioBase, factorBase, factorDestino));
  }

  /**
   * La mejor referencia de precio que haya para un producto, POR UNIDAD BASE.
   *
   * TRES FUENTES, EN ESTE ORDEN: lista, última venta, costo en bodega. Ver
   * {@link OrigenPrecio} para por qué ese orden y por qué se devuelve también de
   * cuál se sacó.
   *
   * La última venta llega EN LA UNIDAD EN QUE SE COTIZÓ -50.000 por galón-, así
   * que hay que normalizarla a unidad base antes de poder compararla o
   * convertirla; sin eso volveríamos al problema original.
   */
  function resolverReferencia(
    dato: PrecioVentaDto,
    unidadBaseId: number,
  ): { precioBase: number; origen: OrigenPrecio } | null {
    // La lista se respeta aunque esté por debajo del costo: si la
    // administración la fijó así, es una decisión suya y no la sobreescribe la
    // pantalla. El panel de abajo igual avisa.
    if (dato.precioVenta !== null) {
      return { precioBase: dato.precioVenta, origen: 'lista' };
    }

    const ultima = dato.ultimaVenta;
    if (ultima?.precioUnitario != null) {
      const factorUltima = factorPorSimbolo(unidades, ultima.unidadSimbolo);
      const factorBase = factorDe(unidadBaseId);
      if (factorUltima !== null && factorUltima > 0 && factorBase !== null) {
        const normalizada = convertirCosto(ultima.precioUnitario, factorUltima, factorBase);

        // LA ÚLTIMA VENTA SOLO VALE SI NO FUE UNA PÉRDIDA. Es una cifra real,
        // pero de UNA venta concreta que pudo llevar un error: el esmalte se
        // cobró a 50.000 el galón, que son 13.209 por litro contra un costo de
        // 37.530. Proponer eso como precio de partida repetiría la pérdida en
        // cada venta siguiente, que es justo lo contrario de para lo que está
        // el autorrelleno. Cuando pasa, se cae al costo -que al menos no
        // pierde- y el panel sigue mostrando la última tal cual, así que el
        // dato no se esconde.
        if (dato.costoPromedio === null || normalizada >= dato.costoPromedio) {
          return { precioBase: normalizada, origen: 'ultima' };
        }
      }
    }

    if (dato.costoPromedio !== null) {
      return { precioBase: dato.costoPromedio, origen: 'costo' };
    }

    return null;
  }

  /**
   * Al elegir producto se preselecciona su unidad base -la del 90 % de los
   * casos- y SE RELLENA EL PRECIO.
   *
   * Rellenarlo es el punto: el campo no decía en qué unidad iba, y de ahí
   * salieron ventas del mismo producto a precios que no se parecen entre sí.
   *
   * El producto y la unidad se ponen YA, sin esperar a la consulta: quien
   * digita sigue escribiendo la cantidad mientras el precio llega. Al aplicar
   * la respuesta se comprueba que la línea siga teniendo ESE producto, porque
   * dos cambios seguidos pueden llegar en orden invertido y el segundo no debe
   * quedar pisado por la respuesta del primero.
   */
  function elegirProducto(clave: number, productoId: string) {
    const id = Number(productoId);
    const unidad = unidadBaseDe(id);

    actualizarLinea(clave, {
      productoId,
      unidadId: unidad === null ? '' : String(unidad),
      precioUnitario: '',
      origenPrecio: null,
      precioBase: null,
    });

    if (!Number.isFinite(id) || id <= 0 || unidad === null) {
      return;
    }

    const sede = Number(sucursalId);
    void obtenerPrecioVenta(id, Number.isFinite(sede) && sede > 0 ? sede : null)
      .then((dato) => {
        const referencia = resolverReferencia(dato, unidad);
        if (referencia === null) {
          return;
        }

        setLineas((previas) =>
          previas.map((linea) => {
            if (linea.clave !== clave || linea.productoId !== productoId) {
              return linea;
            }
            const enLinea = enUnidad(referencia.precioBase, unidad, Number(linea.unidadId));
            return {
              ...linea,
              precioBase: referencia.precioBase,
              origenPrecio: referencia.origen,
              precioUnitario: enLinea === null ? linea.precioUnitario : aTexto(enLinea),
            };
          }),
        );
      })
      // Un producto sin respuesta deja el campo vacío para escribirlo a mano.
      // El panel de abajo ya informa de lo que se sabe del precio.
      .catch(() => undefined);
  }

  /**
   * Al cambiar la unidad hay que rehacer el precio: $22.500 por litro y $22.500
   * por caneca de 5 galones no son lo mismo ni de lejos, y el campo se queda
   * igual mientras el rótulo de al lado cambia.
   *
   * Solo se recalcula si el precio lo puso la pantalla. Uno escrito a mano se
   * respeta, y para eso está el panel de abajo, que avisa si quedó por debajo
   * del costo.
   *
   * Se deriva del precio POR UNIDAD BASE guardado en la línea, no del que se ve:
   * pasar de litro a galón y volver arrastraría el redondeo de los dos saltos.
   */
  function elegirUnidad(clave: number, unidadId: string, linea: LineaBorrador) {
    const unidadBaseId = unidadBaseDe(Number(linea.productoId));

    if (linea.origenPrecio === null || linea.precioBase === null || unidadBaseId === null) {
      actualizarLinea(clave, { unidadId });
      return;
    }

    const precio = enUnidad(linea.precioBase, unidadBaseId, Number(unidadId));
    actualizarLinea(clave, {
      unidadId,
      precioUnitario: precio === null ? linea.precioUnitario : aTexto(precio),
      origenPrecio: precio === null ? null : linea.origenPrecio,
    });
  }

  function agregarLinea() {
    setLineas((previas) => [...previas, lineaVacia(siguienteClave)]);
    setSiguienteClave((n) => n + 1);
  }

  function quitarLinea(clave: number) {
    setLineas((previas) => previas.filter((linea) => linea.clave !== clave));
  }

  // Solo orientativo: el total que vale es el que calcula el servidor con los
  // precios de su base. Aquí sirve para que quien digita note un cero de más.
  const totalEstimado = useMemo(
    () =>
      lineas.reduce((suma, linea) => {
        const cantidad = aNumero(linea.cantidad);
        const precio = aNumero(linea.precioUnitario);
        const descuento = aNumero(linea.descuento) ?? 0;
        if (cantidad === null || precio === null) {
          return suma;
        }
        return suma + cantidad * precio * (1 - descuento / 100);
      }, 0),
    [lineas],
  );

  function alGuardar() {
    setValidacion(null);

    if (!clienteId) {
      setValidacion('Elige el cliente.');
      return;
    }
    if (!sucursalId) {
      setValidacion('Elige la sede desde la que sale la mercancía.');
      return;
    }

    const lineasValidas: CrearLineaVentaDto[] = [];
    for (const linea of lineas) {
      if (!linea.productoId || !linea.unidadId || !linea.cantidad) {
        setValidacion('Cada línea necesita producto, cantidad y unidad.');
        return;
      }
      const cantidad = aNumero(linea.cantidad);
      if (cantidad === null || cantidad <= 0) {
        setValidacion('La cantidad de cada línea debe ser mayor que cero.');
        return;
      }

      lineasValidas.push({
        productoId: Number(linea.productoId),
        cantidad,
        unidadId: Number(linea.unidadId),
        // Nulo deja que el servidor use el de lista convertido a esta unidad, y
        // RECHAZA la venta si el producto tampoco lo tiene. Mandar 0 sería
        // registrar una venta regalada, y por eso el vacío va como null:
        // `aNumero('')` ya devuelve null.
        precioUnitario: aNumero(linea.precioUnitario),
        descuento: aNumero(linea.descuento) ?? 0,
        // Sin `loteId`: el servidor descuenta por FEFO, que es lo correcto salvo
        // que alguien tenga un motivo para bajar un envase concreto.
      });
    }

    if (lineasValidas.length === 0) {
      setValidacion('Agrega al menos una línea.');
      return;
    }

    void enviar(async () => {
      await registrarVenta({
        clienteId: Number(clienteId),
        sucursalId: Number(sucursalId),
        lineas: lineasValidas,
        observaciones: observaciones.trim() === '' ? null : observaciones.trim(),
      });
      onRegistrada();
      onCerrar();
    });
  }

  return (
    <Modal
      titulo="Registrar venta"
      descripcion="El stock se descuenta por FEFO en la misma transacción. Sin existencias suficientes, no queda nada registrado."
      ancho="xl"
      ocupado={enviando}
      onCerrar={onCerrar}
      pie={
        <>
          <span className="mr-auto text-sm text-slate-500">
            Total estimado{' '}
            <span className="font-semibold text-slate-800">{formatearCOP(totalEstimado)}</span>
          </span>
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
              'Registrar venta'
            )}
          </Boton>
        </>
      }
    >
      <div className="flex flex-col gap-5">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {validacion ? <Alerta tipo="info">{validacion}</Alerta> : null}

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {/*
            El alta va PEGADA al selector y no en otra pantalla: quien atiende
            descubre que el cliente no está justo aquí, a medias de la venta, y
            mandarle a otro sitio significa perder lo que lleva escrito.
          */}
          <div className="flex items-end gap-2">
            <div className="min-w-0 flex-1">
              <CampoSelect
                etiqueta="Cliente"
                valor={clienteId}
                onCambio={setClienteId}
                requerido
                placeholder="Selecciona un cliente"
                disabled={enviando}
                opciones={clientes.map((cliente) => ({
                  valor: String(cliente.id),
                  texto: `${cliente.razonSocial} — ${cliente.documento}`,
                }))}
              />
            </div>
            <button
              type="button"
              onClick={() => setClienteNuevoAbierto(true)}
              disabled={enviando}
              title="Registrar un cliente que todavía no está"
              aria-label="Registrar un cliente nuevo"
              className="flex h-11 shrink-0 items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 text-xs font-semibold text-slate-700 transition hover:border-petroleo-400 hover:text-petroleo-700 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <UserPlus size={16} aria-hidden="true" />
              Nuevo
            </button>
          </div>
          <CampoSede valor={sucursalId} onCambio={setSucursalId} disabled={enviando} />
        </div>

        <div className="flex flex-col gap-3">
          <div className="flex items-center gap-3">
            <h3 className="text-sm font-bold text-slate-800">Líneas</h3>
            <div className="h-px flex-1 bg-slate-200" />
            <Boton
              variante="secundaria"
              onClick={agregarLinea}
              disabled={enviando}
              className="!h-9 !px-3 text-xs"
            >
              <Plus size={15} aria-hidden="true" />
              Agregar línea
            </Boton>
          </div>

          {lineas.map((linea) => {
            // El rótulo del campo de precio lleva la unidad: es exactamente lo
            // que faltaba cuando el mismo esmalte se vendió a $5.000.000 la
            // caneca y a $50.000 el galón sin que nadie lo notara.
            const simboloUnidad =
              unidades.find((u) => u.id === Number(linea.unidadId))?.simbolo ?? null;

            return (
            <div
              key={linea.clave}
              className="grid grid-cols-1 items-end gap-3 rounded-xl border border-slate-200 bg-slate-50 p-3 sm:grid-cols-12"
            >
              <div className="sm:col-span-4">
                <CampoSelect
                  etiqueta="Producto"
                  valor={linea.productoId}
                  onCambio={(v) => elegirProducto(linea.clave, v)}
                  opciones={opcionesProducto}
                  placeholder="Elegir"
                  disabled={enviando}
                />
              </div>
              <div className="sm:col-span-2">
                <CampoNumero
                  etiqueta="Cantidad"
                  valor={linea.cantidad}
                  onCambio={(v) => actualizarLinea(linea.clave, { cantidad: v })}
                  disabled={enviando}
                />
              </div>
              <div className="sm:col-span-2">
                <CampoSelect
                  etiqueta="Unidad"
                  valor={linea.unidadId}
                  onCambio={(v) => elegirUnidad(linea.clave, v, linea)}
                  opciones={opcionesUnidad}
                  placeholder="Elegir"
                  disabled={enviando}
                />
              </div>
              <div className="sm:col-span-2">
                <CampoNumero
                  etiqueta={simboloUnidad ? `Precio / ${simboloUnidad}` : 'Precio'}
                  valor={linea.precioUnitario}
                  // Sin texto de ayuda bajo el campo: de dónde sale la cifra se
                  // lee en el panel de abajo, que además da el margen y avisa si
                  // queda por debajo del costo. Repetirlo aquí era ruido sobre
                  // un campo que se mira mientras se teclea.
                  //
                  // Escribir a mano marca la línea como propia: a partir de ahí
                  // cambiar de unidad ya no recalcula el precio.
                  onCambio={(v) =>
                    actualizarLinea(linea.clave, { precioUnitario: v, origenPrecio: null })
                  }
                  disabled={enviando}
                />
              </div>
              <div className="sm:col-span-1">
                <CampoNumero
                  etiqueta="Dcto %"
                  valor={linea.descuento}
                  onCambio={(v) => actualizarLinea(linea.clave, { descuento: v })}
                  disabled={enviando}
                />
              </div>
              <div className="flex justify-end sm:col-span-1">
                <button
                  type="button"
                  onClick={() => quitarLinea(linea.clave)}
                  disabled={enviando || lineas.length === 1}
                  title="Quitar línea"
                  className="flex h-11 w-11 items-center justify-center rounded-lg text-slate-500 transition hover:bg-red-50 hover:text-red-700 disabled:cursor-not-allowed disabled:text-slate-300 disabled:hover:bg-transparent"
                >
                  <Trash2 size={17} aria-hidden="true" />
                  <span className="sr-only">Quitar línea</span>
                </button>
              </div>
            </div>
            );
          })}

          <p className="text-xs text-slate-500">
            El precio se rellena solo con el de lista y se recalcula si cambias de unidad; en
            cuanto lo escribas a mano deja de tocarse. Si lo dejas vacío lo pone el servidor, y si
            el producto tampoco tiene lista, rechaza la venta en vez de registrarla en cero. El
            lote no se elige: sale por FEFO, primero el que vence antes.
          </p>
        </div>

        <PanelPreciosVenta
          lineas={lineas.map((l) => ({
            clave: l.clave,
            productoId: l.productoId,
            unidadId: l.unidadId,
            precioUnitario: l.precioUnitario,
          }))}
          sucursalId={sucursalId}
          // Llega también el precio POR UNIDAD BASE para poder recalcularlo si
          // luego se cambia de unidad, igual que con el autorrelleno.
          onUsarPrecio={(clave, precio, precioBase) =>
            actualizarLinea(clave, {
              precioUnitario: aTexto(precio),
              precioBase,
              origenPrecio: 'lista',
            })
          }
          disabled={enviando}
        />

        <CampoArea
          etiqueta="Observaciones"
          valor={observaciones}
          onCambio={setObservaciones}
          disabled={enviando}
          placeholder="Opcional"
        />
      </div>

      {/*
        Va DENTRO del modal de la venta, y por eso este no se desmonta: lo que
        lleve escrito de la venta sigue ahí cuando se cierre el alta. El cliente
        recién creado queda elegido sin que nadie lo tenga que buscar.
      */}
      {clienteNuevoAbierto ? (
        <FormularioCliente
          onCerrar={() => setClienteNuevoAbierto(false)}
          onCreado={(cliente) => {
            setClienteId(String(cliente.id));
            recargarClientes();
          }}
        />
      ) : null}
    </Modal>
  );
}
