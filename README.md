# Whispr Invite Service

[Русская версия](README.ru.md)

`Whispr Invite Service` is a gRPC service that creates short-lived invites for obtaining a server-side secret.

The service supports:
- invite creation
- stateless redeem challenge generation
- invite redemption with PIN + proof of work
- invite revocation
- expired invite cleanup through a separate worker

The repository also contains:
- `Migrator` for PostgreSQL schema migrations
- `Worker` for one-shot cleanup of expired invites

## How it works

### Invite lifecycle

1. A client calls `CreateInvite`.
2. The service returns:
   - `invite_id`
   - `server_secret` as Base64
   - `pin`
   - `revoke_token` as Base64
   - `expires_at`
3. Before redeeming, the client calls `GetRedeemChallenge`.
4. The client computes a proof-of-work solution for the current PIN attempt.
5. The client calls `RedeemInvite`.
6. The invite can later be revoked with `RevokeInvite`.

### Business rules

- Invite lifetime is configurable. Default: `15 minutes`.
- PIN length is `8`.
- PIN alphabet is `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`.
- PIN is normalized to uppercase during redeem.
- After `3` failed attempts the invite is locked for `2 minutes`.
- After `5` failed attempts and every failed attempt after that, the invite is locked for `5 minutes`.
- Failed attempts are cumulative and never reset.

### Proof of work

Redeem is protected by a Hashcash-like proof of work.

- Base difficulty: `18`
- Max difficulty: `26`
- Effective difficulty: `min(26, 18 + attempts)`
- Challenge is tied to the current invite `attempts` value
- Challenge TTL is configurable. Default: `2 minutes`

The client must find `pow_solution` such that:

```text
SHA256(invite_id || nonce || normalized_pin || pow_solution)
```

starts with at least `difficulty` leading zero bits.

`normalized_pin` means the PIN converted to uppercase.

## gRPC contract

Proto file: [Services/Protos/invites.proto](Services/Protos/invites.proto)

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

Challenge statuses:
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

Redeem statuses:
- `SUCCEEDED`
- `NOT_FOUND`
- `EXPIRED`
- `USED`
- `INVALID_PIN`
- `LOCKED`
- `REVOKED`

Transport-level gRPC errors:
- `InvalidArgument`
  - invalid `invite_id`
- `FailedPrecondition`
  - invalid or expired PoW challenge
- `ResourceExhausted`
  - insufficient proof of work

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

Revoke statuses:
- `SUCCEEDED`
- `NOT_FOUND`
- `USED`
- `REVOKED`

Transport-level gRPC errors:
- `InvalidArgument`
  - invalid `invite_id`
  - invalid Base64 `revoke_token`
- `PermissionDenied`
  - revoke token is syntactically valid but does not authorize revocation

## Health checks

The service exposes gRPC health checks only.

There are no HTTP health endpoints in the current setup. This is intentional because the service is configured for HTTP/2 and is intended to be used with Kubernetes gRPC probes.

## Local startup

### Requirements

- Docker
- Docker Compose

### Start with Docker Compose

1. Create a local environment file:

```bash
cp .env.example .env
```

2. Build and start PostgreSQL, migrator, and the gRPC service:

```bash
docker compose up --build
```

3. Start in detached mode if needed:

```bash
docker compose up --build -d
```

4. Stop the stack:

```bash
docker compose down
```

5. Stop the stack and remove the PostgreSQL volume:

```bash
docker compose down -v
```

### Run cleanup worker locally

The cleanup worker is not started by default. Run it explicitly:

```bash
docker compose --profile ops run --rm worker
```

### Re-run migrations

```bash
docker compose run --rm migrator
```

## Default local configuration

Example values are stored in [.env.example](.env.example):

- PostgreSQL database: `whispr_invites`
- PostgreSQL user: `postgres`
- PostgreSQL password: `postgres`
- Service port: `8080`

The compose setup passes these environment variables into the service:
- `ConnectionStrings__Invites`
- `Pow__SigningKeyBase64`
- `Encryption__KeyBase64`
- `Hashing__KeyBase64`

## Project structure

- `Services` - gRPC API host
- `Application` - use cases and service abstractions
- `Domain` - domain model and rules
- `Infrastructure.Storage` - PostgreSQL persistence
- `Infrastructure.Security` - hashing, encryption, and PoW services
- `Migrator` - FluentMigrator-based schema migration app
- `Worker` - one-shot expired invite cleanup app
- `Tests.Unit` - minimal unit tests
- `Tests.Integration` - PostgreSQL-backed integration tests
