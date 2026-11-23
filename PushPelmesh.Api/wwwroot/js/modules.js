import { dateKey, dateLabel, decimal, empty, errorLine, escapeHtml as e, onSubmit, safeLink, title, today } from './ui.js';

const votesPath = '/api/votes/polls';
const audienceNames = { RegularUsers: 'Обычные пользователи', Ministers: 'Министры' };

export async function votes({ main, api, id, user, roles, notify }) {
  if (id) {
    const showPoll = poll => {
      main.innerHTML = `<div class="section-head"><a href="#votes">← К голосованиям</a><span class="badge">${poll.isClosed ? 'Завершено' : 'Открыто'}</span></div>`
        + title(poll.title) + `<section class="panel stack">
        ${poll.description ? `<p class="preline">${e(poll.description)}</p>` : ''}
        <p class="meta">${e(poll.createdByUser)} · до ${dateLabel(poll.endDate)} включительно по Москве</p>
        <p class="meta">${poll.audienceGroups.map(group => e(audienceNames[group] || group)).join(' · ')}</p>
        <form id="vote-form" class="stack">
          ${poll.canVote ? `<h2>${poll.isMultipleChoice ? 'Выберите один или несколько вариантов' : 'Выберите один вариант'}</h2>` : '<h2>Результаты</h2>'}
          ${poll.options.map(option => `<div class="poll-option">
            ${poll.canVote ? `<label class="check"><input type="${poll.isMultipleChoice ? 'checkbox' : 'radio'}" name="option" value="${option.id}" ${poll.isMultipleChoice ? '' : 'required'}>${e(option.text)}</label>` :
        `<div class="row"><strong>${e(option.text)}${option.isSelected ? ' ✓' : ''}</strong><span class="meta">${option.votes} · ${decimal(option.percent)}%</span></div>
            <progress max="100" value="${option.percent}" aria-label="${e(option.text)}"></progress>`}
          </div>`).join('')}
          ${poll.canVote ? `<div><button class="btn" type="submit">Проголосовать</button></div>${errorLine}` :
        `<p class="meta">Всего ответов: ${poll.totalVotes}${poll.hasVoted ? ' · Ваш голос учтён' : ''}</p>`}
        </form>
      </section>`;
      if (poll.canVote) onSubmit(main.querySelector('#vote-form'), async data => {
        const optionIds = data.getAll('option').map(Number);
        if (!optionIds.length) throw new Error('Выберите хотя бы один вариант.');
        try {
          const updated = await api(`${votesPath}/${id}/vote`, { method: 'POST', body: { optionIds } });
          showPoll(updated.poll);
          notify('Голос учтён.');
        } catch (error) {
          if (['Poll is closed', 'User already voted'].includes(error.data?.message)) {
            showPoll((await api(`${votesPath}/${id}`)).poll);
            notify(error.message);
            return;
          }
          throw error;
        }
      });
    };
    showPoll((await api(`${votesPath}/${id}`)).poll);
    return;
  }

  const { polls } = await api(votesPath);
  const canChoose = user.type === 'Admin' || roles.some(role => ['President', 'Governor'].includes(role.roleType));
  main.innerHTML = title('Голосования') + `<details class="panel block"><summary>Создать голосование</summary>
    <form id="create-poll" class="stack">
      <label>Заголовок<input name="title" maxlength="256" required></label>
      <label>Описание<textarea name="description" maxlength="2048" rows="3"></textarea></label>
      <div class="fields"><label>Дата окончания по Москве<input name="endDate" type="date" min="${today()}" value="${today()}" required></label>
        <label class="check"><input name="multiple" type="checkbox">Несколько ответов</label></div>
      <div class="stack" id="poll-options"></div>
      <div><button class="btn secondary" id="add-option" type="button">Добавить вариант</button></div>
      ${canChoose ? `<fieldset><legend>Аудитория</legend>${Object.entries(audienceNames).map(([key, text]) => `<label class="check"><input name="audience" type="checkbox" value="${key}" checked>${text}</label>`).join('')}</fieldset>` : ''}
      <div><button class="btn" type="submit">Создать</button></div>${errorLine}
    </form>
  </details>${[false, true].map(closed => {
    const items = polls.filter(poll => poll.isClosed === closed);
    return `<section class="block"><div class="section-head"><h2>${closed ? 'Завершённые' : 'Открытые'}</h2></div>
      <div class="rows">${items.length ? items.map(poll => `<a class="panel row" href="#votes/${poll.id}">
        <div><h3>${e(poll.title)}</h3><p class="meta">До ${dateLabel(poll.endDate)} · ${poll.isMultipleChoice ? 'Несколько вариантов' : 'Один вариант'}</p></div>
        <span class="badge">${poll.hasVoted ? 'Голос учтён' : poll.isClosed ? 'Результаты' : 'Открыть'}</span></a>`).join('') : empty(closed ? 'Завершённых голосований пока нет.' : 'Открытых голосований пока нет.')}</div></section>`;
  }).join('')}`;
  const options = main.querySelector('#poll-options');
  const renumber = () => [...options.children].forEach((row, index) => {
    row.querySelector('span').textContent = `Вариант ${index + 1}`;
    row.querySelector('button').disabled = options.children.length <= 2;
  });
  function addOption(focus = true) {
    const row = document.createElement('div');
    row.className = 'row';
    row.innerHTML = '<label class="grow"><span></span><input name="option" maxlength="256" required></label><button class="icon-button" type="button" aria-label="Удалить вариант">×</button>';
    options.append(row);
    renumber();
    row.querySelector('button').onclick = () => { row.remove(); renumber(); };
    if (focus) row.querySelector('input').focus();
  }
  addOption(false);
  addOption(false);
  main.querySelector('#add-option').onclick = () => addOption();
  onSubmit(main.querySelector('#create-poll'), async data => {
    const audienceGroups = data.getAll('audience');
    if (canChoose && !audienceGroups.length) throw new Error('Выберите хотя бы одну группу аудитории.');
    const result = await api(votesPath, { method: 'POST', body: {
      title: data.get('title').trim(), description: data.get('description').trim(), endDate: data.get('endDate'),
      isMultipleChoice: data.has('multiple'), options: data.getAll('option').map(option => option.trim()), audienceGroups
    } });
    location.hash = `votes/${result.poll.id}`;
    notify('Голосование создано.');
  });
}

