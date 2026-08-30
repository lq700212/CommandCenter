# CommandCenter MES 对接说明（SN 上传）

> 版本：V2.15.20（2026-08-30）
> 适用对象：后续负责 MES 对接的开发/调试人员。本文档说明"扫码 SN 传 MES"的**配置方法、当前报文格式、运行行为**，以及**客户 MES 协议定稿后代码要改哪里**。

---

## 一、功能定位

扫码枪扫到产品 SN 序列号后，上位机把 SN 交给外部，去向由配置变量**二选一**（V2.15.20 起，无界面选项）：

| target 值 | 行为 |
|-----------|------|
| `Mes`（**默认**） | SN 不写 PLC 寄存器区（40013~40024 保持全 0），改由上位机后台 HTTP POST 上传给 MES |
| `Plc` | 走既有流程：SN 写进 PLC SN 寄存器区（协议 40013 起），不上传 MES |

**无论哪种去向，PLC 的扫码结果握手（40004 写 1/2、人工补录覆盖成 1）完全不受影响**——SN 去向只路由 SN 本身，不碰 PLC 通讯协议。

## 二、配置方法（appconfig.json）

配置文件位于程序目录 `Config\appconfig.json`，顶层 `sn` 段：

```json
"sn": {
  "target": "Mes",
  "mesUrl": "http://19.87.6.50:8080/api/sn",
  "mesTimeoutMs": 3000
}
```

| 字段 | 默认值 | 说明 |
|------|--------|------|
| `target` | `"Mes"` | SN 去向，二选一：`"Mes"`（传 MES）/ `"Plc"`（写 PLC 寄存器区）。**大小写不敏感**，空/非法值自动按 `Mes` 处理 |
| `mesUrl` | 空 | MES 接收 SN 的完整 URL（http/https）。`target=Mes` 时**必须配置**，留空则 SN 不上传且日志 WARN 一次 |
| `mesTimeoutMs` | `3000` | 单次上传超时（毫秒）。只影响该次上传多久判失败，不影响 PLC 节拍 |

⚠️ **手改 json 后重启软件生效**（启动加载配置）。没有界面入口，运维需要改去向/地址时直接编辑此文件。

## 三、当前上传报文（通用占位格式）

MES 协议**尚未定稿**，当前为通用 HTTP 上报：

- **方式**：`HTTP POST`
- **Content-Type**：`application/json`
- **编码**：UTF-8
- **报文体**（小驼峰字段）：

```json
{
  "sn": "A1B2C3D4E5F6",
  "model": "U171",
  "time": "2026-08-30 14:30:25.123"
}
```

| 字段 | 含义 |
|------|------|
| `sn` | 本件产品序列号（扫码枪读到 / 人工补录输入的值） |
| `model` | 当前产品型号（主界面标题栏选中的型号，便于 MES 对账） |
| `time` | 扫码/补录完成时刻（`yyyy-MM-dd HH:mm:ss.fff`，本地时间） |

**上传时机**：① 扫码枪扫到有效 SN；② 操作员经"人工补录"录入 SN。扫码失败/超时**不上传**（"空 SN 清 PLC 区"是 PLC 侧专属语义，MES 没有空 SN 概念）。

## 四、运行行为与特性

- **后台异步**：整个 HTTP 交互在线程池后台执行，绝不阻塞扫码通道与 40004 结果写入（MES 慢/断网都不影响 PLC 节拍）。
- **尽力而为**：上传成功记 INFO 日志（`SN 已上传 MES：sn=...`）、失败记 WARN 日志（`MES 上传失败`/`MES 上传异常`），**不重试、不做回执判定**——PLC 流程不依赖 MES 这条路。
- **防堆积**：断网时在途上传超过 10 条，新的上传直接丢弃并 WARN（一件工件最多一次上传，正常几十毫秒发完）。
- **URL 未配置**：`target=Mes` 但 `mesUrl` 空 → 每进程只 WARN 一次提示配置缺失，不上传。

## 五、客户 MES 协议定稿后，代码改哪里（重点）

### 5.1 总览（通讯链路）

```
扫码枪/人工补录 → ProductionCoordinator.DeliverSerialNumber（分流唯一收口）
                       ├─ target=Plc → PlcService.WriteSerialNumber（写 40013~ 寄存器区）
                       └─ target=Mes → MesService.SendSerialAsync（后台 HTTP POST）→ 客户 MES
```

### 5.2 必改位置

