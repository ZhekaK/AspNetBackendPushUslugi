import { empty, errorLine, escapeHtml as e, onSubmit, title } from './ui.js';

const roomState = { Waiting: 'Ожидание игроков', InProgress: 'Идёт партия', Finished: 'Партия завершена' };
const suits = { Clubs: '♣', Diamonds: '♦', Hearts: '♥', Spades: '♠' };
const suitNames = { Clubs: 'трефы', Diamonds: 'бубны', Hearts: 'червы', Spades: 'пики' };

export async function game(context, kind) {
  const { id, main, api, signal } = context;
  const durak = kind === 'durak';
  const name = durak ? 'Дурак' : 'Зонг';
  const path = `/api/${kind}/rooms`;
  const roomLink = room => { location.hash = `${kind}/${room.id}`; };
  if (id) {
    await roomPage(context, kind, name, `${path}/${id}`);
    return;
  }
  main.innerHTML = title(name, durak ? 'Переводите, подкидывайте и отбивайтесь.' : 'Бросайте кости и забирайте очки до того, как выпадет зонг.')
    + `<div class="split"><section class="stack">
      <div class="actions push"><h2>Комнаты</h2><button class="btn secondary" id="reload-rooms" type="button">Обновить</button></div>
      <label>Название или номер комнаты<input id="search-room" type="search"></label>
      <div id="rooms" class="rows"></div><p id="rooms-error" class="form-error" role="alert"></p>
      </section><section class="panel stack"><h2>Создать комнату</h2>
      <form id="room-form" class="stack"><label>Название<input name="name" maxlength="128" required></label>
        <div class="fields"><label>Игроков<input name="maxPlayers" type="number" min="2" max="${durak ? 6 : 8}" value="2" required></label>
        ${durak ? '<label>Колода<select name="cardCount"><option>24</option><option selected>36</option><option>52</option></select></label>'
          : '<label>Цель по очкам<input name="targetScore" type="number" min="1000" max="50000" value="5000" required></label>'}</div>
        <label>Пароль комнаты<input type="password" name="password" maxlength="128" autocomplete="new-password" placeholder="Можно оставить пустым"></label>
        <div><button class="btn" type="submit">Создать</button></div>${errorLine}</form>
      </section></div>`;
  const list = main.querySelector('#rooms');
  const form = main.querySelector('#room-form');
  const reload = main.querySelector('#reload-rooms');
  const search = main.querySelector('#search-room');
  const error = main.querySelector('#rooms-error');
  let joining = false;
  let rooms = [];
  onSubmit(form, async data => {
    const options = durak ? { cardCount: Number(data.get('cardCount')) } : { targetScore: Number(data.get('targetScore')) };
    const result = await api(path, { method: 'POST', body: {
      name: data.get('name').trim(), maxPlayers: Number(data.get('maxPlayers')), password: data.get('password'), ...options
    } });
    roomLink(result.room);
  });
  if (durak) form.elements.cardCount.onchange = () => {
    const limit = form.elements.cardCount.value === '24' ? 4 : 6;
    form.elements.maxPlayers.max = limit;
    if (Number(form.elements.maxPlayers.value) > limit) form.elements.maxPlayers.value = limit;
  };
  function draw() {
    const query = search.value.trim().toLocaleLowerCase('ru-RU');
    const visible = rooms.filter(room => (room.isMember || room.status === 'Waiting') &&
      (!query || String(room.id) === query || room.name.toLocaleLowerCase('ru-RU').includes(query)));
    list.innerHTML = visible.length ? visible.map(room => `<article class="panel record">
        <div class="row"><h3>${e(room.name)}</h3><span class="badge">${room.playerCount}/${room.maxPlayers}</span></div>
        <p class="meta">№ ${room.id} · ${roomState[room.status]} · ${durak ? `колода ${room.cardCount}` : `цель ${room.targetScore}`}${room.hasPassword ? ' · С паролем' : ''}</p>
        ${room.isMember ? `<div><a class="btn secondary" href="#${kind}/${room.id}">Открыть комнату</a></div>` :
          `<form class="stack" data-room="${room.id}">${room.hasPassword ? '<label>Пароль<input type="password" name="password" maxlength="128" required></label>' : ''}
          <div><button class="btn secondary" type="submit" ${room.playerCount >= room.maxPlayers ? 'disabled' : ''}>Войти в комнату</button></div>${errorLine}</form>`}
        </article>`).join('') : empty(query ? 'Подходящих комнат нет.' : 'Открытых комнат пока нет. Создайте свою или обновите список.');
    list.querySelectorAll('form').forEach(join => onSubmit(join, async data => {
      if (joining) return;
      joining = true;
      try {
        const { room } = await api(`${path}/${join.dataset.room}/join`, { method: 'POST', body: { password: data.get('password') || '' } });
        roomLink(room);
      } finally { joining = false; }
    }));
  }
  async function load() {
    if (joining) return;
    reload.disabled = true;
    error.textContent = '';
    try {
      rooms = (await api(path)).rooms;
      draw();
    } catch (problem) {
      if (!signal.aborted) error.textContent = problem.message;
    } finally { reload.disabled = false; }
  }
  search.oninput = draw;
  reload.onclick = load;
  await load();
}

