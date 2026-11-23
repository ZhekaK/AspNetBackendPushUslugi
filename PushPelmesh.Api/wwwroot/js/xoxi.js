import { escapeHtml as e, title } from './ui.js';
import { drawColumns, width, height, figure, winningLines } from './xoxi-board.js';
import { xoxiRoom, xoxiRoomList } from './xoxi-online.js';

const seconds = [40, 20, 10];
const statKeys = ['gamesAll', 'gamesForCross', 'gamesForNulls', 'cellsFilled', 'wins', 'loses'];
const localKeys = ['gamesInLocalMode', 'crossWinsInLocalMode', 'nullsWinsInLocalMode'];
const opposite = player => 3 - player;
const count = value => Math.max(0, Number(value) || 0);
const pendingSaves = new Map();

function normalize(raw) {
  const save = {
    _lang: 0, difficult: 0, playerFigure: 1, anim: true, animationSpeed: .15, sound: true, timerTurn: true, markLastFigure: true, ...raw
  };
  if (![0, 1, 2].includes(save.difficult)) save.difficult = 0;
  if (save.playerFigure !== 2) save.playerFigure = 1;
  statKeys.forEach(key => { save[key] = Array.from({ length: 3 }, (_, index) => count(save[key]?.[index])); });
  localKeys.forEach(key => { save[key] = count(save[key]); });
  return save;
}

function botColumn(board, player, difficulty) {
  const available = board.flatMap((column, index) => column.length < height ? [index] : []);
  const completes = (column, who) => winningLines(board, column, board[column].length, who).length > 0;
  const win = available.find(column => completes(column, player));
  if (win !== undefined) return win;
  if (difficulty > 0) {
    const block = available.find(column => completes(column, opposite(player)));
    if (block !== undefined) return block;
  }
  if (difficulty === 2) {
    const safe = available.filter(column => {
      board[column].push(player);
      const allowsWin = available.some(index => board[index].length < height && completes(index, opposite(player)));
      board[column].pop();
      return !allowsWin;
    });
    if (safe.length) return safe.sort((a, b) => Math.abs(4.5 - a) - Math.abs(4.5 - b))[0];
  }
  return available[Math.floor(Math.random() * available.length)];
}

async function loadProgress({ api, saveXoxiProgress, user, signal, notify }) {
  const key = `push-uslugi.xoxi.${user.id}`;
  const online = user.type !== 'Guest';
  const pendingKey = `${key}.pending`;
  let raw;
  try { raw = JSON.parse(localStorage.getItem(key) || 'null'); } catch { raw = null; }
  if (online && !localStorage.getItem(pendingKey)) {
    try { raw = await api('/api/xoxi/save'); }
    catch (error) {
      if (signal.aborted) return;
      if (error.status !== 404) notify('Серверное сохранение недоступно. Используется копия из браузера.');
    }
  }
  if (signal.aborted) return;
  const save = normalize(raw);
  const tr = (ru, en) => save._lang === 1 ? en : ru;
  function persist() {
    const snapshot = JSON.stringify(save);
    localStorage.setItem(key, snapshot);
    if (!online) return;
    localStorage.setItem(pendingKey, 'true');
    // A settings write can outlive navigation. Keep writes for the same account in order.
    const saving = (pendingSaves.get(key) ?? Promise.resolve()).then(async () => {
      try {
        await saveXoxiProgress(JSON.parse(snapshot));
        if (pendingSaves.get(key) === saving && localStorage.getItem(key) === snapshot) localStorage.removeItem(pendingKey);
      } catch {
        if (!signal.aborted && localStorage.getItem(key) === snapshot) {
          notify(tr('Настройки и статистика сохранены в браузере. Синхронизация повторится при открытии ХОХИ.',
            'Settings and stats are saved in the browser. Sync will retry when you open XOHI.'));
        }
      }
    });
    pendingSaves.set(key, saving);
    void saving.then(() => {
      if (pendingSaves.get(key) === saving) pendingSaves.delete(key);
    });
  }
  if (online && raw && localStorage.getItem(pendingKey) && !pendingSaves.has(key)) persist();
  return { save, tr, persist };
}

