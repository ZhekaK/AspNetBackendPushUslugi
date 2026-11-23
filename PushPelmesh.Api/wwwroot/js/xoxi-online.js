import { empty, errorLine, escapeHtml as e, onSubmit, title } from './ui.js';
import { drawColumns, figure, height, width, winningLines } from './xoxi-board.js';

const path = '/api/xoxi/rooms';
const back = (href, label) => `<div><a class="btn secondary" href="${href}">← ${label}</a></div>`;

export async function xoxiRoomList({ main, api, signal }) {
  const rooms = await api(path);
  if (signal.aborted) return;
  main.innerHTML = `<div class="xoxi-shell stack">${back('#xoxi', 'В меню ХОХИ')}${title('ХОХИ по сети')}
    <div class="xoxi-layout"><section class="xoxi-panel stack">
      <div class="row"><h2>Комнаты</h2><button class="btn secondary" id="refresh-xoxi" type="button">Обновить</button></div>
      <div id="xoxi-room-list" class="rows"></div><p class="form-error" id="xoxi-list-error" role="status"></p>
    </section><form id="xoxi-create" class="xoxi-panel stack"><h2>Новая комната</h2>
      <label>Название<input name="roomName" maxlength="80" required></label>
      <label>Пароль (необязательно)<input name="password" type="password" maxlength="128" autocomplete="new-password"></label>
      <p class="meta">В закрытой комнате пароль нужен и игрокам, и наблюдателям.</p>
      <div><button class="btn" type="submit">Создать комнату</button></div>${errorLine}
    </form></div></div>`;

  const list = main.querySelector('#xoxi-room-list');
  const refresh = main.querySelector('#refresh-xoxi');
  const message = main.querySelector('#xoxi-list-error');
  const show = entries => {
    list.innerHTML = entries.length ? entries.map(room => `<a class="xoxi-room-link row" href="#xoxi/${room.id}">
      <strong>${e(room.name)}</strong><span class="meta">${room.locked ? 'Под паролем · ' : ''}${room.started ? 'Идёт игра' : room.full ? 'Ожидает старта' : 'Ждёт игрока'}</span></a>`).join('')
      : empty('Пока нет доступных комнат.');
  };
  show(rooms);
  refresh.onclick = async () => {
    refresh.disabled = true;
    message.textContent = '';
    try { show(await api(path)); }
    catch (error) { if (!signal.aborted) message.textContent = error.message; }
    finally { refresh.disabled = false; }
  };
  onSubmit(main.querySelector('#xoxi-create'), async data => {
    const room = await api(path, { method: 'POST', body: { name: data.get('roomName').trim(), password: data.get('password') } });
    if (!signal.aborted) location.hash = `xoxi/${room.id}`;
  });
}

export async function xoxiRoom(context) {
  const { main, api, signal, id } = context;
  const roomPath = `${path}/${id}`;
  try {
    const state = await api(roomPath);
    if (!signal.aborted) showRoom(context, roomPath, state);
  } catch (error) {
    if (signal.aborted) return;
    if (error.status !== 403 || error.data?.code !== 'xoxi_password_required') throw error;
    main.innerHTML = `<div class="xoxi-screen stack">${back('#xoxi/rooms', 'К комнатам')}${title(error.data.name, 'Для входа нужен пароль комнаты.')}
      <form class="panel stack"><label>Пароль комнаты<input name="password" type="password" maxlength="128" required></label>
      <div><button class="btn" type="submit">Войти в комнату</button></div>${errorLine}</form></div>`;
    onSubmit(main.querySelector('form'), async data => {
      const state = await api(`${roomPath}/access`, { method: 'POST', body: { password: data.get('password') } });
      if (!signal.aborted) showRoom(context, roomPath, state);
    });
  }
}

