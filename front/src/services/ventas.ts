import axiosInstance from '../interceptors/axiosInstance';
import { paramsDeSede } from './comun';
import type { ClienteDto, CrearVentaDto, VentaDto, VentaRegistradaDto } from '../models/ventas';

const RUTA = '/ventas';

export async function obtenerClientes(): Promise<ClienteDto[]> {
  const { data } = await axiosInstance.get<ClienteDto[]>(`${RUTA}/clientes`);
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
