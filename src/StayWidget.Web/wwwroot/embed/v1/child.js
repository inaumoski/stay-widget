(() => {
  'use strict';
  const context = document.querySelector('#embed-context');
  if (!context || window.parent === window) return;
  const instance = context.dataset.instance;
  if (!/^[a-f0-9]{32}$/i.test(instance || '')) return;
  let allowed;
  try { allowed = JSON.parse(context.dataset.origins); } catch { return; }
  let parentOrigin = null, pending = null, lastHeight = 0, lastSent = -Infinity;
  function measure() {
    pending = null;
    if (!parentOrigin) return;
    const height = Math.max(320, Math.min(640, Math.ceil(document.body.getBoundingClientRect().height)));
    if (height === lastHeight || performance.now() - lastSent < 100) return;
    lastHeight = height; lastSent = performance.now();
    window.parent.postMessage({ v: 1, type: 'staywidget:resize', instance, height }, parentOrigin);
  }
  function schedule() { if (!pending) pending = setTimeout(measure, 120); }
  window.addEventListener('message', event => {
    const data = event.data;
    if (event.source !== window.parent || !allowed.includes(event.origin) || !data || typeof data !== 'object' || Array.isArray(data)) return;
    if (Object.keys(data).sort().join(',') !== 'instance,type,v' || data.v !== 1 || data.type !== 'staywidget:init' || data.instance !== instance) return;
    if (parentOrigin && parentOrigin !== event.origin) return;
    parentOrigin = event.origin; schedule();
  });
  if (typeof ResizeObserver === 'function') new ResizeObserver(schedule).observe(document.body);
  window.addEventListener('resize', schedule);
})();
