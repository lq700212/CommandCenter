// ═══════════════════════════════════════════════════════════════════════════
// CommandCenter UI 交互回归探针（commandcenter-test skill 的 uitests.ps1 编译运行）
//
// 【定位】补 TestRunner.cs 覆盖不到的"真实窗体交互"层（70 条，V2.16.4）：
//   ① 窗口点位配置表下拉改值不回退（V2.15.x 值无效回退锚点）；
//   ② 窗口点位编辑（SwapCells 点位互换+禁用跟随/HighlightFor 三态优先级/Clone 深拷贝/
//      相机型号单向联动/FlushProgramGrid 去重排序）；
//   ③ 产品型号配置窗（预载/深拷贝隔离/确定写回/DeleteRows 优先勾选）；
//   ④ 补录/异常弹窗（构造预填/失败文本显隐/MuteToday/读到真码自动关闭，V2.14.48 首覆盖）。
//   用代码模拟用户点选下拉、切型号/相机、点确定，断言界面显示与写回配置的值。
//   不需要现场设备、不连 PLC/相机，纯内存配置 + 真实 UI 控件。
// 【探针规范】显示态（Visible）断言必须 Show 后做——未 Show 窗体的子控件读 Visible 恒 false。
//
// 【为什么要这一层】V2.15.x 的"改完程序号/点位被悄悄回退"是 DataGridView 控件层的行为
//   （单元格值不在下拉候选里 → 判"值无效" → 回退成候选第一项），纯逻辑用例测不到，
//   必须真开一次窗体才能复现/守住。本文件就是那道"UI 行为锚点"。
//
// 【红线】禁止调用 ConfigStore.Load()/Save()（会覆盖开发机 bin\Debug\Config\appconfig.json）；
//   配置全部在内存里 new，点确定只改内存对象，不落盘。
//
// 【编译】uitests.ps1 用 Roslyn csc 编译到 bin\Debug\cc_ui_probe.exe 运行（/r:CommandCenter.exe）。
//   必须 [STAThread]，WinForms 控件要求单线程套间。
// 退出码：0=全部通过；1=存在失败。
// ═══════════════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using CommandCenter.Models;
using CommandCenter.Services;
using CommandCenter.Views;

internal static class UiProbe
{
    private static int _pass, _fail;
    private static readonly List<string> Failures = new List<string>();

    private static void Check(string name, bool cond)
    {
        if (cond) { _pass++; Console.WriteLine("  [PASS] " + name); }
        else { _fail++; Failures.Add(name); Console.WriteLine("  [FAIL] " + name); }
    }

    private static void Eq<T>(string name, T expected, T actual)
    {
        Check(name + " (期望=" + expected + ", 实际=" + actual + ")", Equals(expected, actual));
    }

