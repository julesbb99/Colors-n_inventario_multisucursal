import { useEffect, useState } from 'react';
import {
  AlertTriangle,
  CalendarClock,
  DollarSign,
  Droplet,
  TrendingUp,
  Truck,
} from 'lucide-react';
import { useSede } from '../../hooks/useSede';
import { obtenerLotesProximosAVencer, obtenerResumenGeneral } from '../../services/api';
import { esApiError, mensajeDeError } from '../../models/api';
import type { ResumenGeneralDto } from '../../models/dashboard';
import type { LoteDto } from '../../models/inventario';
import { TarjetaKpi } from '../../components/dashboard/TarjetaKpi';
import { TablaAlertasVencimiento } from '../../components/dashboard/TablaAlertasVencimiento';
import { Alerta } from '../../components/ui/Alerta';
import { CargandoPanel } from '../../components/ui/Spinner';
import { formatearCOP, formatearEntero, formatearFechaHora, formatearLitros } from '../../utils/formato';

export function Dashboard() {
  const { sedeActiva, nombreSedeActiva } = useSede();

  const [resumen, setResumen] = useState<ResumenGeneralDto | null>(null);
  const [lotes, setLotes] = useState<LoteDto[]>([]);
  const [cargando, setCargando] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [esPermisos, setEsPermisos] = useState(false);

  useEffect(() => {
    let vigente = true;

    setCargando(true);
    setError(null);
    setEsPermisos(false);

    // Las dos consultas van en paralelo porque son independientes y ninguna
    // necesita el resultado de la otra. `all` y no `allSettled`: si el resumen
    // falla, la tabla sola no dice gran cosa, y media pantalla cargada confunde
    // mas que un error claro.
    Promise.all([obtenerResumenGeneral(sedeActiva), obtenerLotesProximosAVencer(sedeActiva)])
      .then(([datosResumen, datosLotes]) => {
        if (!vigente) {
          return;
        }
        setResumen(datosResumen);
        setLotes(datosLotes);
      })
      .catch((fallo: unknown) => {
        if (!vigente) {
          return;
        }
        setError(mensajeDeError(fallo));
        setEsPermisos(esApiError(fallo) && fallo.esPermisos);
        setResumen(null);
        setLotes([]);
      })
      .finally(() => {
        if (vigente) {
          setCargando(false);
        }
      });

    // Cambiar de sede dispara una consulta nueva antes de que llegue la
    // anterior. Sin esta bandera, la respuesta lenta de la sede vieja pisaria a
    // la rapida de la nueva y la pantalla mostraria cifras de otra sede.
    return () => {
      vigente = false;
    };
  }, [sedeActiva]);

  if (cargando) {
    return <CargandoPanel texto="Cargando el panel…" />;
  }

  if (error) {
    return <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta>;
  }

  if (!resumen) {
    return <Alerta tipo="info">No hay datos para mostrar.</Alerta>;
  }

  const alcance = resumen.sucursalNombre ?? 'Toda la red';

  return (
    <div className="flex flex-col gap-5">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6">
        <TarjetaKpi
          etiqueta="Ventas del dia"
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
              ? `${alcance} · ${resumen.productosSinConversionALitros} saldos sin conversion`
              : alcance
          }
          Icono={Droplet}
        />
        <TarjetaKpi
          etiqueta="En transito"
          valor={formatearEntero(resumen.transferenciasEnTransito)}
          detalle="Traslados despachados sin recibir"
          Icono={Truck}
        />
        <TarjetaKpi
          etiqueta="Stock bajo"
          valor={formatearEntero(resumen.alertasStockBajo)}
          detalle="Saldos por debajo del minimo"
          Icono={AlertTriangle}
          tono={resumen.alertasStockBajo > 0 ? 'alerta' : 'neutro'}
        />
        <TarjetaKpi
          etiqueta="Por vencer"
          valor={formatearEntero(resumen.alertasVencimiento)}
          detalle={`Umbral de ${resumen.diasUmbralVencimiento} dias, vencidos incluidos`}
          Icono={CalendarClock}
          tono={resumen.alertasVencimiento > 0 ? 'critico' : 'neutro'}
        />
      </div>

      <TablaAlertasVencimiento lotes={lotes} diasUmbral={resumen.diasUmbralVencimiento} />

      {/*
        La hora de generacion no es decorativa: el tablero se recalcula en cada
        llamada contra las tablas operativas, asi que dos lecturas seguidas pueden
        diferir. Sin esta linea no hay forma de saber a que momento corresponde lo
        que se esta mirando.
      */}
      <p className="text-xs text-slate-500">
        {nombreSedeActiva} · corte del {resumen.fechaCorte} · generado el{' '}
        {formatearFechaHora(resumen.generadoEn)}
      </p>
    </div>
  );
}
