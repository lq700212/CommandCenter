using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using CommandCenter.Models;

namespace CommandCenter.Views
{
    /// <summary>
    /// 系统设置窗体：直接编辑 AppConfig（引用同一实例，保存由上层 ConfigStore 完成）。
    ///
    /// ┌─────────────────────────────────────────────────────────────┐
    /// │ PLC IP:  [192.168.1.10]  端口:[502]                         │
    /// │ 显示窗口: 行[4] 列[7]                                       │
    /// │ 图片保存根目录: [D:\CommandCenter\Images]                   │
    /// │ 目录结构: [配置目录结构...] {年月日}/{SN}/{OKNG}             │
    /// │           ↑ 下方与文件名模板行留 12px 空隙（上下一致）      │
    /// │ 文件名模板:   [{点位}]   （占位符提示见界面）              │
    /// │ 窗口点位: [窗口/点位配置...] 点格改存图点位/可交换窗口位置   │
    /// │ OK/NG显示: [√标题栏高亮]                                   │
    /// │ 相机列表: ┌────────┬────┬────────────────────────┐          │
    /// │            │ 相机IP │端口│ FTP上传目录            │          │
    /// │            ├────────┼────┼────────────────────────┤          │
    /// │            │ 192…   │8500│ D:\…\ftp\cam1          │          │
    /// │            └────────┴────┴────────────────────────┘          │
    /// │            [添加一台] [删除选中]      [保存] [取消]               │
    /// └─────────────────────────────────────────────────────────────┘
    /// 布局（静态控件）在 SettingsForm.Designer.cs 里可视化维护；
    /// 本文件只负责"数据 ↔ 控件"：构造时把 AppConfig 填进界面（LoadFromConfig），
    /// 点保存回写（OnSave，仅改内存对象，返回 DialogResult.OK，上层写盘并提示重启）。
    /// 相机行数即相机台数：多台直接加行，各配各的 IP / 触发端口 / FTP 上传目录。
    /// "配置目录结构..."按钮打开 DirTreeEditForm，可视化编辑目录层级与文件名规则，
    /// 返回后把当前目录结构刷进该按钮的 ToolTip（原常驻预览标签 lblDirPreview 已删，
    /// 界面说明统一用悬停气泡，见 SettingsForm.Designer.cs 的 tip）。
    /// "窗口/点位配置..."按钮打开 WindowPointForm，可视化设置每个窗口的存图点位
    /// （默认点位=窗口编号，可自定义、可交换窗口位置，见 DisplayConfig.WindowStationMap）。
    /// </summary>
    public partial class SettingsForm : Form
    {
        private readonly AppConfig _cfg;

        public SettingsForm(AppConfig cfg)
        {
            _cfg = cfg;
            InitializeComponent();          // 先解析设计器里的控件

            LoadFromConfig();               // 把当前配置值填进各输入框
            WireButtonEvents();             // 添加/删除相机按钮事件
        }

        /// <summary>把现有配置值填充到控件（配置来源是上层传来的 _cfg 实例）。</summary>
        private void LoadFromConfig()
        {
            // PLC 基础参数
            txtPlcIp.Text = _cfg.Plc.IpAddress;
            nudPlcPort.Value = _cfg.Plc.Port;
            // 显示窗口行列
            nudRows.Value = _cfg.Display.Rows;
            nudCols.Value = _cfg.Display.Columns;
            // OK/NG 显示配置（V1.5.0：标题栏 OK/NG 计数色块高亮开关）
            chkTitleOkNg.Checked = _cfg.Display.TitleOkNgHighlight;
            // 图片保存根目录、目录结构与文件名模板（目录结构用只读预览，实际编辑进可视化对话框）
            txtSaveDir.Text = _cfg.Image.SaveRootDir;
            RefreshDirPreview();
            txtFileNameTpl.Text = _cfg.Image.FileNameTemplate;
            // 相机表格：先建列，再逐行填数据
            SetupCameraGridColumns();
            LoadCameraRows();
        }

        /// <summary>
        /// 刷新"配置目录结构..."按钮的 ToolTip：把当前目录结构（层级用 / 拼接）动态挂到按钮上，
        /// 现场鼠标悬停即可查看当前配置，界面不再占用常驻标签行。
        /// </summary>
        private void RefreshDirPreview()
        {
            var dirs = _cfg.Image.SubDirs ?? new List<string>();
            string cur = dirs.Count > 0 ? string.Join("/", dirs) : "（未配置）";
            tip.SetToolTip(btnEditDirs,
                "可视化编辑存图目录结构（目录层级列表 + 文件名规则），并实时预览 OK/NG 落盘路径。\r\n当前结构：" + cur);
        }

        /// <summary>给相机表格建好 3 列结构（列固定，运行时加一次即可，不用进设计器序列化）。
        /// 注意：旧版的"点位号"列已移除——存图点位统一由"窗口/点位配置…"（WindowStationMap）驱动。</summary>
        private void SetupCameraGridColumns()
        {
            // 仅在还没有"相机IP"列时初始化，保证重复调用不会越建越多
            if (gridCameras.Columns["IpAddress"] == null)
            {
                gridCameras.Columns.Add("IpAddress", "相机IP");
                gridCameras.Columns.Add("CommandPort", "触发端口");
                gridCameras.Columns.Add("FtpUploadDir", "FTP上传目录（留空用全局目录）");
            }
        }

