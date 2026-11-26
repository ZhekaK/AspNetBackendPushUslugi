import { dateLabel, errorLine, escapeHtml as e, fullName, onSubmit, roleLabel, title } from './ui.js';
import { calendar, cinema, rewards, votes } from './modules.js';
import { messenger } from './messenger.js';
import { game } from './games.js';
import { bill, alcohol, xoxi } from './tools.js';
import { bindIdentityPhoto } from './identity-photo.js';
import { candidates, candidateFullName, sexLabel, showCandidateReview } from './candidates.js';

const modules = {
  home: { name: 'Главная', guest: true, render: home },
  calendar: { name: 'Календарь событий', text: 'Встречи и дни рождения', guest: true, key: 'CalendarScene', render: calendar },
  messenger: { name: 'Мессенджер Пыш', text: 'Общий чат, личные сообщения и группы', render: messenger },
  votes: { name: 'Голосования', text: 'Опросы и результаты', key: 'VoteModule', render: votes },
  cinema: { name: 'Кинотоп', text: 'Просмотры и оценки фильмов', guest: true, render: cinema },
  rewards: { name: 'Зал славы', text: 'Чемпионаты и государственные награды', render: rewards },
  candidates: { name: 'Реестр кандидатов', text: 'Выданные удостоверения и новые предложения', citizen: true, render: candidates },
  durak: { name: 'Дурак', text: 'Подкидная и переводная игра', guest: true, render: context => game(context, 'durak') },
  zong: { name: 'Зонг', text: 'Игра в кости на очки', guest: true, render: context => game(context, 'zong') },
  xoxi: { name: 'ХОХИ', text: 'Четыре фигуры в ряд', guest: true, render: xoxi },
  bill: { name: 'Калькулятор министров', text: 'Разделите счёт с учётом льготы', render: bill },
  alcohol: { name: 'Алкометр', text: 'Приблизительный расчёт по напиткам', render: alcohol },
  profile: { name: 'Профиль', guest: true, render: profile }
};
const groups = [
  ['Сообщество', ['calendar', 'messenger', 'votes', 'cinema', 'rewards', 'candidates']],
  ['Игры', ['durak', 'zong', 'xoxi']],
  ['Инструменты', ['bill', 'alcohol']]
];

const main = document.querySelector('#content');
const nav = document.querySelector('#navigation');
const account = document.querySelector('#account');
const menu = document.querySelector('#menu');
const catalogMenu = document.querySelector('#catalog-menu');
const menuScrim = document.querySelector('.menu-scrim');
const toast = document.querySelector('#announcement');
const candidateReviewDialog = document.querySelector('#candidate-review');
const workerRegistration = 'serviceWorker' in navigator
  ? navigator.serviceWorker.register('/service-worker.js').catch(() => null)
  : Promise.resolve(null);
let token = localStorage.getItem('push-uslugi.token') || '';
let rememberToken = localStorage.getItem('push-uslugi.remember') || '';
let user;
let roles = [];
let unread = {};
let messengerUnread = 0;
let pageController;
let toastTimer;
let currentPage = 'home';
let installationPrompt;
const standalone = window.matchMedia('(display-mode: standalone)');
const syncInstallVisibility = () => document.documentElement.classList.toggle('app-installed',
  standalone.matches || navigator.standalone === true);
syncInstallVisibility();
standalone.addEventListener?.('change', syncInstallVisibility);