const eventPath = '/api/calendar/events';
export async function calendar({ main, api, user, roles, id, notify }) {
  const president = roles.some(role => role.roleType === 'President');
  const minister = roles.some(role => role.roleType === 'Minister');
  const deepLink = String(id || '').replace(/^(\d{4})(\d{2})(\d{2})$/, '$1-$2-$3');
  let selected = /^\d{4}-\d{2}-\d{2}$/.test(deepLink) && !Number.isNaN(Date.parse(deepLink)) ? deepLink : today();
  let month = selected.slice(0, 7);
  let events = [];
  main.innerHTML = title('Календарь') + `<div class="split">
    <section class="panel stack"><div class="actions push">
      <button class="icon-button" type="button" id="prev" aria-label="Предыдущий месяц">←</button>
      <h2 id="month"></h2><button class="icon-button" type="button" id="next" aria-label="Следующий месяц">→</button>
    </div><div id="calendar" class="calendar"></div>
    <div class="calendar-legend meta"><span class="meeting">Встреча</span><span class="birthday">День рождения</span></div>
    <p id="calendar-error" class="form-error" role="alert"></p></section>
    <section class="stack"><div id="events" class="panel stack"></div>
    ${president || minister ? `<details class="panel"><summary>Добавить событие</summary>
      <form id="create-event" class="stack">
        <label>Название<input name="title" maxlength="256" required></label>
        <div class="fields"><label>Тип<select name="type"><option value="1">Встреча</option>${president ? '<option value="0">День рождения</option>' : ''}</select></label>
          <label>Дата<input name="date" type="date" value="${selected}" required></label></div>
        <label>Описание<textarea name="description" rows="3"></textarea></label>
        <div><button class="btn" type="submit">Добавить событие</button></div>${errorLine}
      </form></details>` : ''}</section>
  </div>`;
  const calendarGrid = main.querySelector('#calendar');
  const list = main.querySelector('#events');
  const errorLineElement = main.querySelector('#calendar-error');
  const form = main.querySelector('#create-event');
  const monthDate = () => new Date(`${month}-01T12:00:00Z`);
  const dateForDay = day => `${month}-${String(day).padStart(2, '0')}`;
  let loadCount = 0;
  const redraw = () => {
    const start = monthDate();
    const offset = (start.getUTCDay() + 6) % 7;
    const last = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth() + 1, 0)).getUTCDate();
    main.querySelector('#month').textContent = new Intl.DateTimeFormat('ru-RU', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(start);
    const days = Array.from({ length: last }, (_, number) => {
      const day = dateForDay(number + 1);
      const meeting = events.some(event => event.date === day && event.type === 1);
      const birthday = events.some(event => event.date === day && event.type === 0);
      const markers = [meeting && 'есть встречи', birthday && 'есть дни рождения'].filter(Boolean);
      return `<button type="button" class="${meeting ? 'meeting' : ''} ${birthday ? 'birthday' : ''}" data-day="${day}" aria-label="${[dateLabel(day), ...markers].join(', ')}" aria-pressed="${day === selected}" ${day === today() ? 'aria-current="date"' : ''}>${number + 1}</button>`;
    });
    calendarGrid.innerHTML = ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'].map(label => `<span>${label}</span>`).join('') + '<span></span>'.repeat(offset) + days.join('');
    const entries = events.filter(event => event.date === selected);
    list.innerHTML = `<h2>${dateLabel(selected)}</h2>` + (entries.length ? entries.map(event => `<div class="record">
      <div class="row"><h3>${e(event.title)}</h3><span class="badge">${event.type === 0 ? 'День рождения' : 'Встреча'}</span></div>
      ${event.description ? `<p class="preline">${e(event.description)}</p>` : ''}
      ${event.createdByUser ? `<p class="meta">${e(event.createdByUser)}</p>` : ''}
      ${!event.isSystemEvent && (president || user.type === 'Admin' || event.createdByUserId === user.id) ? `<div><button class="btn secondary" type="button" data-delete="${event.id}">Удалить</button></div>` : ''}
    </div>`).join('') : '<p class="muted">На этот день событий нет.</p>');
  };
  const load = async () => {
    const request = ++loadCount;
    const start = monthDate();
    const end = new Date(Date.UTC(start.getUTCFullYear(), start.getUTCMonth() + 1, 0, 12));
    errorLineElement.textContent = '';
    try {
      const result = await api(`${eventPath}?from=${month}-01&to=${dateKey(end)}`);
      if (request !== loadCount) return;
      events = result.events;
      redraw();
    } catch (error) {
      if (request === loadCount && error.name !== 'AbortError') errorLineElement.textContent = error.message;
    }
  };
  const move = direction => {
    const date = monthDate();
    date.setUTCMonth(date.getUTCMonth() + direction);
    month = date.toISOString().slice(0, 7);
    selected = today().startsWith(month) ? today() : dateForDay(1);
    events = [];
    if (form) form.elements.date.value = selected;
    redraw();
    void load();
  };
  main.querySelector('#prev').onclick = () => move(-1);
  main.querySelector('#next').onclick = () => move(1);
  calendarGrid.onclick = event => {
    const button = event.target.closest('[data-day]');
    if (!button) return;
    selected = button.dataset.day;
    if (form) form.elements.date.value = selected;
    redraw();
    calendarGrid.querySelector('[aria-pressed="true"]').focus();
  };
  list.onclick = async event => {
    const button = event.target.closest('[data-delete]');
    if (!button || !confirm('Удалить событие?')) return;
    button.disabled = true;
    try {
      await api(`${eventPath}/${button.dataset.delete}`, { method: 'DELETE' });
      await load();
      notify('Событие удалено.');
    } catch (error) { if (error.name !== 'AbortError') notify(error.message); }
    finally { button.disabled = false; }
  };
  if (form) onSubmit(form, async data => {
    const date = data.get('date');
    await api(eventPath, { method: 'POST', body: {
      title: data.get('title').trim(), description: data.get('description').trim(), date, type: Number(data.get('type'))
    } });
    selected = date;
    month = date.slice(0, 7);
    form.reset();
    form.elements.date.value = date;
    form.closest('details').open = false;
    await load();
    notify('Событие добавлено.');
  });
  redraw();
  await load();
}

