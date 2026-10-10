import xml.etree.ElementTree as ET
import re, json, sys

NS_T = "urn:oasis:names:tc:opendocument:xmlns:table:1.0"
NS_TX = "urn:oasis:names:tc:opendocument:xmlns:text:1.0"
NS_OFF = "urn:oasis:names:tc:opendocument:xmlns:office:1.0"
T = f"{{{NS_T}}}"
TXP = f"{{{NS_TX}}}p"

ITEM_SHEETS = [
    "Fruits", "Veggies", "Flowers", "Animal Products", "Seeds", "Fertilizer",
    "Geodes", "Gems", "Minerals", "Resources", "Forage", "Fish", "Smoked Fish",
    "Roe", "Bait", "Monster Drops", "Artisan Goods Jar", "Artisan Goods Keg",
    "Artisan Goods Dehydrator", "Artisan Goods Other", "Cooking",
    "MachinesApparatuses", "Consumables", "Books", "GiftsRewards", "Trash",
    "Artifacts", "Decor Lighting", "Decor Infrastructure", "Decor Other",
]
AGGREGATE_ROWS = re.compile(
    r"(extra$|^category:?$|^wild seeds: total$)", re.I)

root = ET.parse("content.xml").getroot()
tables = {t.get(T+"name"): t for t in root.iter(T+"table")}

# --- Pass 1: physical row index per sheet (sheet -> {rownum: label}), label-only, unfiltered ---
physical = {}
for sheet in ITEM_SHEETS:
    t = tables[sheet]
    idx = {}
    rownum = 0
    for r in t.iter(T+"table-row"):
        reps = int(r.get(T+"number-rows-repeated", "1"))
        cells = list(r)
        label = ""
        if cells:
            label = "".join("".join(p.itertext()) for p in cells[0].findall(TXP)).strip()
        for k in range(reps):
            rownum += 1
            if label:
                idx[rownum] = label
    physical[sheet] = idx

# sanity: label -> rownum (first occurrence) per sheet, for ref resolution
label_to_row = {}
for sheet, idx in physical.items():
    m = {}
    for rn, lbl in idx.items():
        m.setdefault(lbl, rn)
    label_to_row[sheet] = m

# --- Tokenizer / parser for "of:=..." formulas ---
TOKEN_RE = re.compile(r"""
    (?P<ws>\s+)
  | (?P<num>\d+\.\d+|\d+)
  | (?P<ref>\[(?P<abs1>\$)?(?:'(?P<sheetq>[^']+)'|(?P<sheet>[A-Za-z0-9_]+))?\.(?P<col>\$?[A-Z]+)(?P<row>\$?\d+)(?::\.?(?P<col2>\$?[A-Z]+)?(?P<row2>\$?\d+))?\])
  | (?P<func>SUM|MIN|MAX)\(
  | (?P<lparen>\()
  | (?P<rparen>\))
  | (?P<semi>;)
  | (?P<plus>\+)
  | (?P<star>\*)
  | (?P<slash>/)
""", re.VERBOSE)

class Node(dict):
    pass

