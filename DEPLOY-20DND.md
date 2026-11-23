# Пыш-Услуги на 20DND

`push-pelmesh.ru` и `www.push-pelmesh.ru` обслуживает отдельный виртуальный хост Nginx. Приложение работает как `push-pelmesh.service` на `127.0.0.1:5088`. Каталог активного релиза задан ссылкой `/srv/push-pelmesh/current`. Сертификат Let’s Encrypt и его обновление уже настроены.

Действующая база `pushpelmesh_restore` восстановлена из production-базы `pushpelmeshdb` с `adminvps_fin`. Публичный API направлен на `20DND`. Дамп и архив файлов переноса хранятся в закрытом каталоге `/var/backups/push-pelmesh/` на новом сервере.

## Обновление сайта через WinSCP

Подключитесь по **SFTP** к `79.133.183.193:22` пользователем `codex` с тем же SSH-ключом, что у алиаса `20DND`. В правой панели WinSCP откройте `/home/codex/deploy/push-pelmesh/`. Из локальной папки `Publish\20DND\` положите туда два файла.

- `push-pelmesh-web-linux-x64.tar.gz` содержит собранный ASP.NET Core, библиотеки и весь сайт в `wwwroot`, включая админку.
- `push-pelmesh-web-linux-x64.tar.gz.sha256` содержит контрольную сумму архива.

Production-настройки хранятся отдельно в `/etc/push-pelmesh/appsettings.Production.json` с правами `0640 root:pushpelmesh`. Они подключаются к каждому релизу ссылкой. Архив обновляет приложение и не содержит ключей или базы. Исходники Unity, каталоги `bin`/ `obj`, локальные `appsettings*.json` и ключи TLS загружать не требуется.

После загрузки откройте SSH-сеанс на `20DND` и выполните команды.

```bash
set -eu
cd /home/codex/deploy/push-pelmesh
sha256sum -c push-pelmesh-web-linux-x64.tar.gz.sha256
previous=$(readlink /srv/push-pelmesh/current)
release="/srv/push-pelmesh/releases/$(date -u +%Y%m%dT%H%M%SZ)"
sudo install -d -o root -g root -m 0755 "$release"
sudo tar -xzf push-pelmesh-web-linux-x64.tar.gz --no-same-owner --no-same-permissions -C "$release"
sudo test -f "$release/PushPelmesh.Api.dll"
sudo test -f "$release/wwwroot/admin/index.html"
sudo ln -s /etc/push-pelmesh/appsettings.Production.json "$release/appsettings.Production.json"
sudo ln -s "$release" /srv/push-pelmesh/current.next
sudo mv -Tf /srv/push-pelmesh/current.next /srv/push-pelmesh/current
sudo systemctl restart push-pelmesh.service
if ! curl -fsS --retry 5 --retry-connrefused --max-time 5 http://127.0.0.1:5088/api/ping; then
  sudo ln -s "$previous" /srv/push-pelmesh/current.next
  sudo mv -Tf /srv/push-pelmesh/current.next /srv/push-pelmesh/current
  sudo systemctl restart push-pelmesh.service
  exit 1
fi
```

## Перенос базы с прежнего сервера

**1. Найдите активную production-базу.** На прежнем сервере определите службу ASP.NET Core и имя её PostgreSQL-базы по действующей строке `ConnectionStrings:DefaultConnection`. Используйте конфигурацию этой службы: имя из локального проекта может относиться к тестовой базе. Сравните версии PostgreSQL командой `sudo -u postgres psql -XAtc 'show server_version;'` на обоих серверах. На `20DND` используется PostgreSQL 16. Если исходный сервер новее, подготовьте отдельный кластер подходящей версии; перенос на более старую версию этой инструкцией не предусмотрен.

**2. Сделайте дамп.** Пробную копию можно сделать во время работы старого сайта. Для окончательного дампа остановите именно старую службу «Пыш-Услуг», чтобы записи не менялись после снимка. На старом сервере подставьте найденное имя базы в следующую команду.

```bash
DB_NAME='имя_активной_production_базы'
umask 077
sudo -u postgres pg_dump --format=custom --no-owner --no-privileges \
  --dbname="$DB_NAME" > "$HOME/pushpelmesh-prod.dump"
