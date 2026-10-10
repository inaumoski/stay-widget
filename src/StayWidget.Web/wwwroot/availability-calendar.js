(() => {
  'use strict';
  const calendar = document.querySelector('.availability-calendar');
  if (!calendar) return;
  const arrival = document.querySelector('#Arrival'), departure = document.querySelector('#Departure');
  const guests = document.querySelector('#Guests'), unit = document.querySelector('#UnitId');
  const previous = calendar.querySelector('[data-calendar-prev]'), next = calendar.querySelector('[data-calendar-next]');
  const status = calendar.querySelector('[data-calendar-status]'), grid = calendar.querySelector('[data-calendar-grid]');
  const title = calendar.querySelector('[data-calendar-title]'), retry = calendar.querySelector('[data-calendar-retry]');
  let month = calendar.dataset.month, generation = 0, controller;
  const date = value => new Date(value + 'T00:00:00Z');
  const iso = value => value.toISOString().slice(0, 10);
  const shift = delta => { const d = date(month); d.setUTCMonth(d.getUTCMonth() + delta); return iso(d); };
  const monthTitle = value => new Intl.DateTimeFormat('en', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(date(value));
  async function load() {
    const current = ++generation;
    controller?.abort(); controller = new AbortController();
    grid.replaceChildren(); title.textContent = monthTitle(month);
    previous.disabled = next.disabled = true; retry.hidden = true;
    status.textContent = 'Loading available nights…';
    try {
      const url = new URL(calendar.dataset.endpoint, location.origin);
      url.searchParams.set('month', month); url.searchParams.set('guests', guests?.value || '2');
      if (unit) url.searchParams.set('unitId', unit.value); else if (calendar.dataset.unit) url.searchParams.set('unitId', calendar.dataset.unit);
      const response = await fetch(url, { credentials: 'omit', signal: controller.signal });
      if (!response.ok) throw new Error('Unavailable');
      const data = await response.json();
      if (current !== generation) return;
      if (!Array.isArray(data.nights) || data.nights.length > 31 || data.month !== month) throw new Error('Invalid calendar');
      const table = document.createElement('table'); table.className = 'availability-month';
      const caption = table.createCaption(); caption.textContent = monthTitle(month) + ' · night availability';
      const heading = table.createTHead().insertRow();
      for (const name of ['Mon','Tue','Wed','Thu','Fri','Sat','Sun']) { const th = document.createElement('th'); th.scope = 'col'; th.textContent = name; heading.append(th); }
      const body = table.createTBody(); let row = body.insertRow(), cells = 0;
      const padding = (date(month).getUTCDay() + 6) % 7;
      for (let i = 0; i < padding; i++) { row.insertCell(); cells++; }
      for (const night of data.nights) {
        if (!/^\d{4}-\d{2}-\d{2}$/.test(night.date) || typeof night.available !== 'boolean') throw new Error('Invalid night');
        if (cells === 7) { row = body.insertRow(); cells = 0; }
        const cell = row.insertCell(); cells++;
        const text = date(night.date).getUTCDate() + (night.available ? ' A' : ' ×');
        cell.className = night.available ? 'available-night' : 'unavailable-night';
        if (night.available) {
          const button = document.createElement('button'); button.type = 'button'; button.textContent = text;
          button.setAttribute('aria-label', night.date + ': Available night. Choose check-in.');
          button.addEventListener('click', () => {
            arrival.value = night.date;
            const min = Number(unit?.selectedOptions[0]?.dataset.minNights || calendar.dataset.minNights || 1);
            const end = date(night.date); end.setUTCDate(end.getUTCDate() + Math.max(1, Math.min(28, min)));
            departure.value = iso(end);
            status.textContent = 'Check-in selected. Adjust check-out and check availability for the complete stay.';
            departure.focus();
          }); cell.append(button);
        } else { cell.textContent = text; cell.setAttribute('aria-label', night.date + ': Unavailable night'); }
      }
      while (cells < 7) { row.insertCell(); cells++; }
      grid.append(table); previous.disabled = month <= data.firstMonth; next.disabled = month >= data.lastMonth;
      status.textContent = 'Available nights for the selected accommodation and guest count. Choose check-in or use the date fields.';
    } catch (error) {
      if (error.name === 'AbortError' || current !== generation) return;
      grid.replaceChildren(); status.textContent = 'Calendar unavailable. Use the date fields and Check availability, or retry.'; retry.hidden = false;
    }
  }
  previous.addEventListener('click', () => { month = shift(-1); load(); });
  next.addEventListener('click', () => { month = shift(1); load(); });
  retry.addEventListener('click', load); unit?.addEventListener('change', load); guests?.addEventListener('change', load);
  // Refresh visible inventory after owner decisions without claiming a live date hold.
  document.addEventListener('visibilitychange', () => { if (!document.hidden) load(); });
  setInterval(() => { if (!document.hidden && !calendar.contains(document.activeElement)) load(); }, 60000);
  load();
})();