window.addEventListener('beforeinstallprompt', event => {
  event.preventDefault();
  installationPrompt = event;
});
window.addEventListener('appinstalled', () => {
  installationPrompt = undefined;
  document.documentElement.classList.add('app-installed');
});
document.addEventListener('click', async event => {
  if (!event.target.closest('[data-install-app]')) return;
  if (installationPrompt) {
    const prompt = installationPrompt;
    installationPrompt = undefined;
    try { await prompt.prompt(); return; }
    catch { /* Fall back to the browser instructions. */ }
  }
  const agent = navigator.userAgent;
  const appleMobile = /iPhone|iPad|iPod/.test(agent) || (/Macintosh/.test(agent) && navigator.maxTouchPoints > 1);
  document.querySelector('#install-steps').textContent = appleMobile
    ? 'Откройте сайт в Safari. Нажмите «Поделиться» → «На экран Домой». Если доступен переключатель «Открывать как веб-приложение», включите его и нажмите «Добавить».'
    : /Android/.test(agent)
      ? 'Откройте сайт в Chrome. В меню ⋮ выберите «Добавить на главный экран» → «Установить».'
      : 'Откройте меню Chrome или Edge и выберите установку сайта как приложения.';
  const dialog = document.querySelector('#install-help');
  if (!dialog.open) dialog.showModal();
});

class ApiError extends Error {
  constructor(status, data) {
    const translations = {
      'Access key not found': 'Удостоверение не найдено. Проверьте серию и номер.',
      'Incorrect name': `Неверное имя. Осталось попыток: ${data?.attemptsRemaining}.`,
      'Full login required': 'Войдите по серии и номеру удостоверения.',
      'Poll text is too long': 'Проверьте длину заголовка, описания и вариантов.',
      'At least two options are required': 'Укажите хотя бы два разных варианта.',
      'End date cannot be in the past': 'Дата окончания уже прошла по московскому времени.',
      'Poll is closed': 'Голосование закрыто.',
      'User already voted': 'Ваш голос уже учтён.',
      'Invalid room password': 'Неверный пароль комнаты.',
      'Room is full': 'В комнате нет мест.',
      'Game already started': 'Игра уже началась.',
      'Game state changed. Refresh and try again.': 'Игровое состояние изменилось. Попробуйте ещё раз.',
      'Web Push is not configured': 'На сервере пока не настроены push-уведомления.'
    };
    super(translations[data?.message] || ({
      400: 'Проверьте введённые данные.', 401: 'Нужно войти в аккаунт.',
      403: 'У вас нет доступа к этому действию.', 404: 'Запись не найдена.',
      409: 'Данные изменились. Обновите страницу.',
      429: 'Слишком много попыток. Попробуйте через минуту.',
      503: 'Сервисы временно недоступны. Попробуйте позже.'
    })[status] || `Ошибка сервера (${status}). Попробуйте ещё раз.`);
    this.status = status;
    this.data = data;
  }
}

async function api(path, { method = 'GET', body, signal, version, auth = true, responseType, requestToken = token } = {}) {
  const headers = { Accept: 'application/json' };
  if (auth && requestToken) headers.Authorization = `Bearer ${requestToken}`;
  const binary = body instanceof Blob;
  if (body !== undefined) headers['Content-Type'] = binary ? body.type : 'application/json';
  if (version) headers['X-Game-Version'] = version;
  const response = await fetch(path, { method, headers, signal, body: binary || body === undefined ? body : JSON.stringify(body) });
  if (response.ok && responseType === 'blob') return response.blob();
  const data = response.headers.get('content-type')?.includes('json') ? await response.json() : null;
  if (!response.ok) {
    if (response.status === 401 && auth && requestToken && requestToken === token) {
      storeSession('', rememberToken);
      user = undefined;
      roles = [];
      if (candidateReviewDialog?.open) candidateReviewDialog.close();
      render();
    }
    throw new ApiError(response.status, data);
  }
  return data;
}

function storeSession(jwt, remember) {
  token = jwt;
  rememberToken = remember;
  for (const [key, value] of [['token', jwt], ['remember', remember]]) {
    if (value) localStorage.setItem(`push-uslugi.${key}`, value);
    else localStorage.removeItem(`push-uslugi.${key}`);
  }
}

function notify(text) {
  clearTimeout(toastTimer);
  toast.textContent = text;
  toast.hidden = false;
  toastTimer = setTimeout(() => { toast.hidden = true; }, 4300);
}

function graphic(key) {
  const icon = key === 'candidates' ? 'person' : key;
  const path = key === 'zong' ? '/assets/dice/5.png' : `/assets/portal/${icon}.png`;
  return `<span class="graphic" style="--graphic: url('${path}')" aria-hidden="true"></span>`;
}