def parse_formula(formula_str, self_sheet):
    assert formula_str.startswith("of:=")
    body = formula_str[4:]
    pos = [0]
    n = len(body)

    def peek():
        while pos[0] < n and body[pos[0]] == ' ':
            pos[0] += 1
        if pos[0] >= n:
            return None
        m = TOKEN_RE.match(body, pos[0])
        if not m or m.start() != pos[0]:
            raise ValueError(f"tokenize fail at {pos[0]} in {body!r}")
        return m

    def advance():
        m = peek()
        pos[0] = m.end()
        return m

    def parse_expr():
        # expr := term (('+') term)*
        node = parse_term()
        while True:
            m = peek()
            if m and m.lastgroup == "plus":
                advance()
                rhs = parse_term()
                node = {"op": "sum", "args": [node, rhs]} if node.get("op") != "sum" else {"op": "sum", "args": node["args"] + [rhs]}
            else:
                break
        return node

    def parse_term():
        # term := factor (('*'|'/') NUMBER_OR_FACTOR)*
        node = parse_factor()
        while True:
            m = peek()
            if m and m.lastgroup == "star":
                advance()
                rhs = parse_factor()
                node = mul_node(node, rhs)
            elif m and m.lastgroup == "slash":
                advance()
                rhs = parse_factor()
                node = div_node(node, rhs)
            else:
                break
        return node

    def mul_node(a, b):
        if a.get("op") == "const" and b.get("op") == "const":
            return {"op": "const", "value": a["value"] * b["value"]}
        if a.get("op") == "const":
            return {"op": "mul", "factor": a["value"], "arg": b}
        if b.get("op") == "const":
            return {"op": "mul", "factor": b["value"], "arg": a}
        raise ValueError(f"non-constant multiplication in {body!r}: {a} * {b}")

    def div_node(a, b):
        if b.get("op") != "const":
            raise ValueError(f"division by non-constant in {body!r}")
        if a.get("op") == "const":
            return {"op": "const", "value": a["value"] / b["value"]}
        return {"op": "div", "arg": a, "divisor": b["value"]}

    def parse_factor():
        m = peek()
        if m is None:
            raise ValueError(f"unexpected end in {body!r}")
        g = m.lastgroup
        if g == "num":
            advance()
            txt = m.group("num")
            return {"op": "const", "value": float(txt) if "." in txt else int(txt)}
        if g == "lparen":
            advance()
            node = parse_expr()
            m2 = peek()
            if not m2 or m2.lastgroup != "rparen":
                raise ValueError(f"expected ) in {body!r} at {pos[0]}")
            advance()
            return node
        if g == "func":
            fname = m.group(0)[:-1]
            advance()
            args = [parse_expr()]
            while True:
                m2 = peek()
                if m2 and m2.lastgroup == "semi":
                    advance()
                    m3 = peek()
                    if m3 and m3.lastgroup == "rparen":
                        break  # trailing empty arg, e.g. "SUM(a;b;)"
                    args.append(parse_expr())
                else:
                    break
            m2 = peek()
            if not m2 or m2.lastgroup != "rparen":
                raise ValueError(f"expected ) after {fname} in {body!r}")
            advance()
            opname = {"SUM": "sum", "MIN": "min", "MAX": "max"}[fname]
            return {"op": opname, "args": args}
        if g == "ref":
            advance()
            sheet = m.group("sheetq") or m.group("sheet") or self_sheet
            col = m.group("col").replace("$", "")
            row = int(m.group("row").replace("$", ""))
            row2_txt = m.group("row2")
            if row2_txt:
                row2 = int(row2_txt.replace("$", ""))
                return {"op": "range", "sheet": sheet, "col": col, "row": row, "row2": row2}
            return {"op": "ref", "sheet": sheet, "col": col, "row": row}
        raise ValueError(f"unexpected token group {g} in {body!r} at {pos[0]}")

    node = parse_expr()
    m = peek()
    if m is not None:
        raise ValueError(f"trailing content in {body!r} at {pos[0]}: {body[pos[0]:]}")
    return node

def resolve_refs(node, physical_idx, self_sheet):
    """Replace {op:ref, sheet, col, row} with {op:ref, sheet, label} (col must be D, i.e. Required)."""
    op = node["op"]
    if op == "ref":
        sheet = node["sheet"]
        if node["col"] != "D":
            raise ValueError(f"unexpected column ref {node} (only D/Required refs expected)")
        lbl = physical_idx.get(sheet, {}).get(node["row"])
        if lbl is None:
            print(f"WARNING: dead ref to blank row -> treating as 0: {sheet} row {node['row']}")
            return {"op": "const", "value": 0}
        return {"op": "ref", "sheet": sheet, "label": lbl}
    if op == "range":
        sheet = node["sheet"]
        if node["col"] != "D":
            raise ValueError(f"unexpected range column {node}")
        refs = []
        for rn in range(node["row"], node["row2"] + 1):
            lbl = physical_idx.get(sheet, {}).get(rn)
            if lbl:
                refs.append({"op": "ref", "sheet": sheet, "label": lbl})
        return {"op": "sum", "args": refs}
    if op == "const":
        return node
    if op == "mul":
        return {"op": "mul", "factor": node["factor"], "arg": resolve_refs(node["arg"], physical_idx, self_sheet)}
    if op == "div":
        return {"op": "div", "divisor": node["divisor"], "arg": resolve_refs(node["arg"], physical_idx, self_sheet)}
    if op in ("sum", "min", "max"):
        return {"op": op, "args": [resolve_refs(a, physical_idx, self_sheet) for a in node["args"]]}
    raise ValueError(f"unknown op {op}")