test -s "$HOME/pushpelmesh-prod.dump"
cd "$HOME"
sha256sum pushpelmesh-prod.dump > pushpelmesh-prod.dump.sha256
```

В WinSCP скачайте `pushpelmesh-prod.dump` и `pushpelmesh-prod.dump.sha256` со старого сервера и загрузите на `20DND` в `/home/codex/deploy/push-pelmesh/`. Дамп содержит данные пользователей. Храните его в закрытой папке SFTP.

**3. Восстановите дамп в отдельную базу.** Приложение использует выделенного пользователя PostgreSQL `pushpelmesh` с локальным соединением через Unix-сокет. Базы остальных сайтов для восстановления не используются.

```bash
set -eu
cd /home/codex/deploy/push-pelmesh
chmod 600 pushpelmesh-prod.dump pushpelmesh-prod.dump.sha256
sha256sum -c pushpelmesh-prod.dump.sha256
sudo -u postgres createdb --owner=pushpelmesh pushpelmesh_restore
sudo -u postgres psql -X -v ON_ERROR_STOP=1 \
  -c 'REVOKE CONNECT ON DATABASE pushpelmesh_restore FROM PUBLIC'
sudo -u pushpelmesh pg_restore --dbname=pushpelmesh_restore \
  --no-owner --no-privileges --single-transaction --exit-on-error < pushpelmesh-prod.dump
sudo -u pushpelmesh psql -X -d pushpelmesh_restore -v ON_ERROR_STOP=1 \
  -c 'SELECT count(*) AS users FROM "Users"; SELECT count(*) AS keys FROM "AccessKeys";'
```

Используйте незанятое имя для каждой отдельной пробной копии. Сверьте количество пользователей, ключей и других важных записей со старым сервером.

**4. Подключите восстановленную базу.** Сохраните текущий конфиг и откройте его через `sudoedit`.

```bash
sudo cp -a /etc/push-pelmesh/appsettings.Production.json /etc/push-pelmesh/appsettings.before-db.json
sudoedit /etc/push-pelmesh/appsettings.Production.json
```

В `ConnectionStrings:DefaultConnection` укажите `Host=/var/run/postgresql;Database=pushpelmesh_restore;Username=pushpelmesh;SSL Mode=Disable`. Перенесите действующие значения `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`, `Admin:Key` и `Vapid` со старого сервера. Те же JWT-настройки сохранят действующие токены, а те же VAPID-ключи — совместимость существующих push-подписок.

```bash
sudo chown root:pushpelmesh /etc/push-pelmesh/appsettings.Production.json
sudo chmod 0640 /etc/push-pelmesh/appsettings.Production.json
sudo systemctl restart push-pelmesh.service
curl -fsS --retry 5 --retry-connrefused --max-time 5 http://127.0.0.1:5088/api/ping
sudo journalctl -u push-pelmesh.service -n 40 --no-pager
```

При запуске приложение применит недостающие миграции к восстановленной базе. Проверьте подключение до переключения Nginx.

**5. Переключите только хост Пыш-Услуг.** При первом переносе с `adminvps_fin` этот шаг уже выполнен. Файл `/etc/push-pelmesh/nginx.direct.conf` на `20DND` направляет весь трафик домена на новый локальный backend.

```bash
set -eu
site=/etc/nginx/sites-available/push-pelmesh.ru
rollback=/etc/push-pelmesh/nginx.before-db.conf
sudo install -o root -g root -m 0600 "$site" "$rollback"
sudo install -o root -g root -m 0644 /etc/push-pelmesh/nginx.direct.conf "$site"
if ! sudo nginx -t || ! sudo systemctl reload nginx; then
  sudo install -o root -g root -m 0644 "$rollback" "$site"
  sudo nginx -t
  sudo systemctl reload nginx
  exit 1
fi
curl -fsS --retry 4 --max-time 6 --resolve push-pelmesh.ru:443:79.133.183.193 https://push-pelmesh.ru/api/ping
```

Затем проверьте вход существующим удостоверением, голосования и чат. Старый дамп и исходную базу сохраните до окончания проверки.