        /// <summary>把现有相机配置逐行填进表格，方便现场看着改。</summary>
        private void LoadCameraRows()
        {
            foreach (var c in _cfg.Cameras ?? new List<CameraConfig>())
                gridCameras.Rows.Add(c.IpAddress, c.CommandPort, c.FtpUploadDir);
            // 至少留一行可见，别让表格空着无从下手
            if (gridCameras.Rows.Count == 0)
                gridCameras.Rows.Add("192.168.1.100", 8500, "");
        }

        /// <summary>
        /// 挂上"添加一台/删除选中/保存"按钮的点击事件。
        /// （保存/取消 按钮的 DialogResult 已在设计器里设好；取消无需挂线）
        /// </summary>
        private void WireButtonEvents()
        {
            // 添加一台相机：直接往表格追加一行默认值，现场改 IP/端口/点位即可
            btnAddCam.Click += (s, e) => gridCameras.Rows.Add("192.168.1.1", 8500, 1, "");
            // 删除选中：把当前选中的行整行移除；没有选中行则什么都不做
            btnDelCam.Click += (s, e) =>
            {
                // IsNewRow（AllowUserToAddRows 附带的"新行"）不算真实相机行，删不了也没必要删；
                // 其余选中的行逐个移除
                var rows = gridCameras.SelectedRows.Cast<DataGridViewRow>()
                    .Where(r => !r.IsNewRow).ToList();
                if (rows.Count == 0)
                {
                    // 没有可删的选中行（含只点了"新行"的情况），提示一句让操作员先点选中
                    MessageBox.Show("请先点击表格中要删除的相机行（整行高亮），再点\"删除选中\"。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                foreach (var r in rows)
                    gridCameras.Rows.Remove(r);
            };
            // 保存：把界面值回写内存配置，返回 DialogResult.OK（上层负责写盘与提示）
            btnSave.Click += OnSave;
            // 打开目录结构可视化配置对话框；改的是同一 _cfg.Image 实例，返回后刷新预览
            btnEditDirs.Click += (s, e) =>
            {
                using (var dlg = new DirTreeEditForm(_cfg.Image))
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                        RefreshDirPreview();
                }
            };
            // 打开窗口/存图点位可视化配置对话框：改的是同一 _cfg.Display.WindowStationMap 实例。
            // 注意：行列数取【界面 nud 上的最新值】（用户可能刚改了行/列还没保存），
            // 而不是 _cfg.Display.Rows/Columns（那是上次已保存的旧值）——保证格子矩阵
            // 与"用户即将保存的新窗口总数"一致，改完行列再配置点位所见即所得。
            btnEditPoints.Click += (s, e) =>
            {
                using (var dlg = new WindowPointForm(_cfg.Display.WindowStationMap,
                                                     (int)nudRows.Value, (int)nudCols.Value))
                {
                    dlg.ShowDialog(this);
                }
            };
        }

        /// <summary>把界面值回写内存配置（注意：窗口行列/相机台数改动需重启生效）。</summary>
        private void OnSave(object sender, EventArgs e)
        {
            _cfg.Plc.IpAddress = txtPlcIp.Text.Trim();
            _cfg.Plc.Port = (int)nudPlcPort.Value;
            _cfg.Display.Rows = (int)nudRows.Value;
            _cfg.Display.Columns = (int)nudCols.Value;
            _cfg.Display.TitleOkNgHighlight = chkTitleOkNg.Checked;
            _cfg.Image.SaveRootDir = txtSaveDir.Text.Trim();
            _cfg.Image.FileNameTemplate = txtFileNameTpl.Text.Trim();
            // 目录结构由 DirTreeEditForm 直接写入 _cfg.Image.SubDirs，这里不用回写；
            // 未打开过对话框则保持 SubDirs 原值（首次为模型默认的三层）。

            // 相机：逐行回写；IP 空的行视为"未填写"自动剔除；剔除后一台都不剩则补一台默认。
            // 注意：存图点位不再在此配置（由"窗口/点位配置…"的 WindowStationMap 驱动，见 DisplayConfig）
            var cams = new List<CameraConfig>();
            foreach (DataGridViewRow r in gridCameras.Rows)
            {
                string ip = r.Cells["IpAddress"].Value != null ? r.Cells["IpAddress"].Value.ToString().Trim() : "";
                if (string.IsNullOrEmpty(ip)) continue; // 空行/未填IP行忽略
                int port = 8500;
                string portTxt = r.Cells["CommandPort"].Value == null ? "" : r.Cells["CommandPort"].Value.ToString();
                if (!int.TryParse(portTxt, out port)) port = 8500;   // TryParse 失败会写 0，手动回默认
                cams.Add(new CameraConfig
                {
                    IpAddress = ip,
                    CommandPort = Math.Max(1, port),
                    FtpUploadDir = r.Cells["FtpUploadDir"].Value == null ? "" : r.Cells["FtpUploadDir"].Value.ToString().Trim()
                });
            }
            if (cams.Count == 0) cams.Add(new CameraConfig()); // 兜底：至少一台相机
            _cfg.Cameras = cams;
        }
    }
}