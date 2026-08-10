# 제출 문서 3종(SUBMIT_3/4/5) → 인쇄용 HTML 변환기 (D7).
# 사용: python build_pdf_html.py  → 같은 폴더에 .html 3개 생성
# PDF 제작: 브라우저에서 열고 Ctrl+P → "PDF로 저장" (A4, 배경 그래픽 켜기 권장).
# "🟡 제출 전 확인" 절은 화면에는 보이고 @media print에서 자동으로 숨겨진다 — 손 삭제 불필요.
import io
import os
import re
import markdown

HERE = os.path.dirname(os.path.abspath(__file__))
DOCS = os.path.normpath(os.path.join(HERE, ".."))

SOURCES = [
    ("SUBMIT_3_게임소개.md", "SUBMIT_3_게임소개.html", "러다이트 2026 — 게임 소개"),
    ("SUBMIT_4_AI활용.md", "SUBMIT_4_AI활용.html", "러다이트 2026 — AI 활용 기술 문서"),
    ("SUBMIT_5_팀역할.md", "SUBMIT_5_팀역할.html", "러다이트 2026 — 팀원 롤 기술서"),
]

CUT_MARKER = "제출 전 확인"   # 이 문구가 든 ## 헤딩부터 문서 끝까지 = 인쇄 제외 절

STYLE = """
body { font-family: 'Malgun Gothic', 'Apple SD Gothic Neo', sans-serif; color: #1a1a1e;
       max-width: 800px; margin: 0 auto; padding: 40px 24px; line-height: 1.65; font-size: 14px; }
h1 { font-size: 26px; border-bottom: 3px solid #c026d3; padding-bottom: 8px; }
h2 { font-size: 20px; margin-top: 36px; border-bottom: 1px solid #ddd; padding-bottom: 4px; }
h3 { font-size: 16px; margin-top: 26px; }
code { background: #f2f0f5; padding: 1px 5px; border-radius: 3px; font-size: 12.5px; }
pre { background: #f2f0f5; padding: 12px; border-radius: 6px; overflow-x: auto; }
pre code { background: none; padding: 0; }
table { border-collapse: collapse; width: 100%; margin: 14px 0; font-size: 13px; }
th, td { border: 1px solid #ccc; padding: 6px 9px; text-align: left; vertical-align: top; }
th { background: #f5f2f8; }
blockquote { border-left: 4px solid #c026d3; margin: 12px 0; padding: 4px 16px; color: #444;
             background: #faf6fc; }
a { color: #9333ea; }
hr { border: none; border-top: 1px solid #ddd; margin: 28px 0; }
.cut { border: 2px dashed #d97706; border-radius: 8px; padding: 4px 18px; margin-top: 32px;
       background: #fffbeb; }
.cut::before { content: "⚠ 아래 절은 인쇄(PDF)에서 자동 제외됩니다 — 화면 확인용";
               display: block; color: #b45309; font-weight: bold; margin: 10px 0; }
@media print {
  body { padding: 0; font-size: 12px; }
  .cut { display: none; }
  a { color: inherit; }
  h2 { page-break-after: avoid; }
  table, pre, blockquote { page-break-inside: avoid; }
}
"""

TEMPLATE = """<!DOCTYPE html>
<html lang="ko"><head><meta charset="utf-8">
<title>{title}</title><style>{style}</style></head>
<body>
{body}
</body></html>
"""


def split_cut_section(md_text):
    """'제출 전 확인' ## 헤딩부터 끝까지를 분리해 (본문, 컷 절)로 돌려준다."""
    lines = md_text.split("\n")
    for i, line in enumerate(lines):
        if line.startswith("## ") and CUT_MARKER in line:
            return "\n".join(lines[:i]), "\n".join(lines[i:])
    return md_text, ""


# 태그와 텍스트를 갈라 텍스트 부분에서만 URL을 찾는다 (HTML 속성 안의 URL을 건드리지 않으려고)
_TAG_SPLIT = re.compile(r"(<[^>]+>)")
_URL = re.compile(r"https?://[^\s<>\"'()\[\]]+")


def autolink(html):
    """본문에 맨몸으로 적힌 URL을 <a>로 감싼다.

    ⚠️ 이게 없으면 **PDF에 클릭 가능한 링크가 하나도 생기지 않는다.**
    Python-Markdown은 표 안의 맨몸 URL을 자동 링크하지 않고, 문서들이 URL을
    `**...**` 나 백틱으로만 적어 뒀다. 제출물 #3은 "PDF 내 링크 클릭 동작"이
    필수 점검 항목이라 여기서 일괄 처리한다 (문서마다 손으로 고치면 또 빠진다).
    """
    parts = _TAG_SPLIT.split(html)
    in_anchor = False
    out = []
    for part in parts:
        if part.startswith("<"):
            lowered = part.lower()
            if lowered.startswith("<a "):
                in_anchor = True
            elif lowered.startswith("</a"):
                in_anchor = False
            out.append(part)
            continue
        if in_anchor:            # 이미 링크 안이면 중첩시키지 않는다
            out.append(part)
            continue

        def wrap(match):
            url = match.group(0).rstrip(".,;:")      # 문장 끝 구두점은 링크에서 뺀다
            tail = match.group(0)[len(url):]
            return '<a href="%s">%s</a>%s' % (url, url, tail)

        out.append(_URL.sub(wrap, part))
    return "".join(out)


def convert(md_text):
    html = markdown.markdown(md_text, extensions=["tables", "fenced_code", "sane_lists"])
    return autolink(html)


def main():
    for source, target, title in SOURCES:
        raw = io.open(os.path.join(DOCS, source), encoding="utf-8").read()
        main_md, cut_md = split_cut_section(raw)

        body = convert(main_md)
        if cut_md:
            body += '\n<div class="cut">\n' + convert(cut_md) + "\n</div>"

        html = TEMPLATE.format(title=title, style=STYLE, body=body)
        out = os.path.join(HERE, target)
        io.open(out, "w", encoding="utf-8", newline="\n").write(html)
        print(f"{target}  ({len(html)} bytes)")


if __name__ == "__main__":
    main()