function unreadCount(key) {
  return key === 'messenger' ? messengerUnread : unread[modules[key].key]?.count || 0;
}

function isCitizen() {
  return Boolean(user?.series && user.number && (user.type === 'KeyUser' || user.type === 'Admin'));
}

function canReviewCandidates() {
  return isCitizen() && roles.some(role => role.roleType === 'President');
}

function reviewCandidates() {
  if (!candidateReviewDialog || !canReviewCandidates()) return;
  const owner = user;
  const seenKey = `push-uslugi.candidate-review.${owner.id}`;
  showCandidateReview(candidateReviewDialog, api, latestId => {
    if (latestId > Number(sessionStorage.getItem(seenKey) || 0)) sessionStorage.setItem(seenKey, String(latestId));
  });
}

async function refreshCandidateReview(owner) {
  if (!candidateReviewDialog || !canReviewCandidates() || document.querySelector('dialog[open]')) return;
  try {
    const { latestId } = await api('/api/candidates/proposals/pending');
    if (user !== owner || document.hidden || document.querySelector('dialog[open]')) return;
    const seen = Number(sessionStorage.getItem(`push-uslugi.candidate-review.${owner.id}`) || 0);
    if (latestId > seen) reviewCandidates();
  } catch {
    // The next refresh can check for new proposals again.
  }
}

function updateNavigation() {
  const link = key => {
    const count = unreadCount(key);
    return `<a class="nav-link" href="#${key}" ${key === currentPage ? 'aria-current="page"' : ''}>${graphic(key)}${modules[key].name}${count ? `<span class="badge notification" aria-label="Непрочитанные: ${count}">${count}</span>` : ''}</a>`;
  };
  nav.innerHTML = groups.map(([label, keys]) =>
    `<div><p class="nav-group">${label}</p>${keys.map(link).join('')}</div>`).join('');

  main.querySelectorAll('[data-unread]').forEach(badge => {
    const count = unreadCount(badge.dataset.unread);
    badge.hidden = !count;
    badge.textContent = count;
    badge.setAttribute('aria-label', `Непрочитанные: ${count}`);
  });
}

function updateHeader() {
  if (!user) {
    account.innerHTML = '<a class="btn secondary sign-in" href="#login">Войти</a>';
    return;
  }
  const name = user.type === 'Guest' ? 'Гость' : user.firstName;
  account.innerHTML = `<a class="account-link" href="#profile" aria-label="Личный кабинет, ${e(name)}"><span class="avatar" aria-hidden="true">${e(name[0]?.toUpperCase() || 'П')}</span><span>${e(name)}</span></a>`;
}

async function refreshUnread() {
  const owner = user;
  if (!owner || owner.type === 'Guest' || currentPage === 'login' || document.hidden) return;
  void refreshCandidateReview(owner);
  const [notifications, chats] = await Promise.allSettled([
    api('/api/module-notifications/unread'), api('/api/messenger/chats')
  ]);
  if (user !== owner) return;
  if (notifications.status === 'fulfilled') {
    unread = Object.fromEntries(notifications.value.notifications.map(item => [item.moduleKey, item]));
  }
  if (chats.status === 'fulfilled') messengerUnread = chats.value.chats.filter(chat => chat.unread).length;
  updateNavigation();
}

function setMenu(open) {
  catalogMenu.hidden = !open;
  menuScrim.hidden = !open;
  menu.setAttribute('aria-expanded', String(open));
}

