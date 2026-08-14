# CameraId 关联逻辑审查报告

> 审查日期：2026-08-14
> 审查对象：`CameraConfig.CameraId`（基恩士相机真编号，上=2/下=1，与存图目录号一致）贯穿的全部代码路径
> 审查方式：逐文件静态阅读 + 调用链推演 + **C# harness（bin\Debug\CameraIdVerify.exe）反射实测私有方法**
> 状态：**已逐条排查并修复**（R1/R2/R3 已修复于 V2.13.11，R4 判定安全不改）

---

## 一、审查范围

| 模块 | 文件 | CameraId 的用途 |
| --- | --- | --- |
| 模型 | `Models/AppConfig.cs` | `CameraConfig.CameraId` 字段；`DefaultCameras()` 预置上=2/下=1；`WindowPointItem.CameraId`（窗口↔点位关联键）；`DefaultWindowPointMap` 铺排时取 CameraId（0 回退行序）；`PointMapValidForCameras`（V2.13.11 新增，孤儿映射校验） |
| 配置 | `Utils/ConfigStore.cs` | `EnsureCameraIdentity`（CameraId<=0 时按 IP 匹配默认相机补号，V2.13.11 改三遍分配全局唯一）；`EnsureDefaultCameraOrder`（按 IP 恢复 [上,下] 顺序）；`EnsureWindowPointMaps`（表长度/孤儿 CameraId 校验，V2.13.11 并入 PointMapValidForCameras） |
| 协调器 | `Services/ProductionCoordinator.cs` | `_activeCh`=相机 ID；`CameraIdFor`/`IndexOfCamera`（相机 ID↔列表下标互转）；`BeginCameraChannel`/`StepCameraChannel`/`TryResolveActiveWindow`（按 (CameraId, 点位) 反查窗口） |
| 主界面 | `Views/MainForm.cs` | `CamDisplayName`（无名称时优先 CameraId 显示"相机N"） |
| 设置页 | `Views/SettingsForm.cs` | 相机表第一列"相机ID"读/写 `CameraId`；`LoadCameraRows` 按 CameraId 升序展示；`CollectCamerasFromGrid` 保存时回写；`OnSave` 唯一性拦截（V2.13.11 新增） |
| 点位配置 | `Views/WindowPointForm.cs` | 点位编辑候选/占位检测/交换/标注全部以 CameraId 为键；`FindCameraById`/`IndexOfCameraById` 反查 |
| 功能测试 | `Views/DevTestForm.cs` | 相机下拉显示名、T2 取图存图 `cameraName` 兜底用 CameraId |
| 相机服务 | `Services/KeyenceIV4Camera.cs` | `DisplayName`=`Name`（不涉及 CameraId）；通讯无关 |
| PLC 服务 | `Services/PlcService.cs` | 相机通道地址全按 `PlcRequestAddress/PlcResultAddress`，与 CameraId 无关 |
| 图像存储 | `Services/ImageStore.cs` | `{相机}` 目录层按相机名隔离，与 CameraId 无关 |

---

## 二、一致性结论（经过验证、状态良好）

所有"按相机定位"的代码都用**同一把钥匙**：`CameraId>0` 用真编号、`0`/`<=0` 回退"行序+1"。以下四处兜底规则完全一致，没有分叉：

1. 协调器 `ProductionCoordinator.CameraIdFor`（真编号优先，0 回退行序）——同时用于 `IndexOfCamera` 反查；
2. 点位窗体 `WindowPointForm.FindCameraById` / `IndexOfCameraById`（同上规则）；
3. 默认铺排 `DisplayConfig.DefaultWindowPointMap`（`camId = cam.CameraId > 0 ? cam.CameraId : ci + 1`）；
4. 点位编辑候选 `WindowPointForm.EditSelectedPoint` 的占位键 `$"{camId}:{it.StationNo}"`。

**通讯链路（PLC 通道、取图、存图目录）与 CameraId 无关**：PLC 通道走 `PlcRequestAddress/PlcResultAddress`、存图 `{相机}` 目录层走相机名字符串，改编号不会破坏这两条链路。

旧配置迁移（`EnsureCameraIdentity` 按 **IP** 匹配补号、`PointMapValidForCameras` 重置孤儿/旧格式映射、`EnsureDefaultCameraOrder` 恢复顺序）也未发现错位。

---

## 三、风险点清单（已逐条排查）