| 改什么 | 文件/位置 | 说明 |
|--------|-----------|------|
| **报文字段/格式** | `CommandCenter/Services/MesService.cs` → `BuildPayload()` | 当前把 `{sn, model, time}` 匿名对象用 Newtonsoft 序列化成 JSON。按客户接口文档改字段名/嵌套结构/时间格式即可；静态方法、有测试用例覆盖（TestRunner 第⑨组） |
| **发送方式** | `CommandCenter/Services/MesService.cs` → `SendSerialAsync()` 内 `Task.Run` 块 | 客户若要求鉴权头（Token/AppKey）、PUT 方式、表单/XML 格式，改这里的 `StringContent` 与 `http.PostAsync` 调用；需要签名的在此处加 |
| **新增报文字段** | `CommandCenter/Models/AppConfig.cs` → `SnRouteConfig` | 客户若要求随 SN 带工位号/设备编号/批次等**可配置字段**，在 `SnRouteConfig` 加属性（小驼峰即 JSON 字段名），并在 BuildPayload 里引用 |
| **回执判定/重试**（按需） | `MesService.SendSerialAsync` | 若客户要求"MES 返回成功才算数"，在 `resp.IsSuccessStatusCode` 分支里按客户返回体判定；要重试就在此实现（注意保持后台线程、别阻塞协调器） |

### 5.3 一般不用动的位置

| 位置 | 为什么不用动 |
|------|--------------|
| `Services/ProductionCoordinator.cs` → `DeliverSerialNumber()` | 分流收口已定稿：target 判定走 `SerialNumberTargets`，协议改动与它无关 |
| `Models/AppConfig.cs` → `SerialNumberTargets` | 二选一常量与判定，配置结构不变就不用动 |
| `Utils/ConfigStore.cs`（ApplyDefaults 的 sn 段兜底） | 只做空段/脏值归一，与报文无关 |
| PLC 握手、寄存器协议（docs/CommandCenter.md §5） | SN 去向与 PLC 结果握手完全解耦 |
| `Views/SettingsForm.*` | 设置页已无 SN 去向相关控件（V2.15.20 删除），纯配置变量控制 |

### 5.4 注意事项（红线）

1. **异步红线**：MES 上传必须在后台线程（现有 `Task.Run` 结构别改成同步），绝不能拖慢"SN 先于结果 40004=1 落地"与 PLC 握手节拍。
2. **混淆红线**：`CommandCenter.Models` 命名空间在 obfuscar.xml 里是豁免区（属性名=JSON 字段名）——往 `SnRouteConfig` 加字段没问题；但**别把报文模型类放到 Models 之外还指望属性名不被混淆**，或直接在 `BuildPayload` 里用匿名对象（当前做法，最稳）。
3. **改完必须跑测试**：`powershell -ExecutionPolicy Bypass -File ".opencode\skills\commandcenter-test\scripts\run-all.ps1"`；改了 `BuildPayload` 就同步改 TestRunner 第⑨组的报文断言。
4. **日志是中文**，保持现状；`OK/NG/SN/MES/HTTP` 等专有名词不翻译。

## 六、调试与验证方法

1. **看日志**（程序目录 `Logs\`）：
   - 成功：`SN 已上传 MES：sn=xxx，型号=xxx，HTTP 200`
   - 失败：`MES 上传失败：sn=xxx，HTTP 500 ...` 或 `MES 上传异常：sn=xxx，...`（网络不通/超时）
   - 未配置：`sn.mesUrl 未配置，SN 未上传 MES...`
2. **本地模拟 MES**：用任意 HTTP 回显服务（如 `nc -l` / Postman mock / 简易 python http server）当 `mesUrl`，扫一件看报文内容。
3. **对照 PLC 侧**：`target=Mes` 时用 DevTest（开发者模式）读 40013~ 应为全 0；切回 `target=Plc` 重启后恢复正常写入。
4. **节拍确认**：MES 地址填一个不通的 IP，扫码流程应照常推进（结果照写、通道照常释放），只有日志 WARN——这正是"尽力而为"语义的验证。

## 七、相关文件索引

| 文件 | 职责 |
|------|------|
| `CommandCenter/Services/MesService.cs` | MES 上传服务（HTTP POST、报文组装、防堆积）——**协议适配主要改这里** |
| `CommandCenter/Models/AppConfig.cs` | `SnRouteConfig`（sn 段配置模型）+ `SerialNumberTargets`（二选一常量与判定） |
| `CommandCenter/Services/ProductionCoordinator.cs` | `DeliverSerialNumber()`：SN 分流唯一收口（4 处调用点：扫码 OK/读码失败/超时/人工补录） |
| `CommandCenter/Views/MainForm.cs` | MesService 生命周期（BuildServices 创建、热更/关窗 Dispose） |
| `.opencode/skills/commandcenter-test/scripts/TestRunner.cs` | 第⑨组用例：路由判定/配置往返/报文格式断言 |