const backToMenu = tr => `<div><a class="btn secondary" href="#xoxi">← ${tr('В меню ХОХИ', 'XOHI menu')}</a></div>`;
const botLabel = tr => tr('Игра против бота', 'Play against the bot');
const localLabel = tr => tr('Игра на одном экране', 'Play on one screen');
const levels = tr => [tr('Лёгкая', 'Easy'), tr('Средняя', 'Medium'), tr('Сложная', 'Hard')];

export async function xoxi(context) {
  if (context.id !== null) return xoxiRoom(context);
  if (context.segment === 'rooms') return xoxiRoomList(context);
  const progress = await loadProgress(context);
  if (!progress || context.signal.aborted) return;
  const { main, signal, segment } = context;
  const { tr } = progress;
  if (segment === 'settings') return xoxiSettings(main, progress);
  if (segment === 'bot' || segment === 'local') return xoxiGame(main, signal, progress, segment === 'bot');
  main.innerHTML = `${title('ХОХИ', tr('Соберите четыре фигуры в ряд.', 'Connect four pieces in a row.'))}
    <nav class="xoxi-menu" aria-label="${tr('Меню ХОХИ', 'XOHI menu')}">
      <a class="btn" href="#xoxi/bot">${botLabel(tr)}</a>
      <a class="btn" href="#xoxi/local">${localLabel(tr)}</a>
      <a class="btn" href="#xoxi/rooms">${tr('Мультиплеер', 'Multiplayer')}</a>
      <a class="btn secondary" href="#xoxi/settings">${tr('Настройки', 'Settings')}</a>
    </nav>`;
}

function xoxiSettings(main, { save, tr, persist }) {
  const settings = () => {
    main.innerHTML = `<div class="xoxi-screen stack">${backToMenu(tr)}${title(tr('Настройки ХОХИ', 'XOHI settings'))}
      <section class="panel stack" aria-label="${tr('Параметры игры', 'Game options')}">
        <label>${tr('Ваша фигура против бота', 'Your piece against the bot')}<select data-option="playerFigure">
          <option value="1" ${save.playerFigure === 1 ? 'selected' : ''}>×</option>
          <option value="2" ${save.playerFigure === 2 ? 'selected' : ''}>○</option></select></label>
        <label>${tr('Сложность', 'Difficulty')}<select data-option="difficult">
          ${levels(tr).map((text, index) => `<option value="${index}" ${index === save.difficult ? 'selected' : ''}>${text}</option>`).join('')}</select></label>
        <label>${tr('Язык', 'Language')}<select data-option="_lang">
          <option value="0" ${save._lang !== 1 ? 'selected' : ''}>Русский</option>
          <option value="1" ${save._lang === 1 ? 'selected' : ''}>English</option></select></label>
        ${[['timerTurn', tr('Таймер хода', 'Turn timer')], ['markLastFigure', tr('Выделять последний ход', 'Highlight the last move')],
          ['anim', tr('Анимация', 'Animation')], ['sound', tr('Звук', 'Sound')]].map(([key, text]) =>
            `<label class="check"><input type="checkbox" data-option="${key}" ${save[key] ? 'checked' : ''}>${text}</label>`).join('')}
        <label>${tr('Скорость анимации', 'Animation speed')}<input data-option="animationSpeed" type="range" min=".05" max=".5" step=".05" value="${e(save.animationSpeed)}"></label>
        <details><summary>${tr('Статистика', 'Statistics')}</summary><div class="rows">
          <p>${tr('Вдвоём', 'Two players')}: ${save.gamesInLocalMode} · × ${save.crossWinsInLocalMode} · ○ ${save.nullsWinsInLocalMode}</p>
          ${levels(tr).map((label, index) => `<p>${label}: ${save.gamesAll[index]} · ${tr('побед', 'wins')} ${save.wins[index]} · ${tr('поражений', 'losses')} ${save.loses[index]}</p>`).join('')}
        </div></details>
      </section></div>`;
    main.querySelectorAll('[data-option]').forEach(input => {
      input.onchange = () => {
        const option = input.dataset.option;
        save[option] = input.type === 'checkbox' ? input.checked : Number(input.value);
        persist();
        if (option === '_lang') {
          settings();
          main.querySelector('[data-option="_lang"]').focus();
        }
      };
    });
  };
  settings();
}

