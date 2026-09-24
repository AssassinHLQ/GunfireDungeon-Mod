# -*- coding: utf-8 -*-
"""把回退字体 FusionPixel 子集化, 只保留"游戏真的会用到的字"。

为什么可以这么做:
  ArkPixel(主字体) 24471 字, FusionPixel(回退) 36558 字, 而且 FusionPixel 是 ArkPixel 的超集
  (方舟有的字 FusionPixel 全都有)。回退字体只在【主字体没有某个字】时才被查询,
  所以它只要覆盖"游戏里会出现、且方舟没有"的那部分就够。
  实测游戏一共用到 1466 个不同字符, 其中方舟缺的只有 59 个。
  这里【保守地保留全部用到的字 + 一份常用字余量】, 以后改文案不会因为漏字出方框。

用法(改完文案后重跑一次):
  uv run --with fonttools python subset_fusion.py

产物:
  resource/font/FusionPixel-12px-zh_hans.ttf    子集后的字体(覆盖)
  resource/font/FusionPixel-subset-chars.md     保留的字表
备份(放在项目外, 免得被 Godot 扫描/打包):
  C:\\Users\\WY157\\Desktop\\GODOT\\_art\\fonttest\\FusionPixel-full-backup.ttf
"""
import glob
import os
import re

from fontTools import subset as ftsubset
from fontTools.ttLib import TTFont

PROJ = r"C:\Users\WY157\Desktop\GunfireDungeon\GunfireDungeon_Godot"
FONTDIR = os.path.join(PROJ, "resource", "font")
FUS = os.path.join(FONTDIR, "FusionPixel-12px-zh_hans.ttf")
# 备份必须放在项目外, 否则 Godot 会扫描它、导入它、并把它打进 pck
BACKUP = r"C:\Users\WY157\Desktop\GODOT\_art\fonttest\FusionPixel-full-backup.ttf"

SRC_GLOBS = ["**/*.json", "**/*.tscn", "**/*.tres", "**/*.cs", "**/*.gd", "*.godot"]
SKIP_DIRS = ("\\.godot\\", "\\obj\\", "\\bin\\")

COMMON = (
    "的一是不了在人有我他这个们中来上大为和国地到以说时要就出会可也你对生能而子那得于着下自之年过发后作里用道行所然家种事成方多经么去法学如都同现当没动面起看定天分还进好小部其些主样理心她本前开但因只从想实日军者意无力它与长把机十民第公此已工使情明性知全三又关点正业外将两高间由问很最重并物手应战向头文体政美相见被利什二等产或新己制身果加西斯月话合回特代内信表化老给世位次度门任常先海通教儿原东声提立及比员解水名真论处走义各入几口认条平系气题活尔更别打女变四神总何电数安少报才结反受目太量再感建务做接必场件计管期市直德资命山金指克许统区保至队形社便空决治展马科司五基眼书非则听白却界达光放强即像难且权思王象完设式色路记南品住告类求据程北边死张该交规万取拉格望觉术领共确传师观清今切院让识候带导争运笑飞风步改收根干造言联持组每济车亲极林服快办议往元英士证近失转夫令准布始怎呢存未远叫台单影具罗字爱击流备兵连调深商算质团集百需价花党华城石级整府离况亚请技际约示复病息究线似官火断精满支视消越器容照须九增研写称企八功吗包片史委乎查轻易早曾除农找装广显吧阿李标谈吃图念六引历首医局突专费号尽另周较注语仅考落青随选列武红响虽推势参希古众构房半节土投某案黑维革划敌致陈律足态护七兴派孩验责营星够章音跟志底站严巴例防族供效续施留讲型料终答紧黄绝奇察母京段依批群项故按河米围江织害斗双境客纪采举杀攻父苏密低朝友诉止细愿千值仍男钱破网热助倒育属坐帝限船脸职速刻乐否刚威毛状率甚独球般普怕弹校苦创假久错承印晚兰试股拿脑预谁益阳若哪微尼继送急血惊伤素药适波夜省初喜卫源食险待述陆习置居劳财环排福纳欢雷警获模充负云停木游龙树疑层冷洲冲射略范竟句室异激汉村哈策演简卡罪判担州静既衣您宗积余痛检差富灵协角占配征修皮挥胜降阶审沉坚善妈刘读啊超免压银买皇养伊怀执副乱抗犯追帮宣佛岁航优怪香著田铁控税左右份穿艺背阵草脚概恶块顿敢守酒岛托央户烈洋哥索胡款靠评版宝座释景顾弟登货互付伯慢欧换闻危忙核暗姐介坏讨丽良序升监临亮露永呼味野架域沙掉括舰鱼杂误湾吉减编楚肯测败屋跑梦散温困剑渐封救贵枪缺楼县尚毫移娘朋画班智亦耳恩短掌恐遗固席尊严禁刑词伸汽命晓趁射童"
    "，。、；：？！（）《》【】“”‘’…—－·＋＝／％＃＆＊＠～×÷±≈≠≤≥°"
    "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
    " !\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~"
)


