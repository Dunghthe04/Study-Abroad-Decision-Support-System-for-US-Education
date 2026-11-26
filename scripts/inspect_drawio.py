import xml.etree.ElementTree as ET

tree = ET.parse('01_architecture_overview.drawio')
root = tree.getroot()

ids_to_check = ['p2', 'p11', 'p16', 'p20', 'p21', 'p23', 'p25', 'p26', 'p27', 'p28', 'p29', 'p31', 'p33', 'p34', 'p36', 'p38', 'p39', 'p41', 'p43', 'p45', 'p47', 'p56', 'p57', 'p58', 'p59', 'p60', 'p61', 'p62', 'p63', 'p64']

for elem in root.iter('mxCell'):
    eid = elem.get('id', '')
    if eid in ids_to_check:
        geo = elem.find('mxGeometry')
        gx = geo.get('x') if geo is not None else None
        gy = geo.get('y') if geo is not None else None
        gw = geo.get('width') if geo is not None else None
        gh = geo.get('height') if geo is not None else None
        v = elem.get('value', '')[:50].replace('\n', ' ')
        print(f"{eid} (is_edge={elem.get('edge')=='1'}): x={gx}, y={gy}, w={gw}, h={gh}, val={v}")
        if geo is not None:
            pts = geo.findall('mxPoint')
            for pt in pts:
                print(f"   pt: as={pt.get('as')} x={pt.get('x')} y={pt.get('y')}")
        if elem.get('style'):
            print(f"   style: {elem.get('style')}")
