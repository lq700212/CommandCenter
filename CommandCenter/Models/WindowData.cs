using System;

namespace CommandCenter.Models
{
    /// <summary>
    /// 单个显示窗口的运行数据：记录一路相机一次拍照的结果，供界面与统计使用。
    /// 窗口是"动态池"——Rows×Columns 个固定壳，本次拍照结果落到哪个窗口由调度决定，
    /// 因此这里只存"一次结果"，不绑定具体窗口。
    /// </summary>
    public class WindowData
    {
        /// <summary>本次检测序号（全局递增）</summary>
        public int SeqNo { get; set; }

        /// <summary>结果：true=OK，false=NG</summary>
        public bool IsOk { get; set; }

        /// <summary>本次保存的照片完整路径</summary>
        public string ImagePath { get; set; }

        /// <summary>检测完成时间</summary>
        public DateTime CapturedAt { get; set; } = DateTime.Now;

        /// <summary>扫码序列号（本次产品条码）</summary>
        public string SerialNumber { get; set; }

        /// <summary>相机判定文本（例如 IV4 标准结果 8 位："00000000"），非判定模式为空</summary>
        public string ResultText { get; set; } = "";

        /// <summary>拍照点位号（来自 DisplayConfig.WindowStationMap：该结果落到的窗口对应点位，进存图文件名 {点位}）</summary>
        public int StationNo { get; set; } = 1;
    }
}