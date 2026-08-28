import React, { Suspense } from 'react';
import ReactDOM from 'react-dom/client';
import './styles/studio.css';

// Lazy-load the heavy App component so the splash screen shows immediately
// while Vite transforms ~200 source modules in dev mode
const App = React.lazy(() =>
  import('./App').then(m => ({ default: m.App }))
);

window.addEventListener('error', (e) => {
  console.error('[Global Window Error]:', e.error || e.message, e);
  document.body.innerHTML += `<div style="position:fixed;top:0;left:0;z-index:9999;background:red;color:white;padding:20px;font-family:monospace;">
    <h3>Fatal Error:</h3>
    <pre>${e.error?.stack || e.message}</pre>
  </div>`;
});

window.addEventListener('unhandledrejection', (e) => {
  console.error('[Unhandled Promise Rejection]:', e.reason);
  document.body.innerHTML += `<div style="position:fixed;bottom:0;left:0;z-index:9999;background:orange;color:white;padding:20px;font-family:monospace;">
    <h3>Promise Rejection:</h3>
    <pre>${e.reason?.stack || e.reason}</pre>
  </div>`;
});

/** Remove the HTML splash screen once React has mounted */
function dismissSplash() {
  const splash = document.getElementById('splash');
  if (splash) {
    splash.style.transition = 'opacity 0.3s';
    splash.style.opacity = '0';
    setTimeout(() => splash.remove(), 300);
  }
}

console.log('Initializing FlowMy AI Studio...');

const rootElement = document.getElementById('root');
if (!rootElement) {
  console.error('Root element #root not found!');
} else {
  ReactDOM.createRoot(rootElement).render(
    <React.StrictMode>
      <Suspense fallback={null}>
        <App />
        <SplashDismisser />
      </Suspense>
    </React.StrictMode>
  );
}

/** Tiny component that dismisses splash on mount and triggers adaptive prefetch */
function SplashDismisser() {
  React.useEffect(() => {
    dismissSplash();
    // On powerful devices, prefetch all lazy chunks during idle time
    // so tab switching feels instant. Weak devices skip this.
    import('./core/performance/AdaptivePrefetcher').then(({ prefetchLazyChunks }) => {
      prefetchLazyChunks(2500);
    });
  }, []);
  return null;
}
