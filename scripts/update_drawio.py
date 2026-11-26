import xml.etree.ElementTree as ET

# Read raw file to ensure we preserve exact formatting / encoding
tree = ET.parse('01_architecture_overview.drawio')
root = tree.getroot()

# Find root container (mxCell id="1" or root tag)
model_root = root.find('root')
if model_root is None:
    raise ValueError("Could not find <root> element in XML")

# 1. Update p57 to SMTP 587
p57 = None
for elem in model_root.findall('mxCell'):
    if elem.get('id') == 'p57':
        p57 = elem
        break

if p57 is not None:
    p57.set('value', '<font style="font-size: 13px;">SMTP 587</font>')
    p57.set('style', 'edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#333333;fontSize=12;labelBackgroundColor=#FFFFFF;endArrow=classic;endSize=6;exitX=0.75;exitY=0;exitDx=0;exitDy=0;entryX=0;entryY=0.13;entryDx=0;entryDy=0;entryPerimeter=0;')
    geo = p57.find('mxGeometry')
    if geo is not None:
        geo.set('x', '0.15')
        geo.set('y', '-12')
        # remove targetPoint if needed, or update
        for pt in geo.findall('mxPoint'):
            if pt.get('as') == 'targetPoint':
                geo.remove(pt)
    print("Updated p57 (SMTP 587)")

# 2. Add p65 for HTTPS (Web API -> External services)
p65 = ET.Element('mxCell', {
    'id': 'p65',
    'value': '<font style="font-size: 13px;">HTTPS</font>',
    'style': 'edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;strokeColor=#333333;fontSize=12;labelBackgroundColor=#FFFFFF;endArrow=classic;endSize=6;exitX=0.92;exitY=0;exitDx=0;exitDy=0;entryX=0;entryY=0.28;entryDx=0;entryDy=0;entryPerimeter=0;',
    'parent': '1',
    'source': 'p28',
    'target': 'p2',
    'edge': '1'
})
geo65 = ET.SubElement(p65, 'mxGeometry', {
    'x': '0.15',
    'y': '-12',
    'relative': '1',
    'as': 'geometry'
})
ET.SubElement(geo65, 'mxPoint', {'as': 'offset'})
model_root.append(p65)
print("Added p65 (HTTPS)")

# 3. Update p60 (dashed -> solid)
for elem in model_root.findall('mxCell'):
    if elem.get('id') == 'p60':
        style = elem.get('style', '')
        style = style.replace('dashed=1;', '')
        elem.set('style', style)
        print("Updated p60 (dashed removed -> solid)")
        break

# 4. Update p38 (Embedding API label)
for elem in model_root.findall('mxCell'):
    if elem.get('id') == 'p38':
        elem.set('value', '<b>Embedding API</b><br><font style="font-size:12px" color="#555555">Ollama: bge-m3</font>')
        print("Updated p38 (Embedding API: Ollama: bge-m3)")
        break

# 5. Update p61 (pgvector TLS label)
for elem in model_root.findall('mxCell'):
    if elem.get('id') == 'p61':
        elem.set('value', '<font style="font-size: 13px;">TLS :5432 (pgvector)</font>')
        print("Updated p61 (TLS :5432 (pgvector))")
        break

tree.write('01_architecture_overview.drawio', encoding='utf-8', xml_declaration=False)
print("Successfully written changes to 01_architecture_overview.drawio")
