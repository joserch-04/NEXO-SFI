export interface Identity { id: number; username: string; name?: string; roles: string[]; permissions: string[] }
export interface Session { accessToken: string; expiresIn: number; user: Identity }
export interface User { id: number; username: string; name: string; active: boolean }
export const IDLE_MS = 5 * 60 * 1000;
export class ApiError extends Error {
  constructor(public status: number, public code: string, public traceId?: string) { super(messages[code] ?? messages[`http_${status}`] ?? 'No se pudo completar la operación. Inténtalo nuevamente.'); }
}
const messages: Record<string, string> = {
  invalid_business_icon: 'El icono no es válido. Elige nuevamente una imagen PNG, JPG o WebP de hasta 2 MB.',
  invalid_business_profile: 'Escribe un nombre de tienda de hasta 120 caracteres y un lema de hasta 160, sin saltos de línea.',
  invalid_catalog: 'Revisa los datos, relaciones activas, cantidades y fechas del catálogo.',
  catalog_not_found: 'El registro ya no existe.', stale_version: 'Otra persona editó este registro. Recarga y vuelve a editar.',
  location_cycle: 'La ubicación superior forma un ciclo.', location_in_use: 'La ubicación tiene saldo o ubicaciones hijas activas.',
  catalog_in_use: 'La subcategoría está asociada a productos.', catalog_duplicate: 'El SKU, código, identificación o nombre ya está registrado.',
  catalog_conflict: 'La operación entra en conflicto con datos relacionados.', stock_requires_tracking: 'Primero retira el saldo mediante un movimiento justificado.',
  insufficient_stock: 'No hay existencias suficientes en la ubicación de origen.', inventory_disabled: 'El producto está inactivo o no controla inventario.',
  idempotency_conflict: 'Esta operación ya se registró con otros datos. Inicia otra operación si corresponde.',
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
  private generation = 0;
  private lastActivity = Date.now();
  activityAge = () => Date.now() - this.lastActivity;
  recordActivity = () => { if (this.session && this.activityAge() < IDLE_MS) this.lastActivity = Date.now(); };
  private listeners = new Set<() => void>();
  subscribe = (fn: () => void) => { this.listeners.add(fn); return () => { this.listeners.delete(fn); }; };
  snapshot = () => this.session;
  clear = () => { this.generation++; this.session = null; this.listeners.forEach(fn => fn()); };
  private requireActive() {
    if (!this.session || this.activityAge() >= IDLE_MS) { this.clear(); throw new ApiError(401, 'http_401'); }
  }
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
    try { this.requireActive(); } catch (e) { return Promise.reject(e); }
    const generation = this.generation;
    const identityId = this.session!.user.id;
    if (!this.renewal) this.renewal = this.lock(async () => {
      try {
        if (generation !== this.generation) throw new ApiError(401, 'http_401');
        const session = await this.send<Session>('/auth/refresh', { method: 'POST' });
        if (generation !== this.generation) throw new ApiError(401, 'http_401');
        if (session.user.id !== identityId) throw new ApiError(401, 'http_401');
        this.requireActive(); this.set(session);
      }
      catch (e) { this.clear(); throw e; }
    }).finally(() => { this.renewal = null; });
    return this.renewal;
  }
  async login(username: string, password: string) {
    const generation = this.generation;
    await this.lock(async () => {
      const session = await this.send<Session>('/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) });
      if (generation !== this.generation) throw new ApiError(401, 'http_401');
      this.lastActivity = Date.now(); this.set(session);
    });
  }
  async logout() {
    // Bloquear localmente antes de intentar la revocación: una caída de red no deja
    // información visible en la terminal abandonada.
    this.clear();
    await this.lock(async () => { await this.send('/auth/logout', { method: 'POST', keepalive: true }); });
  }
  async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    this.requireActive();
    const generation = this.generation;
    const token = this.session?.accessToken;
    if (!token) throw new ApiError(401, 'http_401');
    try {
      const result = await this.send<T>(path, init, token);
      if (generation !== this.generation) throw new ApiError(401, 'http_401');
      return result;
    }
    catch (e) {
      if (!(e instanceof ApiError) || e.status !== 401) throw e;
      if (generation !== this.generation || !this.session) throw e;
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
