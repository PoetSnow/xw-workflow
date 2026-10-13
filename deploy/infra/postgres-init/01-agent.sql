-- 与 docs/sql/xw_agent.postgresql.sql 保持一致（容器 POSTGRES_DB=xw_agent，无需建库）
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS agent_knowledge_document (
    id              text PRIMARY KEY,
    title           text NOT NULL,
    domain          text NOT NULL DEFAULT 'general',
    source_name     text NOT NULL,
    content_type    text NOT NULL DEFAULT 'application/octet-stream',
    storage_path    text NULL,
    byte_size       bigint NOT NULL DEFAULT 0,
    status          text NOT NULL DEFAULT 'ready',
    error_message   text NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS agent_knowledge_chunk (
    id              text PRIMARY KEY,
    document_id     text NOT NULL REFERENCES agent_knowledge_document(id) ON DELETE CASCADE,
    chunk_index     int NOT NULL,
    domain          text NOT NULL DEFAULT 'general',
    title           text NOT NULL,
    source_name     text NOT NULL,
    content         text NOT NULL,
    token_estimate  int NOT NULL DEFAULT 0,
    embedding       vector(1024) NOT NULL,
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE (document_id, chunk_index)
);

CREATE INDEX IF NOT EXISTS ix_agent_knowledge_chunk_document
    ON agent_knowledge_chunk (document_id);
CREATE INDEX IF NOT EXISTS ix_agent_knowledge_chunk_domain
    ON agent_knowledge_chunk (domain);

CREATE TABLE IF NOT EXISTS agent_audit_record (
    id                  bigserial PRIMARY KEY,
    trace_id            text NOT NULL,
    session_id          text NOT NULL,
    user_id             text NULL,
    tenant_id           text NULL,
    intent              text NULL,
    user_message        text NOT NULL,
    assistant_reply     text NOT NULL,
    tool_calls          jsonb NOT NULL DEFAULT '[]'::jsonb,
    permission_denied   boolean NOT NULL DEFAULT false,
    approval_required   boolean NOT NULL DEFAULT false,
    created_at          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_agent_audit_session_created
    ON agent_audit_record (session_id, created_at);
CREATE INDEX IF NOT EXISTS ix_agent_audit_tenant_created
    ON agent_audit_record (tenant_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_agent_audit_trace
    ON agent_audit_record (trace_id);

CREATE TABLE IF NOT EXISTS agent_session (
    id              text PRIMARY KEY,
    user_id         text NULL,
    tenant_id       text NULL,
    slots           jsonb NOT NULL DEFAULT '{}'::jsonb,
    version         integer NOT NULL DEFAULT 1,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS agent_session_message (
    id              bigserial PRIMARY KEY,
    session_id      text NOT NULL,
    role            text NOT NULL,
    content         text NOT NULL,
    intent          text NULL,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_agent_session_message_session
    ON agent_session_message (session_id, id);

CREATE TABLE IF NOT EXISTS agent_turn (
    id              bigserial PRIMARY KEY,
    trace_id        text NOT NULL,
    session_id      text NOT NULL,
    lane            text NULL,
    lane_reason     text NULL,
    phase           text NULL,
    analysis        jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_agent_turn_session_created
    ON agent_turn (session_id, created_at);

CREATE INDEX IF NOT EXISTS ix_agent_turn_trace
    ON agent_turn (trace_id);
