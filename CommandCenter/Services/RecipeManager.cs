using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommandCenter.Models;
using CommandCenter.Utils;
using Newtonsoft.Json;

namespace CommandCenter.Services
{
    /// <summary>
    /// 配方管理：负责加载/保存配方清单并记录当前配方。
    ///
    /// 【设计说明】
    ///   上位机不存放检测参数，只维护"配方号 + 配方名"清单（写 Config/recipes.json），
    ///   用户切换时把配方号发给 PLC，切换动作由 PLC 完成，上位机不查询结果。
    ///   配方清单是现场维护数据，故不入库（参照统一约定放 Config 目录）。
    /// </summary>
    public class RecipeManager
    {
        /// <summary>配方清单（按 Id 升序）</summary>
        public List<RecipeConfig> Recipes { get; private set; } = new List<RecipeConfig>();

        /// <summary>当前选中配方；null 表示未选择</summary>
        public RecipeConfig Current { get; private set; }

        /// <summary>当前配方变化事件（参数为配方号，-1 表示无）</summary>
        public event EventHandler<int> CurrentRecipeChanged;

        /// <summary>配方文件路径 = 程序目录\Config\recipes.json</summary>
        private static string FilePath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Config", "recipes.json");

        /// <summary>
        /// 加载配方清单。文件存在则读取，否则用几组占位配方并落盘，方便现场直接改。
        /// </summary>
        public void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath, Encoding.UTF8);
                    var list = JsonConvert.DeserializeObject<List<RecipeConfig>>(json);
                    Recipes = list ?? new List<RecipeConfig>();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("读取配方清单失败", ex);
            }

            if (Recipes.Count == 0)
            {
                Recipes = new List<RecipeConfig>
                {
                    new RecipeConfig { Id = 1, Name = "产品A" },
                    new RecipeConfig { Id = 2, Name = "产品B" },
                    new RecipeConfig { Id = 3, Name = "产品C" }
                };
                Save();
            }
            Recipes = Recipes.OrderBy(r => r.Id).ToList();
        }

        /// <summary>保存配方清单到 json 文件。</summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath,
                    JsonConvert.SerializeObject(Recipes, Formatting.Indented),
                    new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                LogHelper.Error("保存配方清单失败", ex);
            }
        }

        /// <summary>切换到指定配方号并触发切换事件。</summary>
        public void SwitchTo(int recipeId)
        {
            Current = Recipes.FirstOrDefault(r => r.Id == recipeId);
            CurrentRecipeChanged?.Invoke(this, Current?.Id ?? -1);
        }
    }
}