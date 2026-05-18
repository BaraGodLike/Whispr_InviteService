# Whispr Invite Service

[English version](README.md)

`Whispr Invite Service` — это gRPC-сервис для создания короткоживущих инвайтов, через которые клиент может получить серверный секрет.

Сервис поддерживает:
- создание инвайта
- получение stateless challenge для redeem
- активацию инвайта по PIN + proof of work
- отзыв инвайта
- очистку истекших инвайтов через отдельный worker

В репозитории также есть:
- `Migrator` для миграций PostgreSQL
- `Worker` для одноразовой очистки истекших инвайтов

## Что делает сервис

### Жизненный цикл инвайта

1. Клиент вызывает `CreateInvite`.
2. Сервис возвращает:
   - `invite_id`
   - `server_secret` в Base64
   - `pin`
   - `revoke_token` в Base64
   - `expires_at`
3. Перед redeem клиент вызывает `GetRedeemChallenge`.
4. Клиент считает proof-of-work для текущей попытки PIN.
5. Клиент вызывает `RedeemInvite`.
6. Позже инвайт можно отозвать через `RevokeInvite`.

### Бизнес-правила

- Время жизни инвайта настраивается через конфиг. По умолчанию: `15 минут`.
- Длина PIN: `8`.
- Алфавит PIN: `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`.
- При redeem PIN всегда приводится к uppercase.
- После `3` неудачных попыток инвайт блокируется на `2 минуты`.
- После `5` неудачных попыток и после каждой следующей неудачной попытки инвайт блокируется на `5 минут`.
- Количество неудачных попыток накопительное и не сбрасывается.

### Proof of work

Redeem защищен Hashcash-подобным proof of work.

- Базовая сложность: `18`
- Максимальная сложность: `26`
- Эффективная сложность: `min(26, 18 + attempts)`
- Challenge привязан к текущему значению `attempts` у инвайта
- Время жизни challenge настраивается через конфиг. По умолчанию: `2 минуты`

Клиент должен найти `pow_solution`, для которого:

```text
SHA256(invite_id || nonce || normalized_pin || pow_solution)
```

начинается как минимум с `difficulty` нулевых бит.

`normalized_pin` — это PIN, приведенный к uppercase.

## gRPC-контракт

Proto-файл: [Services/Protos/invites.proto](Services/Protos/invites.proto)

Package:

```proto
package invites.v1;
```

Service:

```proto
service InviteService {
  rpc CreateInvite (CreateInviteRequest) returns (CreateInviteReply);
  rpc GetRedeemChallenge (GetRedeemChallengeRequest) returns (GetRedeemChallengeReply);
  rpc RedeemInvite (RedeemInviteRequest) returns (RedeemInviteReply);
  rpc RevokeInvite (RevokeInviteRequest) returns (RevokeInviteReply);
}
```

### CreateInvite

Request:

```proto
message CreateInviteRequest {}
```

Response:

```proto
message CreateInviteReply {
  string invite_id = 1;
  string server_secret = 2;
  string pin = 3;
  string revoke_token = 4;
  google.protobuf.Timestamp expires_at = 5;
}
```

### GetRedeemChallenge

Request:

```proto
message GetRedeemChallengeRequest {
  string invite_id = 1;
}
```

Response:

```proto
message GetRedeemChallengeReply {
  RedeemChallengeStatus status = 1;
  string nonce = 2;
  int32 difficulty = 3;
  int32 attempts = 4;
  google.protobuf.Timestamp expires_at = 5;
  google.protobuf.Timestamp locked_until = 6;
}
```

Статусы challenge:
- `AVAILABLE`
- `NOT_FOUND`
- `EXPIRED`
- `USED`
- `LOCKED`
- `REVOKED`

### RedeemInvite

Request:

```proto
message RedeemInviteRequest {
  string invite_id = 1;
  string pin = 2;
  string nonce = 3;
  uint64 pow_solution = 4;
}
```

Response:

```proto
message RedeemInviteReply {
  RedeemInviteStatus status = 1;
  string server_secret = 2;
  google.protobuf.Timestamp locked_until = 3;
}
```

Статусы redeem:
- `SUCCEEDED`
- `NOT_FOUND`
- `EXPIRED`
- `USED`
- `INVALID_PIN`
- `LOCKED`
- `REVOKED`

Transport-level gRPC ошибки:
- `InvalidArgument`
  - невалидный `invite_id`
- `FailedPrecondition`
  - невалидный или истекший PoW challenge
- `ResourceExhausted`
  - недостаточный proof of work

### RevokeInvite

Request:

```proto
message RevokeInviteRequest {
  string invite_id = 1;
  string revoke_token = 2;
}
```

Response:

```proto
message RevokeInviteReply {
  RevokeInviteStatus status = 1;
}
```

Статусы revoke:
- `SUCCEEDED`
- `NOT_FOUND`
- `USED`
- `REVOKED`

Transport-level gRPC ошибки:
- `InvalidArgument`
  - невалидный `invite_id`
  - невалидный Base64 в `revoke_token`
- `PermissionDenied`
  - `revoke_token` синтаксически корректен, но не авторизует revoke

## Health checks

Сервис публикует только gRPC health checks.

HTTP health endpoints в текущей конфигурации отсутствуют. Это сделано специально: сервис работает по HTTP/2 и рассчитан на Kubernetes `grpc` probes.

## Локальный запуск

### Требования

- Docker
- Docker Compose

### Запуск через Docker Compose

1. Создайте локальный файл окружения:

```bash
cp .env.example .env
```

2. Соберите и поднимите PostgreSQL, migrator и gRPC-сервис:

```bash
docker compose up --build
```

3. Если нужен фоновый запуск:

```bash
docker compose up --build -d
```

4. Остановите стек:

```bash
docker compose down
```

5. Остановите стек и удалите volume PostgreSQL:

```bash
docker compose down -v
```

### Локальный запуск cleanup worker

По умолчанию worker не стартует. Его нужно запускать отдельно:

```bash
docker compose --profile ops run --rm worker
```

### Повторный запуск миграций

```bash
docker compose run --rm migrator
```

## Локальная конфигурация по умолчанию

Пример значений лежит в [.env.example](.env.example):

- PostgreSQL database: `whispr_invites`
- PostgreSQL user: `postgres`
- PostgreSQL password: `postgres`
- Service port: `8080`

Через compose в сервис передаются такие переменные:
- `ConnectionStrings__Invites`
- `Pow__SigningKeyBase64`
- `Encryption__KeyBase64`
- `Hashing__KeyBase64`

## Структура проекта

- `Services` - хост gRPC API
- `Application` - use cases и абстракции сервисов
- `Domain` - доменная модель и правила
- `Infrastructure.Storage` - PostgreSQL persistence
- `Infrastructure.Security` - hashing, encryption и PoW сервисы
- `Migrator` - приложение для миграций на FluentMigrator
- `Worker` - одноразовое приложение для очистки истекших инвайтов
- `Tests.Unit` - минимальные unit tests
- `Tests.Integration` - integration tests с PostgreSQL
