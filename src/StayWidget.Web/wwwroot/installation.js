'use strict';
for (const button of document.querySelectorAll('[data-copy-snippet]')) {
  button.addEventListener('click', async () => {
    const field = document.getElementById(button.dataset.copySnippet);
    try { await navigator.clipboard.writeText(field.value); document.querySelector('#snippet-result').textContent = 'Snippet copied. Paste it into a permitted HTML block.'; }
    catch { field.focus(); field.select(); document.querySelector('#snippet-result').textContent = 'Select and copy this snippet using your keyboard.'; }
  });
}
