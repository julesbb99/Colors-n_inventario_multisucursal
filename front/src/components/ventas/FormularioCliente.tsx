import { useState } from 'react';
import { Modal } from '../ui/Modal';
import { Boton } from '../ui/Boton';
import { Alerta } from '../ui/Alerta';
import { Spinner } from '../ui/Spinner';
import { CampoTexto, CampoSelect } from '../ui/Campos';
import { useEnvio } from '../../hooks/useEnvio';
import { crearCliente } from '../../services/ventas';
import type { ClienteDto, CrearClienteDto } from '../../models/ventas';

const TIPOS_PERSONA = [
  { valor: 'Natural', texto: 'Natural — cédula' },
  { valor: 'Juridica', texto: 'Jurídica — NIT' },
];

interface FormularioClienteProps {
  onCerrar: () => void;
  /**
   * Se llama con el cliente recién creado.
   *
   * Recibe el cliente entero y no solo un aviso de «recarga» porque quien abre
   * esto está a medias de una venta: lo que hace falta es dejarlo YA elegido en
   * el selector, no devolver a la persona a una lista para que lo busque.
   */
  onCreado: (cliente: ClienteDto) => void;
  /** Documento tecleado antes de abrir, si lo hubo. */
  documentoInicial?: string;
}

/**
 * Alta de un cliente desde el mostrador.
 *
 * ABIERTO A CUALQUIER ROL, igual que en la API: aparece un cliente nuevo a
 * diario y no poder registrarlo significa no poder facturarle.
 *
 * No pide sede: un cliente le compra a la red, no a una bodega. Lo que
 * pertenece a una sede es la venta.
 */
export function FormularioCliente({
  onCerrar,
  onCreado,
  documentoInicial = '',
}: FormularioClienteProps) {
  const { enviando, error, esPermisos, enviar } = useEnvio();

  const [razonSocial, setRazonSocial] = useState('');
  const [tipoPersona, setTipoPersona] = useState('Natural');
  const [documento, setDocumento] = useState(documentoInicial);
  const [telefono, setTelefono] = useState('');
  const [email, setEmail] = useState('');
  const [direccion, setDireccion] = useState('');
  const [validacion, setValidacion] = useState<string | null>(null);

  const esJuridica = tipoPersona === 'Juridica';

  function alGuardar() {
    setValidacion(null);

    if (razonSocial.trim() === '') {
      setValidacion(esJuridica ? 'Escribe la razón social.' : 'Escribe el nombre del cliente.');
      return;
    }
    if (documento.trim() === '') {
      setValidacion(
        esJuridica
          ? 'Escribe el NIT: es por lo que se busca a la empresa.'
          : 'Escribe la cédula: es por lo que se busca a la persona.',
      );
      return;
    }

    // Los opcionales vacíos van como null, no como cadena vacía: guardados así,
    // la pantalla pintaría un hueco donde debería decir que no hay dato.
    const peticion: CrearClienteDto = {
      razonSocial: razonSocial.trim(),
      tipoPersona: esJuridica ? 'Juridica' : 'Natural',
      documento: documento.trim(),
      telefono: telefono.trim() === '' ? null : telefono.trim(),
      email: email.trim() === '' ? null : email.trim(),
      direccion: direccion.trim() === '' ? null : direccion.trim(),
    };

    void enviar(async () => {
      const cliente = await crearCliente(peticion);
      onCreado(cliente);
      onCerrar();
    });
  }

  return (
    <Modal
      titulo="Nuevo cliente"
      descripcion="Queda disponible para toda la red, no solo para esta sede."
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
                <Spinner etiqueta="Guardando" />
                Guardando…
              </>
            ) : (
              'Crear cliente'
            )}
          </Boton>
        </>
      }
    >
      <div className="flex flex-col gap-4">
        {/*
          El 409 de documento repetido llega aquí como texto, y el mensaje de la
          API nombra al cliente que ya existe. Es el caso frecuente: la persona
          ya estaba registrada y no se encontró al buscar.
        */}
        {error ? <Alerta tipo={esPermisos ? 'permisos' : 'error'}>{error}</Alerta> : null}
        {validacion ? <Alerta tipo="info">{validacion}</Alerta> : null}

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <CampoSelect
            etiqueta="Tipo"
            valor={tipoPersona}
            onCambio={setTipoPersona}
            opciones={TIPOS_PERSONA}
            requerido
            disabled={enviando}
          />
          <CampoTexto
            etiqueta={esJuridica ? 'NIT' : 'Cédula'}
            valor={documento}
            onCambio={setDocumento}
            requerido
            disabled={enviando}
            ayuda="Único en la base. Es por lo que se busca en el mostrador."
            placeholder={esJuridica ? '901345678-2' : '1094567821'}
          />
        </div>

        <CampoTexto
          etiqueta={esJuridica ? 'Razón social' : 'Nombre completo'}
          valor={razonSocial}
          onCambio={setRazonSocial}
          requerido
          disabled={enviando}
          placeholder={esJuridica ? 'Constructora Andina S.A.S.' : 'Wilson Andrés Corrales'}
        />

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <CampoTexto
            etiqueta="Teléfono"
            valor={telefono}
            onCambio={setTelefono}
            disabled={enviando}
            placeholder="Opcional"
          />
          <CampoTexto
            etiqueta="Correo"
            valor={email}
            onCambio={setEmail}
            disabled={enviando}
            placeholder="Opcional"
          />
        </div>

        <CampoTexto
          etiqueta="Dirección"
          valor={direccion}
          onCambio={setDireccion}
          disabled={enviando}
          placeholder="Opcional"
        />
      </div>
    </Modal>
  );
}
