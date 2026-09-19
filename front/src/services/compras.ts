import axiosInstance from '../interceptors/axiosInstance';
import { paramsDeSede } from './comun';
import type {
  ConfirmarRecepcionDto,
  CrearOrdenCompraDto,
  GuardarPrecioReferenciaDto,
  GuardarProveedorDto,
  OrdenCompraDto,
  PrecioReferenciaDto,
  ProveedorDto,
} from '../models/compras';

/** El recurso es `/compras/ordenes`. `/compras` a secas no existe: responde 404. */
const RUTA = '/compras/ordenes';
const PROVEEDORES = '/compras/proveedores';

export async function obtenerProveedores(incluirInactivos = false): Promise<ProveedorDto[]> {
  const { data } = await axiosInstance.get<ProveedorDto[]>(PROVEEDORES, {
    params: incluirInactivos ? { incluirInactivos: true } : {},
  });
  return data;
}

// -----------------------------------------------------------------------------
// Proveedores y lista de precios: SOLO ADMINISTRADOR GENERAL.
//
// El catálogo de proveedores y sus precios son compartidos por las tres sedes,
// así que un cambio aquí lo hereda toda la red. A un gerente o un operador la
// API responde 403.
// -----------------------------------------------------------------------------

export async function crearProveedor(peticion: GuardarProveedorDto) {
  const { data } = await axiosInstance.post(PROVEEDORES, peticion);
  return data;
}

export async function actualizarProveedor(id: number, peticion: GuardarProveedorDto) {
  const { data } = await axiosInstance.put(`${PROVEEDORES}/${id}`, peticion);
  return data;
}

/** BAJA LÓGICA. No borra: conserva órdenes históricas y lista de precios. */
export async function retirarProveedor(id: number) {
  const { data } = await axiosInstance.delete(`${PROVEEDORES}/${id}`);
  return data;
}

export async function reactivarProveedor(id: number) {
  const { data } = await axiosInstance.post(`${PROVEEDORES}/${id}/reactivar`);
  return data;
}

/** El precio va POR UNIDAD BASE del producto. Nulo lo quita de la lista. */
export async function guardarPrecioReferencia(
  proveedorId: number,
  productoId: number,
  peticion: GuardarPrecioReferenciaDto,
) {
  const { data } = await axiosInstance.put(
    `${PROVEEDORES}/${proveedorId}/precios/${productoId}`,
    peticion,
  );
  return data;
}

/**
 * Precio de lista y último precio pagado. Lo consulta cualquier rol: es lo que
 * hace falta para pedir con criterio.
 *
 * Devuelve `null` si el producto o el proveedor no existen; si simplemente no
 * hay precio ni histórico, responde con los dos campos nulos.
 */
export async function obtenerPrecioReferencia(
  productoId: number,
  proveedorId: number,
): Promise<PrecioReferenciaDto> {
  const { data } = await axiosInstance.get<PrecioReferenciaDto>('/compras/precios', {
    params: { productoId, proveedorId },
  });
  return data;
}

export async function obtenerOrdenesCompra(
  sucursalId: number | null,
  proveedorId?: number | null,
): Promise<OrdenCompraDto[]> {
  const { data } = await axiosInstance.get<OrdenCompraDto[]>(RUTA, {
    params: {
      ...paramsDeSede(sucursalId),
      ...(proveedorId === undefined || proveedorId === null ? {} : { proveedorId }),
    },
  });
  return data;
}

export async function obtenerOrdenCompra(id: number): Promise<OrdenCompraDto> {
  const { data } = await axiosInstance.get<OrdenCompraDto>(`${RUTA}/${id}`);
  return data;
}

/**
 * Crea la orden. Nace 'Pendiente' y NO mueve stock: comprometer dinero con un
 * proveedor y recibir mercancia son dos hechos distintos.
 *
 * ABIERTO AL OPERADOR: una orden Pendiente es un borrador. Lo que obliga a la
 * empresa es confirmarla o recibirla, y esas dos siguen siendo de supervisión.
 * La sede sí se comprueba: solo se pide para la propia.
 */
export async function crearOrdenCompra(peticion: CrearOrdenCompraDto) {
  const { data } = await axiosInstance.post(RUTA, peticion);
  return data;
}

/**
 * Edita una orden que siga en 'Pendiente'.
 *
 * REEMPLAZA las líneas: las que van en el cuerpo sustituyen a las que había,
 * que es lo que significa un PUT. La API responde 409 en cualquier otro estado.
 */
export async function actualizarOrdenCompra(id: number, peticion: CrearOrdenCompraDto) {
  const { data } = await axiosInstance.put(`${RUTA}/${id}`, peticion);
  return data;
}

/**
 * Retira una orden 'Pendiente' pasándola a 'Cancelada'. NO la borra: la fila se
 * conserva con su detalle, y es la pantalla la que la esconde del listado.
 *
 * 409 si ya fue confirmada o recibida.
 */
export async function cancelarOrdenCompra(id: number) {
  const { data } = await axiosInstance.delete(`${RUTA}/${id}`);
  return data;
}

/**
 * Registra una recepcion. ESTE es el paso que mueve stock: suma al saldo de la
 * sede, crea o alimenta el lote y deja fila en el libro mayor.
 *
 * Cuerpo sin `lineas` significa "llego todo lo que faltaba de la orden".
 * Restringido a supervision.
 */
export async function recibirOrdenCompra(id: number, peticion: ConfirmarRecepcionDto) {
  const { data } = await axiosInstance.post(`${RUTA}/${id}/recepciones`, peticion);
  return data;
}
