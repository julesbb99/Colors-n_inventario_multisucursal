import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// El puerto 5173 no es un capricho: es uno de los origenes que la API ya trae
// en su lista blanca de CORS (Cors:OrigenesPermitidos de
// back/colorsin.Api/appsettings.Development.json). Si se cambia aqui hay que
// agregarlo alla, o el navegador bloquea cada llamada.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
  },
});
