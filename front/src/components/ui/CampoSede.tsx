import { useSede } from '../../hooks/useSede';
import { CampoSelect } from './Campos';

interface CampoSedeProps {
  etiqueta?: string;
  valor: string;
  onCambio: (valor: string) => void;
  disabled?: boolean;
  ayuda?: string;
}

/**
 * La sede sobre la que opera un formulario.
 *
 * Toda escritura nombra una sede concreta, y de dónde sale depende del rol:
 *
 *   Gerente / Operador  la de su token. El control va bloqueado, porque la API
 *                       responde 403 a cualquier otra y no tiene sentido
 *                       ofrecer una opción que va a fallar.
 *   AdminGeneral        elige. Si venía mirando "Todas las sedes" no hay una
 *                       implícita, así que el formulario OBLIGA a escogerla: un
 *                       traslado o una venta necesitan saber de qué bodega salen.
 */
export function CampoSede({
  etiqueta = 'Sede',
  valor,
  onCambio,
  disabled,
  ayuda,
}: CampoSedeProps) {
  const { sucursales, puedeCambiarSede } = useSede();

  return (
    <CampoSelect
      etiqueta={etiqueta}
      valor={valor}
      onCambio={onCambio}
      requerido
      disabled={disabled || !puedeCambiarSede}
      placeholder="Selecciona una sede"
      ayuda={
        ayuda ?? (puedeCambiarSede ? undefined : 'Fija según tu rol: es la sede de tu sesión.')
      }
      opciones={sucursales.map((sede) => ({
        valor: String(sede.id),
        texto: `${sede.nombre} — ${sede.ciudad}`,
      }))}
    />
  );
}
