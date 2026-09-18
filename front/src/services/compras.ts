import axiosInstance from '../interceptors/axiosInstance';
import { paramsDeSede } from './comun';
import type {
  ConfirmarRecepcionDto,
  CrearOrdenCompraDto,
  OrdenCompraDto,
  ProveedorDto,
} from '../models/compras';

/** El recurso es `/compras/ordenes`. `/compras` a secas no existe: responde 404. */
const RUTA = '/compras/ordenes';

export async function obtenerProveedores(): Promise<ProveedorDto[]> {
  const { data } = await axiosInstance.get<ProveedorDto[]>('/compras/proveedores');
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
 * Restringido a supervision; a un operador la API responde 403.
 */
export async function crearOrdenCompra(peticion: CrearOrdenCompraDto) {
  const { data } = await axiosInstance.post(RUTA, peticion);
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
