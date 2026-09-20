import axiosInstance from '../interceptors/axiosInstance';
import { paramsDeSede } from './comun';
import type {
  ClienteDto,
  CrearClienteDto,
  CrearVentaDto,
  GuardarPrecioVentaDto,
  PrecioVentaDto,
  VentaDto,
  VentaRegistradaDto,
} from '../models/ventas';

const RUTA = '/ventas';

export async function obtenerClientes(): Promise<ClienteDto[]> {
  const { data } = await axiosInstance.get<ClienteDto[]>(`${RUTA}/clientes`);
  return data;
}

/**
 * Da de alta un cliente y devuelve el creado, ya con su id.
 *
 * Abierta a cualquier rol: en el mostrador aparece un cliente nuevo a diario.
 * Responde 409 si el documento ya existe, y el mensaje trae el nombre y el id
 * del que ya está.
 */
export async function crearCliente(peticion: CrearClienteDto): Promise<ClienteDto> {
  const { data } = await axiosInstance.post<ClienteDto>(`${RUTA}/clientes`, peticion);
  return data;
}

/**
 * A cuánto se vende un producto: lo fijado, lo que se cobró la última vez y lo
 * que ha costado en bodega.
 *
 * `sucursalId` acota la última venta a una sede; omitido, responde por la red,
 * y la API lo recorta igual a lo que quien pregunta puede ver.
 */
export async function obtenerPrecioVenta(
  productoId: number,
  sucursalId?: number | null,
): Promise<PrecioVentaDto> {
  const { data } = await axiosInstance.get<PrecioVentaDto>(`${RUTA}/precios/${productoId}`, {
    params: sucursalId == null ? undefined : { sucursalId },
  });
  return data;
}

/**
 * Fija el precio de venta POR UNIDAD BASE, o lo quita si va nulo.
 *
 * Solo Administrador General: el precio es de toda la red y las tres sedes lo
 * heredan. La API responde 403 a los demás.
 */
export async function fijarPrecioVenta(
  productoId: number,
  peticion: GuardarPrecioVentaDto,
): Promise<PrecioVentaDto> {
  const { data } = await axiosInstance.put<PrecioVentaDto>(
    `${RUTA}/precios/${productoId}`,
    peticion,
  );
  return data;
}

/** Busqueda por cedula o NIT: es como se identifica a alguien en el mostrador. */
export async function buscarClientePorDocumento(documento: string): Promise<ClienteDto> {
  const { data } = await axiosInstance.get<ClienteDto>(
    `${RUTA}/clientes/documento/${encodeURIComponent(documento)}`,
  );
  return data;
}

interface FiltrosVentas {
  sucursalId: number | null;
  clienteId?: number | null;
  /** Fechas en ISO. El endpoint las recibe como `DateTime`. */
  desde?: string | null;
  hasta?: string | null;
}

export async function obtenerVentas({
  sucursalId,
  clienteId = null,
  desde = null,
  hasta = null,
}: FiltrosVentas): Promise<VentaDto[]> {
  const { data } = await axiosInstance.get<VentaDto[]>(RUTA, {
    params: {
      ...paramsDeSede(sucursalId),
      ...(clienteId === null ? {} : { clienteId }),
      ...(desde === null ? {} : { desde }),
      ...(hasta === null ? {} : { hasta }),
    },
  });
  return data;
}

export async function obtenerVenta(id: number): Promise<VentaDto> {
  const { data } = await axiosInstance.get<VentaDto>(`${RUTA}/${id}`);
  return data;
}

/**
 * Registra la venta y descuenta stock por FEFO en la MISMA transaccion.
 *
 * La respuesta dice de que lotes salio cada linea, que es lo que permite
 * mostrarle a quien despacha cual envase tiene que bajar del estante.
 *
 * Sin stock suficiente la API responde 409 y no queda nada registrado.
 */
export async function registrarVenta(peticion: CrearVentaDto): Promise<VentaRegistradaDto> {
  const { data } = await axiosInstance.post<VentaRegistradaDto>(RUTA, peticion);
  return data;
}