def eval_tree(node, resolve_ref):
    op = node["op"]
    if op == "const":
        return node["value"]
    if op == "ref":
        return resolve_ref(node["sheet"], node["label"])
    if op == "mul":
        return node["factor"] * eval_tree(node["arg"], resolve_ref)
    if op == "div":
        return eval_tree(node["arg"], resolve_ref) / node["divisor"]
    if op == "sum":
        return sum(eval_tree(a, resolve_ref) for a in node["args"])
    if op == "min":
        return min(eval_tree(a, resolve_ref) for a in node["args"])
    if op == "max":
        return max(eval_tree(a, resolve_ref) for a in node["args"])
    raise ValueError(op)

# --- Pass 2: build filtered item rows (mirrors tracker999.py read_checklist) + capture D-cell formula/value ---
rows = []  # list of dict: sheet,label,required_literal,required_expr(resolved or None), cached_value
row_lookup = {}  # (sheet,label) -> row dict

for sheet in ITEM_SHEETS:
    t = tables[sheet]
    trs = list(t.iter(T+"table-row"))[1:]  # skip header (matches tracker999.py's [1:])
    for r in trs:
        cells = []
        for c in r:
            txt = "\n".join("".join(p.itertext()) for p in c.findall(TXP))
            rep = min(int(c.get(T+"number-columns-repeated", "1")), 20)
            cells += [(txt, c)] * rep
        label = cells[0][0].strip() if cells else ""
        if not label or AGGREGATE_ROWS.search(label):
            continue
        if len(cells) <= 3:
            continue
        dtext, dcell = cells[3]
        formula = dcell.get(T+"formula")
        cached_value = dcell.get(f"{{{NS_OFF}}}value")
        if formula:
            tree = parse_formula(formula, sheet)
            resolved = resolve_refs(tree, physical, sheet)
            # top-level must be `const(999-ish) + rest` (sum) or just the tree itself if no bare leading const
            if resolved["op"] == "sum":
                const_terms = [a for a in resolved["args"] if a["op"] == "const"]
                rest_terms = [a for a in resolved["args"] if a["op"] != "const"]
                base = sum(a["value"] for a in const_terms)
                rest = rest_terms
            elif resolved["op"] == "const":
                base = resolved["value"]
                rest = []
            else:
                base = 0
                rest = [resolved]
            row = {"sheet": sheet, "label": label, "requiredLiteral": base,
                   "requiredExpr": {"op": "sum", "args": rest} if rest else None,
                   "_cached": float(cached_value) if cached_value else None}
        else:
            try:
                lit = float(dtext.replace(",", ""))
                if lit == int(lit):
                    lit = int(lit)
            except ValueError:
                lit = 999
            row = {"sheet": sheet, "label": label, "requiredLiteral": lit, "requiredExpr": None,
                   "_cached": lit}
        rows.append(row)
        row_lookup[(sheet, label)] = row

print(f"Parsed {len(rows)} item rows, {sum(1 for r in rows if r['requiredExpr'])} with cross-sheet formulas")

# --- Validation: Required(row) with live Have=0 everywhere must equal cached office:value ---
memo = {}
def required_of(sheet, label):
    key = (sheet, label)
    if key in memo:
        if memo[key] == "...":
            raise ValueError(f"cycle at {key}")
        return memo[key]
    memo[key] = "..."
    row = row_lookup.get(key)
    if row is None:
        raise ValueError(f"unresolved ref to {key}")
    val = row["requiredLiteral"]
    if row["requiredExpr"]:
        val += eval_tree(row["requiredExpr"], required_of)
    memo[key] = val
    return val

mismatches = []
checked = 0
for row in rows:
    if row["_cached"] is None:
        continue
    computed = required_of(row["sheet"], row["label"])
    checked += 1
    if abs(computed - row["_cached"]) > 0.01:
        mismatches.append((row["sheet"], row["label"], computed, row["_cached"]))

print(f"Validated {checked} rows against cached .ods values")
print(f"Mismatches: {len(mismatches)}")
for m in mismatches[:20]:
    print(" ", m)

# spot checks
for s, l in [("Fruits","Ancient Fruit"), ("Resources","Wood"), ("Monster Drops","Slime"), ("Geodes","Omni Geode")]:
    print(s, l, "=>", required_of(s, l))

# --- Emit JSON (strip internal _cached) ---
out_rows = []
for row in rows:
    out_rows.append({
        "sheet": row["sheet"], "label": row["label"],
        "requiredLiteral": row["requiredLiteral"],
        "requiredExpr": row["requiredExpr"],
    })
with open("checklist999.json", "w") as f:
    json.dump({"sheets": ITEM_SHEETS, "rows": out_rows}, f, indent=1)
print("wrote checklist999.json,", len(out_rows), "rows")
