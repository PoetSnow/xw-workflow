-- 检测项目：法规目录与草稿。由人手工执行，代码不建表。
-- 列名跟随宿主的小写命名约定。

CREATE TABLE IF NOT EXISTS insp_regulation (
    id              bigint        NOT NULL PRIMARY KEY,
    corpusid        varchar(128)  NOT NULL,
    name            varchar(256)  NOT NULL,
    standardno      varchar(128)  NOT NULL,
    status          varchar(16)   NOT NULL,
    documentid      varchar(64)   NULL,
    chunkid         varchar(64)   NULL,
    clauseno        varchar(32)   NULL,
    scopesnippet    varchar(2000) NULL,
    UNIQUE KEY uk_insp_regulation_standard (standardno)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS insp_regulation_industry (
    id              bigint       NOT NULL PRIMARY KEY,
    regulationid    bigint       NOT NULL,
    industry        varchar(64)  NOT NULL,
    KEY ix_insp_regulation_industry (regulationid, industry)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS insp_regulation_project_type (
    id              bigint       NOT NULL PRIMARY KEY,
    regulationid    bigint       NOT NULL,
    projecttype     varchar(64)  NOT NULL,
    KEY ix_insp_regulation_project_type (regulationid, projecttype)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS insp_project (
    id              bigint       NOT NULL PRIMARY KEY,
    industry        varchar(64)  NOT NULL,
    projecttype     varchar(64)  NOT NULL,
    medium          varchar(64)  NULL,
    buildkind       varchar(32)  NULL,
    status          varchar(16)  NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS insp_project_regulation (
    id              bigint       NOT NULL PRIMARY KEY,
    projectid       bigint       NOT NULL,
    regulationid    bigint       NOT NULL,
    corpusid        varchar(128) NOT NULL,
    name            varchar(256) NOT NULL,
    source          varchar(16)  NOT NULL,
    removed         tinyint(1)   NOT NULL,
    KEY ix_insp_project_regulation (projectid)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS insp_project_item (
    id              bigint        NOT NULL PRIMARY KEY,
    projectid       bigint        NOT NULL,
    name            varchar(256)  NOT NULL,
    requirement     varchar(2000) NOT NULL,
    corpusid        varchar(128)  NOT NULL,
    documentid      varchar(64)   NOT NULL,
    chunkid         varchar(64)   NOT NULL,
    clauseno        varchar(32)   NULL,
    snippet         varchar(2000) NOT NULL,
    source          varchar(16)   NOT NULL,
    edited          tinyint(1)    NOT NULL,
    KEY ix_insp_project_item (projectid)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
