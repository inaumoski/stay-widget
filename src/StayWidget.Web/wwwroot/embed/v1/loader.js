(() => {
  'use strict';
  const script = document.currentScript;
  if (!script) return;
  const origin = new URL(script.src).origin;
  if (!origin.startsWith('https://')) return;
  const ids = /^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i;
  function scan() {
    for (const container of [...document.querySelectorAll('[data-staywidget]')].slice(0, 10)) {
      if (container.dataset.staywidgetReady || !ids.test(container.dataset.staywidget)) continue;
      const instance = [...crypto.getRandomValues(new Uint8Array(16))].map(x => x.toString(16).padStart(2, '0')).join('');
      const frame = document.createElement('iframe');
      frame.title = 'Reservation requests';
      frame.width = '100%'; frame.height = '860';
      frame.style.border = '0'; frame.style.display = 'block';
      frame.referrerPolicy = 'no-referrer';
      frame.setAttribute('sandbox', 'allow-scripts allow-same-origin allow-forms allow-popups allow-popups-to-escape-sandbox');
      frame.dataset.staywidgetInstance = instance;
      frame.src = `${origin}/w/${container.dataset.staywidget}?InstanceId=${instance}`;
      let last = -Infinity;
      window.addEventListener('message', event => {
        const data = event.data;
        if (event.origin !== origin || event.source !== frame.contentWindow || !data || typeof data !== 'object' || Array.isArray(data)) return;
        if (Object.keys(data).sort().join(',') !== 'height,instance,type,v' || data.v !== 1 || data.type !== 'staywidget:resize' || data.instance !== instance) return;
        if (!Number.isInteger(data.height) || data.height < 320 || data.height > 1600 || performance.now() - last < 100) return;
        last = performance.now(); frame.height = String(data.height);
      });
      frame.addEventListener('load', () => {
        last = -Infinity;
        frame.contentWindow.postMessage({ v: 1, type: 'staywidget:init', instance }, origin);
      });
      container.dataset.staywidgetReady = '1';
      container.prepend(frame);
    }
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', scan, { once: true });
  else scan();
})();
