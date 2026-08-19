import React from 'react';
import ReactDOM from 'react-dom/client';
import { App } from './App';
import './styles/studio.css';

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

console.log('Initializing FlowMy AI Studio...');

const rootElement = document.getElementById('root');
if (!rootElement) {
  console.error('Root element #root not found!');
} else {
  ReactDOM.createRoot(rootElement).render(
    <React.StrictMode>
      <App />
    </React.StrictMode>
  );
}

