import {
  AlertTriangle,
  CalendarClock,
  DollarSign,
  Droplet,
  PackageX,
  TrendingUp,
  Truck,
} from 'lucide-react';
import { useSede } from '../../hooks/useSede';
import { useAuth } from '../../hooks/useAuth';
import { useConsulta } from '../../hooks/useConsulta';
import { obtenerResumenGeneral } from '../../services/dashboard';
import { obtenerLotesProximosAVencer } from '../../services/inventario';
import type { ResumenGeneralDto } from '../../models/dashboard';
import type { LoteDto } from '../../models/inventario';
import { TarjetaKpi } from '../../components/dashboard/TarjetaKpi';
import { TablaAlertasVencimiento } from '../../components/dashboard/TablaAlertasVencimiento';
import { HistoricoVentas } from '../../components/dashboard/HistoricoVentas';
import { RotacionProductos } from '../../components/dashboard/RotacionProductos';
import { ComparativaSucursales } from '../../components/dashboard/ComparativaSucursales';
import { Alerta } from '../../components/ui/Alerta';
import { CargandoPanel } from '../../components/ui/Spinner';
import {
  formatearCOP,
  formatearEntero,
  formatearFechaHora,
  formatearLitros,
} from '../../utils/formato';

/** Los dos bloques van juntos porque el umbral del resumen clasifica la tabla. */
interface DatosPanel {
  resumen: ResumenGeneralDto | null;
  lotes: LoteDto[];
}

const VACIO: DatosPanel = { resumen: null, lotes: [] };

export function Dashboard() {
  const { sedeActiva, nombreSedeActiva } = useSede();
  const { esSupervision } = useAuth();

  const { datos, cargando, error, esPermisos } = useConsulta<DatosPanel>(
    async () => {
      // En paralelo porque son independientes. `all` y no `allSettled`: si el
      // resumen falla, la tabla sola no dice gran cosa y media pantalla cargada
      // confunde mas que un error claro.
      const [resumen, lotes] = await Promise.all([
        obtenerResumenGeneral(sedeActiva),
        obtenerLotesProximosAVencer(sedeActiva),
      ]);
      return { resumen, lotes };
    },
    [sedeActiva],
    VACIO,
  );

  if (cargando) {
    return <CargandoPanel texto="Cargando el panel…" />;
  }

  if (error) {
    return <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>;
  }

  const { resumen, lotes } = datos;

  if (!resumen) {
    return <Alerta tipo="info">No hay datos para mostrar.</Alerta>;
  }

  const alcance = resumen.sucursalNombre ?? 'Toda la red';

  return (
    <div className="flex flex-col gap-5">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6">
        <TarjetaKpi
          etiqueta="Ventas del día"
          valor={formatearCOP(resumen.ventasDelDia)}
          detalle={`${formatearEntero(resumen.cantidadVentasDelDia)} facturas`}
          Icono={DollarSign}
        />
        <TarjetaKpi
          etiqueta="Ventas del mes"
          valor={formatearCOP(resumen.ventasDelMes)}
          detalle={`${formatearEntero(resumen.cantidadVentasDelMes)} facturas`}
          Icono={TrendingUp}
        />
        <TarjetaKpi
          etiqueta="Saldo en bodega"
          valor={formatearLitros(resumen.saldoInventarioLitros)}
          detalle={
            resumen.productosSinConversionALitros > 0
              ? `${alcance} · ${resumen.productosSinConversionALitros} saldos sin conversión`
              : alcance
          }
          Icono={Droplet}
        />
        <TarjetaKpi
          etiqueta="En tránsito"
          valor={formatearEntero(resumen.transferenciasEnTransito)}
          detalle="Traslados despachados sin recibir"
          Icono={Truck}
        />
        <TarjetaKpi
          etiqueta="Stock bajo"
          valor={formatearEntero(resumen.alertasStockBajo)}
          detalle="Saldos por debajo del mínimo"
          Icono={AlertTriangle}
          tono={resumen.alertasStockBajo > 0 ? 'alerta' : 'neutro'}
        />
        <TarjetaKpi
          etiqueta="Por vencer"
          valor={formatearEntero(resumen.alertasVencimiento)}
          detalle={`Umbral de ${resumen.diasUmbralVencimiento} días, vencidos incluidos`}
          Icono={CalendarClock}
          tono={resumen.alertasVencimiento > 0 ? 'critico' : 'neutro'}
        />
        {/*
          Lo que se pidió y llegó corto.

          El rótulo dice "por llegar" y no "merma" a propósito: esa mercancía
          nunca entró a la bodega, así que no hay saldo del que descontarla, y
          la orden sigue abierta esperando el resto. Llamarlo merma haría pensar
          que el inventario ya bajó por esta cifra, y no bajó.
        */}
        <TarjetaKpi
          etiqueta="Faltante por llegar"
          valor={formatearCOP(resumen.faltanteRecepcionValor)}
          detalle={
            resumen.ordenesParcialmenteRecibidas > 0
              ? `${formatearEntero(resumen.ordenesParcialmenteRecibidas)} orden(es) recibidas a medias`
              : 'Ninguna orden llegó corta'
          }
          Icono={PackageX}
          tono={resumen.ordenesParcialmenteRecibidas > 0 ? 'alerta' : 'neutro'}
        />
      </div>

      {/*
        Cada bloque pide sus propios datos y maneja su propia carga, en vez de
        engordar la consulta de arriba. Son consultas agregadas y pesadas: si
        fueran juntas, la pantalla entera se quedaría en blanco esperando a la
        más lenta, y hoy los KPI aparecen en cuanto responde el resumen.
      */}
      <HistoricoVentas />

      <RotacionProductos />

      {/* La comparativa es SOLO de administración y gerencia. Esto no es el
          control -la API responde 403 al operario y el servicio lo vuelve a
          comprobar-: es no pedir algo que va a fallar. */}
      {esSupervision ? <ComparativaSucursales /> : null}

      <TablaAlertasVencimiento lotes={lotes} diasUmbral={resumen.diasUmbralVencimiento} />

      {/*
        La hora de generación no es decorativa: el tablero se recalcula en cada
        llamada contra las tablas operativas, así que dos lecturas seguidas pueden
        diferir. Sin esta línea no hay forma de saber a qué momento corresponde lo
        que se está mirando.
      */}
      <p className="text-xs text-slate-500">
        {nombreSedeActiva} · corte del {resumen.fechaCorte} · generado el{' '}
        {formatearFechaHora(resumen.generadoEn)}
      </p>
    </div>
  );
}