async function roomPage({ main, api, signal, notify }, kind, name, path) {
  const durak = kind === 'durak';
  let { room } = await api(path);
  let selected = '';
  let target = '';
  let mutating = false;
  let refreshing = false;
  let timer;
  main.innerHTML = `<p class="section-head"><a href="#${kind}">← К комнатам</a></p>` + title(room.name, name)
    + `<div class="split"><section id="board" class="stack"></section>
      <aside id="players" class="panel stack"></aside></div>
      <p id="room-error" class="form-error block" role="alert"></p>`;
  const board = main.querySelector('#board');
  const players = main.querySelector('#players');
  const error = main.querySelector('#room-error');
  const redraw = () => {
    if (durak) {
      if (!room.myHand.some(card => card.code === selected)) selected = '';
      if (!room.table.some(pair => pair.attack.code === target && !pair.defense)) target = '';
    }
    players.innerHTML = `<div class="row"><h2>Игроки</h2><span class="badge">${room.players.length}/${room.maxPlayers}</span></div>`
      + room.players.map(player => `<div class="row"><div><strong>${e(player.displayName)}${player.id === room.myPlayerId ? ' (вы)' : ''}</strong>
        <p class="meta">${durak
          ? player.isOut ? 'Вышел из раунда' : player.isAttacker ? 'Атакует' : player.isDefender ? 'Защищается' : 'Ожидает'
          : player.isWinner ? 'Победитель' : player.isCurrentTurn && room.status === 'InProgress' ? 'Ходит' : room.status === 'Waiting' ? `Жребий: ${player.initialRoll}` : `Ход ${player.turnOrder}`}</p>
        </div><span class="badge">${durak ? player.handCount : player.score}</span></div>`).join('');
    board.innerHTML = `<div class="row"><span class="badge">${roomState[room.status]}</span>${room.status === 'Waiting' ? '<button type="button" class="btn secondary" data-invite>Скопировать номер комнаты</button>' : ''}</div>`;
    if (room.status === 'Waiting') {
      board.innerHTML += `<div class="panel stack"><h2>Комната № ${room.id}</h2><p class="muted">Для начала нужны хотя бы два игрока.</p>
        ${room.canStart ? '<div><button class="btn" type="button" data-action="start">Начать игру</button></div>' : ''}</div>`;
    } else board.innerHTML += durak ? durakBoard(room, selected, target) : zongBoard(room);
  };
  redraw();

  async function refresh() {
    if (mutating || refreshing || signal.aborted || document.hidden || room.status === 'Finished') return;
    refreshing = true;
    try {
      const next = (await api(path)).room;
      if (!mutating && !signal.aborted && next.version !== room.version) {
        room = next;
        redraw();
      }
      if (!mutating && !error.dataset.actionError) error.textContent = '';
    } catch (problem) {
      if (!signal.aborted && !mutating) error.textContent = problem.message;
    } finally { refreshing = false; }
  }

  board.onclick = async event => {
    if (mutating) return;
    const button = event.target.closest('button');
    if (!button) return;
    if (button.hasAttribute('data-invite')) {
      try { await navigator.clipboard.writeText(String(room.id)); notify('Номер комнаты скопирован.'); }
      catch { notify(`Номер комнаты: ${room.id}`); }
      return;
    }
    if (button.dataset.card) {
      selected = selected === button.dataset.card ? '' : button.dataset.card;
      redraw();
      board.querySelector(`[data-card="${CSS.escape(button.dataset.card)}"]`)?.focus();
      return;
    }
    if (button.dataset.target) {
      target = target === button.dataset.target ? '' : button.dataset.target;
      redraw();
      board.querySelector(`[data-target="${CSS.escape(button.dataset.target)}"]`)?.focus();
      return;
    }
    const action = button.dataset.action;
    if (!action) return;
    const body = ['attack', 'defend', 'transfer'].includes(action)
      ? { cardCode: selected, attackCardCode: target } : undefined;
    mutating = true;
    button.disabled = true;
    error.textContent = '';
    delete error.dataset.actionError;
    try {
      room = (await api(`${path}/${action}`, { method: 'POST', body, version: room.version })).room;
      selected = '';
      target = '';
      redraw();
      board.querySelector('[data-action]:enabled')?.focus();
    } catch (problem) {
      if (!signal.aborted) {
        error.textContent = problem.message;
        error.dataset.actionError = 'true';
      }
    } finally {
      mutating = false;
      button.disabled = false;
      void refresh();
    }
  };
  document.addEventListener('visibilitychange', refresh, { signal });
  timer = setInterval(refresh, 2200);
  signal.addEventListener('abort', () => clearInterval(timer), { once: true });
}

function cardLabel(card) {
  return `${e(card.rank)} ${suits[card.suit] || ''}`;
}

