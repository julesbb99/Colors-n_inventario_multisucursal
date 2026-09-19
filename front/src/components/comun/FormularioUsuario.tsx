import { useState } from 'react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { CampoSelect, CampoTexto } from '../ui/Campos';
import { CampoSede } from '../ui/CampoSede';
import { useAuth } from '../../hooks/useAuth';
import { useSede } from '../../hooks/useSede';
import { useEnvio } from '../../hooks/useEnvio';
import { crearUsuario } from '../../services/comun';
import { ROL_DOMINIO } from '../../models/comun';
import type { ValorRolDominio } from '../../models/comun';

/** Mínimo que exige la API. Repetirlo aquí evita el viaje para un error obvio. */
const LARGO_MINIMO_PASSWORD = 12;

interface FormularioUsuarioProps {
  onCerrar: () => void;
  onCreado: () => void;
}

/**
 * Alta de usuario.
 *
 * LOS ROLES QUE SE OFRECEN DEPENDEN DE QUIÉN MIRA:
 *
 *   AdminGeneral      Gerente de Sucursal y Operador, en cualquier sede.
 *   GerenteSucursal   solo Operador, y el selector de sede va bloqueado en la
 *                     suya (es lo que ya hace CampoSede).
 *
 * Esto es comodidad, no seguridad. Quien decide es la API, que compara el rol
 * del token: un gerente que edite la petición para crear otro gerente recibe un
 * 403 igual. Acotar el formulario solo evita ofrecer algo que va a fallar.
 */
export function FormularioUsuario({ onCerrar, onCreado }: FormularioUsuarioProps) {
  const { esAdminGeneral } = useAuth();
  const { sedeActiva } = useSede();
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const rolesDisponibles = esAdminGeneral
    ? [
        { valor: String(ROL_DOMINIO.gerenteDeSucursal), texto: 'Gerente de sede' },
        { valor: String(ROL_DOMINIO.operador), texto: 'Operador' },
      ]
    : [{ valor: String(ROL_DOMINIO.operador), texto: 'Operador' }];

  const [nombre, setNombre] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmacion, setConfirmacion] = useState('');
  const [rol, setRol] = useState(String(ROL_DOMINIO.operador));
  const [sucursalId, setSucursalId] = useState(sedeActiva === null ? '' : String(sedeActiva));
  const [validacion, setValidacion] = useState<string | null>(null);

  function alGuardar() {
    setValidacion(null);

    if (nombre.trim() === '' || email.trim() === '') {
      setValidacion('El nombre y el correo son obligatorios.');
      return;
    }
    if (!sucursalId) {
      setValidacion('Elige la sede a la que queda asignado.');
      return;
    }
    if (password.length < LARGO_MINIMO_PASSWORD) {
      setValidacion(`La contraseña debe tener al menos ${LARGO_MINIMO_PASSWORD} caracteres.`);
      return;
    }
    // La confirmación es solo del formulario: la API no la recibe. Existe porque
    // quien teclea la contraseña no es su dueño, así que un dedazo lo descubriría
    // la otra persona al no poder entrar.
    if (password !== confirmacion) {
      setValidacion('Las dos contraseñas no coinciden.');
      return;
    }

    void enviar(async () => {
      await crearUsuario({
        nombre: nombre.trim(),
        email: email.trim(),
        password,
        rol: Number(rol) as ValorRolDominio,
        sucursalId: Number(sucursalId),
      });
      onCreado();
      onCerrar();
    });
  }

  return (
    <Modal
      titulo="Nuevo usuario"
      descripcion={
        esAdminGeneral
          ? 'Puedes dar de alta gerentes y operadores en cualquier sede.'
          : 'Puedes dar de alta operadores de tu sede.'
      }
      ocupado={enviando}
      onCerrar={onCerrar}
      pie={
        <>
          <Boton variante="secundaria" onClick={onCerrar} disabled={enviando}>
            Cancelar
          </Boton>
          <Boton onClick={alGuardar} disabled={enviando}>
            {enviando ? (
              <>
                <Spinner etiqueta="Creando" />
                Creando…
              </>
            ) : (
              'Crear usuario'
            )}
          </Boton>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {validacion ? <Alerta tipo="info">{validacion}</Alerta> : null}

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <CampoTexto
            etiqueta="Nombre completo"
            valor={nombre}
            onCambio={setNombre}
            requerido
            disabled={enviando}
            placeholder="Ana María Restrepo"
          />
          <CampoTexto
            etiqueta="Correo"
            valor={email}
            onCambio={setEmail}
            requerido
            disabled={enviando}
            placeholder="nombre@colorsin.com.co"
            ayuda="Es con lo que inicia sesión. Único en el sistema."
          />
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <CampoSelect
            etiqueta="Rol"
            valor={rol}
            onCambio={setRol}
            opciones={rolesDisponibles}
            requerido
            disabled={enviando}
            ayuda={
              esAdminGeneral
                ? 'Un Administrador General no se crea desde la aplicación.'
                : 'Un gerente solo da de alta operadores.'
            }
          />
          <CampoSede valor={sucursalId} onCambio={setSucursalId} disabled={enviando} />
        </div>

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {/*
            Ocultas, y con confirmación. Las dos cosas van juntas: si se
            mostraran en claro, repetirla no detectaría nada -se leerían las dos-
            y el campo de confirmación sería decorado. Ocultas, la repetición sí
            atrapa el dedazo, que aquí importa más de lo normal porque quien
            teclea no es el dueño de la cuenta: un error lo descubriría la otra
            persona al no poder entrar.

            `new-password` evita que el navegador ofrezca autocompletar con las
            credenciales de quien está conectado.
          */}
          <CampoTexto
            etiqueta="Contraseña inicial"
            tipo="password"
            autoComplete="new-password"
            valor={password}
            onCambio={setPassword}
            requerido
            disabled={enviando}
            ayuda={`Mínimo ${LARGO_MINIMO_PASSWORD} caracteres.`}
          />
          <CampoTexto
            etiqueta="Repetir contraseña"
            tipo="password"
            autoComplete="new-password"
            valor={confirmacion}
            onCambio={setConfirmacion}
            requerido
            disabled={enviando}
          />
        </div>

        <Alerta tipo="info">
          El sistema todavía no tiene cambio de contraseña, así que quien la escribe aquí la
          seguirá conociendo y su dueño no podrá cambiarla desde la aplicación. Entrégasela por un
          canal aparte y anótalo como pendiente.
        </Alerta>
      </div>
    </Modal>
  );
}
