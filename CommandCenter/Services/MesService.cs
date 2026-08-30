using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using CommandCenter.Models;
using CommandCenter.Utils;

namespace CommandCenter.Services
{
    /// <summary>
    /// MES 上传服务（V2.15.19）：把扫码 SN 序列号以 HTTP POST JSON 的方式发给 MES。
    ///
    /// 【背景与定位】客户提出后续 SN 可能改为"上位机直接传 MES、不再写 PLC 寄存器区（40013 起）"，
    /// 但 MES 对接协议【尚未定稿】。本服务先把"SN→MES"这条通路搭起来：
    ///   - 路由目标由配置 sn.target 控制（默认 "Mes" 才上传，二选一见 SerialNumberTargets）；
    ///   - 接收地址/超时由配置 sn.mesUrl / sn.mesTimeoutMs 控制（SnRouteConfig）。
    /// ⚠️ 当前报文是【通用占位格式】（POST JSON：sn / model / time）。客户 MES 协议定稿后
    /// 只需要改 BuildPayload（报文字段/格式）与本类的发送方式（如加鉴权头、改 PUT），
    /// 路由架构、配置段、设置页都不用动——改协议先看本注释，别在协调器里另起炉灶。
    ///
    /// 【线程与红线】SendSerialAsync 用 Task.Run 把整个 HTTP 交互丢到线程池后台执行，
    /// 【绝不阻塞】调用方（ProductionCoordinator 扫码通道后台线程）——MES 上传慢/断网
    /// 都不能拖慢"SN 先于结果 40004=1 落地"的时序与 PLC 握手节拍（UI/协调器线程禁 IO 红线）。
    /// 上传结果只记日志（成功 INFO / 失败 WARN），不产生任何业务重试/回执判定——MES 这条路
    /// 是"尽力而为"的数据上报，PLC 流程不依赖它。
    ///
    /// 【防堆积】现场断网时每件工件都会产生一次"连不上"的等待（最长 MesTimeoutMs），
    /// 若放任不管，Task 队列会随时间堆积。用 _inFlight 在途计数兜底：在途超过
    /// MaxInFlight 时丢弃本次上传并 WARN（丢的是 MES 上报，PLC 侧 SN 流程完全不受影响）。
    ///
    /// 【生命周期】归 MainForm 所有（BuildServices 创建、FormClosing/热更时 Dispose），
    /// 与 PlcService 同级；协调器只持有引用调用，不负责释放（同 ImageStore 的所有权约定，
    /// 切型号只重建协调器、MES 服务复用同一实例）。
    /// </summary>
    public class MesService : IDisposable
    {
        /// <summary>在途上传数量上限：超过即丢弃新上传并 WARN（防断网时 Task 无限堆积）。
        /// 一件工件最多一次上传，10 个在途已远超正常节拍（正常几十毫秒就发完一件）。</summary>
        private const int MaxInFlight = 10;

        private readonly SnRouteConfig _cfg;
        private HttpClient _http;            // 惰性创建：只有真的要发送（配了 URL）才建，避免无谓的连接池资源
        private readonly object _httpLock = new object();
        private bool _urlWarned;             // MesUrl 未配置只 WARN 一次（每件都 WARN 会刷屏，配置缺失提示一次足够显眼）
        private long _inFlight;              // 在途上传计数（Interlocked 维护）：超过 MaxInFlight 丢弃新上传防堆积

        /// <summary>用 SN 去向配置创建服务（MainForm.BuildServices 传入 _config.Sn）。</summary>
        public MesService(SnRouteConfig snRoute)
        {
            _cfg = snRoute ?? new SnRouteConfig();
        }

