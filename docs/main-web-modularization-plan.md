# 主 Web 依赖收口方案

目标：保持现在的模块切分（Host 拼 System 和 Approve），把依赖方向收成以后能拆的口子。

这版替换 `docs/main-web-modularization-fix-plan.md`。那份文档里的 `IRepository<TEntity,TKey>`、`IIdGenerator`、`SnowflakeIdGenerator`、`IUserRepository`、`IDepartmentQueryService`、`IEventBus`、`IEventHandler<>` 在仓库里都不存在，不要按它改。

业务代码会有改动。能不动的是审批规则、表结构和 System 的仓储。

## 现在的依赖

```
Approve.Domain
  → Infra.Repository          配置实体继承 EfFullAuditEntity
  → Infra.IdGenerater         聚合里直接调用 IdGenerater.GetNextId()
  → Shared.Domain             聚合基类本身又引用了 Infra

Shared.Domain
  → Infra.Repository
  → Infra.Repository.EfCore   AbstractDomainEntityInfo，没有子类
  → Infra.EventBus            AggregateRoot 用 Service Locator 拿 IEventPublisher，没有调用方
  → Shared.Repository

Approve.Infrastructure
  → System.Application.Contracts
    只有 OrgSnapshotReader、ApproveActorDirectory 两个文件
    注入的是 IUserService、IPositionService、IUserAppointmentService、IOrganizationService

Approve.Application.Contracts → Infra.Repository     接口上的 [UnitOfWork]
System.Application.Contracts  → Infra.Repository     同上
                              → Infra.Redis.Caching  接口上的 [CachingAble] / [CachingEvict]
```

已经做对、这版不动的部分：

- System 继续是 API / Application / Repository，不为用户、组织再造一层 Domain。
- Approve 的领域端口留在 Domain：`IOrgSnapshotReader`、`IApproveActorDirectory`、各仓储接口。
- 审批结束通知留 `IApproveCompletedHandler`。`ApproveInstanceService.NotifyIfCompletedAsync` 已经按 `BusinessType` 回调。
- 出进程消息留现有的 CAP + `IEventPublisher`。不新增内存事件总线。

## 顺序

1. 先做跨模块只读目录。范围小，切得掉 Approve 对 System 的项目引用。
2. 再把拦截特性从 EF、Redis 程序集里挪走。契约不再带着数据库和缓存。
3. 领域基类脱离 EF 单独排期。这一步要改框架泛型约束，不和前两步绑在一起。

每步做完看「验收」，通过再做下一步。编译通过只说明没写错类型，不能说明依赖已经切断。

---

## 第 1 步：Approve 不再引用 System 的应用契约

### 原则

查询留在现有两个 Reader 里。System 只实现一份只读目录，不把 `IUserService` 上的登录、改密、缓存特性暴露给 Approve。

组织在本仓库里叫 Organization，字段是 `DeptId`，没有 Department。

查询范围与现在一致：启用岗位、启用部门、有效任职都是整表取出，再在 Approve 里过滤。不要改成按 Id 批量查询。

### 1. 在 Shared.Application.Contracts 增加只读目录

新建 `src/Shared/XW.Shared.Application.Contracts/Integrations/`。

`IOrgDirectory` 的方法对应两个 Reader 今天实际取的数据：

| 方法 | 供给 | 字段 |
| --- | --- | --- |
| `ListEnabledPositionsAsync` | 找人、岗位名 | Id、Code、Name |
| `ListActiveAppointmentsAsync` | 找人 | UserId、DeptId、PositionId、IsPrimary |
| `ListActiveAppointmentsByUserAsync` | 发起人部门 | 同上 |
| `ListEnabledDeptsAsync` | 部门树、部门名 | Id、ParentId、Name |
| `FindUserAsync` | 没有任职时回退 `sys_user.deptid` | Id、Name、Account、DeptId；没有用户返回 null |
| `ListUsersAsync` | 时间线姓名，对应现有 `GetManyAsync` | 同上，不存在的 Id 不返回 |

展示名规则留在 `ApproveActorDirectory`：姓名为空用账号。不要放到 System 适配器里。

`XW.Shared.Application.Contracts` 只引用 `XW.Infra.Core`，放这些类型不会把 System 或 EF 带进来。

### 2. System.Application 实现

新建 `OrgDirectory`，构造函数注入现有的 `IUserService`、`IPositionService`、`IUserAppointmentService`、`IOrganizationService`，映射成上面的只读类型。

数据来源对表现有方法，不新写 SQL，不新增 `IUserRepository`：

- `IPositionService.GetEnabledListAsync`
- `IUserAppointmentService.GetActiveListAsync` / `GetActiveByUserAsync`
- `IOrganizationService.GetEnabledListAsync`
- `IUserService.GetAsync` / `GetManyAsync`

在 `XW.System.Application/DependencyRegistrar.AddApplicationServices` 里：

```csharp
services.AddScoped<IOrgDirectory, OrgDirectory>();
```

注册方法是 `AddApplicationServices`，不是 `AddAdncServices`。

### 3. 两个 Reader 改注入

`OrgSnapshotReader`、`ApproveActorDirectory` 改为只依赖 `IOrgDirectory`。

`GetUserDeptIdAsync` 的回退顺序保持不变：主职，否则第一条有效任职，再否则 `FindUserAsync` 的 `DeptId`，用户不存在则返回 null。

删掉这两个文件里的 `using XW.System.Application.Contracts.Interfaces`。

### 4. 删除项目引用

`XW.Approve.Infrastructure.csproj` 删除：

```xml
<ProjectReference Include="..\..\XW.System\XW.System.Application.Contracts\XW.System.Application.Contracts.csproj" />
```

Approve.Infrastructure 已经引用 `XW.Shared.Application`，能看见 `IOrgDirectory`。不要再引用 System 的任何项目。

