import { createContext, useContext, useEffect, useState, type FormEvent, type ReactNode } from 'react';
import { api, errorText } from './api';
import { prepareBusinessIcon } from './business-icon';

export interface BusinessProfile { name: string; tagline: string; version: number; iconDataUrl?: string | null }
interface BusinessContext { profile: BusinessProfile | null; loading: boolean; error: string; reload: () => void; update: (profile: BusinessProfile) => void }
const Business = createContext<BusinessContext | null>(null);
export const useBusinessProfile = () => { const context = useContext(Business); if (!context) throw new Error('Falta BusinessProfileProvider.'); return context; };

// Se monta dentro de la sesión: no se publica la identidad ni se conserva al cambiar de operador.
export function BusinessProfileProvider({ children }: { children: ReactNode }) {
  const [profile, setProfile] = useState<BusinessProfile | null>(null), [loading, setLoading] = useState(true), [error, setError] = useState(''), [revision, setRevision] = useState(0);
  useEffect(() => {
    let live = true; setLoading(true); setError('');
    api.request<BusinessProfile>('/configuracion/negocio').then(value => { if (live) setProfile(value); })
      .catch(e => { if (live) setError(errorText(e)); }).finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [revision]);
  return <Business.Provider value={{ profile, loading, error, reload: () => setRevision(r => r + 1), update: setProfile }}>{children}</Business.Provider>;
}

export function BusinessSettings() {
  const { profile, loading, error, reload, update } = useBusinessProfile();
  const [name, setName] = useState(''), [tagline, setTagline] = useState(''), [busy, setBusy] = useState(false), [message, setMessage] = useState(''), [saveError, setSaveError] = useState('');
  const [icon, setIcon] = useState<string | null>(null), [processingIcon, setProcessingIcon] = useState(false);
  useEffect(() => { if (profile) { setName(profile.name); setTagline(profile.tagline); setIcon(profile.iconDataUrl ?? null); } }, [profile]);
  const canEdit = !!api.session?.user.roles.includes('Administrador') && !!api.session.user.permissions.includes('configuracion.administrar');
  async function save(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); if (!profile || !canEdit) return; setBusy(true); setMessage(''); setSaveError('');
    try {
      const saved = await api.request<BusinessProfile>('/configuracion/negocio', { method: 'PUT', body: JSON.stringify({ name, tagline, version: profile.version, iconDataUrl: icon }) });
      update(saved); setMessage('Identidad del negocio guardada.');
    } catch (err) { setSaveError(errorText(err)); } finally { setBusy(false); }
  }
  return <><div className="settings-heading"><h1>Configuración</h1><p>Personaliza el espacio de trabajo de tu negocio.</p></div>
    <div className="business-settings-grid"><section className="card"><h2>Identidad del negocio</h2><p className="settings-muted">NEXO es el software que utiliza tu tienda. Estos datos identifican el negocio que administras.</p>
      {error && <p role="alert" className="notice">{error}</p>}{error && <button className="secondary" onClick={reload} disabled={loading}>Volver a cargar</button>}
      {loading ? <p role="status" className="mt-6">Cargando configuración…</p> : profile && <form onSubmit={save}><fieldset disabled={busy || processingIcon || !canEdit} className="business-settings-fields">
        <label>Nombre comercial de la tienda<input name="businessName" value={name} onChange={e => setName(e.target.value)} required maxLength={120} placeholder="Ej. Tienda El Encuentro" autoComplete="organization" aria-describedby="business-name-help"/></label><small id="business-name-help">Se mostrará en el menú, encabezado e inicio del sistema.</small>
        <label>Lema o frase de tu negocio (opcional)<input name="tagline" value={tagline} onChange={e => setTagline(e.target.value)} maxLength={160} placeholder="Ej. Siempre cerca de vos"/></label>
        <div className="business-icon-editor"><label>Icono de la tienda<input type="file" accept="image/png,image/jpeg,image/webp" aria-describedby="business-icon-help" onChange={async e => {
          const file = e.currentTarget.files?.[0]; e.currentTarget.value = ''; if (!file) return;
          setProcessingIcon(true); setSaveError(''); setMessage('');
          try { setIcon(await prepareBusinessIcon(file)); } catch (err) { setSaveError(errorText(err)); } finally { setProcessingIcon(false); }
        }}/></label><small id="business-icon-help">PNG, JPG o WebP, hasta 2 MB. Se adapta a un máximo de 256 × 256 píxeles sin recortar.</small>
        {processingIcon && <p role="status" className="settings-muted">Procesando imagen…</p>}
        {icon && <button type="button" className="secondary business-icon-remove" onClick={() => { setIcon(null); setMessage(''); }}>Quitar imagen</button>}
        </div>
        {saveError && <p role="alert" className="notice">{saveError}</p>}{message && <p role="status" className="business-settings-success">{message}</p>}
        <button disabled={busy || processingIcon || !name.trim()}>{busy ? 'Guardando…' : 'Guardar cambios'}</button>
      </fieldset></form>}
      {saveError && <button className="secondary mt-3" onClick={() => { setSaveError(''); setMessage(''); reload(); }} disabled={busy}>Recargar datos guardados</button>}
    </section><section className="card business-preview"><p className="eyebrow">VISTA PREVIA</p><div className="business-preview-mark" aria-hidden="true">{icon ? <img src={icon} alt=""/> : (name.trim() || 'Mi tienda').slice(0, 1).toLocaleUpperCase()}</div><h2>{name.trim() || 'Nombre de tu tienda'}</h2>{tagline.trim() && <p>{tagline.trim()}</p>}<div className="business-powered">Gestionado con <strong>NEXO</strong></div><p className="settings-muted">La identidad se comparte con todas las terminales. El cambio se aplica al guardar.</p></section></div></>;
}