function render(focus = false) {
  pageController?.abort();
  pageController = new AbortController();
  const signal = pageController.signal;
  let [page = 'home', id] = location.hash.slice(1).split('/');
  if (!Object.hasOwn(modules, page) && page !== 'login') page = 'home';
  currentPage = page;
  setMenu(false);
  updateHeader();
  updateNavigation();
  document.title = page === 'home' ? 'Пыш-Услуги' : `${modules[page]?.name || 'Вход'} · Пыш-Услуги`;
  if (focus) {
    window.scrollTo(0, 0);
    main.focus({ preventScroll: true });
  }
  const module = modules[page];
  if (page === 'login' || (!user && page !== 'home') || (user?.type === 'Guest' && !module?.guest)) {
    login(page === 'login' ? 'home' : location.hash.slice(1));
    return;
  }
  if (module.citizen && !isCitizen()) {
    main.innerHTML = title(module.name) + '<section class="panel"><p>Для просмотра реестра нужно удостоверение гражданина.</p></section>';
    return;
  }
  main.innerHTML = '<p class="empty" role="status">Загрузка…</p>';
  const pageToken = token;
  const context = {
    main, user, roles, id: /^\d+$/.test(id || '') ? Number(id) : null, segment: id, signal, notify,
    api: (path, options) => api(path, { ...options, signal }),
    saveXoxiProgress: body => api('/api/xoxi/save', { method: 'PUT', body, requestToken: pageToken }),
    updateNavigation, refreshUnread, canReviewCandidates: canReviewCandidates(), reviewCandidates
  };
  Promise.resolve().then(() => module.render(context)).then(async () => {
    if (!signal.aborted && module.key && user?.type !== 'Guest') {
      await context.api(`/api/module-notifications/${module.key}/read`, { method: 'POST' });
      delete unread[module.key];
      updateNavigation();
    }
  }).catch(error => {
    if (signal.aborted) return;
    main.innerHTML = title(module.name) + `<div class="panel stack"><p class="error" role="alert">${e(error.message)}</p><div><button class="btn secondary" type="button">Повторить</button></div></div>`;
    main.querySelector('button').onclick = () => render();
  });
}

async function loadUser() {
  user = await api('/api/user/profile');
  roles = [];
  if (isCitizen()) {
    try {
      roles = (await api('/api/user/role')).roles;
    } catch (error) {
      if (error.status === 401) throw error;
      notify('Не удалось загрузить должности.');
    }
  }
}

function login(next = 'home', container = main) {
  const embedded = container !== main;
  const citizenshipRequired = modules[next.split('/')[0]]?.citizen;
  const membership = citizenshipRequired ? 'Для просмотра реестра войдите по удостоверению гражданина.'
    : user?.type === 'Guest' ? 'Этот раздел доступен после входа по удостоверению.' : 'Войдите по серии и номеру удостоверения.';
  container.innerHTML = (embedded ? '' : title('Вход', membership)) + `<section class="${embedded ? '' : 'panel standalone-entry '}stack" aria-label="Вход">
    ${embedded ? `<div class="entry-heading"><span class="entry-icon">${graphic('person')}</span>
      <div><h2>Личный кабинет</h2><p class="meta">${rememberToken ? 'Вход по имени' : 'Вход по удостоверению'}</p></div></div>` : ''}
    <form class="stack" id="key-login" ${rememberToken ? 'hidden' : ''}>
      <div class="fields">
        <label>Серия<input name="series" required maxlength="16" autocomplete="username" placeholder="0526"></label>
        <label>Номер<input name="number" required maxlength="16" autocomplete="current-password" placeholder="123456"></label>
      </div>
      <div class="actions"><button class="btn" type="submit">Войти</button>
        ${!user ? '<button class="btn guest-entry" type="button" data-guest>Войти гостем</button>' : '<a href="#home">На главную</a>'}</div>${errorLine}
    </form>
    ${rememberToken ? `<form class="stack" id="name-login">
      <label>Имя<input name="firstName" required autocomplete="given-name"></label>
      <div class="actions"><button class="btn" type="submit">Войти</button><button class="btn secondary" type="button" data-full>Войти по удостоверению</button></div>${errorLine}
    </form>` : ''}
  </section>`;
  const enter = async (path, body, guest = false) => {
    const data = await api(`/api/auth/${path}`, { method: 'POST', body, auth: false });
    storeSession(data.token, data.rememberToken || '');
    unread = {};
    messengerUnread = 0;
    await loadUser();
    const target = guest && !modules[next.split('/')[0]]?.guest ? 'home' : next;
    if (location.hash === `#${target}` || (!location.hash && target === 'home')) render();
    else location.hash = target;
    await refreshUnread();
  };
  const key = container.querySelector('#key-login');
  onSubmit(key, data => enter('login-by-key', { series: data.get('series').trim(), number: data.get('number').trim() }));
  const guestButton = container.querySelector('[data-guest]');
  if (guestButton) guestButton.onclick = async () => {
    if (key.dataset.busy) return;
    guestButton.disabled = true;
    try { await enter('guest', undefined, true); }
    catch (error) { key.querySelector('.form-error').textContent = error.message; }
    finally { guestButton.disabled = false; }
  };
  const remembered = container.querySelector('#name-login');
  const showKey = () => {
    remembered.hidden = true;
    key.hidden = false;
    key.elements.series.focus();
  };
  if (remembered) {
    container.querySelector('[data-full]').onclick = showKey;
    onSubmit(remembered, async data => {
      try { await enter('login-by-name', { rememberToken, firstName: data.get('firstName').trim() }); }
      catch (error) {
        if (error.data?.requiresKeyLogin) {
          storeSession(token, '');
          showKey();
          key.querySelector('.form-error').textContent = error.message;
          return;
        }
        throw error;
      }
    });
  }
}

