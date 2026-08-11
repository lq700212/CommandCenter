using System.Collections.Generic;
using System.IO;

namespace CommandCenter.Models
{
    /// <summary>
    /// 配方模型：上位机只保存"配方名 + 配方号"，不保存任何检测参数。
    /// 实际参数在 PLC 侧，上位机切换时把配方号写给 PLC 即可，无需关心是否切换成功
    /// （业务约束：PLC 会自行处理，上位机不查询结果，否则可能卡流程）。
    /// </summary>
    public class RecipeConfig
    {
        /// <summary>配方号（唯一，写入 PLC 的字段）</summary>
        public int Id { get; set; }

        /// <summary>配方名（界面显示，自动补全输入也用它）</summary>
        public string Name { get; set; }
    }
}