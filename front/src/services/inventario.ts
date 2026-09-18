import axiosInstance from '../interceptors/axiosInstance';
import { paramsDeSede } from './comun';
import type {
  ActualizarLoteDto,
  CrearLoteDto,
  ExistenciaDto,
  LoteDto,
  ProductoDto,
  RegistrarMovimientoDto,
} from '../models/inventario';

export async function obtenerExistencias(
  sucursalId: number | null,
): Promise<ExistenciaDto[]> {
  const { data } = await axiosInstance.get<ExistenciaDto[]>('/inventario/existencias', {
    params: paramsDeSede(sucursalId),
  });
  return data;
}

interface FiltrosLotes {
  sucursalId: number | null;
  productoId?: number | null;
  /** Deja fuera los agotados. Falso por defecto: un lote recien creado esta en cero. */
  soloConSaldo?: boolean;
}

/**
 * GET /api/inventario/lotes — el listado general, en orden FEFO.
 *
 * NO FILTRA POR CATEGORIA: el endpoint acepta `sucursalId`, `productoId`,
 * `soloConSaldo` y `limite`, y nada mas. La categoria vive en el producto, asi
 * que para acotar por ella hay que resolver primero los productos de esa
 * categoria con `/api/productos?categoria=` y filtrar por `productoId`.
 */
export async function obtenerLotes({
  sucursalId,
  productoId = null,
  soloConSaldo = false,
}: FiltrosLotes): Promise<LoteDto[]> {
  const { data } = await axiosInstance.get<LoteDto[]>('/inventario/lotes', {
    params: {
      ...paramsDeSede(sucursalId),
      ...(productoId === null ? {} : { productoId }),
      soloConSaldo,
    },
  });
  return data;
}

/**
 * GET /api/inventario/lotes/proximos-a-vencer
 *
 * `dias` omitido usa el umbral configurado en la API
 * (`AlertasInventario:DiasUmbralVencimiento`), que es el mismo con el que el
 * resumen cuenta sus alertas. Pasar otro aqui haria que la tarjeta del panel y
 * la tabla contaran cosas distintas.
 *
 * Los lotes YA VENCIDOS con existencias entran por defecto y salen de primeros.
 */
export async function obtenerLotesProximosAVencer(
  sucursalId: number | null,
  dias?: number,
): Promise<LoteDto[]> {
  const { data } = await axiosInstance.get<LoteDto[]>('/inventario/lotes/proximos-a-vencer', {
    params: {
      ...paramsDeSede(sucursalId),
      ...(dias === undefined ? {} : { dias }),
    },
  });
  return data;
}

/**
 * GET /api/productos — catalogo de la red.
 *
 * Cuelga de la raiz, NO de `/api/inventario`: no es una consulta de stock sino
 * el catalogo con el que se arma cualquier linea de compra, venta o traslado.
 */
export async function obtenerProductos(categoria?: string): Promise<ProductoDto[]> {
  const { data } = await axiosInstance.get<ProductoDto[]>('/productos', {
    params: categoria ? { categoria } : {},
  });
  return data;
}

// -----------------------------------------------------------------------------
// Escritura. Las tres exigen supervision: la API responde 403 a un operador.
// -----------------------------------------------------------------------------

/**
 * Registra una entrada o salida de stock a mano.
 *
 * Es la unica via que cambia el saldo SIN un documento detras -un ajuste, una
 * merma, una devolucion- y por eso mismo la que hay que vigilar.
 */
export async function registrarMovimiento(peticion: RegistrarMovimientoDto) {
  const { data } = await axiosInstance.post('/inventario/movimientos', peticion);
  return data;
}

/** Abre un lote VACIO. La mercancia entra despues; ver CrearLoteDto. */
export async function crearLote(peticion: CrearLoteDto) {
  const { data } = await axiosInstance.post('/inventario/lotes', peticion);
  return data;
}

/** Corrige numero y caducidad. PUT: lo que no venga se borra, no se conserva. */
export async function actualizarLote(id: number, peticion: ActualizarLoteDto) {
  const { data } = await axiosInstance.put(`/inventario/lotes/${id}`, peticion);
  return data;
}
