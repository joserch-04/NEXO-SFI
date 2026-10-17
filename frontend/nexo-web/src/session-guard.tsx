import { useEffect, useState } from 'react';
import { api, IDLE_MS } from './api';

// Se monta solo durante una sesión autenticada, fuera de las rutas de negocio.
export function SessionGuard() {
  const [remaining, setRemaining] = useState(IDLE_MS);
  useEffect(() => {
    const channel = typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel('nexo-terminal') : null;
    let ended = false, heartbeat = false, lastBeat = Date.now();
    const end = () => {
      if (ended) return;
      ended = true; channel?.postMessage('sign-out');
      void api.logout().catch(() => { /* Ya bloqueada; servidor vence por inactividad. */ });
    };
    if (channel) channel.onmessage = event => { if (event.data === 'sign-out') { ended = true; api.clear(); } };
    // Un nuevo login sustituye la identidad visible en otras pestañas de la caja.
    channel?.postMessage('sign-out');
    const check = () => {
      const age = api.activityAge();
      if (age >= IDLE_MS) { end(); return; }
      setRemaining(IDLE_MS - age);
      // Solo hay pulso si hubo interacción humana desde el pulso anterior.
      if (!heartbeat && document.visibilityState === 'visible' && Date.now() - lastBeat >= 60000 && age < Date.now() - lastBeat) {
        lastBeat = Date.now(); heartbeat = true;
        void api.request('/auth/me').catch(end).finally(() => { heartbeat = false; });
      }
    };
    const activity = (event: Event) => {
      if (!event.isTrusted) return;
      if (api.activityAge() >= IDLE_MS) { end(); return; }
      api.recordActivity(); check();
    };
    const events = ['pointerdown', 'keydown', 'wheel', 'touchstart'];
    events.forEach(name => window.addEventListener(name, activity, { passive: true }));
    window.addEventListener('pagehide', end);
    window.addEventListener('pageshow', check);
    document.addEventListener('visibilitychange', check);
    const timer = window.setInterval(check, 1000);
    return () => {
      window.clearInterval(timer);
      if (!ended) channel?.postMessage('sign-out');
      channel?.close();
      events.forEach(name => window.removeEventListener(name, activity));
      window.removeEventListener('pagehide', end); window.removeEventListener('pageshow', check);
      document.removeEventListener('visibilitychange', check);
    };
  }, []);
  return remaining <= 60000 ? <div role="alert" className="notice fixed bottom-4 right-4 z-50 max-w-md shadow-lg">Tu sesión se cerrará por inactividad en {Math.ceil(remaining / 1000)} segundos. Interactúa con NEXO para continuar.</div> : null;
}
