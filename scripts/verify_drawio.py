import xml.etree.ElementTree as ET

tree = ET.parse('01_architecture_overview.drawio')
root = tree.getroot()

check_ids = ['p57', 'p65', 'p60', 'p38', 'p61', 'p56']
for elem in root.iter('mxCell'):
    eid = elem.get('id', '')
    if eid in check_ids:
        print(f"ID: {eid}")
        print(f"  Val: {elem.get('value', '')}")
        print(f"  Style: {elem.get('style', '')}")
        print(f"  Src: {elem.get('source', '')} -> Tgt: {elem.get('target', '')}")
