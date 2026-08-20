namespace CommandCenter.Utils
{
    /// <summary>
    /// 占位符"显示层本地化"（V2.15.12）：英文界面把中文占位符显示成英文、保存时还原中文。
    ///
    /// 【核心契约（改这里前必须先读这段）】配置存储与 ImageStore.RenderTemplate 渲染【始终用中文占位符】
    /// （{年月日}/{点位}/{相机}…，见 ImageStore.RenderTemplate 的 Replace 链）——英文界面只是"给人看"时
    /// 翻译成 {Date}/{Station}…，【绝不把英文占位符写进配置文件】，否则 RenderTemplate 不识别、
    /// 归档路径会变成字面 "{Date}" 目录的脏配置。
    ///
    /// 【使用方（禁止各层各写一套）】
    ///   - DirTreeEditForm：目录层级列表 lstLevels、文件名规则框 txtFileNameTpl、占位符下拉 cmbPlaceholder；
    ///   - SettingsForm：文件名模板框 txtFileNameTpl。
    /// 统一模式：载入/新增时 ToDisplay 显示，保存/渲染前 ToStorage 还原。
    /// </summary>
    public static class PlaceholderLocalizer
    {
        /// <summary>中文存储占位符 → 英文显示占位符（en-US 界面；其它语言原样返回）。</summary>
        public static string ToDisplay(string s)
        {
            if (I18n.Language != "en-US" || string.IsNullOrEmpty(s)) return s;
            // 先替换最长项（{年月日}），避免与 {年}/{月}/{日} 相互干扰；{SN}/{OKNG} 本就是拉丁字母，无需翻译
            return s.Replace("{年月日}", "{Date}")
                    .Replace("{年}", "{Year}")
                    .Replace("{月}", "{Month}")
                    .Replace("{日}", "{Day}")
                    .Replace("{点位}", "{Station}")
                    .Replace("{相机}", "{Camera}")
                    .Replace("{时间}", "{Time}");
        }

        /// <summary>英文显示占位符 → 中文存储占位符（与 ToDisplay 互逆；en-US 界面才需还原）。</summary>
        public static string ToStorage(string s)
        {
            if (I18n.Language != "en-US" || string.IsNullOrEmpty(s)) return s;
            return s.Replace("{Date}", "{年月日}")
                    .Replace("{Year}", "{年}")
                    .Replace("{Month}", "{月}")
                    .Replace("{Day}", "{日}")
                    .Replace("{Station}", "{点位}")
                    .Replace("{Camera}", "{相机}")
                    .Replace("{Time}", "{时间}");
        }
    }
}