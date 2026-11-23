self.addEventListener('install', event => event.waitUntil(self.skipWaiting()));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));

self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET' || event.request.mode !== 'navigate') return;
  const url = new URL(event.request.url);
  if (url.origin === self.location.origin && (url.pathname === '/' || url.pathname === '/index.html')) {
    event.respondWith(fetch(event.request, { cache: 'no-cache' }));
  }
});

function sameOrigin(value, fallback) {
  try {
    const url = new URL(value || fallback, self.location.origin);
    return url.origin === self.location.origin ? url.href : fallback;
  } catch {
    return fallback;
  }
}

self.addEventListener('push', event => {
  let payload = {};
  if (event.data) {
    try { payload = event.data.json() || {}; }
    catch { payload = { body: event.data.text() }; }
  }
  event.waitUntil(self.registration.showNotification(payload.title || 'Пыш-Услуги', {
    body: payload.body || 'У вас новое уведомление.',
    icon: sameOrigin(payload.icon, '/favicon.png'),
    badge: '/favicon.png',
    data: { url: sameOrigin(payload.url, '/') }
  }));
});

self.addEventListener('notificationclick', event => {
  event.notification.close();
  const target = sameOrigin(event.notification.data?.url, '/');
  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    const tab = windows.find(client => new URL(client.url).pathname === '/');
    if (!tab) return self.clients.openWindow(target);
    await tab.focus();
    return tab.navigate(target);
  })());
});
