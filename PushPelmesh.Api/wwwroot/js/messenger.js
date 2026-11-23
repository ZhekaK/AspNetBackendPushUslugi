import { empty, errorLine, escapeHtml as e, onSubmit, timeLabel, title } from './ui.js';

const root = '/api/messenger';

export async function messenger({ main, api, id, user, signal, notify, refreshUnread }) {
  const [chatData, directory] = await Promise.all([api(`${root}/chats`), api(`${root}/users`)]);
  const people = directory.users.filter(person => person.id !== user.id);
  const selected = id ?? 0;
  let chats = chatData.chats;
  let chat = chats.find(item => item.id === selected);
  if (!chat) {
    main.innerHTML = title('Мессенджер') + empty('Чат недоступен.') + '<p class="block"><a href="#messenger">К общему чату</a></p>';
    return;
  }
  const path = `${root}/chats/${selected}`;
  main.innerHTML = title('Мессенджер') + `<div class="split messenger">
    <aside class="panel stack"><h2>Чаты</h2><nav class="chat-list rows" aria-label="Чаты"></nav>
      ${people.length ? `<details><summary>Новый чат</summary><div class="stack">
        <form id="direct" class="stack"><label>Личное сообщение<select name="userId" required><option value="">Выберите участника</option>
        ${people.map(person => `<option value="${person.id}">${e(person.displayName)}</option>`).join('')}</select></label>
        <div><button class="btn secondary" type="submit">Открыть чат</button></div>${errorLine}</form>
        <form id="group" class="stack"><label>Название группы<input name="title" maxlength="120" required></label>
        <fieldset class="participant-list"><legend>Участники</legend>${people.map(person => `<label class="check"><input type="checkbox" name="person" value="${person.id}">${e(person.displayName)}</label>`).join('')}</fieldset>
        <div><button class="btn secondary" type="submit">Создать группу</button></div>${errorLine}</form>
      </div></details>` : ''}
    </aside>
    <section class="panel stack"><div class="row"><h2>${e(chat.title)}</h2><button class="btn secondary" id="mute" type="button"></button></div>
      <div class="chat-area" tabindex="0" role="log" aria-label="Сообщения"></div>
      <p id="chat-status" class="form-error" role="status"></p>
      <form id="send-message" class="stack"><label>Сообщение<textarea name="text" maxlength="2000" rows="2" required></textarea></label>
      <div><button class="btn" type="submit">Отправить</button></div>${errorLine}</form>
    </section>
  </div>`;
  const area = main.querySelector('.chat-area');
  const status = main.querySelector('#chat-status');
  const mute = main.querySelector('#mute');
  const list = main.querySelector('.chat-list');
  let lastId = 0;
  let readId = 0;
  let loading = false;
  let refreshAgain = false;
  let timer;
  const drawChats = () => {
    list.innerHTML = chats.map(item => `<a class="row" href="#messenger/${item.id}" ${item.id === selected ? 'aria-current="page"' : ''}>
      <span>${e(item.title)}</span>${item.unread && item.id !== selected ? '<span class="badge">Новое</span>' : ''}</a>`).join('');
    mute.textContent = chat.muted ? 'Включить уведомления' : 'Отключить уведомления';
  };
  drawChats();

  async function refresh() {
    if (signal.aborted || document.hidden) return;
    if (loading) { refreshAgain = true; return; }
    clearTimeout(timer);
    loading = true;
    try {
      const [messagesResponse, chatsResponse] = await Promise.all([
        api(`${path}/messages?afterId=${lastId}`), api(`${root}/chats`)
      ]);
      if (signal.aborted) return;
      const incoming = messagesResponse.messages;
      const stickToEnd = !lastId || area.scrollHeight - area.scrollTop - area.clientHeight < 50;
      if (incoming.length) {
        area.querySelector('.meta-empty')?.remove();
        area.insertAdjacentHTML('beforeend', incoming.map(message => `<article class="message${message.senderUserId === user.id ? ' mine' : ''}">
          <div class="meta">${e(message.senderName)} · ${timeLabel(message.createdAt)}</div><p class="preline">${e(message.text)}</p></article>`).join(''));
        lastId = incoming.at(-1).id;
        if (stickToEnd) area.scrollTop = area.scrollHeight;
      } else if (!area.children.length) area.innerHTML = '<p class="meta meta-empty">Сообщений пока нет.</p>';
      if (lastId > readId && area.scrollHeight - area.scrollTop - area.clientHeight < 50) await markRead();
      chats = chatsResponse.chats;
      chat = chats.find(item => item.id === selected) || chat;
      drawChats();
      status.textContent = '';
      if (incoming.length === 100) refreshAgain = true;
    } catch (error) {
      if (!signal.aborted) status.textContent = error.message;
    } finally {
      loading = false;
      if (!signal.aborted) {
        timer = setTimeout(refresh, refreshAgain ? 0 : 4000);
        refreshAgain = false;
      }
    }
  }

  async function markRead() {
    if (lastId <= readId || document.hidden || signal.aborted) return;
    const messageId = lastId;
    await api(`${path}/read`, { method: 'POST', body: { lastMessageId: messageId } });
    readId = Math.max(readId, messageId);
    void refreshUnread();
  }

  area.onscroll = () => {
    if (!loading && area.scrollHeight - area.scrollTop - area.clientHeight < 50) {
      void markRead().catch(error => { if (!signal.aborted) status.textContent = error.message; });
    }
  };
  mute.onclick = async () => {
    mute.disabled = true;
    const muted = !chat.muted;
    try {
      await api(`${path}/mute`, { method: 'POST', body: { muted } });
      chat.muted = muted;
      drawChats();
      void refresh();
    } catch (error) { if (!signal.aborted) notify(error.message); }
    finally { mute.disabled = false; }
  };
  const send = main.querySelector('#send-message');
  onSubmit(send, async data => {
    const text = data.get('text').trim();
    if (!text) throw new Error('Напишите сообщение.');
    await api(`${path}/messages`, { method: 'POST', body: { text } });
    if (send.elements.text.value.trim() === text) send.elements.text.value = '';
    area.scrollTop = area.scrollHeight;
    await refresh();
    send.elements.text.focus();
  });
  const direct = main.querySelector('#direct');
  if (direct) {
    onSubmit(direct, async data => {
      const result = await api(`${root}/chats/direct`, { method: 'POST', body: { userId: Number(data.get('userId')) } });
      location.hash = `messenger/${result.chat.id}`;
    });
    onSubmit(main.querySelector('#group'), async data => {
      const userIds = data.getAll('person').map(Number);
      if (userIds.length < 1 || userIds.length > 50) throw new Error('Выберите от 1 до 50 участников.');
      const result = await api(`${root}/chats/group`, { method: 'POST', body: { title: data.get('title').trim(), userIds } });
      location.hash = `messenger/${result.chat.id}`;
    });
  }
  document.addEventListener('visibilitychange', refresh, { signal });
  signal.addEventListener('abort', () => clearTimeout(timer), { once: true });
  await refresh();
}
