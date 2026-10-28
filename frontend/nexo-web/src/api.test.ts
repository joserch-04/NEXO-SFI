import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError } from './api';

const session = (token: string) => ({ accessToken: token, expiresIn: 600, user: { id: 1, username: 'prueba', roles: [], permissions: [] } });
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });
beforeEach(() => {
  vi.stubGlobal('navigator', { locks: { request: (_name: string, action: () => unknown) => action() } });
});
afterEach(() => { vi.useRealTimers(); vi.unstubAllGlobals(); });
describe('Cliente de sesión', () => {
  it('explica una ruta ausente en una API anterior sin repetir el registro ni cerrar sesión', async () => {
    const fetch = vi.fn().mockResolvedValue(new Response(null, { status: 404 }));
    vi.stubGlobal('fetch', fetch);
    const client = new ApiClient(); client.session = session('vigente');
    await expect(client.request('/inventario/recepciones', { method: 'POST', body: '{}' }))
      .rejects.toMatchObject({ status: 404, message: expect.stringContaining('recompila y reinicia la API') });
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(client.session).not.toBeNull();
  });
  it('coordina dos renovaciones y envía cookie y protección CSRF', async () => {
    const fetch = vi.fn().mockResolvedValue(json(session('nuevo'))); vi.stubGlobal('fetch', fetch);
    const client = new ApiClient(); client.session = session('anterior'); await Promise.all([client.refresh(), client.refresh()]);
    expect(fetch).toHaveBeenCalledTimes(1);
    const init = fetch.mock.calls[0][1];
    expect(init.credentials).toBe('include'); expect(init.headers.get('X-Nexo-CSRF')).toBe('1');
    expect(client.session?.accessToken).toBe('nuevo');
  });
  it('renueva una sola vez para dos recursos que reciben 401', async () => {
    const fetch = vi.fn(async (url: string, init: RequestInit) => {
      if (url.endsWith('/auth/refresh')) return json(session('nuevo'));
      return new Headers(init.headers).get('Authorization') === 'Bearer nuevo' ? json({ ok: true }) : json({}, 401);
    }); vi.stubGlobal('fetch', fetch);
    const client = new ApiClient(); client.session = session('anterior');
    expect(await Promise.all([client.request('/usuarios'), client.request('/roles')])).toEqual([{ ok: true }, { ok: true }]);
    expect(fetch.mock.calls.filter(([url]) => url.endsWith('/auth/refresh'))).toHaveLength(1);
  });
  it('no renueva ante 403 ni repite operaciones rechazadas por permisos', async () => {
    const fetch = vi.fn().mockResolvedValue(json({}, 403)); vi.stubGlobal('fetch', fetch);
    const client = new ApiClient(); client.session = session('vigente');
    await expect(client.request('/usuarios')).rejects.toBeInstanceOf(ApiError); expect(fetch).toHaveBeenCalledTimes(1);
    expect(client.session).not.toBeNull();
  });
  it('borra la sesión cuando la renovación es rechazada', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json({}, 401)));
    const client = new ApiClient(); client.session = session('vencido');
    await expect(client.request('/usuarios')).rejects.toMatchObject({ status: 401 }); expect(client.session).toBeNull();
  });
  it('bloquea la terminal incluso si logout no consigue revocar en el servidor', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('network')));
    const client = new ApiClient(); client.session = session('vigente');
    await expect(client.logout()).rejects.toThrow('No se pudo conectar'); expect(client.session).toBeNull();
  });
  it('tolera respuestas sin cuerpo y conserva el identificador de diagnóstico', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(new Response(null, { status: 204 })).mockResolvedValueOnce(json({ code: 'password_policy', traceId: 'ejemplo' }, 400));
    vi.stubGlobal('fetch', fetch); const client = new ApiClient(); client.session = session('vigente');
    await expect(client.request('/usuarios/1/cambio-contrasena', { method: 'POST' })).resolves.toBeUndefined();
    await expect(client.request('/usuarios')).rejects.toMatchObject({ code: 'password_policy', traceId: 'ejemplo' });
  });
  it('no restaura con cookie una sesión nueva sin login en memoria', async () => {
    const fetch = vi.fn(); vi.stubGlobal('fetch', fetch);
    await expect(new ApiClient().refresh()).rejects.toMatchObject({ status: 401 });
    expect(fetch).not.toHaveBeenCalled();
  });
  it('rechaza actividad y peticiones al alcanzar cinco minutos sin interacción', async () => {
    vi.useFakeTimers(); const client = new ApiClient(); client.session = session('vigente');
    const fetch = vi.fn(); vi.stubGlobal('fetch', fetch);
    vi.advanceTimersByTime(5 * 60000); client.recordActivity();
    await expect(client.request('/usuarios')).rejects.toMatchObject({ status: 401 });
    expect(client.session).toBeNull(); expect(fetch).not.toHaveBeenCalled();
  });
  it('una renovación en curso no reabre la sesión después del bloqueo', async () => {
    let resolve!: (value: Response) => void;
    vi.stubGlobal('fetch', vi.fn(() => new Promise<Response>(done => { resolve = done; })));
    const client = new ApiClient(); client.session = session('anterior');
    const renewal = client.refresh(); client.clear(); resolve(json(session('nuevo')));
    await expect(renewal).rejects.toMatchObject({ status: 401 }); expect(client.session).toBeNull();
  });
  it('no cambia de operador silenciosamente si otra pestaña cambió la cookie', async () => {
    const other = session('otra-cuenta'); other.user.id = 2;
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json(other)));
    const client = new ApiClient(); client.session = session('actual');
    await expect(client.refresh()).rejects.toMatchObject({ status: 401 }); expect(client.session).toBeNull();
  });
});