function cardMarkup(card, attribute = '', pressed = false) {
  const className = `card${['Hearts', 'Diamonds'].includes(card.suit) ? ' red' : ''}`;
  const face = `<img draggable="false" src="/assets/cards/${encodeURIComponent(card.code)}.png" alt="${cardLabel(card)}">`;
  return attribute ? `<button type="button" class="${className}" ${attribute}="${e(card.code)}" aria-pressed="${pressed}">${face}</button>`
    : `<span class="${className}">${face}</span>`;
}

function durakBoard(room, selected, target) {
  const card = room.myHand.find(item => item.code === selected);
  const open = room.table.filter(pair => !pair.defense);
  const attack = room.table.find(pair => pair.attack.code === target)?.attack || (open.length === 1 ? open[0].attack : null);
  const canBeat = card && attack && ((card.suit === attack.suit && card.value > attack.value) || (card.suit !== attack.suit && card.suit === room.trumpSuit));
  const playableRank = card && (!room.table.length || room.table.some(pair => pair.attack.rank === card.rank || pair.defense?.rank === card.rank));
  const canTransfer = card && room.canTransfer && room.table.some(pair => pair.attack.rank === card.rank);
  const button = (name, action, enabled, secondary = false) =>
    `<button class="btn${secondary ? ' secondary' : ''}" type="button" data-action="${action}" ${enabled ? '' : 'disabled'}>${name}</button>`;
  return `<div class="game-table stack"><div class="row"><h2>Стол</h2>
    <div class="cards"><span class="meta">Козырь ${e(room.trumpCardCode.slice(0, -1))} ${suits[room.trumpSuit] || ''} · В колоде ${room.deckCount}</span>
    ${room.deckCount ? '<img class="deck-back" src="/assets/cards/back.png" alt="">' : ''}</div></div>
    <div class="cards">${room.table.length ? room.table.map(pair => `<div class="table-pair">
    ${cardMarkup(pair.attack, room.canDefend && !pair.defense ? 'data-target' : '', pair.attack.code === target)}
    ${pair.defense ? cardMarkup(pair.defense) : ''}</div>`).join('') : '<p class="meta">Стол пуст.</p>'}</div>
    <p aria-live="polite">${e(room.message)}</p></div>
    <section class="panel stack"><h2>Ваши карты</h2>
    <div class="cards" aria-label="Карты в руке">${room.myHand.length ? room.myHand.map(item => cardMarkup(item, room.status === 'InProgress' ? 'data-card' : '', item.code === selected)).join('') : '<p class="muted">Карт нет.</p>'}</div>
    ${room.status !== 'InProgress' ? '' : `<p class="meta">${room.canDefend && open.length > 1 ? 'Выберите атакующую карту и карту для защиты.' :
      room.canAttack || room.canDefend ? 'Выберите карту из руки.' : 'Ожидайте хода.'} Козырная масть: ${suitNames[room.trumpSuit] || '—'}.</p>
    <div class="actions">${room.canAttack ? button('Атаковать', 'attack', playableRank) : ''}
    ${room.canDefend ? button('Отбить', 'defend', canBeat) : ''}
    ${room.canTransfer ? button('Перевести', 'transfer', canTransfer, true) : ''}
    ${room.canTake ? button('Взять', 'take', true, true) : ''}
    ${room.canPass ? button('Бито', 'pass', true, true) : ''}</div>`}
    </section>`;
}

function zongBoard(room) {
  const dice = room.lastDice.split(',').map(Number).filter(value => value >= 1 && value <= 6);
  return `<div class="game-table stack">
    <div class="row"><h2>Кости</h2><span class="meta">Цель ${room.targetScore} очков</span></div>
    <div class="dice" aria-label="Последний бросок">${dice.length ? dice.map(value => `<img src="/assets/dice/${value}.png" alt="${value}">`).join('') : '<p class="meta">Первого броска ещё не было.</p>'}</div>
    <p aria-live="polite">${e(room.lastRollMessage)}</p>
    </div><section class="panel stack">
    <h2>${room.status === 'Finished' ? 'Итог партии' : room.isMyTurn ? 'Ваш ход' : 'Ход соперника'}</h2>
    ${room.status === 'InProgress' ? `<p>Очков за ход: <strong>${room.currentTurnScore}</strong> · Костей в следующем броске: <strong>${room.remainingDice}</strong></p>
    <div class="actions"><button class="btn" type="button" data-action="roll" ${room.canRoll ? '' : 'disabled'}>Бросить кости</button>
    <button class="btn secondary" type="button" data-action="bank" ${room.canBank ? '' : 'disabled'}>Забрать очки</button></div>` : ''}
    <details><summary>Правила подсчёта</summary><p class="meta">Единица даёт 100 очков, пятёрка — 50. Три единицы дают 1000, остальные тройки дают 100 × значение кости. Каждый дополнительный кубик с тем же значением удваивает очки за комбинацию. Ряд 1–6 или три пары дают 1500. Если все кости принесли очки, следующий бросок снова делается шестью. Бросок без очков завершает ход и сжигает накопленное за этот ход.</p></details>
    </section>`;
}
