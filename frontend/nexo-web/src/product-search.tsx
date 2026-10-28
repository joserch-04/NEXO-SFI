import { useEffect, useId, useRef, useState } from 'react';
import { api, errorText } from './api';
import type { Product } from './catalog';

/** Búsqueda y selección en un solo campo; no registra movimientos por sí misma. */
export function ProductSearch({ value, onSelect, disabled = false, active = true, excludeIds = [], resetAfterSelect = false, label = 'Buscar producto' }: {
  value: Product | null; onSelect: (product: Product | null) => void; disabled?: boolean; active?: boolean; excludeIds?: number[]; resetAfterSelect?: boolean; label?: string;
}) {
  const id = useId();
  const input = useRef<HTMLInputElement>(null), typing = useRef(false);
  const [query, setQuery] = useState(''), [results, setResults] = useState<Product[]>([]);
  const [open, setOpen] = useState(false), [loading, setLoading] = useState(false), [error, setError] = useState(''), [index, setIndex] = useState(-1);
  useEffect(() => { if (typing.current) { typing.current = false; return; } if (value) setQuery(value.name); else setQuery(''); setOpen(false); setIndex(-1); }, [value]);
  useEffect(() => {
    if (!active || !open || !query.trim() || value) return;
    let live = true; setLoading(true); setResults([]); setError(''); setIndex(-1);
    const timer = window.setTimeout(() => {
      api.request<Product[]>('/productos?active=true&pageSize=25&search=' + encodeURIComponent(query.trim()))
        .then(rows => { if (live) setResults(rows.filter(p => p.active && p.trackInventory)); })
        .catch(e => { if (live) setError(errorText(e)); })
        .finally(() => { if (live) setLoading(false); });
    }, 200);
    return () => { live = false; window.clearTimeout(timer); };
  }, [query, active, open, value]);
  const visible = results.filter(p => !excludeIds.includes(p.id));
  const expanded = open && active && !disabled && !value && !!query.trim();
  function choose(product: Product) { onSelect(product); setQuery(resetAfterSelect ? '' : product.name); setOpen(false); setIndex(-1); }
  return <div className="product-search" onBlur={e => { if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setOpen(false); }}>
    <label htmlFor={id}>{label}</label>
    <div className="product-search-field"><input ref={input} id={id} role="combobox" aria-autocomplete="list" aria-expanded={expanded} aria-controls={id + '-results'} aria-activedescendant={expanded && index >= 0 && visible[index] ? id + '-option-' + visible[index].id : undefined} autoComplete="off" placeholder="Nombre, SKU o código de barras" maxLength={180} disabled={disabled} value={query}
      onFocus={() => { if (!value) setOpen(true); }}
      onChange={e => { const text = e.target.value; typing.current = value !== null; onSelect(null); setQuery(text); setResults([]); setLoading(!!text.trim()); setError(''); setIndex(-1); setOpen(true); }}
      onKeyDown={e => {
        if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); setOpen(false); return; }
        if (e.key === 'ArrowDown' || e.key === 'ArrowUp') { e.preventDefault(); setOpen(true); setIndex(old => visible.length ? (e.key === 'ArrowDown' ? (old + 1) % visible.length : (old <= 0 ? visible.length - 1 : old - 1)) : -1); }
        if (e.key === 'Enter' && !value) { e.preventDefault(); if (expanded && visible.length && !loading) choose(visible[index >= 0 ? index : 0]); }
      }}/>{value && <button type="button" className="product-search-clear" aria-label="Cambiar producto seleccionado" disabled={disabled} onClick={() => { onSelect(null); setQuery(''); setOpen(true); input.current?.focus(); }}>×</button>}</div>
    {value && <small className="product-search-selection">{value.sku} · {value.unit} · Seleccionado</small>}
    {expanded && <div className="product-search-menu"><ul role="listbox" id={id + '-results'} aria-label="Productos encontrados">{!loading && visible.map((product, i) => <li key={product.id}><button type="button" role="option" id={id + '-option-' + product.id} aria-selected={index === i} onMouseDown={e => e.preventDefault()} onClick={() => choose(product)}><strong>{product.name}</strong><small>{product.sku} · {product.unit}{product.barcode ? ' · ' + product.barcode : ''}</small></button></li>)}</ul>{loading ? <p role="status">Buscando productos…</p> : error ? <p role="alert">{error}</p> : !visible.length ? <p role="status">No hay coincidencias. Prueba otro nombre o código.</p> : <p className="product-search-help">↑ ↓ para recorrer · Enter para seleccionar</p>}</div>}
  </div>;
}
