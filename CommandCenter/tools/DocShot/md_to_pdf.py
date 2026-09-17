#!/usr/bin/env python3
# md_to_pdf.py —— 培训手册 md 转 PDF（单脚本，链条：markdown → 样式 HTML → Edge 无头打印）。
# 不用 pandoc/weasyprint/Word COM。用法：
#   python md_to_pdf.py [docs目录] [手册名前缀(缺省"光阑视界")] [--keep-html]
# 成功后自删 .html 中间件（--keep-html 仅调试保留）；预览截图 preview_*.png 留给人眼核验，
# 核验通过后手动删除（check_docs 只拦 html，不拦 png，但文档目录不许留无关 png——删）。
import os
import subprocess
import sys

import markdown

DOCS = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "docs"
PREFIX = sys.argv[2] if len(sys.argv) > 2 and not sys.argv[2].startswith("--") else "光阑视界"
KEEP_HTML = "--keep-html" in sys.argv
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
TRIO = ["操作员手册", "客户技术手册", "内部手册"]

CSS = """
body{font-family:"Microsoft YaHei","PingFang SC","SimSun",sans-serif;
  font-size:14px;line-height:1.7;color:#222;max-width:1000px;margin:0 auto;padding:24px;}
h1{font-size:24px;border-bottom:3px solid #3498db;padding-bottom:8px;}
h2{font-size:19px;color:#2c3e50;border-left:5px solid #3498db;padding-left:10px;margin-top:28px;}
table{border-collapse:collapse;width:100%;margin:12px 0;font-size:13px;}
th,td{border:1px solid #999;padding:6px 10px;text-align:left;}
th{background:#dce9f5;print-color-adjust:exact;-webkit-print-color-adjust:exact;}
img{max-width:100%;border:1px solid #ccc;margin:8px 0;}
code{background:#f2f2f2;padding:1px 5px;border-radius:3px;font-size:13px;}
@page{size:A4;margin:18mm 15mm;}
"""

fail = 0
docs_abs = os.path.abspath(DOCS)
for name in TRIO:
    md_path = os.path.join(docs_abs, PREFIX + name + ".md")
    if not os.path.isfile(md_path):
        print("FAIL: 缺 md " + md_path)
        fail += 1
        continue
    with open(md_path, encoding="utf-8") as fh:
        text = fh.read()
    body = markdown.markdown(text, extensions=["tables", "fenced_code"])
    # 图片改绝对 file:// 路径（无头打印不认相对路径）
    body = body.replace('src="images/',
                        'src="file:///' + docs_abs.replace("\\", "/") + '/images/')
    html = ("<html><head><meta charset='utf-8'><style>" + CSS
            + "</style></head><body>" + body + "</body></html>")
    html_path = os.path.join(docs_abs, PREFIX + name + ".html")
    pdf_path = os.path.join(docs_abs, PREFIX + name + ".pdf")
    with open(html_path, "w", encoding="utf-8") as fh:
        fh.write(html)
    # 预览截图（人眼核验字体/图片/表格用）
    shot = os.path.join(docs_abs, "preview_" + name + ".png")
    p1 = subprocess.run([EDGE, "--headless", "--disable-gpu",
                         "--screenshot=" + shot, "--window-size=1400,1000",
                         "--hide-scrollbars", html_path],
                        capture_output=True, timeout=120)
    # 转 PDF（stderr 按字节收再解码：Edge 的 stderr 含非 GBK 字节，text=True 会炸）
    p2 = subprocess.run([EDGE, "--headless", "--disable-gpu",
                         "--print-to-pdf=" + pdf_path, "--no-pdf-header-footer",
                         html_path], capture_output=True, timeout=180)
    err = p2.stderr.decode("utf-8", errors="replace") if p2.stderr else ""
    ok = os.path.isfile(pdf_path) and os.path.getsize(pdf_path) > 50 * 1024
    print(("OK: " if ok else "FAIL: ") + name + " pdf=%dKB %s"
          % (os.path.getsize(pdf_path) // 1024 if os.path.isfile(pdf_path) else 0,
             err.strip().splitlines()[-1] if err.strip() else ""))
    if not ok:
        fail += 1
    if not KEEP_HTML and os.path.isfile(html_path):
        os.remove(html_path)
print("ALL PASS" if fail == 0 else "FAILURES=%d" % fail)
sys.exit(1 if fail else 0)
