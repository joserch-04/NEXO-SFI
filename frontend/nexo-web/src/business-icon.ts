// El archivo original se procesa en el navegador; al servidor llega un icono pequeño.
export async function prepareBusinessIcon(file: File): Promise<string> {
  if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) throw new Error('Elige una imagen PNG, JPG o WebP.');
  if (file.size > 2 * 1024 * 1024) throw new Error('La imagen debe pesar como máximo 2 MB.');
  const url = URL.createObjectURL(file);
  try {
    const image = new Image(); image.src = url;
    try { await image.decode(); } catch { throw new Error('No se pudo leer la imagen. Elige otro archivo.'); }
    if (!image.naturalWidth || !image.naturalHeight || image.naturalWidth > 4096 || image.naturalHeight > 4096) throw new Error('La imagen debe medir como máximo 4096 × 4096 píxeles.');
    const scale = Math.min(1, 256 / Math.max(image.naturalWidth, image.naturalHeight));
    const canvas = document.createElement('canvas'); canvas.width = Math.max(1, Math.round(image.naturalWidth * scale)); canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
    const context = canvas.getContext('2d'); if (!context) throw new Error('No se pudo procesar la imagen en este navegador.');
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    const result = canvas.toDataURL('image/png');
    if (!result.startsWith('data:image/png;base64,') || result.length > 174786) throw new Error('La imagen es demasiado compleja. Elige una imagen más sencilla.');
    return result;
  } finally { URL.revokeObjectURL(url); }
}