function showRoom({ main, api, signal, notify }, roomPath, state) {
  main.innerHTML = `<div class="xoxi-shell stack">${back('#xoxi/rooms', 'К комнатам')}${title(state.name, 'ХОХИ по сети')}
    <section class="xoxi-panel stack"><div class="row xoxi-players"><strong id="xoxi-crosses"></strong><strong id="xoxi-noughts"></strong></div>
      <p id="xoxi-room-status" role="status"></p><div class="xoxi" id="xoxi-board"></div>
      <div class="actions"><button class="btn" type="button" id="xoxi-join" hidden></button>
        <button class="btn" type="button" id="xoxi-start" hidden>Начать игру</button>
        <button class="btn secondary" type="button" id="xoxi-swap" hidden>Поменять фигуры</button>
        <button class="btn secondary" type="button" id="xoxi-share">Копировать ссылку</button>
        <a class="btn secondary" href="#xoxi/rooms" id="xoxi-finished" hidden>К комнатам</a></div>
      <label id="xoxi-share-field" hidden>Ссылка на комнату<input readonly></label>
      <p class="form-error" id="xoxi-room-error" role="status"></p>
    </section></div>`;
  const grid = main.querySelector('#xoxi-board');
  const status = main.querySelector('#xoxi-room-status');
  const join = main.querySelector('#xoxi-join');
  const start = main.querySelector('#xoxi-start');
  const swap = main.querySelector('#xoxi-swap');
  const problem = main.querySelector('#xoxi-room-error');
  const finished = main.querySelector('#xoxi-finished');
  let updating = false;
  let poll;

  function draw(animate = false) {
    const other = state.opponent ?? 'Свободное место';
    const [crosses, noughts] = state.ownerFigure === 1 ? [state.owner, other] : [other, state.owner];
    main.querySelector('#xoxi-crosses').textContent = `× ${crosses}`;
    main.querySelector('#xoxi-noughts').textContent = `○ ${noughts}`;
    const role = state.figure ? `Вы играете за ${figure(state.figure)}` : 'Вы наблюдаете';
    const lobby = !state.opponent ? 'Ждём второго игрока' : state.isOwner ? 'Можно начать игру' : 'Ждём запуска игры';
    const round = state.winner !== null ? state.winner === 0 ? 'Ничья' : `Победа ${figure(state.winner)}`
      : !state.started ? lobby
      : state.turn === state.figure ? 'Ваш ход' : `Ход ${figure(state.turn)}`;
    const description = `${role} · ${round}`;
    if (status.textContent !== description) status.textContent = description;
    join.hidden = Boolean(state.figure || state.opponent);
    join.textContent = `Занять место ${figure(3 - state.ownerFigure)}`;
    start.hidden = !state.isOwner || !state.opponent || state.started;
    swap.hidden = !state.isOwner || state.started;
    for (const button of [join, start, swap]) button.disabled = updating;
    finished.hidden = state.winner === null;
    const board = Array.from({ length: width }, (_, column) => state.cells.slice(column * height, (column + 1) * height));
    const last = state.lastCell === null ? [] : [Math.floor(state.lastCell / height), state.lastCell % height];
    const lines = state.winner > 0 && last.length ? winningLines(board, ...last, state.winner) : [];
    drawColumns(grid, board, { canMove: !updating && state.figure > 0 && state.turn === state.figure, last, animate, lines });
  }

  async function refresh() {
    try {
      if (updating || document.hidden) return;
      const next = await api(roomPath);
      if (!updating && !signal.aborted && BigInt(next.version) > BigInt(state.version)) {
        const animate = next.lastCell !== state.lastCell;
        state = next;
        draw(animate);
      }
    } catch (error) {
      if (!signal.aborted) problem.textContent = error.message;
    } finally {
      if (!signal.aborted && state.winner === null) poll = setTimeout(refresh, 1200);
    }
  }

  async function update(action, options) {
    if (updating) return;
    updating = true;
    problem.textContent = '';
    draw();
    try {
      state = await api(`${roomPath}/${action}`, { method: 'POST', ...options });
    } catch (error) {
      if (signal.aborted) return;
      problem.textContent = error.message;
    } finally {
      updating = false;
      if (!signal.aborted) draw(action === 'move' && !problem.textContent);
    }
  }

  join.onclick = () => update('join');
  start.onclick = () => update('start', { version: state.version });
  swap.onclick = () => update('swap', { version: state.version });
  grid.onclick = event => {
    const button = event.target.closest('[data-column]');
    if (button && !button.disabled)
      void update('move', { version: state.version, body: { column: Number(button.dataset.column) } });
  };
  const share = main.querySelector('#xoxi-share');
  share.onclick = async () => {
    try {
      await navigator.clipboard.writeText(location.href);
      notify('Ссылка скопирована.');
    } catch {
      const field = main.querySelector('#xoxi-share-field');
      field.hidden = false;
      field.querySelector('input').value = location.href;
      field.querySelector('input').select();
    }
  };
  signal.addEventListener('abort', () => clearTimeout(poll), { once: true });
  draw();
  if (state.winner === null) poll = setTimeout(refresh, 1200);
}