function home() {
  const tiles = keys => keys.map(key => {
    const module = modules[key];
    return `<a class="tile" href="#${key}"><span class="tile-icon">${graphic(key)}</span>
      <span class="tile-title"><strong>${module.name}</strong><small>${module.text}</small></span>
      <span class="tile-aside"><span class="badge notification" data-unread="${key}" hidden></span><span class="arrow" aria-hidden="true">→</span></span></a>`;
  }).join('');
  main.innerHTML = `<section class="hero">
    <div class="hero-copy"><h1>${user?.type && user.type !== 'Guest' ? `Привет, ${e(user.firstName)}!` : 'Пыш-Услуги'}</h1>
      <p>События, общение, голосования и игры сообщества</p>
      <div class="actions"><button type="button" class="btn" id="to-services">К сервисам <span aria-hidden="true">↓</span></button>
      <button type="button" class="btn secondary" data-install-app>Установить приложение</button></div>
    </div>
    ${user ? `<a class="panel entry-card profile-link" href="#profile">
      <span class="entry-icon">${graphic('person')}</span>
      <span><strong>${user.type === 'Guest' ? 'Гостевой аккаунт' : 'Личный кабинет'}</strong>
      <small>${user.type === 'Guest' ? 'Войти по удостоверению' : 'Удостоверение и настройки'}</small></span>
      <span class="arrow" aria-hidden="true">→</span>
    </a>` : '<div id="home-login" class="panel entry-card"></div>'}
  </section>
  ${groups.map(([label, keys], index) => `<section class="block services"><div class="section-head">
    <h2${index === 0 ? ' id="services-title" tabindex="-1"' : ''}>${label}</h2>
  </div><div class="tiles">${tiles(keys)}</div></section>`).join('')}`;
  if (!user) login('home', main.querySelector('#home-login'));
  main.querySelector('#to-services').onclick = () => {
    const heading = main.querySelector('#services-title');
    heading.focus({ preventScroll: true });
    heading.scrollIntoView({ block: 'start' });
  };
  updateNavigation();
}

