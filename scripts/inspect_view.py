import xml.etree.ElementTree as ET

tree = ET.parse('01_architecture_overview.drawio')
root = tree.getroot()

print("=== Elements near View box (x: 320-530, y: 200-420) ===")
for elem in root.iter('mxCell'):
    eid = elem.get('id', '')
    geo = elem.find('mxGeometry')
    if geo is not None:
        try:
            gx = float(geo.get('x', 0) or 0)
            gy = float(geo.get('y', 0) or 0)
            if 320 <= gx <= 530 and 200 <= gy <= 420:
                print(f"{eid}: x={gx}, y={gy}, w={geo.get('width')}, h={geo.get('height')}, style={elem.get('style', '')[:40]}")
        except:
            pass
