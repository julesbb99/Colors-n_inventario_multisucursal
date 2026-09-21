import axiosInstance from '../interceptors/axiosInstance';
import { paramsDeSede } from './comun';
import type {
  ActualizarExistenciaDto,
  ActualizarLoteDto,
  CrearExistenciaDto,
  ExistenciaDto,
  LoteDto,
  ProductoDto,
  RegistrarMovimientoDto,
} from '../models/inventario';

/**
 * GET /api/inventario/existencias
 *
 * LA ÚNICA CONSULTA DEL SISTEMA QUE NO AÍSLA POR SEDE: un gerente o un operador
 * pueden pedir cualquier sede, o la red entera con `sucursalId` nulo. El resto
 * de módulos sigue devolviendo 403 ante una sede ajena.
 *
 * Verlas no da derecho a tocarlas: crear, editar y deshabilitar siguen exigiendo
 * que la sede sea la propia.
 */
export async function obtenerExistencias(
  sucursalId: number | null,
  incluirInactivas = false,
): Promise<ExistenciaDto[]> {
  const { data } = await axiosInstance.get<ExistenciaDto[]>('/inventario/existencias', {
    params: {
      ...paramsDeSede(sucursalId),
      // Solo se manda cuando es true: un `incluirInactivas=false` explícito en
      // la URL es ruido, porque es justo lo que hace la API por defecto.
      ...(incluirInactivas ? { incluirInactivas: true } : {}),
    },
  });
  return data;
}

export async function crearExistencia(peticion: CrearExistenciaDto) {
  const { data } = await axiosInstance.post('/inventario/existencias', peticion);
  return data;
}

export async function actualizarExistencia(id: number, peticion: ActualizarExistenciaDto) {
  const { data } = await axiosInstance.put(`/inventario/existencias/${id}`, peticion);
  return data;
}

/**
 * BAJA LÓGICA. El verbo es DELETE porque expresa la intención -retirar el
 * producto de esa sede- pero el servidor solo marca la fila como inactiva: no
 * se borra nada, y se puede deshacer con `reactivarExistencia`.
 *
 * La API responde 409 si la existencia todavía tiene saldo.
 */
export async function desactivarExistencia(id: number) {
  const { data } = await axiosInstance.delete(`/inventario/existencias/${id}`);
  return data;
}

export async function reactivarExistencia(id: number) {
  const { data } = await axiosInstance.post(`/inventario/existencias/${id}/reactivar`);
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

// Aqui estaba crearLote(). Se quito con su endpoint: el numero de lote y su
// vencimiento los pone el fabricante y llegan impresos en el envase, asi que no
// se conocen hasta que el camion descarga. Los lotes nacen al recibir una compra
// -con el numero de la factura- o un traslado, que recrea en el destino el que
// salio del origen. Corregir un lote -actualizarLote, aqui abajo- si sigue.

/** Corrige numero y caducidad. PUT: lo que no venga se borra, no se conserva. */
export async function actualizarLote(id: number, peticion: ActualizarLoteDto) {
  const { data } = await axiosInstance.put(`/inventario/lotes/${id}`, peticion);
  return data;
}