function profileDocuments(person, posts) {
  const value = (label, text) => `<div><dt>${label}</dt><dd>${e(text || '—')}</dd></div>`;
  const issued = (date, place) => [date ? dateLabel(date) : '', place].filter(Boolean).join(' · ');
  const documents = person.type === 'Candidate' ? [{
    kind: 'candidate', title: 'Удостоверение кандидата', number: [person.series, person.number].join(' '),
    fields: value('Серия', person.series) + value('Фамилия, имя, отчество', candidateFullName(person)) +
      value('Пол', sexLabel(person.sex)) + value('Дата выдачи', dateLabel(person.giveDate)) +
      value('Кем выдано', person.givePlace)
  }] : [{
    kind: 'personal', title: 'Удостоверение личности', number: [person.series, person.number].filter(Boolean).join(' '),
    fields: value('Фамилия, имя, отчество', fullName(person)) +
      value('Дата рождения', dateLabel(person.birthDate)) + value('Пол', ({ Man: 'Мужской', Women: 'Женский' })[person.sex]) +
      value('Выдано', issued(person.giveDate, person.givePlace))
  }, ...posts.map(post => ({
    kind: 'role', title: post.postName || roleLabel(post.roleType), number: post.number,
    fields: value('Должность', post.postName || roleLabel(post.roleType)) +
      value('Владелец', fullName(person)) + value('Выдано', issued(post.startDate, post.givePlace))
  }))];
  return `<section class="document-folder" aria-labelledby="folder-title"><h3 id="folder-title">Удостоверения</h3>
    <div class="document-choices">${documents.map((document, index) => `<button type="button" data-document="${index}" class="document-thumbnail ${document.kind}">
      <strong>${e(document.title)}</strong><span>${e(document.number || '—')}</span>
    </button>`).join('')}</div>
    <dialog class="document-dialog" aria-labelledby="document-name">
      <form method="dialog" class="document-close"><button class="btn secondary" type="submit" autofocus>Закрыть</button></form>
      ${documents.map((document, index) => `<article class="document-card ${document.kind}" data-card="${index}" hidden>
        <h3>${document.kind === 'role' ? 'Удостоверение госслужащего' : document.title}</h3>
        ${document.kind === 'personal' ? '<img id="identity-photo" alt="Моя фотография" hidden>' : ''}
        <dl>${value('Номер удостоверения', document.kind === 'candidate' ? person.number : document.number)}${document.fields}</dl>
      </article>`).join('')}
      ${person.type === 'Candidate' ? '' : `<form id="identity-photo-form" class="stack" hidden>
        <label>Фотография для удостоверения<input type="file" name="photo" accept="image/*" required></label>
        <div class="actions"><button class="btn" type="submit">Сохранить фото</button>
          <button class="btn secondary" type="button" id="delete-identity-photo" hidden>Удалить фото</button></div>
        ${errorLine}
      </form>`}
      <span id="document-name" class="sr-only"></span>
    </dialog>
  </section>`;
}

