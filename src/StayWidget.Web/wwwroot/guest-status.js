"use strict";
const box = document.querySelector('#status');
const actions = document.querySelector('#guest-actions');
const parts = location.hash.slice(1).split('.');
const token = parts[0] || '';
const actionToken = parts.length === 2 ? parts[1] : '';
const valid = value => /^[a-f0-9]{64}$/i.test(value);
async function load() {
  actions.replaceChildren();
  try {
    if (!valid(token)) throw new Error();
    const res = await fetch('/api/guest/status', { headers: { Authorization: `Bearer ${token}` }, credentials: 'omit', cache: 'no-store' });
    if (!res.ok) throw new Error();
    const s = await res.json(); box.replaceChildren();
    const heading = document.createElement('h2');
    heading.textContent = s.state === 'Pending' ? 'Request received — awaiting approval' : s.state === 'Accepted' ? 'Reservation confirmed' : s.state;
    box.append(heading);
    for (const text of [`${s.property} · ${s.unit}`, `${s.arrival} → ${s.departure} · ${s.guests} guest(s)`,
      s.state === 'Pending' ? 'Your dates are not held. Decision deadline: ' + new Date(s.expires).toLocaleString() : '',
      s.amount !== null ? `Agreed total: ${s.amount} ${s.currency}` : '', s.reason,
      s.cancellationRequested ? 'Cancellation requested — awaiting owner review. Your reservation remains confirmed and its dates remain occupied.' : '',
      `Property contact: ${s.contact}`]) {
      if (text) { const p = document.createElement('p'); p.textContent = text; box.append(p); }
    }
    if (valid(actionToken) && s.canWithdraw) actionForm('withdraw', 'Withdraw pending request', 'I want to withdraw this pending enquiry. It has not reserved any dates.');
    if (valid(actionToken) && s.canRequestCancellation && !s.cancellationRequested)
      actionForm('request-cancellation', 'Request cancellation', 'I understand that this only asks the owner to review cancellation. The reservation and its inventory remain confirmed until the owner cancels.');
  } catch { box.textContent = 'This private link is unavailable, revoked or expired. Contact the property, or try again shortly.'; }
}
function actionForm(action, label, explanation) {
  const form = document.createElement('form'); form.className = 'card';
  const heading = document.createElement('h2'); heading.textContent = label;
  const check = document.createElement('input'); check.type = 'checkbox'; check.required = true;
  const review = document.createElement('label'); review.className = 'check'; review.append(check, document.createTextNode(explanation));
  const button = document.createElement('button'); button.textContent = label;
  form.append(heading, review, button); actions.append(form);
  form.addEventListener('submit', async event => {
    event.preventDefault(); if (!check.checked) return;
    button.disabled = true; const result = document.querySelector('#action-result');
    try {
      const res = await fetch(`/api/guest/${action}`, { method: 'POST', credentials: 'omit', cache: 'no-store',
        headers: { Authorization: `Bearer ${actionToken}`, 'Content-Type': 'application/json' }, body: JSON.stringify({ confirmed: true }) });
      if (!res.ok) {
        let message = 'The action could not be recorded. Refresh status, check your saved link or contact the property before retrying.';
        if (res.status === 409) message = (await res.json()).message;
        result.textContent = message; return;
      }
      result.textContent = action === 'withdraw' ? 'Withdrawal recorded.' : 'Cancellation request recorded; the reservation is still confirmed.';
      await load(); result.focus();
    } catch { result.textContent = 'Delivery is uncertain. Refresh status before retrying or contact the property.'; }
    finally { button.disabled = false; }
  });
}
async function copy(link) {
  const result = document.querySelector('#copy-result');
  try { await navigator.clipboard.writeText(link); result.textContent = 'Private link copied. Save it somewhere safe.'; }
  catch {
    result.textContent = 'Clipboard access is unavailable. Copy the address from your browser, or select the view-only link below.';
    const field = document.createElement('input'); field.readOnly = true; field.value = link;
    field.setAttribute('aria-label', 'Private link to copy'); result.append(field); field.focus(); field.select();
  }
}
document.querySelector('#copy').addEventListener('click', () => copy(location.href));
document.querySelector('#copy-view').addEventListener('click', () => copy(location.href.split('#')[0] + '#' + token));
document.querySelector('#action-result').tabIndex = -1;
load();