const cinemaPath = '/api/cinema/movies';
export async function cinema({ main, api, roles, notify }) {
  const president = roles.some(role => role.roleType === 'President');
  const { movies } = await api(cinemaPath);
  let sortKey = 'watchedAt';
  let sortDirection = -1;
  main.innerHTML = title('Кинотоп') + (president ? `<details class="panel"><summary>Добавить фильм</summary>
    <form id="create-movie" class="stack"><label>Название<input name="title" maxlength="256" required></label>
    <div class="fields"><label>Оценка от 0 до 10<input type="number" name="rating" min="0" max="10" step="any" required></label>
      <label>Дата просмотра<input type="date" name="watchedAt" value="${today()}" required></label></div>
    <label>Ссылка<input type="url" name="url" maxlength="1024" placeholder="https://"></label>
    <label class="check"><input type="checkbox" name="isExtra">Дополнительный фильм</label>
    <div><button class="btn" type="submit">Добавить</button></div>${errorLine}</form></details>` : '')
    + `<div class="table-panel block" role="region" aria-label="Список фильмов" tabindex="0">
      <table class="data-table movie-table"><thead><tr><th scope="col">Фильм</th>
        <th scope="col"><button type="button" data-sort="watchedAt">Дата просмотра <span aria-hidden="true"></span></button></th>
        <th scope="col"><button type="button" data-sort="rating">Рейтинг <span aria-hidden="true"></span></button></th>
      </tr></thead><tbody id="movies"></tbody></table></div>`;
  const redraw = () => {
    const sorted = [...movies].sort((left, right) => {
      const difference = sortKey === 'rating' ? left.rating - right.rating : left.watchedAt.localeCompare(right.watchedAt);
      return sortDirection * difference || right.watchedAt.localeCompare(left.watchedAt) || right.id - left.id;
    });
    main.querySelectorAll('[data-sort]').forEach(button => {
      if (button.dataset.sort === sortKey) button.parentElement.setAttribute('aria-sort', sortDirection > 0 ? 'ascending' : 'descending');
      else button.parentElement.removeAttribute('aria-sort');
      button.querySelector('span').textContent = button.dataset.sort !== sortKey ? '↕' : sortDirection > 0 ? '↑' : '↓';
    });
    main.querySelector('#movies').innerHTML = sorted.length ? sorted.map(movie => {
      const url = safeLink(movie.url);
      return `<tr${movie.isExtra ? ' class="extra-movie"' : ''}>
        <th scope="row">${url ? `<a href="${url}" target="_blank" rel="noopener">${e(movie.title)} ↗</a>` : e(movie.title)}
          ${movie.isExtra ? '<small>Дополнительный</small>' : ''}</th>
        <td>${dateLabel(movie.watchedAt)}</td><td>${decimal(movie.rating)}</td></tr>`;
    }).join('') : '<tr><td colspan="3">В списке пока нет фильмов.</td></tr>';
  };
  main.querySelectorAll('[data-sort]').forEach(button => {
    button.onclick = () => {
      sortDirection = button.dataset.sort === sortKey ? -sortDirection : -1;
      sortKey = button.dataset.sort;
      redraw();
    };
  });
  const form = main.querySelector('#create-movie');
  if (form) onSubmit(form, async data => {
    const url = data.get('url').trim();
    if (url && !safeLink(url)) throw new Error('Введите ссылку с http:// или https://.');
    const movie = await api(cinemaPath, { method: 'POST', body: {
      title: data.get('title').trim(), url, watchedAt: data.get('watchedAt'), rating: Number(data.get('rating')), isExtra: data.has('isExtra')
    } });
    movies.push(movie);
    form.reset();
    form.closest('details').open = false;
    redraw();
    notify('Фильм добавлен.');
  });
  redraw();
}

