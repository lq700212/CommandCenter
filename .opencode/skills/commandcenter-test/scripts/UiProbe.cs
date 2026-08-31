// ═══════════════════════════════════════════════════════════════════════════
// CommandCenter UI 交互回归探针（commandcenter-test skill 的 uitests.ps1 编译运行）
//
// 【定位】补 TestRunner.cs 覆盖不到的"真实窗体交互"层：拉起【真实产品窗体】WindowPointForm，
//   用代码模拟用户点选下拉、切型号/相机、点确定，断言界面显示与写回配置的值。
//   不需要现场设备、不连 PLC/相机，纯内存配置 + 真实 UI 控件。
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
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using CommandCenter.Models;
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
}
