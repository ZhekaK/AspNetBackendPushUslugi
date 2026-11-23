import assert from 'node:assert/strict';
import { runInNewContext } from 'node:vm';

const [, , baseUrl, adminKey] = process.argv;
if (!baseUrl || !adminKey || !['localhost', '127.0.0.1', '[::1]'].includes(new URL(baseUrl).hostname)) {
  throw new Error('Usage: node tests/site-smoke.mjs http://localhost:<port> <test-admin-key>');
}

const suiteName = `Site test ${Date.now()}`;
const today = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Moscow' }).format(new Date());

async function request(path, { body, token, version, method = 'GET', admin = false, status = 200 } = {}) {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  if (version) headers['X-Game-Version'] = version;
  if (admin) headers['X-Admin-Key'] = adminKey;
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  const response = await fetch(new URL(path, baseUrl), { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  const text = await response.text();
  if (status !== null) assert.equal(response.status, status, `${method} ${path}: ${text.slice(0, 300)}`);
  const data = response.headers.get('content-type')?.includes('json') && text ? JSON.parse(text) : text;
  return { data, status: response.status };
}

const post = (path, body, options) => request(path, { method: 'POST', body, ...options });

async function createUser(firstName, role, middleName = null, lastName = null) {
  const { data: key } = await post('/api/admin/keys/generate', {
    series: 'TEST', firstName, middleName, lastName, birthDate: '2000-01-01', sex: 0
  }, { admin: true });
  const { data: session } = await post('/api/auth/login-by-key', { series: key.series, number: key.number });
  if (role) await post('/api/admin/roles/generate', {
    userSeries: key.series, userNumbers: key.number, post: role, startDate: today
  }, { admin: true });
  return session;
}

for (const [path, marker] of [['/', '/js/app.js'], ['/admin', 'Админ-панель'], ['/admin/', 'Админ-панель'],
  ['/service-worker.js', 'notificationclick']]) {
  assert.ok((await request(path)).data.includes(marker), path);
}
const manifest = (await request('/manifest.webmanifest')).data;
assert.equal(manifest.name, 'Пыш-Услуги');
assert.equal(manifest.id, '/');
assert.equal(manifest.start_url, '/?app=web');
assert.deepEqual((await request('/manifest.json')).data, manifest);
for (const path of ['/', '/index.html', manifest.start_url, '/manifest.json', '/manifest.webmanifest', '/service-worker.js']) {
  const response = await fetch(new URL(path, baseUrl), { method: 'HEAD' });
  assert.equal(response.status, 200, path);
  assert.equal(response.headers.get('cache-control'), 'no-cache', path);
}
const legacyLoader = await fetch(new URL('/Build/Push-Uslugi.loader.js', baseUrl));
assert.equal(legacyLoader.status, 200);
assert.equal(legacyLoader.headers.get('cache-control'), 'no-store');
let recoveryPath;
const legacyWindow = { location: { hash: '#xoxi', replace: value => { recoveryPath = value; } } };
runInNewContext(await legacyLoader.text(), { window: legacyWindow });
await legacyWindow.createUnityInstance();
assert.equal(recoveryPath, `${manifest.start_url}#xoxi`);
const recovered = (await request(recoveryPath)).data;
assert.ok(recovered.includes('/js/app.js') && recovered.includes('href="/manifest.json"'));
assert.ok(!recovered.includes('unity-canvas'));
await request('/api/admin/access', { status: 401 });
await request('/api/admin/access', { admin: true, status: 204 });
await request('/api/admin/cleanup-guests', { method: 'DELETE', status: 401 });
await request('/api/admin/users', { status: 401 });
await request('/api/votes/polls', { status: 401 });
console.log('Site, admin and authentication gates passed.');

const president = await createUser('Президент', 3);
const minister = await createUser('Министр', 1);
const member = await createUser('Участник', null, 'Тестов', 'Тестович');
const { data: guest } = await post('/api/auth/guest');
const { data: reopened } = await post('/api/auth/login-by-name', { firstName: 'Участник', rememberToken: member.rememberToken });
assert.equal(reopened.user.id, member.user.id);
await request('/api/user/profile', { token: reopened.token });

const votePath = '/api/votes/polls';
const pollBody = { title: suiteName, endDate: today, options: ['Первый', 'Второй', 'Третий'] };
await post(votePath, { ...pollBody, title: 'а'.repeat(257) }, { token: member.token, status: 400 });
await post(votePath, { ...pollBody, endDate: '2000-01-01' }, { token: member.token, status: 400 });
await post(votePath, { ...pollBody, options: ['Одно', 'одно '] }, { token: member.token, status: 400 });

const { data: { poll: single } } = await post(votePath, pollBody, { token: member.token });
const singleUrl = `${votePath}/${single.id}`;
const concurrentVotes = await Promise.all(single.options.slice(0, 2).map(option =>
  post(`${singleUrl}/vote`, { optionId: option.id }, { token: member.token, status: null })));
assert.deepEqual(concurrentVotes.map(result => result.status).sort(), [200, 400]);
const { data: { poll: singleResult } } = await request(singleUrl, { token: member.token });
assert.equal(singleResult.totalVotes, 1);
assert.equal(singleResult.selectedOptionIds.length, 1);
assert.equal(singleResult.canVote, false);

const { data: { poll: multiple } } = await post(votePath, {
  ...pollBody, title: `${suiteName} ministers`, isMultipleChoice: true, audienceGroups: ['Ministers']
}, { token: president.token });
const multipleUrl = `${votePath}/${multiple.id}`;
await request(multipleUrl, { token: member.token, status: 403 });
const { data: { polls } } = await request(votePath, { token: member.token });
assert.ok(!polls.some(item => item.id === multiple.id));
await request(multipleUrl, { token: president.token });
const options = multiple.options.slice(0, 2).map(option => option.id);
await post(`${multipleUrl}/vote`, { optionIds: [single.options[0].id] }, { token: minister.token, status: 400 });
const { data: { poll: multipleResult } } = await post(`${multipleUrl}/vote`, { optionIds: [...options, options[0]] }, { token: minister.token });
assert.equal(multipleResult.totalVotes, 2);
assert.deepEqual(multipleResult.options.map(option => [option.votes, option.percent]), [[1, 50], [1, 50], [0, 0]]);
const { data: { notifications } } = await request('/api/module-notifications/unread', { token: minister.token });
assert.ok(notifications.some(item => item.moduleKey === 'VoteModule'));
await post('/api/module-notifications/VoteModule/read', undefined, { token: minister.token });
console.log('Single and multiple choice, concurrent voting, dates and audiences passed.');

async function startGame(kind) {
  const path = `/api/${kind}/rooms`;
  const { data: { room: pending } } = await post(path, {
    name: suiteName, maxPlayers: 2, password: 'fixture', cardCount: 36, targetScore: 5000
  }, { token: member.token });
  const roomUrl = `${path}/${pending.id}`;
  await post(`${roomUrl}/join`, { password: 'wrong' }, { token: minister.token, status: 400 });
  const { data: { room: joined } } = await post(`${roomUrl}/join`, { password: 'fixture' }, { token: minister.token });
  await request(roomUrl, { token: guest.token, status: 403 });
  await post(`${roomUrl}/start`, undefined, { token: member.token, version: pending.version, status: 409 });
  const { data: { room: started } } = await post(`${roomUrl}/start`, undefined, { token: member.token, version: joined.version });
  const { data: { rooms } } = await request(path, { token: member.token });
  assert.equal(rooms.find(room => room.id === started.id).isMember, true);
  return [roomUrl, started];
}

const [zongUrl, zong] = await startGame('zong');
const roller = zong.isMyTurn ? member : minister;
const waiting = zong.isMyTurn ? minister : member;
await post(`${zongUrl}/roll`, undefined, { token: waiting.token, status: 400 });
const throws = await Promise.all([1, 2].map(() => post(`${zongUrl}/roll`, undefined, {
  token: roller.token, version: zong.version, status: null
})));
assert.deepEqual(throws.map(result => result.status).sort(), [200, 409]);
const rolled = throws.find(result => result.status === 200).data.room;
assert.equal(rolled.lastDice.split(',').length, 6);
if (rolled.canBank) {
  const { data: { room: banked } } = await post(`${zongUrl}/bank`, undefined, { token: roller.token, version: rolled.version });
  assert.equal(banked.players.find(player => player.id === rolled.myPlayerId).score, rolled.currentTurnScore);
}

const [durakUrl, durak] = await startGame('durak');
const attacker = durak.canAttack ? member : minister;
const defender = durak.canAttack ? minister : member;
const { data: { room: opening } } = await request(durakUrl, { token: attacker.token });
assert.equal(opening.myHand.length, 6);
assert.ok(opening.players.every(player => !('hand' in player)));
await post(`${durakUrl}/take`, undefined, { token: defender.token, version: opening.version, status: 400 });
const card = opening.myHand[0];
const { data: { room: attacking } } = await post(`${durakUrl}/attack`, { cardCode: card.code }, { token: attacker.token, version: opening.version });
assert.equal(attacking.table[0].attack.code, card.code);
const { data: { room: defending } } = await request(durakUrl, { token: defender.token });
await post(`${durakUrl}/defend`, {
  cardCode: defending.myHand[0].code, attackCardCode: 'wrong'
}, { token: defender.token, version: defending.version, status: 400 });
const { data: { room: taken } } = await post(`${durakUrl}/take`, undefined, { token: defender.token, version: defending.version });
assert.equal(taken.status, 'InProgress');
assert.equal(taken.table.length, 0);
assert.ok(taken.myHand.some(item => item.code === card.code));
console.log('Room membership, stale game actions, dice and Durak moves passed.');

const { data: { chat } } = await post('/api/messenger/chats/direct', { userId: minister.user.id }, { token: member.token });
await post(`/api/messenger/chats/${chat.id}/messages`, { text: suiteName }, { token: member.token });
const { data: { messages } } = await request(`/api/messenger/chats/${chat.id}/messages?afterId=0`, { token: minister.token });
assert.equal(messages.at(-1).text, suiteName);
assert.equal(messages.at(-1).senderName, 'Тестов Участник Тестович');
const { data: { users } } = await request('/api/messenger/users', { token: minister.token });
assert.equal(users.find(user => user.id === member.user.id).displayName, 'Тестов Участник Тестович');
const { data: { chats } } = await request('/api/messenger/chats', { token: minister.token });
assert.equal(chats.find(item => item.id === chat.id).title, 'Тестов Участник Тестович');
await request('/api/xoxi/save', { method: 'PUT', token: member.token, body: { gamesAll: [2, 0, 0] } });
assert.deepEqual((await request('/api/xoxi/save', { token: member.token })).data, { gamesAll: [2, 0, 0] });
console.log('Messenger and Xoxi save round trips passed.');

const photoPath = '/api/user/profile/photo';
const photo = Buffer.from('/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAACAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD8qqKKKAP/2Q==', 'base64');
const readPhoto = (token, options = {}) => fetch(new URL(photoPath, baseUrl), {
  ...options, headers: { Authorization: `Bearer ${token}`, ...options.headers }
});
assert.equal((await readPhoto(guest.token)).status, 403);
let photoResponse = await readPhoto(member.token, { method: 'PUT', headers: { 'Content-Type': 'image/jpeg' }, body: photo });
assert.equal(photoResponse.status, 204);
photoResponse = await readPhoto(member.token);
assert.equal(photoResponse.headers.get('cache-control'), 'private, no-store');
assert.deepEqual(Buffer.from(await photoResponse.arrayBuffer()), photo);
assert.equal((await readPhoto(minister.token)).status, 404);
assert.equal((await readPhoto(member.token, { method: 'DELETE' })).status, 204);
assert.equal((await readPhoto(member.token)).status, 404);

const xoxiPath = '/api/xoxi/rooms';
const { data: room } = await post(xoxiPath, { name: suiteName }, { token: member.token });
const roomPath = `${xoxiPath}/${room.id}`;
const { data: observing } = await request(roomPath, { token: guest.token });
assert.equal(observing.figure, 0);
assert.equal(observing.turn, 0);
assert.equal(observing.started, false);
await post(`${roomPath}/start`, undefined, { token: member.token, version: observing.version, status: 409 });
const candidates = [minister.token, president.token];
const joins = await Promise.all(candidates.map(token => post(`${roomPath}/join`, undefined, { token })));
assert.deepEqual(joins.map(result => result.data.figure).sort(), [0, 2]);
const secondPlayer = candidates[joins.findIndex(result => result.data.figure === 2)];
let { data: board } = await request(roomPath, { token: member.token });
assert.equal(board.turn, 0);
await post(`${roomPath}/move`, { column: 0 }, { token: member.token, version: board.version, status: 409 });
await post(`${roomPath}/swap`, undefined, { token: secondPlayer, version: board.version, status: 403 });
await post(`${roomPath}/start`, undefined, { token: guest.token, version: board.version, status: 403 });
const beforeSwap = board.version;
board = (await post(`${roomPath}/swap`, undefined, { token: member.token, version: board.version })).data;
assert.equal(board.figure, 2);
assert.equal(board.ownerFigure, 2);
assert.equal((await request(roomPath, { token: secondPlayer })).data.figure, 1);
await post(`${roomPath}/start`, undefined, { token: member.token, version: beforeSwap, status: 409 });
board = (await post(`${roomPath}/start`, undefined, { token: member.token, version: board.version })).data;
assert.equal(board.started, true);
assert.equal(board.turn, 1);
await post(`${roomPath}/swap`, undefined, { token: member.token, version: board.version, status: 409 });
await post(`${roomPath}/move`, { column: 0 }, { token: guest.token, version: board.version, status: 403 });
await post(`${roomPath}/move`, { column: 1 }, { token: member.token, version: board.version, status: 409 });
const shots = await Promise.all([1, 2].map(() =>
  post(`${roomPath}/move`, { column: 0 }, { token: secondPlayer, version: board.version, status: null })));
assert.deepEqual(shots.map(result => result.status).sort(), [200, 409]);
board = shots.find(result => result.status === 200).data;
for (let round = 0; round < 3; round++) {
  board = (await post(`${roomPath}/move`, { column: 1 }, { token: member.token, version: board.version })).data;
  board = (await post(`${roomPath}/move`, { column: 0 }, { token: secondPlayer, version: board.version })).data;
}
assert.equal(board.winner, 1);
assert.deepEqual(board.cells.slice(0, 5), [1, 1, 1, 1, 0]);
const { data: finalBoard } = await request(roomPath, { token: guest.token });
assert.equal(finalBoard.winner, 1);
assert.equal(finalBoard.figure, 0);
await post(`${roomPath}/move`, { column: 0 }, { token: member.token, version: board.version, status: 409 });

const roomPassword = 'room pass 🔑';
const { data: privateRoom } = await post(xoxiPath, { name: 'Закрытая комната', password: roomPassword }, { token: member.token });
const privatePath = `${xoxiPath}/${privateRoom.id}`;
const { data: rooms } = await request(xoxiPath, { token: guest.token });
assert.deepEqual(rooms.find(value => value.id === privateRoom.id),
  { id: privateRoom.id, name: 'Закрытая комната', started: false, full: false, locked: true });
assert.equal((await request(privatePath, { token: member.token })).data.figure, 1);
assert.equal((await request(privatePath, { token: guest.token, status: 403 })).data.code, 'xoxi_password_required');
await post(`${privatePath}/join`, undefined, { token: guest.token, status: 403 });
await post(`${privatePath}/access`, { password: 'неверный' }, { token: guest.token, status: 400 });
await request(privatePath, { token: guest.token, status: 403 });
const admissions = await Promise.all([1, 2].map(() =>
  post(`${privatePath}/access`, { password: roomPassword }, { token: secondPlayer })));
assert.ok(admissions.every(result => result.data.figure === 0));
assert.equal((await post(`${privatePath}/join`, undefined, { token: secondPlayer })).data.figure, 2);
await request(privatePath, { token: guest.token, status: 403 });
assert.equal((await post(`${privatePath}/access`, { password: roomPassword }, { token: guest.token })).data.figure, 0);
assert.equal((await request(privatePath, { token: guest.token })).data.figure, 0);
await post(xoxiPath, { name: 'Слишком длинный пароль', password: 'a'.repeat(129) }, { token: member.token, status: 400 });
console.log('Private photos, Xoxi lobby, swaps, password protection and spectators passed.');
