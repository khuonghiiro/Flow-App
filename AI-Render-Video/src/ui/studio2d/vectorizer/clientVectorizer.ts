// @ts-ignore
import ImageTracer from 'imagetracerjs';
import { VectorizerParams } from './types';

/**
 * High-performance client-side fallback vectorizer using ImageTracerJS.
 * Ensures image-to-SVG conversion always works even when the Python VTracer sidecar server is offline.
 */
export function traceImageToSvgClientSide(
  imageDataUrl: string,
  params?: Partial<VectorizerParams>
): Promise<string> {
  return new Promise((resolve, reject) => {
    try {
      const numColors = Math.max(4, Math.min(64, Math.pow(2, params?.colorPrecision ?? 6)));
      const blurRadius = params?.edgeSmoothing ? Math.max(0, Math.round(params.edgeSmoothing)) : 0;
      const ltres = params?.lengthThreshold ? Math.max(0.5, params.lengthThreshold) : 1;
      const qtres = params?.cornerThreshold ? Math.max(0.5, params.cornerThreshold / 10) : 1;
      const pathomit = params?.filterSpeckle ? Math.max(1, params.filterSpeckle * 2) : 2;

      const options: any = {
        corsenabled: false,
        numberofcolors: numColors,
        colorquantcycles: 3,
        mincolorratio: 0,
        blurradius: blurRadius,
        blurdelta: 20,
        ltres: ltres,
        qtres: qtres,
        pathomit: pathomit,
        rightangleenhance: true,
        layering: params?.hierarchical === 'cutout' ? 1 : 0,
        roundcoords: 2,
        scale: 1,
        viewbox: true,
        linefilter: false,
      };

      const img = new Image();
      img.crossOrigin = 'anonymous';
      img.onload = () => {
        try {
          const origW = img.naturalWidth || img.width || 512;
          const origH = img.naturalHeight || img.height || 512;
          const maxDim = 640;
          let drawW = origW;
          let drawH = origH;
          if (Math.max(origW, origH) > maxDim) {
            const scale = maxDim / Math.max(origW, origH);
            drawW = Math.round(origW * scale);
            drawH = Math.round(origH * scale);
          }

          const canvas = document.createElement('canvas');
          canvas.width = drawW;
          canvas.height = drawH;
          const ctx = canvas.getContext('2d');
          if (!ctx) {
            return reject(new Error('Không thể tạo 2D Canvas context'));
          }
          ctx.drawImage(img, 0, 0, drawW, drawH);
          const imgData = ctx.getImageData(0, 0, drawW, drawH);

          let tracerInstance: any = null;
          if (ImageTracer && typeof ImageTracer.imagedataToSVG === 'function') {
            tracerInstance = ImageTracer;
          } else if (typeof ImageTracer === 'function') {
            tracerInstance = new (ImageTracer as any)();
          } else if (ImageTracer && ImageTracer.default) {
            tracerInstance = typeof ImageTracer.default === 'function' ? new ImageTracer.default() : ImageTracer.default;
          } else if (typeof window !== 'undefined' && (window as any).ImageTracer) {
            tracerInstance = (window as any).ImageTracer;
          }

          if (!tracerInstance || typeof tracerInstance.imagedataToSVG !== 'function') {
            return reject(new Error('ImageTracerJS instance không hợp lệ'));
          }

          const svgString = tracerInstance.imagedataToSVG(imgData, options);
          if (svgString && typeof svgString === 'string' && svgString.includes('<svg')) {
            resolve(svgString);
          } else {
            reject(new Error('ImageTracer không tạo được dữ liệu SVG'));
          }
        } catch (e: any) {
          reject(new Error(`Lỗi xử lý Canvas ImageTracer: ${e?.message || e}`));
        }
      };
      img.onerror = () => {
        reject(new Error('Không thể đọc dữ liệu ảnh vào Canvas để vector hóa'));
      };
      img.src = imageDataUrl;
    } catch (err: any) {
      reject(new Error(`Lỗi khởi tạo ImageTracer: ${err?.message || err}`));
    }
  });
}