async function profile({ api: request, signal }) {
  main.innerHTML = title('Профиль') + `<div class="split">
    <section class="panel stack"><h2>${user.type === 'Guest' ? 'Гостевой аккаунт' : e(user.type === 'Candidate' ? candidateFullName(user) : fullName(user))}</h2>
    ${user.type === 'Guest' ? '<p class="muted">Войдите по удостоверению для доступа к голосованиям и чатам.</p><div><a class="btn" href="#login">Войти по удостоверению</a></div>' :
      profileDocuments(user, roles)}
    <div><button id="logout" class="btn secondary" type="button">Выйти</button></div></section>
    ${user.type === 'Guest' ? '' : `<div class="stack"><section class="panel stack"><h2>Вес</h2>
      <form id="weight" class="stack"><label>Вес, кг<input name="weight" type="number" step="any" min="0.01" max="500" value="${e(user.weightKg || '')}" required></label>
      <div><button class="btn" type="submit">Сохранить</button></div>${errorLine}</form></section>
      <section class="panel stack"><h2>Уведомления</h2><p class="muted">Сообщения, события и новые голосования.</p>
      <div><button id="push" class="btn secondary" type="button">Включить уведомления</button></div></section></div>`}
  </div>`;
  main.querySelector('#logout').onclick = async () => {
    main.querySelector('#logout').disabled = true;
    try {
      const registration = 'serviceWorker' in navigator && await navigator.serviceWorker.getRegistration();
      const subscription = await registration?.pushManager?.getSubscription();
      if (subscription) await unsubscribe(subscription, request);
    } catch {
      notify('Не удалось отключить уведомления в браузере.');
    }
    storeSession('', '');
    if (candidateReviewDialog?.open) candidateReviewDialog.close();
    user = undefined;
    roles = [];
    unread = {};
    messengerUnread = 0;
    if (location.hash === '#home') render();
    else location.hash = 'home';
  };
  const weight = main.querySelector('#weight');
  if (!weight) return;
  const dialog = main.querySelector('.document-dialog');
  const photoForm = dialog.querySelector('#identity-photo-form');
  if (photoForm) bindIdentityPhoto(dialog, request, signal, notify);
  main.querySelectorAll('[data-document]').forEach(button => {
    button.onclick = () => {
      dialog.querySelector('#document-name').textContent = button.querySelector('strong').textContent;
      dialog.querySelectorAll('[data-card]').forEach(card => {
        card.hidden = card.dataset.card !== button.dataset.document;
      });
      if (photoForm) photoForm.hidden = button.dataset.document !== '0';
      dialog.showModal();
    };
  });
  onSubmit(weight, async data => {
    const result = await request('/api/user/profile/weight', { method: 'PUT', body: { weightKg: Number(data.get('weight')) } });
    user.weightKg = result.weightKg;
    notify('Вес сохранён.');
  });
  const push = main.querySelector('#push');
  if (!('serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window)) {
    push.disabled = true;
    push.textContent = 'Требуется HTTPS и поддержка браузера';
    return;
  }
  const registration = await navigator.serviceWorker.getRegistration();
  let enabled = Boolean(await registration?.pushManager?.getSubscription());
  if (signal.aborted) return;
  const update = () => { push.textContent = enabled ? 'Отключить уведомления' : 'Включить уведомления'; };
  update();
  push.onclick = async () => {
    push.disabled = true;
    try {
      if (!enabled && Notification.permission !== 'granted' && await Notification.requestPermission() !== 'granted') {
        throw new Error('Разрешите уведомления в настройках браузера.');
      }
      const worker = (await workerRegistration) ?? await navigator.serviceWorker.register('/service-worker.js');
      const ready = worker.active ? worker : await navigator.serviceWorker.ready;
      let subscription = await ready.pushManager.getSubscription();
      if (subscription) {
        await unsubscribe(subscription, request);
        enabled = false;
      } else {
        const { publicKey } = await request('/api/push/public-key');
        const bytes = Uint8Array.from(atob(publicKey.replaceAll('-', '+').replaceAll('_', '/')), c => c.charCodeAt(0));
        subscription = await ready.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: bytes });
        const { endpoint, keys } = subscription.toJSON();
        try {
          await request('/api/push/subscribe', { method: 'POST', body: { endpoint, ...keys, platform: 0 } });
        } catch (error) {
          await subscription.unsubscribe();
          throw error;
        }
        enabled = true;
      }
    } catch (error) {
      if (!signal.aborted) notify(error.message);
    } finally {
      update();
      push.disabled = false;
    }
  };
}

async function unsubscribe(subscription, request) {
  try {
    await request('/api/push/unsubscribe', { method: 'POST', body: { endpoint: subscription.endpoint } });
  } finally {
    await subscription.unsubscribe();
  }
}

menu.onclick = () => setMenu(menu.getAttribute('aria-expanded') !== 'true');
document.querySelector('.skip').onclick = event => {
  event.preventDefault();
  main.focus({ preventScroll: true });
  window.scrollTo(0, 0);
};
menuScrim.onclick = event => { event.preventDefault(); setMenu(false); menu.focus(); };
nav.onclick = event => { if (event.target.closest('a')) setMenu(false); };
document.addEventListener('keydown', event => {
  if (event.key === 'Escape' && !catalogMenu.hidden) { setMenu(false); menu.focus(); }
});
window.addEventListener('hashchange', () => render(true));
document.addEventListener('visibilitychange', refreshUnread);
setInterval(refreshUnread, 20000);

if (token) {
  try { await loadUser(); }
  catch (error) {
    if (error.status === 404) storeSession('', rememberToken);
    else if (error.status !== 401) notify('Не удалось загрузить профиль. Попробуйте обновить страницу.');
  }
}
render();
void refreshUnread();
