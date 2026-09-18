import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// El puerto 5173 no es un capricho: es uno de los origenes que la API ya trae
// en su lista blanca de CORS (Cors:OrigenesPermitidos de
// back/colorsin.Api/appsettings.Development.json). Si se cambia aqui hay que
// agregarlo alla, o el navegador bloquea cada llamada.
export default defineConfig({
  plugins: [react()],
  server: {
    // Escucha en todas las interfaces, no solo en localhost. Hace falta para
    // llegar al servidor desde fuera de la maquina que lo corre: otro equipo de
    // la red, un movil probando el diseno, o -el caso que nos obligo a
    // ponerlo- un contenedor de Docker, donde el `localhost` del contenedor no
    // es el de quien abre el navegador.
    host: true,
    port: 5173,
    // Si el 5173 esta ocupado, FALLAR en vez de saltar al 5174.
    //
    // No es rigidez: la lista blanca de CORS de la API nombra el puerto exacto.
    // Con el salto automatico, Vite arrancaria feliz en otro puerto y todas las
    // llamadas moririan con un error de CORS que no menciona el puerto por
    // ninguna parte, que es de lo mas caro de depurar.
    strictPort: true,
  },
});
