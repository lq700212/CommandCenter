# -*- coding: utf-8 -*-
"""工控机一键激活脚本（光阑视界 IrisVision，与 AgingTestSystem/HJVision 同源同口径）。
把原来四步手工活（一：选项菜单【软件授权】抄设备ID/设备码；
二：回办公室用《获取激活码》工具算出激活码；三：回工控机手工输入点激活；
四：出厂手写 MainSetting.ini 的 RunHash1 设备绑定）收成一次双击。
做什么：读本机 CPU 序列号 → 按产品内公式算出两键 → 备份后写入程序目录
    MainSetting.ini [RunHash] → 回读重算状态并打印结论。
为什么这么写：公式与 Services/SoftwareActivation.Encrypt 逐字节一致
    （MD5 取前 15 字节 hex，共 30 字符；输入全是 ASCII，编码无差异），
    写盘走 kernel32 INI API（与产品同一条路，不破坏 ini 里其它段）；
    RunHash1（设备绑定）和 RunHash2（永久/试用起点）一次写齐，
    新机不用再找厂商手写第一键。
怎么用：拷到工控机程序目录（IrisVision.exe 旁边）双击即可，
    默认永久激活；试用 30 天加参数 --mode trial；只看码不写盘加 --dry-run。
依赖：仅 Python 标准库（3.6+），不用装任何第三方包。"""
import argparse
import ctypes
import datetime
import hashlib
import os
import shutil
import subprocess
import sys

SECTION = "RunHash"
KEY_DEVICE = "RunHash1"
KEY_RUNTIME = "RunHash2"
INI_NAME = "MainSetting.ini"
TOTAL_SLOTS = 840
VALID_SLOTS = 768


def encrypt(text):
    """与产品 SoftwareActivation.Encrypt /《获取激活码》Form1.Encrypt 一致。
    MD5 取前 15 字节，每字节两位小写 hex，共 30 字符。
    输入恒为 ASCII（CPU 号与 "A"/"1"/"30"/"ALL"/数字后缀），
    所以 C# 的 Encoding.Default 与这里的 ascii 结果一字不差。"""
    return hashlib.md5(text.encode("ascii")).hexdigest()[:30]


def _run_hidden(args, timeout=20):
    """后台跑一条取 CPU 号的命令，不弹黑窗，超时返回空串。"""
    try:
        kw = {}
        if os.name == "nt":
            si = subprocess.STARTUPINFO()
            si.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            kw["startupinfo"] = si
        out = subprocess.check_output(
            args, stderr=subprocess.STDOUT, timeout=timeout,
            universal_newlines=True, **kw)
        return out or ""
    except Exception:
        return ""


def get_cpu_id():
    """取第一块 CPU 的 ProcessorId（与产品 GetCpuSerialNumber 同口径）。"""
    out = _run_hidden(["powershell", "-NoProfile", "-NonInteractive",
                       "-Command",
                       "Get-CimInstance Win32_Processor | "
                       "Select-Object -First 1 -ExpandProperty ProcessorId"])
    for token in out.replace(",", " ").split():
        token = token.strip()
        if token and token.lower() not in ("processorid",):
            return token
    out = _run_hidden(["wmic", "cpu", "get", "ProcessorId"])
    for line in out.splitlines():
        line = line.strip()
        if line and line.lower() != "processorid":
            return line.split()[0]
    return ""


def compute_all(cpu_id):
    """由 CPU 号算出全部码值，键名与激活窗/工具两侧的叫法对齐。"""
    device_code = encrypt(cpu_id + "1")          # 激活窗显示的"设备码"
    return {
        "cpu_id": cpu_id,
        "device_id_code": encrypt(cpu_id + "A"),  # 出厂写 RunHash1 的值
        "device_code": device_code,               # 工具"设备码"框的输入
        "code_30": encrypt(device_code + "30"),   # 工具"30天激活码"
        "code_perm": encrypt(device_code + "ALL"),  # 工具"永久激活码"
        "runhash1": encrypt(cpu_id + "A"),
        "runhash2_perm": encrypt(cpu_id + "ALL"),  # 永久：RunHash2 定格于此
        "runhash2_trial": encrypt(cpu_id + "0"),   # 30天：RunHash2 从第 0 格起
    }