### 验收

- `src/Modules/XW.Approve` 下没有 `XW.System` 的 using，也没有对应的 ProjectReference。
- 审批找人、发起人部门、时间线姓名仍走原来的端口。
- `dotnet build src/XW.Host.API/XW.Host.API.csproj` 通过。

---

## 第 2 步：契约不再引用 EF 和 Redis

接口上的 `[UnitOfWork]`、`[CachingAble]`、`[CachingEvict]`、`[CachingParam]` 必须留在接口上。Castle 拦截器读的是接口方法上的特性，挪到实现类会让事务和缓存失效。

这些特性类本身几乎没有依赖：缓存特性只含字符串和布尔字段；`UnitOfWorkAttribute` 只带 `Distributed` 和 `ModuleUnitOfWorkScope`。拦截器实现留在原项目。

### 做法

新建无包引用、无项目引用的 `src/Shared/XW.Shared.Interception/`。

搬进去的类型：

- `UnitOfWorkAttribute`
- `ModuleUnitOfWorkScope`
- `CachingInterceptorAttribute`、`CachingAbleAttribute`、`CachingEvictAttribute`、`CachingParamAttribute`

`OperateLogAttribute` 已经在 `XW.Shared.Application.Contracts`，不动。

然后：

- `XW.Infra.Repository`、`XW.Infra.Redis.Caching` 改为引用 `XW.Shared.Interception`，拦截器逻辑不改。
- `XW.Approve.Application.Contracts` 去掉对 `XW.Infra.Repository` 的引用，改为引用 `XW.Shared.Interception`。
- `XW.System.Application.Contracts` 去掉对 `XW.Infra.Repository`、`XW.Infra.Redis.Caching` 的引用，改为引用 `XW.Shared.Interception`。
- 更新 global using 和拦截器文件里的命名空间。

`XW.Shared.Interception` 不要引用 Infra。Infra 引用它，方向才是对的。

### 验收

- 两个 `*.Application.Contracts.csproj` 的 ProjectReference 里没有 `XW.Infra.Repository`、`XW.Infra.Redis.Caching`。
- 带 `[UnitOfWork]` 的写操作仍然开事务；带 `[CachingEvict]` 的组织、字典更新仍然清缓存。

---

## 第 3 步：领域模型脱离 EF（单独排期）

前两步做完，Approve 和 System 之间没有项目引用，契约也不再夹带 EF 和 Redis。Domain 仍会通过基类碰到 Infra。这一步专门处理这件事。

### 为什么不能只删 csproj 里的两行

`XW.Shared.Domain` 已经引用 `XW.Infra.Repository`。SDK 风格的项目引用默认传递，Approve.Domain 删掉直接引用后仍然编译得过，依赖还在。

运行时聚合要进 `EfBasicRepository`，约束是 `TEntity : Entity, IEfEntity<long>`。配置实体要被 `AbstractEntityInfo` 扫到，过滤条件是 `EfEntity`。这两处都在 Infra 里。只改 Approve 的 using 切不断。

`AbstractDomainEntityInfo` 没有子类。Approve 用的是自己的 `EntityInfo : AbstractEntityInfo`，并且在 `SetTableName` 里逐个登记了表。删掉这个空基类，可以先去掉 Shared.Domain 对 `XW.Infra.Repository.EfCore` 和 `XW.Shared.Repository` 的引用。

`AggregateRoot.EventPublisher` 没有调用方。删掉这个属性和对 `IEventPublisher` 的引用，可以去掉 Shared.Domain 对 `XW.Infra.EventBus` 的引用。不要换成另一套 `IEventBus`。

### 这一步要改的框架约束

1. `IEntity<long>`、`IFullAuditInfo`、`IBasicAuditInfo` 这类数据契约放到 Shared.Domain。Infra 里的同名类型改为继承它们，或在这一步里直接改命名空间。System 的实体留在 `XW.System.Repository`，继续继承 `EfFullAuditEntity`。
2. `EfBasicRepository` / `AbstractEfBaseRepository` 的约束从 `Entity, IEfEntity<long>` 放宽到 Shared 里的实体基类。映射仍在 Infrastructure 的 `EntityInfo`。
3. Approve 的配置实体（`ApproveProcessDef`、节点、连线、条件、绑定、办理人、业务字段）改为 Shared 里的审计基类，不再继承 `EfFullAuditEntity`。属性名保持 `Id`、`CreateBy`、`CreateTime`、`ModifyBy`、`ModifyTime`。`EntityInfo.SetTableName` 已经登记这些表，不依赖 `EfEntity` 扫描才能建模型。
4. 四个聚合文件里的 `IdGenerater.GetNextId()` 改为由调用方传入 `long`：`ApproveInstance`、`ApproveInstanceNode`、`ApproveTask`、`ApproveHistory`。`IdGenerater` 只留在 Application 和 Infrastructure。这是这一步里要改的业务代码。

做完后 `XW.Shared.Domain.csproj` 和 `XW.Approve.Domain.csproj` 的 ProjectReference 里没有任何 `XW.Infra.*`。用项目文件确认，不要只看 `dotnet build`。

---

## 明确不做

- 不新建 `IUserQueryService`、`IDepartmentQueryService`，不按 Department 建模。
- 不把 System 的 `IEfRepository<User>` 包一层假的领域仓储。
- 不新增 `IEventBus` / `IEventHandler<>`。进程内结束通知用 `IApproveCompletedHandler`；要出进程时在 Application 里调用现有 `IEventPublisher`，订阅方实现 `ICapSubscribe`，交给已有的 `AddCapEventBus`。
- 不把 Agent 服务纳入这版。Agent 是另一个宿主里的单项目，另案处理。
- 不改表，不写 SQL。