| # | 风险 | 严重度 | 位置 | 现象概述 | 排查状态 |
| --- | --- | --- | --- | --- | --- |
| R1 | 改/互换 CameraId 后 WindowPointMaps 不迁移 | **高** | `Utils/ConfigStore.cs` `EnsureWindowPointMaps` + `Models/AppConfig.cs` `ResolveWindowPointMap` | 映射只校验长度与旧格式（`CameraId<=0`），不校验 CameraId 能否在相机列表反查到 → 改编号后该相机全部点位"跳过/无响应"（harness 实测保留旧 ID=2） | **已修复（V2.13.11）** |
| R2 | CameraId 重复无唯一性校验 | **高** | `Views/SettingsForm.cs` `OnSave` | 两台相机填同号时 `IndexOfCamera` 恒命中第一台 → 相机路由张冠李戴 | **已修复（V2.13.11）** |
| R3 | 自定义 IP + 无 CameraId 时补号语义偏离 | 中 | `Utils/ConfigStore.cs` `EnsureCameraIdentity` | 按行序 `i+1` 补号与默认相机真编号撞出重复（harness 实测自定义 200 与默认 212 同补成 1） | **已修复（V2.13.11）** |
| R4 | 列表下标对齐耦合 | 低 | `MainForm.CamDisplayName` / `DevTestForm` / `ConnectionMonitor` | 依赖 `_cameras[i]` 与 `_config.Cameras[i]` 下标一一对应（当前由 BuildServices 同源构建，安全） | 不修（当前安全，已确认） |

---

## 四、逐条排查记录

### R1：改/互换 CameraId 后 WindowPointMaps 不迁移

状态：**已确认存在 → 已修复（V2.13.11）**

- 实测复现（harness 反射调用生产 `EnsureWindowPointMaps`）：上相机改号 2→3 后，映射条目仍是旧 ID=2
  （>0 且长度 22==窗口总数，不被重置），`firstCamId=2` 不再匹配任何相机 → 运行时该相机全跳 3。
- 根因：映射合法性只判断"长度"与"旧格式（CameraId<=0）"，漏掉"改号后的孤儿 ID（>0 但查无此相机）"；
  `ResolveWindowPointMap` 运行时同样只看长度。
- 修复：新增 `DisplayConfig.PointMapValidForCameras`（有效 ID 集合=`CameraId>0` 真编号、0 行序兜底，
  与铺排/反查同一把钥匙）；`EnsureWindowPointMaps` 重置条件并入该校验（替换 `ContainsLegacyCameraIndex`，
  其 CameraId<=0 判定被完全覆盖）；`ResolveWindowPointMap` 运行时同规则防御。加载/保存清理 + 运行双保险，
  改号后映射自动重置为该型号默认铺排（用当前相机列表生成、天然带新编号）。
- 验证（harness 实测）：改号后 `EnsureWindowPointMaps` 重置 → `firstUpCamId=3`；运行时 `ResolveWindowPointMap`
  遇孤儿映射回退默认铺排 → `resolved[0].CameraId=3`。

### R2：CameraId 重复无唯一性校验

状态：**已确认存在 → 已修复（V2.13.11）**

- 现象：两台相机填同一个 CameraId(>0) 时，`IndexOfCamera` 恒命中第一台，相机路由/存图目录/结果通道错乱。
- 修复：`SettingsForm.OnSave` 最开头加唯一性拦截——遍历相机表，发现重复 ID（>0）弹窗提示并**中止本次保存**
  （`_cfg` 一个字段都不回写）；0=未填不算重复（后续由 `EnsureCameraIdentity` 全局唯一补号兜底）。

### R3：自定义 IP + 无 CameraId 时补号语义偏离

状态：**已确认存在 → 已修复（V2.13.11）**

- 实测复现（harness）：列表 [自定义 19.87.6.200, 默认下相机 212] 都缺 ID 时，自定义按行序得 1、下相机按
  IP 匹配也得 1 → 两台同为 1（重复 ID）。
- 修复：`EnsureCameraIdentity` 改**三遍分配**——①收集已固定 ID；②IP 匹配默认相机的优先取真编号
  （213→2、212→1，现场固定语义=存图目录号，不能被自定义相机先抢）；③仍缺 ID 的取"第一个未被占用正整数"
  （替换行序 `i+1`），全局唯一。
- 验证（harness 实测）：自定义+默认 IP → 自定义=2、下相机=1（保住真编号）；三台全缺号 → 213=2/212=1/
  自定义=3，全部唯一。

### R4：列表下标对齐耦合

状态：**确认当前安全，不改**

- `MainForm.CamDisplayName`/`DevTestForm`/`ConnectionMonitor` 依赖 `_cameras[i]` 与 `_config.Cameras[i]`
  下标一一对应；当前均由 `BuildServices` 同源构建（同一列表顺序），无独立来源可错位。若未来支持"运行时
  独立增删相机"，需同步核对。本次不改。

---

## 五、落地记录（V2.13.11）

1. `Models/AppConfig.cs`：新增 `DisplayConfig.PointMapValidForCameras`；`ResolveWindowPointMap` 接入孤儿校验。
2. `Utils/ConfigStore.cs`：`EnsureWindowPointMaps` 并入孤儿校验（删 `ContainsLegacyCameraIndex`）；
   `EnsureCameraIdentity` 三遍分配全局唯一；`Save` 补调 `EnsureCameraIdentity`。
3. `Views/SettingsForm.cs`：`OnSave` 增加 CameraId 唯一性拦截。

验证：构建通过；`CameraIdVerify.exe` 四场景全绿；冒烟测试进程存活。
