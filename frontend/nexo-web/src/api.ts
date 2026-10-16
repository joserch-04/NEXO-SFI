export interface Identity { id: number; username: string; name?: string; roles: string[]; permissions: string[] }
export interface Session { accessToken: string; expiresIn: number; user: Identity }
export interface User { id: number; username: string; name: string; active: boolean }
export class ApiError extends Error {
  constructor(public status: number, public code: string, public traceId?: string) { super(messages[code] ?? messages[`http_${status}`] ?? 'No se pudo completar la operación. Inténtalo nuevamente.'); }
}
const messages: Record<string, string> = {
  last_administrator: 'Debe permanecer al menos un administrador activo.',
  self_role_change: 'No puedes cambiar tus propios roles.', protected_role: 'El rol Administrador está protegido.',
  administrator_required: 'Solo un administrador puede asignar estos privilegios.',
  invalid_roles: 'Selecciona roles activos válidos.', invalid_permissions: 'La selección de permisos no es válida.',
  invalid_user: 'Revisa el usuario y el nombre completo.', role_not_found: 'El rol ya no existe.',
  user_not_found: 'El usuario ya no existe.', http_409: 'La operación entra en conflicto con el estado actual.',
  http_401: 'Credenciales incorrectas o sesión vencida.', http_403: 'No tienes permiso para realizar esta operación.',
  http_429: 'Demasiados intentos. Espera un minuto.', http_503: 'El servicio no está disponible.',
  current_password_invalid: 'La contraseña actual es incorrecta.', password_policy: 'Usa entre 12 y 128 caracteres, con letras y números.',
  username_exists: 'El nombre de usuario ya está registrado.', http_400: 'Revisa los datos ingresados.',
};
const base = (import.meta.env.VITE_API_URL || 'https://localhost:7080/api/v1').replace(/\/$/, '');
if (!base.startsWith('https://')) throw new Error('VITE_API_URL debe usar HTTPS.');

export class ApiClient {
  session: Session | null = null;
  private renewal: Promise<void> | null = null;
  private listeners = new Set<() => void>();
  subscribe = (fn: () => void) => { this.listeners.add(fn); return () => { this.listeners.delete(fn); }; };
  snapshot = () => this.session;
  clear = () => { this.session = null; this.listeners.forEach(fn => fn()); };
  private set(value: Session) { this.session = value; this.listeners.forEach(fn => fn()); }
  private async lock<T>(action: () => Promise<T>): Promise<T> {
    if (!globalThis.navigator?.locks) throw new Error('Abre NEXO en un navegador actualizado mediante HTTPS.');
    return navigator.locks.request('nexo-session', action);
  }
  private async send<T>(path: string, init: RequestInit = {}, token?: string): Promise<T> {
    const headers = new Headers(init.headers);
    if (init.body) headers.set('Content-Type', 'application/json');
    if (path.startsWith('/auth/')) headers.set('X-Nexo-CSRF', '1');
    if (token) headers.set('Authorization', `Bearer ${token}`);
    let response: Response;
    try { response = await fetch(base + path, { ...init, signal: init.signal ?? AbortSignal.timeout(15000), headers, credentials: 'include', cache: 'no-store' }); }
    catch { throw new Error('No se pudo conectar con la API. Revisa la red y el certificado HTTPS.'); }
    if (!response.ok) {
      const problem = await response.json().catch(() => ({}));
      throw new ApiError(response.status, problem.code ?? `http_${response.status}`, problem.traceId);
    }
    return response.status === 204 ? undefined as T : response.json();
  }
  refresh(): Promise<void> {
    if (!this.renewal) this.renewal = this.lock(async () => {
      try { this.set(await this.send<Session>('/auth/refresh', { method: 'POST' })); }
      catch (e) { this.clear(); throw e; }
    }).finally(() => { this.renewal = null; });
    return this.renewal;
  }
  async login(username: string, password: string) {
    await this.lock(async () => { this.set(await this.send<Session>('/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) })); });
  }
  async logout() {
    await this.lock(async () => { await this.send('/auth/logout', { method: 'POST' }); this.clear(); });
  }
  async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const token = this.session?.accessToken;
    if (!token) throw new ApiError(401, 'http_401');
    try { return await this.send<T>(path, init, token); }
    catch (e) {
      if (!(e instanceof ApiError) || e.status !== 401) throw e;
      // Una petición paralela puede haber renovado ya el token.
      if (this.session?.accessToken === token) await this.refresh();
      if (!this.session) throw e;
      try { return await this.send<T>(path, init, this.session.accessToken); }
      catch (retry) { if (retry instanceof ApiError && retry.status === 401) this.clear(); throw retry; }
    }
  }
}
export const api = new ApiClient();
export const errorText = (e: unknown) => e instanceof Error ? e.message : 'Ocurrió un error inesperado.';
