import { useEffect, useState } from 'react';
import { api } from './api';

export function ProductImage({ id, reference, preview }: { id: number; reference?: string | null; preview?: string | null }) {
  const [url, setUrl] = useState('');
  useEffect(() => {
    let live = true, resource = ''; setUrl('');
    if (reference && preview === undefined) api.request<Blob>(`/productos/${id}/imagen`).then(blob => { resource = URL.createObjectURL(blob); if (live) setUrl(resource); else URL.revokeObjectURL(resource); }).catch(() => {});
    return () => { live = false; if (resource) URL.revokeObjectURL(resource); };
  }, [id, reference, preview]);
  return <img className="product-photo" src={preview || url || '/product-placeholder.svg'} alt={preview || url ? '' : 'Sin fotografía'}/>;
}