export async function rewards({ main, api }) {
  const sections = [['government-awards', 'Государственные награды'], ['championships', 'Чемпионаты']];
  const lists = await Promise.all(sections.map(([path]) => api(`/api/rewards/${path}`)));
  main.innerHTML = title('Награды') + `<div class="tab-list" role="tablist" aria-label="Виды наград">
    ${sections.map(([key, name], index) => `<button class="btn secondary" type="button" role="tab"
      id="tab-${key}" aria-selected="${index === 0}" aria-controls="panel-${key}" tabindex="${index === 0 ? 0 : -1}">${name}</button>`).join('')}
    </div>` + sections.map(([key, name], index) => {
    const { records } = lists[index];
    return `<section role="tabpanel" id="panel-${key}" aria-labelledby="tab-${key}" ${index ? 'hidden' : ''}>
      <div class="table-panel" role="region" aria-label="${name}" tabindex="0">
      <table class="data-table"><thead><tr><th scope="col">${index ? 'Чемпионат' : 'Награда'}</th>
        <th scope="col">${index ? 'Участник' : 'Награждённый'}</th><th scope="col">Дата</th><th scope="col">Место</th></tr></thead>
        <tbody>${records.length ? records.map(record => `<tr>
          <th scope="row">${e(record.eventName)}${record.eventType ? `<small>${e(record.eventType)}</small>` : ''}</th>
          <td>${e(record.fullName)}</td><td>${dateLabel(record.date)}</td><td>${e(record.place || '—')}</td></tr>`).join('')
        : '<tr><td colspan="4">Записей пока нет.</td></tr>'}</tbody></table></div></section>`;
  }).join('');
  const tabs = [...main.querySelectorAll('[role="tab"]')];
  const selectTab = selected => tabs.forEach(tab => {
    const active = tab === selected;
    tab.setAttribute('aria-selected', active);
    tab.tabIndex = active ? 0 : -1;
    document.getElementById(tab.getAttribute('aria-controls')).hidden = !active;
  });
  tabs.forEach((tab, index) => {
    tab.onclick = () => selectTab(tab);
    tab.onkeydown = event => {
      const next = { ArrowLeft: (index - 1 + tabs.length) % tabs.length, ArrowRight: (index + 1) % tabs.length, Home: 0, End: tabs.length - 1 }[event.key];
      if (next === undefined) return;
      event.preventDefault();
      selectTab(tabs[next]);
      tabs[next].focus();
    };
  });
}
