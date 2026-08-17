using System;

namespace CommandCenter.Utils
{
    /// <summary>
    /// 界面多语言支持（V2.15.0 新增，国际化）。
    /// 全局语言开关：界面文本一律经 T("中文","English") 取值，按当前语言返回对应文案。
    ///
    /// 【为什么用"双参内联"而不是"key 字典"】
    ///   本项目界面字符串散落在大量窗体/控件/MessageBox 里，且没有 NuGet、C#7.3；
    ///   双参内联把中英翻译写在使用处，增删改一处搞定，不用维护几千条 key，最直接可靠。
    ///   用法示例：
    ///     btnSave.Text = I18n.T("保存", "Save");
    ///     MessageBox.Show(I18n.T("配置已保存", "Config saved"));
    ///
    /// 【日志不受影响】LogHelper 仍写中文，供工程师排查维护（操作员看不到），
    ///   界面翻译只覆盖"用户看得见"的文本（按钮/标签/弹窗/状态栏/标题栏/悬停提示）。
    ///
    /// 【热更新机制】SetLanguage（Language 属性 setter）修改全局语言并触发 LanguageChanged；
    ///   MainForm 订阅该事件后全量刷新主界面文本（ApplyLanguage），无需重启；
    ///   模态对话框（ShowDialog）在打开瞬间用当前语言初始化文本即可（模态期间语言不会变化，
    ///   因为切语言入口就在设置窗体里，设置窗体打开时其他模态框无法同时打开）。
    /// </summary>
    public static class I18n
    {
        /// <summary>当前语言（"zh-CN"=中文 / "en-US"=英文），默认中文。</summary>
        private static string _language = "zh-CN";

        /// <summary>当前界面语言（"zh-CN" 中文 / "en-US" 英文），赋值即切换并触发 LanguageChanged。</summary>
        public static string Language
        {
            get { return _language; }
            set
            {
                // 只认两种语言，非法值一律回落中文（配置被手改脏也不崩）
                string normalized = (value == "en-US") ? "en-US" : "zh-CN";
                if (normalized == _language) return;
                _language = normalized;

                // 触发语言切换事件：常驻窗体（MainForm）订阅后全量刷新界面文本。
                // 注意 SetLanguage 只应在 UI 线程调用（设置窗体下拉/保存流程），订阅者同步收到通知。
                EventHandler handler = LanguageChanged;
                if (handler != null)
                {
                    try { handler(null, EventArgs.Empty); }
                    catch { /* 单个订阅者异常不阻断其他订阅者 */ }
                }
            }
        }

        /// <summary>语言切换事件（V2.15.0）：MainForm 等常驻窗体订阅后全量刷新界面文本。</summary>
        public static event EventHandler LanguageChanged;

        /// <summary>
        /// 取当前语言对应的文案。
        /// </summary>
        /// <param name="zh">中文文案</param>
        /// <param name="en">英文文案（zh 为 null 时返回 en，英文缺省时回落中文）</param>
        /// <returns>按当前语言返回 zh 或 en</returns>
        public static string T(string zh, string en)
        {
            if (_language == "en-US")
                return string.IsNullOrEmpty(en) ? zh : en;
            return zh;
        }
    }
}