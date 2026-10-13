## 改动说明

- **目标：** （一句话说明为什么改）
- **范围：** （Host / Devices / Integration / Brain / Customer / Console / SQL / 文档）

## 自测

- [ ] 本地已 `dotnet build`（相关项目）通过
- [ ] 涉及 Console 时前端可打开/相关页可用
- [ ] 涉及诊测时：Create →（CAP）→ 结果回写已自测或说明无法自测原因
- [ ] 涉及调理/LLM 提示词时：已说明是否需要**重新发布快照**到 Brain
- [ ] 本 PR 的 GitHub Actions **CI** 已通过（或说明失败原因）

## 审查清单（必过）

- [ ] 跨模块未引用对方 Domain（仅 Contracts；**CI `Architecture gate` 已自动检查**）
- [ ] Integration / Brain 未连接业务库、未写入业务表
- [ ] 无密钥 / 连接串 / 厂商 Secret 进入仓库或日志明文
- [ ] 无无关大重构、无大面积无关格式化
- [ ] 行为变更已同步 `docs/XW-项目说明-V1.0.md` 和/或 `docs/sql` 三脚本（若适用）

## 按改动加看（有则勾）

- [ ] **Devices：** 算法快照含 ProviderCode / Endpoint / ConfigJson
- [ ] **Integration：** 厂商走插件/快照配置，未写死 Endpoint/凭证
- [ ] **Customer 调理/提示词：** Scope（平台/客户）正确；发布路径清晰
- [ ] **Brain：** 只消费快照；LLM 不充当诊测出报告主路径
- [ ] **SQL：** 幂等、无外键；未另起碎片脚本

## 风险与回滚

- **风险：** 
- **回滚方式：** （回退提交 / 关 Stub / 重发旧快照 等）

## AI / 工具辅助（可选）

- [ ] 已用 AI/静态检查扫过 diff（结论简述：）
- [ ] CI 通过后再请人 Approve

---

> 创建 Pull Request 时 GitHub 会自动带上本模板。  
> 合并建议：至少 1 人 Approve + **CI（Backend / Console）Checks 通过**。  
> CI 定义见 `.github/workflows/ci.yml`（Backend 含架构门禁 `scripts/ci/check-module-boundaries.sh`；PulsePpg 原生 DLL 测试在 Linux 上已排除）。