function xoxiGame(main, signal, { save, tr, persist }, bot) {
  main.innerHTML = `<div class="xoxi-screen xoxi-game stack">${backToMenu(tr)}${title(bot ? botLabel(tr) : localLabel(tr),
    tr('Четыре одинаковые фигуры в ряд приносят победу.', 'Connect four to win.'))}
    <section class="xoxi-shell stack">
      <div class="row"><h2 id="xoxi-status" aria-live="polite"></h2><span id="xoxi-time" class="xoxi-clock"></span></div>
      <div id="xoxi-board" class="xoxi"></div>
      <div class="actions"><button class="btn secondary" id="xoxi-play" type="button">${tr('Новая партия', 'New game')}</button></div>
    </section></div>`;
  const grid = main.querySelector('#xoxi-board');
  const status = main.querySelector('#xoxi-status');
  const time = main.querySelector('#xoxi-time');
  const sound = new Audio('/assets/xoxi-drop.mp3');
  const match = { bot, player: save.playerFigure, difficulty: save.difficult, timer: save.timerTurn };
  let board;
  let turn = 1;
  let last = [];
  let lines = [];
  let moves = 0;
  let result = null;
  let deadline = 0;
  let timer;
  let botTimer;
  const isBotTurn = () => match.bot && turn !== match.player;
  function drawBoard(animate = false) {
    status.textContent = result ?? (isBotTurn() ? tr('Ход бота', 'Bot turn') : `${tr('Ход', 'Turn')} ${figure(turn)}`);
    drawColumns(grid, board, { canMove: !result && !isBotTurn(), last, lines,
      animate: animate && save.anim, mark: save.markLastFigure, columnLabel: tr('Колонка', 'Column'), emptyLabel: tr('пусто', 'empty') });
    time.hidden = !match.timer || Boolean(result);
  }

  function finish(winner, timeout = false) {
    clearInterval(timer);
    clearTimeout(botTimer);
    result = winner ? `${tr('Победа', 'Winner')} ${figure(winner)}${timeout ? tr(' по времени', ' on time') : ''}` : tr('Ничья', 'Draw');
    if (match.bot) {
      const level = match.difficulty;
      save.gamesAll[level]++;
      save[match.player === 1 ? 'gamesForCross' : 'gamesForNulls'][level]++;
      save.cellsFilled[level] += moves;
      if (winner) save[winner === match.player ? 'wins' : 'loses'][level]++;
    } else {
      save.gamesInLocalMode++;
      if (winner) save[winner === 1 ? 'crossWinsInLocalMode' : 'nullsWinsInLocalMode']++;
    }
    persist();
  }

  function move(column) {
    const row = board[column].length;
    if (row >= height || result) return;
    board[column].push(turn);
    last = [column, row];
    moves++;
    if (save.sound) {
      sound.currentTime = 0;
      void sound.play().catch(() => {});
    }
    lines = winningLines(board, column, row, turn);
    if (lines.length) finish(turn);
    else if (moves === width * height) finish(0);
    else {
      turn = opposite(turn);
      queueTurn();
    }
    drawBoard(true);
  }

  function queueTurn() {
    deadline = Date.now() + 1000 * seconds[match.difficulty];
    tick();
    if (isBotTurn()) botTimer = setTimeout(() => move(botColumn(board, turn, match.difficulty)), 290);
  }

  function tick() {
    const remaining = Math.max(0, Math.ceil((deadline - Date.now()) / 1000));
    time.textContent = `${remaining} ${tr('с', 's')}`;
    if (remaining === 0 && match.timer && !result) { finish(opposite(turn), true); drawBoard(); }
  }
  function startGame() {
    clearInterval(timer);
    clearTimeout(botTimer);
    board = Array.from({ length: width }, () => []);
    last = [];
    lines = [];
    result = null;
    turn = 1;
    moves = 0;
    queueTurn();
    if (match.timer) timer = setInterval(tick, 200);
    drawBoard();
  }
  grid.onclick = event => {
    const button = event.target.closest('[data-column]');
    if (!button || button.disabled || isBotTurn()) return;
    move(Number(button.dataset.column));
    grid.querySelector(`[data-column="${button.dataset.column}"]:enabled`)?.focus();
  };
  main.querySelector('#xoxi-play').onclick = startGame;
  signal.addEventListener('abort', () => { clearTimeout(botTimer); clearInterval(timer); sound.pause(); }, { once: true });
  grid.style.setProperty('--drop-time', `${Math.round(75 + 25 / Math.max(.05, Number(save.animationSpeed) || .15))}ms`);
  startGame();
}
