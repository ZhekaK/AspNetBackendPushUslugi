export const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, char => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
})[char]);

export const fullName = user => [user.middleName, user.firstName, user.lastName].filter(Boolean).join(' ');
export const dateKey = date => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Moscow' }).format(date);
export const today = () => dateKey(new Date());
export const dateLabel = value => value
  ? new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeZone: 'Europe/Moscow' }).format(new Date(value))
  : '—';
export const timeLabel = value => new Intl.DateTimeFormat('ru-RU', {
  timeStyle: 'short', timeZone: 'Europe/Moscow'
}).format(new Date(value));
export const decimal = value => new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 2 }).format(value);
export const empty = text => `<p class="empty">${escapeHtml(text)}</p>`;
export const title = (name, subtitle = '') => `<div class="page-title"><h1>${escapeHtml(name)}</h1>${subtitle ? `<p class="muted">${escapeHtml(subtitle)}</p>` : ''}</div>`;
export const errorLine = '<p class="form-error" role="alert"></p>';
export const roleLabel = role => ({ Minister: 'Министр', Governor: 'Губернатор', President: 'Президент' })[role] || role;

export function safeLink(value) {
  try {
    const url = new URL(value);
    return ['http:', 'https:'].includes(url.protocol) ? escapeHtml(url.href) : '';
  } catch {
    return '';
  }
}

export function onSubmit(form, submit) {
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (form.dataset.busy) return;
    form.dataset.busy = 'true';
    const buttons = [...form.querySelectorAll('button[type=submit], button:not([type])')].filter(button => !button.disabled);
    const error = form.querySelector('.form-error');
    if (error) error.textContent = '';
    buttons.forEach(button => { button.disabled = true; });
    try {
      await submit(new FormData(form));
    } catch (problem) {
      if (error && problem.name !== 'AbortError') error.textContent = problem.message;
    } finally {
      buttons.forEach(button => { button.disabled = false; });
      delete form.dataset.busy;
    }
  });
}
