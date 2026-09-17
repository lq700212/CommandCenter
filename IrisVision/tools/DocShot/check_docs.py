#!/usr/bin/env python3
# check_docs.py —— 培训文档一键核验（单脚本唯一标准，抄 skill §十一，按本项目适配）。
# checks: 空文件/编码/truncated/图片引用↔文件/PDF配对/html残留/客户+操作员版禁词/scope。
# 用法：python check_docs.py [docs目录] [images目录]
# trio 名单（改名同步改 TRIO）：三份手册 md 必须有同名 pdf（>50KB）。
# 禁词表（改名单同步改 BANNED_SUB/BANNED_RE）：只查"操作员/客户"版，内部版可写。
import os
import re
import subprocess
import sys

DOCS = sys.argv[1] if len(sys.argv) > 1 else "docs"
IMGS = sys.argv[2] if len(sys.argv) > 2 else os.path.join(DOCS, "images")
TRIO = ["光阑视界操作员手册.md", "光阑视界客户技术手册.md", "光阑视界内部手册.md"]
PUBLIC_PREFIXES = ("光阑视界操作员", "光阑视界客户")  # 对外两版：禁词 + 无隐藏账号
BANNED_SUB = ["发码", "后门", "dev123"]  # dev 初始密码不进对外版（内部版可写）
BANNED_RE = [r"\bdev\b"]  # 词边界：开发者/Developer 等中文词不受影响

fail = 0


def bad(msg):
    global fail
    fail += 1
    print("FAIL: " + msg)


def ok(msg):
    print("OK: " + msg)


texts = {}
for m in sorted(os.listdir(DOCS)):
    if not m.endswith(".md"):
        continue
    try:
        with open(os.path.join(DOCS, m), encoding="utf-8") as fh:
            texts[m] = fh.read()
        if not texts[m]:
            bad(m + " 空文件")
    except Exception as e:
        bad(m + " 编码异常: " + str(e))
if any("truncated" in t for t in texts.values()):
    bad("截断标记残留")
else:
    ok("无截断标记")
if any("<!--SEC" in t or "<!--more-->" in t for t in texts.values()):
    bad("写作锚点残留")
else:
    ok("无写作锚点残留")

pat = re.compile(r"images/[A-Za-z0-9_.-]+\.(?:png|jpg)")
refs = sorted({m.group(0) for t in texts.values() for m in pat.finditer(t)})
for r in refs:
    if not os.path.isfile(os.path.join(DOCS, r.replace("/", os.sep))):
        bad("引用缺文件: " + r)
files = sorted(f for f in os.listdir(IMGS) if f.endswith(".png")) if os.path.isdir(IMGS) else []
for f in files:
    if ("images/" + f) not in refs:
        print("WARN: 无人引用 " + f)
ok("图片引用 %d 个，文件 %d 个" % (len(refs), len(files)))

for m in TRIO:  # trio：md 必须有同名 pdf
    if m in texts:
        p = os.path.join(DOCS, os.path.splitext(m)[0] + ".pdf")
        if not (os.path.isfile(p) and os.path.getsize(p) > 50 * 1024):
            bad("缺 PDF 或过小: " + p)
    else:
        bad("缺手册: " + m)
ok("PDF 配对检查完")

leftover = [f for f in os.listdir(DOCS)
            if f.endswith(".html") and os.path.isfile(os.path.join(DOCS, f))]
if leftover:
    bad("html 残留未清: " + ",".join(leftover))
else:
    ok("无 html 残留")

for w in BANNED_SUB:  # 对外版禁词（子串匹配）
    hit = [m for m, t in texts.items() if m.startswith(PUBLIC_PREFIXES) and w in t]
    if hit:
        bad("对外版含禁词[%s]: %s" % (w, ",".join(hit)))
for w in BANNED_RE:  # 账号类禁词走词边界（防误杀中文字段）
    hit = [m for m, t in texts.items()
           if m.startswith(PUBLIC_PREFIXES) and re.search(w, t, re.IGNORECASE)]
    if hit:
        bad("对外版含禁词[%s]: %s" % (w, ",".join(hit)))
ok("对外版禁词检查完")

try:
    print(subprocess.run(["git", "status", "--short"],
          capture_output=True, text=True, timeout=30).stdout)
except Exception as e:
    print("WARN: git status 失败：" + str(e))
print("ALL PASS" if fail == 0 else "FAILURES=%d" % fail)
sys.exit(1 if fail else 0)
