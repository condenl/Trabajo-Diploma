"""Prepare field-scoped, abstract use-case changes from an existing Docs export."""
import argparse
import json
from pathlib import Path
from docx import Document

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('baseline')
args = parser.parse_args()
revisions = {
    ('CU-N01-01', 'Flujo Normal', '3.'): '3. Compras informa las cotizaciones recibidas, elige un proveedor y agrega o ajusta los productos, cantidades y precios de la compra.',
    ('CU-N01-01', 'Flujo Normal', '4.'): '4. El sistema presenta el detalle de cada producto y el importe total del borrador, y señala los datos que requieren corrección.',
    ('CU-N01-01', 'Excepciones', 'E2:'): 'E2: Datos inválidos o preparación cancelada. El sistema identifica cotizaciones, cantidades o precios inválidos y solicita corregirlos. Si Compras pide iniciar otra compra o abandonar la preparación, solicita confirmar su descarte; si no lo confirma, conserva los datos.',
    ('CU-N01-03', 'Flujo Normal', '7.'): '7. El sistema muestra las existencias actualizadas y la trazabilidad de la mercadería ingresada hacia el proveedor y la compra de origen.',
    ('CU-N01-03', 'Flujo Normal', '8.'): '8. Recepción verifica el resultado y puede preparar la siguiente entrega.',
    ('CU-N01-03', 'Excepciones', 'E2:'): 'E2: Preparación inválida o cambio de entrega. El sistema identifica los datos de lote, cantidad o vencimiento que deben corregirse. Si Recepción cambia la orden o el producto con datos preparados, solicita confirmar su descarte; si no lo confirma, conserva la entrega original.',
    ('CU-N01-03', 'Excepciones', 'E3:'): 'E3: Diferencia en cantidades. El sistema identifica un producto ajeno a la orden o una cantidad superior a lo pendiente, incluso si otra recepción cambió el saldo; conserva los datos para que Recepción revise la entrega antes de reintentar.',
}
changes = []
for table in Document(args.baseline).tables:
    rows = [[c.text for c in row.cells] for row in table.rows]
    code = next((r[1] for r in rows if r[0] == 'Código'), None)
    if code not in ('CU-N01-01', 'CU-N01-03'):
        continue
    for field, text in rows:
        for line in text.splitlines():
            key = next((key for key in revisions if key[:2] == (code, field) and line.startswith(key[2])), None)
            if key:
                changes.append(dict(code=code, field=field, old=line, new=revisions[key]))
assert len(changes) == len(revisions), (len(changes), len(revisions))
canonical = ROOT / 'Documentacion/casos de uso/Casos_de_Uso_N01_N02.md'
source = canonical.read_text('utf-8')
for change in changes:
    if change['old'] in source:
        assert source.count(change['old']) == 1, change
        source = source.replace(change['old'], change['new'])
    else:
        assert source.count(change['new']) == 1, change
canonical.write_text(source, 'utf-8')
(ROOT / 'Documentacion/EnterpriseArchitect/demo-use-case-replacements.json').write_text(json.dumps(changes, ensure_ascii=False, indent=2), 'utf-8')
print(f'{len(changes)} paragraphs prepared; no tables or sections added')