def collect_used():
    used = set()
    files = 0
    for g in SRC_GLOBS:
        for p in glob.glob(os.path.join(PROJ, g), recursive=True):
            if any(s in p for s in SKIP_DIRS):
                continue
            try:
                used.update(open(p, encoding="utf-8", errors="ignore").read())
                files += 1
            except Exception:
                pass
    for p in glob.glob(os.path.join(PROJ, "resource", "config", "*.json")):
        txt = open(p, encoding="utf-8", errors="ignore").read()
        for m in re.finditer(r"\\u([0-9a-fA-F]{4})", txt):
            used.add(chr(int(m.group(1), 16)))
    return {c for c in used if c.isprintable() and c != "\ufffd"}, files


def main():
    used, files = collect_used()
    target = used | set(COMMON)
    print(f"扫描 {files} 个文件; 游戏用到 {len(used)} 个不同字符, 加常用字余量后 {len(target)} 个")

    if not os.path.exists(BACKUP):
        print("!! 找不到原始字体备份, 只能从当前字体再切一次 — 结果会越来越小, 请先恢复备份")
        return
    src = BACKUP
    print(f"源字体(备份): {src}")

    f = TTFont(src, fontNumber=0, lazy=True)
    have = set()
    for t in f["cmap"].tables:
        have.update(t.cmap.keys())
    f.close()

    keep = sorted(c for c in target if ord(c) in have)
    dropped = sorted(c for c in target if ord(c) not in have)
    print(f"保留 {len(keep)} 个; 字体里也没有的 {len(dropped)} 个: {''.join(dropped)}")

    orig = os.path.getsize(src)

    opts = ftsubset.Options()
    opts.layout_features = ["*"]
    opts.name_IDs = ["*"]
    opts.name_legacy = True
    opts.name_languages = ["*"]
    opts.notdef_outline = True
    opts.recalc_bounds = True
    opts.recalc_timestamp = False
    opts.drop_tables = ["DSIG", "BASE", "JSTF", "EBDT", "EBLC", "SVG "]

    font = ftsubset.load_font(src, opts)
    ss = ftsubset.Subsetter(options=opts)
    ss.populate(unicodes=[ord(c) for c in keep])
    ss.subset(font)
    ftsubset.save_font(font, FUS, opts)

    new = os.path.getsize(FUS)
    print(f"原始 {orig/1024/1024:.2f} MB -> 子集后 {new/1024:.1f} KB "
          f"(省 {(orig-new)/1024/1024:.2f} MB, {100*(orig-new)/orig:.1f}%)")

    with open(os.path.join(FONTDIR, "FusionPixel-subset-chars.md"), "w", encoding="utf-8") as fp:
        fp.write("# FusionPixel 子集字表\n\n")
        fp.write(f"共 {len(keep)} 个字符，由 `subset_fusion.py` 自动生成，请勿手改。\n\n")
        fp.write("```\n" + "".join(keep) + "\n```\n")
    print(f"字表已写出: FusionPixel-subset-chars.md ({len(keep)} 字)")


if __name__ == "__main__":
    main()