    [STAThread]
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        try { RunAll(); }
        catch (Exception ex)
        {
            Console.WriteLine("[FATAL] UI 探针异常：" + ex);
            return 1;
        }
        Console.WriteLine();
        Console.WriteLine("════════ 汇总：通过 " + _pass + " / 失败 " + _fail + " ════════");
        foreach (var f in Failures) Console.WriteLine("  FAIL: " + f);
        return _fail == 0 ? 0 : 1;
    }

    private static void RunAll()
    {
        TestWindowPointGrid();
        TestWindowEditing();
        TestModelIndexForm();
        TestSerialDialogs();
    }

    // ─────────── 窗口/点位与相机程序配置（WindowPointForm.dgvPrograms）───────────
    private static void TestWindowPointGrid()
    {
        Console.WriteLine();
        Console.WriteLine("── ① 窗口点位配置表：选中高亮 + 下拉改值不回退 + 确定写回 ──");

        var cams = new List<CameraConfig>
        {
            new CameraConfig
            {
                CameraId = 1, Name = "上相机", IpAddress = "19.87.6.213",
                ModelStationPrograms = new List<ModelStationPrograms>
                {
                    new ModelStationPrograms { ModelName = "U171", Programs = new List<StationProgramItem>
                    {
                        new StationProgramItem { StationNo = 1, ProgramNo = -1 },
                        new StationProgramItem { StationNo = 2, ProgramNo = 7 },
                        new StationProgramItem { StationNo = 3, ProgramNo = -1 },
                    } },
                    new ModelStationPrograms { ModelName = "Z121", Programs = new List<StationProgramItem>
                    {
                        new StationProgramItem { StationNo = 1, ProgramNo = 3 },
                        new StationProgramItem { StationNo = 2, ProgramNo = -1 },
                    } },
                }
            },
            new CameraConfig
            {
                CameraId = 2, Name = "下相机", IpAddress = "19.87.6.212",
                ModelStationPrograms = new List<ModelStationPrograms>
                {
                    new ModelStationPrograms { ModelName = "U171", Programs = new List<StationProgramItem>
                    {
                        new StationProgramItem { StationNo = 1, ProgramNo = 2 },
                        // 老配置残留的异常值：点位 9（超出常规点位表）、程序号 200（越界 >127）
                        new StationProgramItem { StationNo = 9, ProgramNo = 200 },
                    } },
                }
            },
        };

        var form = new WindowPointForm(
            new List<int> { 1, 2, 3, 4 }, 2, 2, cams,
            new List<bool> { true, true, true, true },
            new List<string> { "U171", "Z121" }, false, "U171", new List<ModelWindowPointMap>());
        // 放到屏幕外：探针要真实 Show（DataGridView 拉起下拉编辑控件必须有句柄），但不打断用户
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-3000, -3000);
        form.Show();
        Application.DoEvents();

        var dgv = (DataGridView)form.Controls.Find("dgvPrograms", true)[0];
        var cmbModel = (ComboBox)form.Controls.Find("cmbModel", true)[0];
        var cmbCamera = (ComboBox)form.Controls.Find("cmbCamera", true)[0];
        int dataErrors = 0;
        dgv.DataError += (s, e) => { dataErrors++; e.ThrowException = false; };

        // 产品里的"不切换"文案（中英双语由 I18n 决定），用反射取同一份常量，避免用例写死语言
        string noSwitch = (string)typeof(WindowPointForm)
            .GetProperty("NoSwitch", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        // ── 选中高亮（用户诉求①：选中行要整行高亮）──
        Check("选中模式=整行(FullRowSelect)", dgv.SelectionMode == DataGridViewSelectionMode.FullRowSelect);
        Check("选中行有高亮底色(非透明)", dgv.DefaultCellStyle.SelectionBackColor.A == 255
            && dgv.DefaultCellStyle.SelectionBackColor != Color.Empty);
        Check("选中行有高亮字色(非透明)", dgv.DefaultCellStyle.SelectionForeColor.A == 255);
        // 行为锚点：模拟"点击某一行" → 该行整行选中（FullRowSelect 下 SelectedRows=该行，
        // SelectedCells 只含当前单元格，不能用来判整行高亮——这里用 SelectedRows 权威判定）
        dgv.ClearSelection();
        dgv.CurrentCell = dgv.Rows[1].Cells[0];     // 选中第2行
        Application.DoEvents();
        bool row1Sel = dgv.SelectedRows.Count == 1 && dgv.SelectedRows[0].Index == 1;
        bool row0Not = !dgv.Rows[0].Selected;
        Check("点击第2行后整行高亮(SelectedRows=第2行)", row1Sel);
        Check("点击第2行后其它行不高亮", row0Not);

        // ── 初始灌值：点位/程序号都是字符串（与下拉候选同类型，这是"不回退"的前提）──
        string init = Dump(dgv);
        Console.WriteLine("    初始: " + init);
        Check("初始单元格值全为字符串", AllString(dgv));
        Eq("初始第1行程序号显示", noSwitch, Text(dgv, 0, 1));
        Eq("初始第2行程序号显示", "7", Text(dgv, 1, 1));

        // ── 用户诉求②：改程序号 → 点其它行，值不能回退成"不切换"──
        SelectInCombo(dgv, 0, 1, "5");
        dgv.CurrentCell = dgv.Rows[1].Cells[1];      // 点其它行
        Application.DoEvents();
        Console.WriteLine("    改程序号后: " + Dump(dgv));
        Eq("改程序号5后仍显示5（不回退成" + noSwitch + "）", "5", Text(dgv, 0, 1));

        // ── 同类问题：只改点位（不动程序号）──
        SelectInCombo(dgv, 0, 0, "4");
        dgv.CurrentCell = dgv.Rows[1].Cells[0];      // 点其它行
        Application.DoEvents();
        Console.WriteLine("    改点位后: " + Dump(dgv));
        Eq("改点位4后仍显示4（不回退成候选第一项）", "4", Text(dgv, 0, 0));
        Eq("改点位后程序号保持5", "5", Text(dgv, 0, 1));

        // ── 切型号（会重建点位候选 + 重灌表格）：已配的值不能被悄悄改号 ──
        cmbModel.SelectedItem = "Z121";
        Application.DoEvents();
        Console.WriteLine("    切型号Z121后: " + Dump(dgv));
        Eq("Z121 第1行程序号", "3", Text(dgv, 0, 1));
        Eq("Z121 第2行程序号", noSwitch, Text(dgv, 1, 1));
        cmbModel.SelectedItem = "U171";
        Application.DoEvents();
        Console.WriteLine("    切回U171后: " + Dump(dgv));
        Check("切回U171仍保留点位4/程序5的改动", FindRow(dgv, "4", "5") >= 0);

        // ── 老配置异常值：点位9 要显示得出来、程序号200 按"不切换"显示 ──
        cmbCamera.SelectedIndex = 1;                 // 切到"下相机"（表里含 9/200）
        Application.DoEvents();
        Console.WriteLine("    下相机(含异常值): " + Dump(dgv));
        Check("异常点位9能原样显示（补进候选，不回退）", FindRow(dgv, "9", noSwitch) >= 0);
        cmbCamera.SelectedIndex = 0;                 // 切回上相机
        Application.DoEvents();

        // ── 点确定：写回内存配置的值必须等于界面上看到的 ──
        typeof(WindowPointForm).GetMethod("OnOk", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(form, null);
        var saved = new StringBuilder();
        foreach (var p in cams[0].ModelStationPrograms[0].Programs)
            saved.Append("[").Append(p.StationNo).Append("→").Append(p.ProgramNo).Append("]");
        Console.WriteLine("    写回上相机U171表: " + saved);
        Eq("点确定写回 U171 表（点位2→7/3→-1/4→5）", "[2→7][3→-1][4→5]", saved.ToString());

        // ── 全程不该出现"值无效"（DataError）──
        Eq("全程 DataGridView DataError 次数", 0, dataErrors);

        form.Close();
        form.Dispose();
    }

    // ── 工具：模拟"用户点开下拉选中某一项" ──
    private static void SelectInCombo(DataGridView dgv, int row, int col, object item)
    {
        dgv.CurrentCell = dgv.Rows[row].Cells[col];
        dgv.BeginEdit(true);
        var combo = dgv.EditingControl as ComboBox;
        if (combo == null) { Check("拿到第" + row + "行第" + col + "列的下拉编辑控件", false); return; }
        Check("下拉候选含 " + item + "（第" + row + "行第" + col + "列）", combo.Items.Contains(item));
        Check("编辑态下拉为只读(DropDownList，禁手输)", combo.DropDownStyle == ComboBoxStyle.DropDownList);
        combo.SelectedIndex = combo.Items.IndexOf(item);
        dgv.EndEdit();
        Application.DoEvents();
    }

    private static string Text(DataGridView dgv, int row, int col)
    {
        return Convert.ToString(dgv.Rows[row].Cells[col].FormattedValue);
    }

    private static bool AllString(DataGridView dgv)
    {
        foreach (DataGridViewRow r in dgv.Rows)
            foreach (DataGridViewCell c in r.Cells)
                if (c.Value != null && !(c.Value is string)) return false;
        return true;
    }

    private static int FindRow(DataGridView dgv, string station, string program)
    {
        foreach (DataGridViewRow r in dgv.Rows)
            if (Convert.ToString(r.Cells[0].FormattedValue) == station
             && Convert.ToString(r.Cells[1].FormattedValue) == program) return r.Index;
        return -1;
    }

    private static string Dump(DataGridView dgv)
    {
        var sb = new StringBuilder();
        foreach (DataGridViewRow r in dgv.Rows)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append("[点位=").Append(r.Cells[0].FormattedValue)
              .Append(" 程序=").Append(r.Cells[1].FormattedValue).Append("]");
        }
        return sb.ToString();
    }

    // 扫码枪桩（SerialInputForm/ScannerFailForm 自动关闭用例）：可手动触发读码事件
    private sealed class FakeScanner : IScanner
    {
        public event EventHandler<string> SerialNumberScanned;
        public event EventHandler<bool> ConnectionChanged;
        public event EventHandler<string> ScanFailed;
        public bool IsOpen => true;
        public bool Open() => true;
        public bool SendTrigger() => true;
        public void Dispose() { }
        public void FireCode(string code) => SerialNumberScanned?.Invoke(this, code);
    }

    private static List<CameraConfig> ProbeCameras()
    {
        return new List<CameraConfig>
        {
            new CameraConfig
            {
                CameraId = 1, Name = "上相机", IpAddress = "19.87.6.213",
                ModelStationPrograms = new List<ModelStationPrograms>
                {
                    new ModelStationPrograms { ModelName = "U171", Programs = new List<StationProgramItem>
                    {
                        new StationProgramItem { StationNo = 1, ProgramNo = -1 },
                        new StationProgramItem { StationNo = 2, ProgramNo = 7 },
                        new StationProgramItem { StationNo = 3, ProgramNo = -1 },
                    } },
                    new ModelStationPrograms { ModelName = "Z121", Programs = new List<StationProgramItem>
                    {
                        new StationProgramItem { StationNo = 1, ProgramNo = 3 },
                        new StationProgramItem { StationNo = 2, ProgramNo = -1 },
                    } },
                }
            },
            new CameraConfig
            {
                CameraId = 2, Name = "下相机", IpAddress = "19.87.6.212",
                ModelStationPrograms = new List<ModelStationPrograms>
                {
                    new ModelStationPrograms { ModelName = "U171", Programs = new List<StationProgramItem>
                    {
                        new StationProgramItem { StationNo = 1, ProgramNo = 2 },
                        new StationProgramItem { StationNo = 9, ProgramNo = 200 },
                    } },
                }
            },
        };
    }

    private static WindowPointForm NewProbeForm(List<CameraConfig> cams)
    {
        var form = new WindowPointForm(
            new List<int> { 1, 2, 3, 4 }, 2, 2, cams,
            new List<bool> { true, true, true, true },
            new List<string> { "U171", "Z121" }, false, "U171", new List<ModelWindowPointMap>());
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-3000, -3000);
        form.Show();
        Application.DoEvents();
        return form;
    }

    private static object GetField(object o, string name)
    {
        return o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
    }

    private static void SetField(object o, string name, object val)
    {
        o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, val);
    }

    private static object Call(object o, string name, params object[] args)
    {
        return o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(o, args);
    }

    // ─────────── ② 窗口点位编辑：交换+禁用跟随/高亮三态/深拷贝/单向联动/回存去重 ───────────
    private static void TestWindowEditing()
    {
        Console.WriteLine();
        Console.WriteLine("── ② 窗口点位编辑：SwapCells禁用跟随 + HighlightFor三态 + Clone + 联动 + Flush去重 ──");
        var cams = ProbeCameras();
        var form = NewProbeForm(cams);
        try
        {
            var t = typeof(WindowPointForm);
            string noSwitch = (string)t.GetProperty("NoSwitch",
                BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

            // —— SwapCells：点位互换 + 禁用跟随点位走 ——
            var map0 = (List<WindowPointItem>)Call(form, "_windowEditMap");
            int s0 = map0[0].StationNo, s1 = map0[1].StationNo;
            var enabled = (List<bool>)GetField(form, "_enabled");
            enabled[0] = false;   // 窗1禁用（点位跟到哪，禁用跟到哪）
            enabled[1] = true;
            Call(form, "SwapCells", 0, 1);
            var map1 = (List<WindowPointItem>)Call(form, "_windowEditMap");
            Check("Swap后点位互换", map1[0].StationNo == s1 && map1[1].StationNo == s0);
            var enabled2 = (List<bool>)GetField(form, "_enabled");
            Check("Swap后禁用跟随点位（窗1解禁/窗2禁用）", enabled2[0] && !enabled2[1]);
            var flash = (List<int>)GetField(form, "_swapFlash");
            Check("Swap后绿闪含0,1", flash.Contains(0) && flash.Contains(1));
            // a==b 与越界：不动不崩
            var before = string.Join(",", map1.Select(p => p == null ? "null" : p.CameraId + ":" + p.StationNo));
            Call(form, "SwapCells", 0, 0);
            Call(form, "SwapCells", -1, 99);
            var map2 = (List<WindowPointItem>)Call(form, "_windowEditMap");
            Eq("Swap自交/越界不动", before, string.Join(",",
                map2.Select(p => p == null ? "null" : p.CameraId + ":" + p.StationNo)));

            // —— HighlightFor 三态优先级：绿 > 天蓝 > 浅黄 > null ——
            Color selC = (Color)t.GetField("SelectedColor", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Color startC = (Color)t.GetField("SwapStartColor", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            Color doneC = (Color)t.GetField("SwapDoneColor", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            SetField(form, "_swapFlash", new List<int> { 2 });
            SetField(form, "_swapping", true);
            SetField(form, "_swapA", 1);
            SetField(form, "_selectedIdx", 1);
            Eq("绿优先（flash盖过起点/选中）", doneC, (Color)Call(form, "HighlightFor", 2));
            Eq("起点天蓝", startC, (Color)Call(form, "HighlightFor", 1));
            SetField(form, "_swapFlash", new List<int>());
            Eq("清flash后起点仍天蓝", startC, (Color)Call(form, "HighlightFor", 1));
            SetField(form, "_swapping", false);
            Eq("退交换后普通选中浅黄", selC, (Color)Call(form, "HighlightFor", 1));
            SetField(form, "_selectedIdx", -1);
            Check("全清无高亮", Call(form, "HighlightFor", 1) == null);
            Check("未选中窗无高亮", Call(form, "HighlightFor", 3) == null);

            // —— ClonePoints / CloneTable 深拷贝 ——
            var srcPts = new List<WindowPointItem>
            {
                new WindowPointItem { CameraId = 2, StationNo = 3 }, null
            };
            var cpPts = (List<WindowPointItem>)t.GetMethod("ClonePoints",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { srcPts });
            Check("ClonePoints长度同且null保留", cpPts.Count == 2 && cpPts[1] == null);
            Check("ClonePoints不同引用", !ReferenceEquals(srcPts[0], cpPts[0]));
            cpPts[0].StationNo = 99;
            Eq("改副本不污染源", 3, srcPts[0].StationNo);
            var cpNull = (List<WindowPointItem>)t.GetMethod("ClonePoints",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { null });
            Check("ClonePoints(null)→空", cpNull != null && cpNull.Count == 0);
            var srcTab = new List<StationProgramItem>
            {
                null, new StationProgramItem { StationNo = 1, ProgramNo = 5 }
            };
            var cpTab = (List<StationProgramItem>)t.GetMethod("CloneTable",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { srcTab });
            Check("CloneTable跳过null元", cpTab.Count == 1 && cpTab[0].StationNo == 1);

            // —— ModelCandidatesFor / SyncModelForCamera 单向联动 ——
            var candUp = (List<string>)Call(form, "ModelCandidatesFor", 0);
            Check("上相机候选含U171/Z121", candUp.Contains("U171") && candUp.Contains("Z121"));
            var candDown = (List<string>)Call(form, "ModelCandidatesFor", 1);
            Check("下相机候选仅U171（Z121无点位被过滤）", candDown.Count == 1 && candDown[0] == "U171");
            Eq("旧型号在候选保留", "U171", (string)Call(form, "SyncModelForCamera", 1, "U171"));
            Eq("旧型号不在候选落首项", "U171", (string)Call(form, "SyncModelForCamera", 1, "Z121"));

            // —— FlushProgramGrid：非法跳行/越界程序→-1/同点位后者覆盖/按点位排序 ——
            var dgv = (DataGridView)form.Controls.Find("dgvPrograms", true)[0];
            dgv.Rows.Clear();
            dgv.Rows.Add("3", "5");
            dgv.Rows.Add("3", "7");
            dgv.Rows.Add("x", "5");
            dgv.Rows.Add("4", "200");
            dgv.Rows.Add("5", noSwitch);
            Application.DoEvents();
            Call(form, "FlushProgramGrid");
            var slot = (List<StationProgramItem>)Call(form, "_slot");
            Eq("Flush去重后3条", 3, slot.Count);
            var row3 = slot.Find(x => x.StationNo == 3);
            Check("同点位后者覆盖(3→7)", row3 != null && row3.ProgramNo == 7);
            var row4 = slot.Find(x => x.StationNo == 4);
            Check("越界程序200→-1", row4 != null && row4.ProgramNo == -1);
            var row5 = slot.Find(x => x.StationNo == 5);
            Check("不切换→-1", row5 != null && row5.ProgramNo == -1);
            Check("按点位排序", slot[0].StationNo == 3 && slot[1].StationNo == 4 && slot[2].StationNo == 5);
            // 排序：倒序灌入仍正序落副本
            dgv.Rows.Clear();
            dgv.Rows.Add("5", "1");
            dgv.Rows.Add("3", "2");
            Application.DoEvents();
            Call(form, "FlushProgramGrid");
            var slot2 = (List<StationProgramItem>)Call(form, "_slot");
            Check("倒序灌入正序落盘", slot2.Count == 2 && slot2[0].StationNo == 3 && slot2[1].StationNo == 5);
        }
        finally
        {
            form.Close();
            form.Dispose();
        }
    }

    // ─────────── ③ 产品型号配置窗：预载/深拷贝隔离/确定写回/删除 ───────────
    private static void TestModelIndexForm()
    {
        Console.WriteLine();
        Console.WriteLine("── ③ 产品型号配置窗：预载 + 深拷贝隔离 + 确定写回 + DeleteRows ──");
        var target = new List<ModelIndexItem> { new ModelIndexItem { ModelName = "U171", ModelIndex = 2 } };
        var form = new ModelIndexEditForm(target);
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-3000, -3000);
        form.Show();
        Application.DoEvents();
        try
        {
            var grid = (DataGridView)form.Controls.Find("grid", true)[0];
            Eq("打开预载1行", 1, grid.Rows.Count);
            Eq("预载型号", "U171", Convert.ToString(grid.Rows[0].Cells["colModel"].Value));
            Eq("预载序号", "2", Convert.ToString(grid.Rows[0].Cells["colIndex"].Value));

            // 深拷贝隔离：改表格不点确定，原配置不动
            grid.Rows[0].Cells["colModel"].Value = "XXX";
            Eq("未确定原配置不动", "U171", target[0].ModelName);
            grid.Rows[0].Cells["colModel"].Value = "U171";
            Application.DoEvents();

            // BtnAdd_Click：末尾追加空行("",0)
            int n0 = grid.Rows.Count;
            form.GetType().GetMethod("BtnAdd_Click", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(form, null);
            Application.DoEvents();
            Eq("新增行数+1", n0 + 1, grid.Rows.Count);
            var last = grid.Rows[grid.Rows.Count - 1];
            Check("新增行型号空序号0",
                Convert.ToString(last.Cells["colModel"].Value) == ""
                && Convert.ToString(last.Cells["colIndex"].Value) == "0");

            // 确定写回：空型号行跳过，合法行整体写回（无弹窗路径）
            last.Cells["colModel"].Value = "Z121";
            last.Cells["colIndex"].Value = 1;
            grid.Rows.Add(false, 9, "");   // 空型号行 → 写回跳过
            Application.DoEvents();
            form.GetType().GetMethod("OnOk", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(form, null);
            Eq("确定写回条数（空行跳过）", 2, target.Count);
            Check("写回含U171:2", target.Any(x => x.ModelName == "U171" && x.ModelIndex == 2));
            Check("写回含Z121:1", target.Any(x => x.ModelName == "Z121" && x.ModelIndex == 1));
            Eq("确定 DialogResult=OK", DialogResult.OK, form.DialogResult);

            // DeleteRows：优先勾选行（注：失败校验路径会弹窗，本探针只覆盖成功路径）
            var form2 = new ModelIndexEditForm(new List<ModelIndexItem>
            {
                new ModelIndexItem { ModelName = "A", ModelIndex = 1 },
                new ModelIndexItem { ModelName = "B", ModelIndex = 2 },
                new ModelIndexItem { ModelName = "C", ModelIndex = 3 },
            });
            form2.StartPosition = FormStartPosition.Manual;
            form2.Location = new Point(-3000, -3000);
            form2.Show();
            Application.DoEvents();
            try
            {
                var g2 = (DataGridView)form2.Controls.Find("grid", true)[0];
                g2.Rows[0].Cells["colSel"].Value = true;
                g2.Rows[2].Cells["colSel"].Value = true;
                bool del = (bool)form2.GetType().GetMethod("DeleteRows",
                    BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form2, null);
                Check("勾选两行删两行", del && g2.Rows.Count == 1);
                Eq("剩余B行", "B", Convert.ToString(g2.Rows[0].Cells["colModel"].Value));
                // 无勾选删选中行
                g2.Rows[0].Selected = true;
                bool del2 = (bool)form2.GetType().GetMethod("DeleteRows",
                    BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form2, null);
                Check("无勾选删选中行", del2 && g2.Rows.Count == 0);
                bool del3 = (bool)form2.GetType().GetMethod("DeleteRows",
                    BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form2, null);
                Check("无勾选无选中→false", !del3);
            }
            finally
            {
                form2.Close();
                form2.Dispose();
            }
        }
        finally
        {
            form.Close();
            form.Dispose();
        }
    }

    // ─────────── ④ 补录/异常弹窗：构造属性 + 读到真码自动关闭 ───────────
    private static void TestSerialDialogs()
    {
        Console.WriteLine();
        Console.WriteLine("── ④ 补录/异常弹窗：构造预填 + 失败文本显隐 + MuteToday + 真码自动关闭 ──");
        // —— SerialInputForm 构造（不点确定：空提交弹窗会阻塞探针）——
        var s1 = new SerialInputForm(null);
        try
        {
            Eq("构造null SerialNumber空", "", s1.SerialNumber);
            var txt = (TextBox)s1.Controls.Find("txtValue", true)[0];
            Eq("构造null预填空", "", txt.Text);
        }
        finally { s1.Dispose(); }
        var s2 = new SerialInputForm(" ab ");
        try
        {
            var txt = (TextBox)s2.Controls.Find("txtValue", true)[0];
            Eq("构造预填原样", " ab ", txt.Text);
        }
        finally { s2.Dispose(); }

        // —— ScannerFailForm 构造 ——
        var f1 = new ScannerFailForm("");
        try
        {
            f1.StartPosition = FormStartPosition.Manual;
            f1.Location = new Point(-3000, -3000);
            f1.Show();
            Application.DoEvents();
            var lbl = (Label)f1.Controls.Find("lblFailText", true)[0];
            Check("空失败文本隐藏", !lbl.Visible);
            Check("MuteToday默认false", !f1.MuteToday);
        }
        finally { f1.Close(); f1.Dispose(); }
        var f2 = new ScannerFailForm("ERROR");
        try
        {
            // 注：Visible 是显示态属性，未 Show 的窗体子控件读 Visible 恒 false；
            // 显示逻辑必须 Show 后断言（探针窗体一律放屏幕外，不打断用户）。
            f2.StartPosition = FormStartPosition.Manual;
            f2.Location = new Point(-3000, -3000);
            f2.Show();
            Application.DoEvents();
            var lbl = (Label)f2.Controls.Find("lblFailText", true)[0];
            Console.WriteLine("    lblFailText.Visible=" + lbl.Visible + " Text=[" + lbl.Text + "]");
            Check("有失败文本显示", lbl.Visible && lbl.Text.Contains("ERROR"));
            var chk = (CheckBox)f2.Controls.Find("chkMuteToday", true)[0];
            chk.Checked = true;
            Check("勾选后MuteToday true", f2.MuteToday);
        }
        finally { f2.Close(); f2.Dispose(); }

        // —— 读到真码自动关闭（V2.14.48）：Show非模态 + 推码 + 泵消息 ——
        var fake = new FakeScanner();
        var s3 = new SerialInputForm("OLD", new IScanner[] { fake });
        s3.StartPosition = FormStartPosition.Manual;
        s3.Location = new Point(-3000, -3000);
        s3.Show();
        Application.DoEvents();
        fake.FireCode("TRUECODE123");
        bool closed = false;
        for (int i = 0; i < 40 && !closed; i++)
        {
            System.Threading.Thread.Sleep(50);
            Application.DoEvents();
            closed = !s3.Visible || s3.IsDisposed;
        }
        Check("补录窗读到真码自动关闭", closed);
        // 连续推码不重复关闭崩溃
        try { fake.FireCode("AGAIN"); Application.DoEvents(); Check("重复推码不崩", true); }
        catch (Exception ex) { Check("重复推码不崩 (异常:" + ex.GetType().Name + ")", false); }
        if (!s3.IsDisposed) { s3.Close(); s3.Dispose(); }

        var fake2 = new FakeScanner();
        var f3 = new ScannerFailForm("ERROR", new IScanner[] { fake2 });
        f3.StartPosition = FormStartPosition.Manual;
        f3.Location = new Point(-3000, -3000);
        f3.Show();
        Application.DoEvents();
        fake2.FireCode("TRUECODE456");
        bool closed2 = false;
        for (int i = 0; i < 40 && !closed2; i++)
        {
            System.Threading.Thread.Sleep(50);
            Application.DoEvents();
            closed2 = !f3.Visible || f3.IsDisposed;
        }
        Check("异常窗读到真码自动关闭", closed2);
        if (!f3.IsDisposed) { f3.Close(); f3.Dispose(); }
    }
}
