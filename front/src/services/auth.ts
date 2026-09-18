import axiosInstance from '../interceptors/axiosInstance';
import type { LoginRequest, LoginResponse } from '../models/auth';

/**
 * POST /api/auth/login
 *
 * Es el unico endpoint publico de la API y el unico con limite de intentos
 * (5 por minuto y por IP). Un 401 aqui significa credenciales incorrectas, no
 * sesion expirada; el interceptor lo distingue para no borrar el mensaje de
 * error de la pantalla de acceso.
 */
export async function iniciarSesion(credenciales: LoginRequest): Promise<LoginResponse> {
  const { data } = await axiosInstance.post<LoginResponse>('/auth/login', credenciales);
  return data;
}