def find_slot(stored_runhash2, cpu_id):
    """找计数格 0..839（与主窗 LicenseTimer_Tick 的 for 循环一致），找不到返回 -1。"""
    if not stored_runhash2:
        return -1
    for i in range(TOTAL_SLOTS):
        if stored_runhash2 == encrypt(cpu_id + str(i)):
            return i
    return -1


def compute_status(runhash1, runhash2, cpu_id):
    """与产品 ComputeStatus 同判定：先设备绑定，再永久，再计数格。"""
    if not runhash1 or runhash1 != encrypt(cpu_id + "A"):
        return "NewDevice", -1, 0
    if (runhash2 or "") == encrypt(cpu_id + "ALL"):
        return "Permanent", -1, 0
    slot = find_slot(runhash2, cpu_id)
    if slot < 0 or slot >= VALID_SLOTS:
        return "Expired", slot, 0
    return "InTrial", slot, 30 - slot // 24


def status_text(status, days_left):
    return {
        "Permanent": "永久使用",
        "InTrial": "试用中，剩余 %d 天" % days_left,
        "NewDevice": "未绑定设备（新设备）",
        "Expired": "已过期",
    }.get(status, "未知")


# ---------- ini 读写（kernel32，与产品同一条路；失败才走文本兜底） ----------

def _kernel32():
    if os.name != "nt":
        return None
    try:
        return ctypes.windll.kernel32
    except Exception:
        return None


def ini_read(path, key):
    k32 = _kernel32()
    if k32 is not None:
        try:
            buf = ctypes.create_unicode_buffer(1024)
            k32.GetPrivateProfileStringW(SECTION, key, "", buf, len(buf), path)
            return buf.value
        except Exception:
            pass
    return _ini_read_text(path, key)


def ini_write(path, key, value):
    k32 = _kernel32()
    if k32 is not None:
        try:
            if k32.WritePrivateProfileStringW(SECTION, key, value, path):
                return True
        except Exception:
            pass
    return _ini_write_text(path, key, value)


def _read_ini_text_raw(path):
    for enc in ("utf-16", "utf-8-sig", "utf-8", "gbk"):
        try:
            with open(path, "r", encoding=enc) as f:
                return f.read(), enc
        except (UnicodeError, UnicodeDecodeError):
            continue
        except FileNotFoundError:
            return "", "utf-16"
    return "", "utf-16"


def _ini_read_text(path, key):
    text, _ = _read_ini_text_raw(path)
    in_section = False
    for line in text.splitlines():
        s = line.strip()
        if s.startswith("[") and s.endswith("]"):
            in_section = (s[1:-1].strip() == SECTION)
        elif in_section and "=" in s and not s.startswith(";"):
            k, _, v = s.partition("=")
            if k.strip() == key:
                return v.strip()
    return ""


def _ini_write_text(path, key, value):
    """文本兜底：只改 [RunHash] 段内对应键，其它段原样保留。"""
    try:
        text, enc = _read_ini_text_raw(path)
        lines = text.splitlines() if text else []
        out, in_section, done = [], False, False
        for line in lines:
            s = line.strip()
            if s.startswith("[") and s.endswith("]"):
                if in_section and not done:
                    out.append("%s=%s" % (key, value))
                    done = True
                in_section = (s[1:-1].strip() == SECTION)
                out.append(line)
            elif in_section and "=" in s and not s.startswith(";") \
                    and s.partition("=")[0].strip() == key:
                out.append("%s=%s" % (key, value))
                done = True
            else:
                out.append(line)
        if not done:
            if not any(l.strip() == "[%s]" % SECTION for l in out):
                out.append("[%s]" % SECTION)
            out.append("%s=%s" % (key, value))
        d = os.path.dirname(os.path.abspath(path))
        if d and not os.path.isdir(d):
            os.makedirs(d)
        with open(path, "w", encoding=enc) as f:
            f.write("\n".join(out) + "\n")
        return True
    except Exception:
        return False


