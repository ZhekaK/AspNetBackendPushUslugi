import { dateLabel, empty, errorLine, escapeHtml as e, onSubmit, title } from './ui.js';

const path = '/api/candidates';
export const candidateFullName = person => [person.lastName, person.firstName, person.middleName].filter(Boolean).join(' ');
export const sexLabel = sex => ({ Man: 'Мужской', Women: 'Женский' })[sex] || '—';

export async function candidates({ main, api, signal, notify, canReviewCandidates, reviewCandidates }) {
  const records = await api(path);
  if (signal.aborted) return;
  main.innerHTML = title('Реестр кандидатов') + `<div class="stack">
    <div class="actions"><button class="btn" id="propose-candidate" type="button">Предложить кандидата</button>
      ${canReviewCandidates ? '<button class="btn secondary" id="review-candidates" type="button">Предложенные кандидаты</button>' : ''}</div>
    ${records.length ? `<div class="table-panel" role="region" aria-label="Реестр кандидатов" tabindex="0">
      <table class="data-table"><thead><tr><th scope="col">ФИО</th><th scope="col">Пол</th>
        <th scope="col">Дата выдачи</th><th scope="col">Кем выдано</th></tr></thead><tbody>
        ${records.map(person => `<tr><th scope="row">${e(candidateFullName(person))}</th>
          <td>${sexLabel(person.sex)}</td><td>${e(dateLabel(person.giveDate))}</td><td>${e(person.givePlace)}</td></tr>`).join('')}
      </tbody></table></div>` : empty('Удостоверения кандидатов пока не выдавались.')}
    <dialog class="document-dialog proposal-dialog" aria-labelledby="proposal-title">
      <div class="stack">
        <div class="row"><h2 id="proposal-title">Предложить кандидата</h2>
          <form method="dialog"><button class="btn secondary" type="submit">Закрыть</button></form></div>
        <form id="candidate-proposal-form" class="stack">
          <label>ФИО<input name="fullName" maxlength="240" required autofocus></label>
          <label>Причина<textarea name="reason" maxlength="2000" rows="4" required></textarea></label>
          <div><button class="btn" type="submit">Отправить президенту</button></div>${errorLine}
        </form>
      </div>
    </dialog></div>`;
  const dialog = main.querySelector('dialog');
  const form = main.querySelector('#candidate-proposal-form');
  main.querySelector('#propose-candidate').onclick = () => dialog.showModal();
  main.querySelector('#review-candidates')?.addEventListener('click', reviewCandidates);
  onSubmit(form, async data => {
    await api(`${path}/proposals`, { method: 'POST', body: { fullName: data.get('fullName'), reason: data.get('reason') } });
    if (signal.aborted) return;
    form.reset();
    dialog.close();
    notify('Предложение отправлено президенту.');
  });
}

export function showCandidateReview(dialog, api, seen) {
  if (dialog.open) return;
  const controller = new AbortController();
  const { signal } = controller;
  const content = dialog.querySelector('#candidate-review-list');
  const error = dialog.querySelector('.form-error');
  const refresh = dialog.querySelector('#candidate-review-refresh');
  content.innerHTML = '';
  error.textContent = '';
  dialog.addEventListener('close', () => {
    controller.abort();
    content.innerHTML = '';
  }, { once: true });
  dialog.showModal();

  async function load() {
    refresh.disabled = true;
    error.textContent = '';
    try {
      const proposals = await api(`${path}/proposals`, { signal });
      if (signal.aborted) return;
      seen(proposals.reduce((latest, row) => row.approved === null ? Math.max(latest, row.id) : latest, 0));
      content.innerHTML = proposals.length ? proposals.map(row => `<article class="panel stack">
        <div class="row"><h3>${e(row.fullName)}</h3><span class="badge">${row.approved === null ? 'На рассмотрении' : row.approved ? 'Одобрено' : 'Отклонено'}</span></div>
        <p class="proposal-reason">${e(row.reason)}</p>
        <p class="meta">Предложил(а) ${e(candidateFullName(row.author))} · ${e(dateLabel(row.createdAt))}</p>
        ${row.approved === null ? `<div class="actions" data-proposal="${row.id}">
          <button class="btn" type="button" data-approved="true">Одобрить</button>
          <button class="btn secondary" type="button" data-approved="false">Отклонить</button></div>`
          : `<p class="meta">Рассмотрено ${e(dateLabel(row.reviewedAt))}</p>`}
      </article>`).join('') : empty('Предложений пока нет.');
    } catch (problem) {
      if (!signal.aborted) error.textContent = problem.message;
    } finally {
      refresh.disabled = false;
    }
  }

  refresh.onclick = load;
  content.onclick = async event => {
    const button = event.target.closest('[data-approved]');
    if (!button || refresh.disabled) return;
    const actions = button.closest('[data-proposal]');
    const buttons = actions.querySelectorAll('button');
    buttons.forEach(control => { control.disabled = true; });
    refresh.disabled = true;
    error.textContent = '';
    try {
      await api(`${path}/proposals/${actions.dataset.proposal}/decision`, {
        method: 'POST', body: { approved: button.dataset.approved === 'true' }, signal
      });
      if (!signal.aborted) await load();
    } catch (problem) {
      if (!signal.aborted) error.textContent = problem.message;
    } finally {
      buttons.forEach(control => { control.disabled = false; });
      refresh.disabled = false;
    }
  };
  void load();
}
