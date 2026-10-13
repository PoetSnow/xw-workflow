# 补缺中间件：仅 RabbitMQ + Postgres

服务器上 **已有 MySQL、Redis** → 本目录 Compose **只起**：

| 服务 | 端口 | 用途 |
|------|------|------|
| RabbitMQ | 5672 / 15672 | Host CAP（即使不做诊测也需要） |
| Postgres + pgvector | 5432 | Agent 知识库 / 审计 |

## 一键

```powershell
# 仓库根目录
powershell -File deploy/start-infra.ps1
```

停止：`powershell -File deploy/start-infra.ps1 -Down`

密码：`deploy/.env.infra`（首次自动从 example 生成）

## 然后起应用（Host + Agent + Console）

```bash
cp .env.apps.example .env.apps
# MySQL/Redis → 你已有实例的地址
# Rabbit → host.docker.internal:5672（刚起的容器）
# PG → AGENT_PG_CONNECTION=... 且 AGENT_KNOWLEDGE_ENABLED=true（要知识库时）

docker compose -f docker-compose.apps.yml --env-file .env.apps up -d --build
```

详见 `deploy/README-apps.md`。