        /// <summary>
        /// 上传一条 SN 到 MES（异步、不阻塞调用方）。
        /// 调用时机（ProductionCoordinator.DeliverSerialNumber）：扫码 OK 拿到有效 SN、
        /// 人工补录写入补录 SN——只在 serial 非空时调用；扫码失败/超时的清 0 动作
        /// 只属于 PLC SN 区，MES 侧没有"空 SN"的概念，不发送。
        /// </summary>
        /// <param name="sn">本件序列号（调用方保证非空，此处仍防御空串）</param>
        /// <param name="model">当前产品型号（如 "U171"，随报文一起给 MES 便于对账）</param>
        public void SendSerialAsync(string sn, string model)
        {
            // 防御：空 SN 无内容可传（正常调用路径已过滤，这里兜底不抛异常）
            if (string.IsNullOrWhiteSpace(sn)) return;

            // 配置缺失提示：目标选了 MES 却没配 URL——显眼 WARN（只一次），SN 不上传。
            // PLC 侧流程照常（写不写 SN 区由 target 决定，与本方法无关）。
            if (string.IsNullOrWhiteSpace(_cfg.MesUrl))
            {
                if (!_urlWarned)
                {
                    _urlWarned = true;
                    LogHelper.Warn("sn.mesUrl 未配置，SN 未上传 MES（请在 appconfig.json 的 sn.mesUrl 里配 MES 接口地址）");
                }
                return;
            }

            // 防堆积：在途超限直接丢弃（断网场景每件都等超时会堆 Task，丢 MES 上报不影响 PLC 流程）
            if (Interlocked.Read(ref _inFlight) >= MaxInFlight)
            {
                LogHelper.Warn($"MES 上传在途积压超过 {MaxInFlight} 条，丢弃本条 SN 上传：{sn}");
                return;
            }

            string url = _cfg.MesUrl.Trim();
            int timeoutMs = _cfg.MesTimeoutMs > 0 ? _cfg.MesTimeoutMs : 3000;   // 非法超时回落默认 3s
            Interlocked.Increment(ref _inFlight);

            // 整个 HTTP 交互放线程池后台：协调器扫码通道立即返回继续写 40004 结果
            Task.Run(() =>
            {
                try
                {
                    var http = EnsureHttpClient(timeoutMs);
                    var content = new StringContent(BuildPayload(sn, model, DateTime.Now),
                                                    Encoding.UTF8, "application/json");
                    // HttpClient 超时内部已按 timeoutMs 控制；异步等待不占线程（线程池友好）
                    var resp = http.PostAsync(url, content).GetAwaiter().GetResult();
                    if (resp.IsSuccessStatusCode)
                        LogHelper.Info($"SN 已上传 MES：sn={sn}，型号={model}，HTTP {(int)resp.StatusCode}");
                    else
                        LogHelper.Warn($"MES 上传失败：sn={sn}，HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");
                }
                catch (Exception ex)
                {
                    // 网络不通/DNS 失败/超时等都到这：只 WARN 不抛（Task.Run 未观察异常不影响进程）
                    LogHelper.Warn($"MES 上传异常：sn={sn}，{ex.Message}");
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlight);
                }
            });
        }

        /// <summary>
        /// 组装 MES 上传报文（V2.15.19 通用占位格式）。
        /// 【协议适配点】客户 MES 协议定稿后改这里：字段名/嵌套结构/时间格式按客户接口文档调整；
        /// 若客户要求表单或 XML，把 SendSerialAsync 里 StringContent 的媒体类型一并改掉即可。
        /// 报文用项目统一的 Newtonsoft.Json 序列化（小驼峰字段名）。
        /// </summary>
        /// <param name="sn">序列号</param>
        /// <param name="model">产品型号</param>
        /// <param name="time">扫码/补录完成时刻（本地时间，随件对账用）</param>
        public static string BuildPayload(string sn, string model, DateTime time)
        {
            return JsonConvert.SerializeObject(new
            {
                sn = sn ?? "",
                model = model ?? "",
                time = time.ToString("yyyy-MM-dd HH:mm:ss.fff")
            });
        }

        /// <summary>惰性创建/复用 HttpClient（.NET Framework 下复用单实例，避免频繁新建耗尽连接池；
        /// 超时按配置设置，断网时最多等 MesTimeoutMs 即判失败）。</summary>
        private HttpClient EnsureHttpClient(int timeoutMs)
        {
            lock (_httpLock)
            {
                if (_http == null)
                {
                    _http = new HttpClient();
                    _http.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
                }
                return _http;
            }
        }

        /// <summary>释放 HttpClient（MainForm 关窗/热更时调用）。在途上传的 Task 会自然结束，
        /// Dispose 后再来的上传调用会因对象已释放抛异常——由调用方保证 Dispose 后不再调用（同其他服务约定）。</summary>
        public void Dispose()
        {
            lock (_httpLock)
            {
                _http?.Dispose();
                _http = null;
            }
        }
    }
}
