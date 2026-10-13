# 应用 Docker Compose（中间件已单独跑）

## 你的现状

- MySQL / Redis / RabbitMQ：已是 Docker 容器（本仓库 **不再** 起它们）
- LLM：已有
- 本文件负责：**Host + Agent + Console** 三个应用容器

## 一键

```bash
# 仓库根目录
cp .env.apps.example .env.apps
# 编辑 .env.apps：填 MySQL/Redis/Rabbit 密码、LLM 地址

docker compose -f docker-compose.apps.yml --env-file .env.apps up -d --build
```

浏览器：`http://服务器IP:3000`  
（Console 同域反代 `/api`→Host、`/agent-api`→Agent）

## 连已有中间件

**方式 A（推荐，简单）**  
中间件端口已映射到宿主机（3306/6379/5672）时，`.env.apps` 里主机名用 `host.docker.internal`（示例已写好）。

**方式 B**  
和应用进同一 Docker 网络：把 `MIDDLEWARE_NETWORK` 改成中间件所在网络名，连接串主机改成容器名。

```bash
docker network ls
docker inspect <mysql容器名> --format "{{json .NetworkSettings.Networks}}"
```

## 不要做的事

- **不要**再跑 `deploy/start-infra.ps1`（会再起一套 MySQL/Redis 抢端口）
- **不要**把 8080/5002 以外的数据库端口乱开到公网；对外只开 Console 的 3000 或前面再挂 HTTPS 反代

## 常用命令

```bash
docker compose -f docker-compose.apps.yml --env-file .env.apps ps
docker compose -f docker-compose.apps.yml --env-file .env.apps logs -f agent
docker compose -f docker-compose.apps.yml --env-file .env.apps down
```

## 知识库

没有 Postgres：保持 `AGENT_KNOWLEDGE_ENABLED=false`，先演示查库存即可。  
有 Postgres 后：填 `AGENT_PG_CONNECTION`，执行知识库/审计 SQL，再打开 Enabled。
