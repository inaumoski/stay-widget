document.querySelectorAll('[data-season-preset]').forEach(button => button.addEventListener('click', () => {
  const year = Number(document.querySelector('#season-year').value);
  if (!Number.isInteger(year) || year < 2000 || year > 2100) return;
  document.querySelector('#OpenFrom').value = year + (button.dataset.seasonPreset === 'year' ? '-01-01' : '-11-01');
  document.querySelector('#OpenTo').value = (year + 1) + '-01-01';
  document.querySelector('#OpenFrom').focus();
}));