def resolve_ini_path(explicit_ini, exe_dir):
    """定位 MainSetting.ini：显参 > --exe-dir > 有 exe/ini 的当前目录 > 脚本目录。"""
    if explicit_ini:
        return os.path.abspath(explicit_ini)
    if exe_dir:
        return os.path.join(os.path.abspath(exe_dir), INI_NAME)
    cwd = os.getcwd()
    if os.path.isfile(os.path.join(cwd, INI_NAME)) or \
            os.path.isfile(os.path.join(cwd, "IrisVision.exe")):
        return os.path.join(cwd, INI_NAME)
    return os.path.join(os.path.dirname(os.path.abspath(__file__)), INI_NAME)


def backup_ini(path):
    try:
        if os.path.isfile(path):
            stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
            bak = "%s.bak.%s" % (path, stamp)
            shutil.copy2(path, bak)
            return bak
    except Exception:
        pass
    return ""


def main(argv=None):
    ap = argparse.ArgumentParser(description="光阑视界 IrisVision 工控机一键激活")
    ap.add_argument("--mode", choices=["permanent", "trial"],
                    default="permanent", help="permanent=永久（默认），trial=30天试用")
    ap.add_argument("--ini", default="", help="MainSetting.ini 全路径（缺省自动找程序目录）")
    ap.add_argument("--exe-dir", default="",
                    help="程序目录（IrisVision.exe 所在目录，缺省用当前目录/脚本目录）")
    ap.add_argument("--dry-run", action="store_true", help="只算码打印，不写盘")
    ap.add_argument("--no-pause", action="store_true", help="结束不等待按键（计划任务用）")
    args = ap.parse_args(argv)

    print("=" * 60)
    print("光阑视界 IrisVision 一键激活（与软件内【选项】→【软件授权】同口径）")
    print("=" * 60)

    cpu_id = get_cpu_id()
    if not cpu_id:
        print("[失败] 读不到 CPU 序列号（WMI），请检查 WMI 服务后重试。")
        return 1
    print("[1/4] 本机设备ID：%s" % cpu_id)

    c = compute_all(cpu_id)
    print("[2/4] 设备码：%s" % c["device_code"])
    print("      设备ID码（RunHash1）：%s" % c["device_id_code"])
    print("      30天激活码：%s" % c["code_30"])
    print("      永久激活码：%s" % c["code_perm"])
    print("      （以上四码与《获取激活码》工具算出的完全一致，可存档备查）")

    want_runhash2 = c["runhash2_perm"] if args.mode == "permanent" else c["runhash2_trial"]
    print("[3/4] 本次写入：RunHash1=%s / RunHash2=%s（%s）"
          % (c["runhash1"], want_runhash2,
             "永久" if args.mode == "permanent" else "30天试用"))

    ini_path = resolve_ini_path(args.ini, args.exe_dir)
    print("      目标文件：%s" % ini_path)

    if args.dry_run:
        print("[试算模式] 未写盘。若码值无误，去掉 --dry-run 重跑一次即写盘。")
        return 0

    bak = backup_ini(ini_path)
    if bak:
        print("      已备份旧文件：%s" % bak)
    if not ini_write(ini_path, KEY_DEVICE, c["runhash1"]) or \
            not ini_write(ini_path, KEY_RUNTIME, want_runhash2):
        print("[失败] 写盘失败。程序若装在 C 盘请右键“以管理员身份运行”后重试。")
        return 1

    h1 = ini_read(ini_path, KEY_DEVICE)
    h2 = ini_read(ini_path, KEY_RUNTIME)
    status, _, days = compute_status(h1, h2, cpu_id)
    print("[4/4] 回读校验：%s" % status_text(status, days))
    ok = (status == "Permanent" and args.mode == "permanent") or \
         (status == "InTrial" and args.mode == "trial")
    if not ok:
        print("[失败] 回读状态与预期不符，请把上方码值发给维护人员排查。")
        return 1

    print("-" * 60)
    print("[成功] 已激活（%s）。请重启光阑视界 IrisVision，"
          "【选项】→【软件授权】应显示“%s”。"
          % (status_text(status, days), status_text(status, days)))
    print("      提示：激活前若软件开着，先关掉再跑本脚本，重启后生效。")
    return 0


if __name__ == "__main__":
    code = main()
    try:
        if "--no-pause" not in (sys.argv or []) and sys.stdin.isatty():
            input("按回车键退出...")
    except Exception:
        pass
    sys.exit(code)
