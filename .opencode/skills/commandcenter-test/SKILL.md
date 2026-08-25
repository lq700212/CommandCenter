# CommandCenter 自动化测试（commandcenter-test）

CommandCenter（WinForms/.NET Framework 4.7.2）项目专属的**构建验证 + 回归测试 + 冒烟测试**三合一 skill。
把"每次改完代码要做的验证"固化成一条命令，不用重复造轮子。

## 何时使用

- 用户要求"冒烟测试""跑一下测试""验证一下改动没破坏功能"时；
- 每次完成代码改动、构建通过之后，作为交付前的标准验证步骤；
- 排查"这次改动是否引入回归"时。

## 使用方法

总入口（依次执行：构建 → 回归测试用例集 → 进程级冒烟，任一失败即非零退出）：

```powershell
powershell -ExecutionPolicy Bypass -File ".opencode\skills\commandcenter-test\scripts\run-all.ps1"
```

也可单独执行某一层：

```powershell
# 仅构建（Debug）
powershell -File ".opencode\skills\commandcenter-test\scripts\build.ps1"
# 仅回归测试用例集（反射驱动，不需要现场设备）
powershell -File ".opencode\skills\commandcenter-test\scripts\tests.ps1"
# 仅进程级冒烟（启动 exe → 存活 + 建站日志检查 → 关闭）
powershell -File ".opencode\skills\commandcenter-test\scripts\smoke.ps1"
```

## 三层测试设计

| 层 | 脚本 | 验什么 | 依赖 |
| --- | --- | --- | --- |
| 构建 | build.ps1 | MSBuild Debug 编译零 error，产出 bin/Debug/CommandCenter.exe | MSBuild |
| 用例集 | tests.ps1 + TestRunner.cs | 纯逻辑回归：SN/型号 ASCII 寄存器打包（V2.15.17）、PLC 从站读写往返、配置模型默认值与新旧 json 兼容、扫码错误文本过滤、窗口布局统一模型（ResolveLayout/默认铺排/孤儿映射）、点位→程序号映射、密码哈希与 DPAPI 记住密码、I18n 双语切换 | 无设备（离线可跑；反射调用 private 成员，走真实代码路径） |
| 冒烟 | smoke.ps1 | 启动真实 exe → 进程存活 ≥8s → 当日日志含"PLC 从站建站/上电初始化"关键字 → 强制关闭再启动一轮防"二次启动崩" | 无设备（相机/扫码枪连不上只出 WARN，不影响判定） |

## 测试用例维护约定

- 新增**纯逻辑**功能（协议打包/配置兜底/过滤规则/布局计算等）→ 在 `scripts/TestRunner.cs` 对应分组里补用例；
- TestRunner 用 Roslyn csc（VS18 自带）编译到 `bin\Debug\cc_test_runner.exe` 后运行——BaseDirectory=bin\Debug，
  依赖 dll 与 Logs 目录天然正确；**运行完由 tests.ps1 自动删除 runner**；
- ⚠️ TestRunner **禁止调用 `ConfigStore.Load()/Save()`**（无参版本固定读写 bin\Debug\Config\appconfig.json，
  会覆盖开发机现有配置）；配置类测试只用 JsonConvert 序列化往返 + 反射调 `ApplyDefaults`（纯内存）；
- ⚠️ DPAPI 往返测试使用 isDev=true 的开发者记住文件（%LOCALAPPDATA%\CommandCenter），测完自动 Clear；
- UI 视觉类问题不要往这里塞——那类调试走 `winforms-ui-debug` skill（harness 截图/像素扫描）。

## 断言失败怎么处理

tests.ps1 输出每个用例的 `[PASS]/[FAIL]` 与末尾汇总；FAIL 的名字带"期望=…实际=…"。
定位顺序：先看是本次改动直接相关（修代码）还是既有行为锚点（确认是否约定变更，需同步文档后再改断言）。
