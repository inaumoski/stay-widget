(() => {
  'use strict';
  const calendar = document.querySelector('.availability-calendar');
  if (!calendar) return;
  const arrival = document.querySelector('#Arrival'), departure = document.querySelector('#Departure');
  const guests = document.querySelector('#Guests'), unit = document.querySelector('#UnitId');
  const previous = calendar.querySelector('[data-calendar-prev]'), next = calendar.querySelector('[data-calendar-next]');
  const status = calendar.querySelector('[data-calendar-status]'), grid = calendar.querySelector('[data-calendar-grid]');
  const title = calendar.querySelector('[data-calendar-title]'), retry = calendar.querySelector('[data-calendar-retry]');
  let month = calendar.dataset.month, generation = 0, controller, choosingEnd = false;
  const date = value => new Date(value + 'T00:00:00Z');
  const iso = value => value.toISOString().slice(0, 10);
  const shift = delta => { const d = date(month); d.setUTCMonth(d.getUTCMonth() + delta); return iso(d); };
  const monthTitle = value => new Intl.DateTimeFormat('en', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(date(value));
  function paintSelection() {
    const from = arrival.value, to = departure.value, range = from && to && to > from;
    for (const cell of grid.querySelectorAll('[data-night]')) {
      const day = cell.dataset.night, available = cell.dataset.available === 'true';
      const start = day === from, end = range && day === to, inside = range && day >= from && day < to;
      cell.classList.toggle('selected-night', !!inside);
      cell.classList.toggle('range-start', start); cell.classList.toggle('range-end', !!end);
      const button = cell.querySelector('button'), plain = cell.querySelector('[data-night-plain]');
      const checkoutOnly = choosingEnd && from && day > from;
      button.hidden = !available && !checkoutOnly; plain.hidden = !button.hidden;
      const description = day + ': ' + (available ? 'Available night.' : 'Unavailable night.');
      button.setAttribute('aria-label', description + (choosingEnd ? ' Choose check-out' + (!available ? ' (departure only)' : '') + '.' : ' Choose check-in.') + (start ? ' Selected check-in.' : end ? ' Selected check-out.' : inside ? ' Selected stay night.' : ''));
      button.setAttribute('aria-pressed', String(!!(start || end || inside)));
      for (const badge of cell.querySelectorAll('[data-range-badge]')) badge.textContent = start ? 'In' : end ? 'Out' : '';
    }
    const summary = calendar.querySelector('[data-calendar-selection]');
    summary.textContent = from ? range ? 'Selected stay: ' + from + ' → ' + to + '. Check-out is not an occupied night.' : 'Check-in: ' + from + '. Choose a later check-out date.' : 'No dates selected. Choose check-in first.';
  }
  function selectDay(night) {
    if (!choosingEnd) {
      if (!night.available) return;
      arrival.value = night.date; departure.value = ''; choosingEnd = true;
      status.textContent = 'Check-in selected. Next click chooses check-out. An unavailable night can be a departure date only.';
    } else {
      if (!arrival.value || night.date <= arrival.value) {
        status.textContent = 'Check-out must be later than check-in. Choose a later date; the next click still chooses check-out.';
        return;
      }
      departure.value = night.date; choosingEnd = false;
      status.textContent = 'Stay selected. Check availability for the complete stay, including stay-length rules. The next calendar click starts a new check-in.';
    }
    paintSelection();
    // A departure-only button becomes hidden after selection; preserve keyboard focus.
    if (!night.available) departure.focus();
  }
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
        cell.dataset.night = night.date; cell.dataset.available = String(night.available);
        const button = document.createElement('button'); button.type = 'button'; button.append(document.createTextNode(text));
        const plain = document.createElement('span'); plain.dataset.nightPlain = ''; plain.append(document.createTextNode(text));
        for (const node of [button, plain]) { const badge = document.createElement('small'); badge.dataset.rangeBadge = ''; node.append(badge); }
        button.addEventListener('click', () => selectDay(night)); cell.append(button, plain);
        if (!night.available) cell.setAttribute('aria-label', night.date + ': Unavailable night');
      }
      while (cells < 7) { row.insertCell(); cells++; }
      grid.append(table); previous.disabled = month <= data.firstMonth; next.disabled = month >= data.lastMonth;
      paintSelection();
      status.textContent = 'Available nights for the selected accommodation and guest count. Next click chooses ' + (choosingEnd ? 'check-out.' : 'check-in.') + ' Use Check availability to verify the complete stay.';
    } catch (error) {
      if (error.name === 'AbortError' || current !== generation) return;
      grid.replaceChildren(); status.textContent = 'Calendar unavailable. Use the date fields and Check availability, or retry.'; retry.hidden = false;
    }
  }
  previous.addEventListener('click', () => { month = shift(-1); load(); });
  next.addEventListener('click', () => { month = shift(1); load(); });
  retry.addEventListener('click', load);
  const changeInventory = () => { choosingEnd = false; load(); };
  unit?.addEventListener('change', changeInventory); guests?.addEventListener('change', changeInventory);
  const changeDates = () => { choosingEnd = false; paintSelection(); status.textContent = 'Date fields updated. The next calendar click chooses check-in. Check availability for the complete stay.'; };
  arrival.addEventListener('change', changeDates); departure.addEventListener('change', changeDates);
  // Refresh visible inventory after owner decisions without claiming a live date hold.
  document.addEventListener('visibilitychange', () => { if (!document.hidden) load(); });
  setInterval(() => { if (!document.hidden && !calendar.contains(document.activeElement)) load(); }, 60000);
  load();
})();
