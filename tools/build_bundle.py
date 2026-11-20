"""Build the BirdGut Unity 2017 bundle using the compatible source font bundle."""
from pathlib import Path
import UnityPy
from UnityPy.enums import ClassIDType
from UnityPy.files import ObjectReader
from UnityPy.files.SerializedFile import SerializedType
from UnityPy.helpers import Tpk
from UnityPy.streams import EndianBinaryWriter

assets = Path(__file__).resolve().parents[1] / "assets"
source = UnityPy.load(str(assets / "birdgut-ru-font"))
bundle = next(iter(source.files.values()))
catalog = next(obj for obj in source.objects if obj.type == ClassIDType.AssetBundle)
serialized = catalog.assets_file
tree = catalog.read_typetree()
tree["m_Name"] = tree["m_AssetBundleName"] = "rtlc"
text_type = SerializedType.__new__(SerializedType)
text_type.__attrs_init__(int(ClassIDType.TextAsset), is_stripped_type=False, old_type_hash=bytes(16),
                         node=Tpk.get_typetree_node(int(ClassIDType.TextAsset), serialized.version))
for node in text_type.node.traverse():
    node.m_TypeFlags = int(node.m_Type == "Array")
type_index = len(serialized.types)
serialized.types.append(text_type)

inputs = [assets / name for name in ("translations.json", "textures_meta.json", "stomach-letter.png")]
for folder in ("textures", "intro-logo"):
    inputs.extend(sorted((assets / folder).glob("*.png")))

def write_string(writer, value):
    writer.write_int(len(value))
    writer.write_bytes(value)
    writer.align_stream()

for path_id, path in enumerate(inputs, start=max(serialized.objects) + 1):
    address = "assets/" + path.relative_to(assets).as_posix().lower() + ".bytes"
    writer = EndianBinaryWriter(endian=serialized.header.endian)
    write_string(writer, path.name.encode("utf-8"))
    write_string(writer, path.read_bytes())
    data = writer.bytes
    serialized.objects[path_id] = ObjectReader(
        serialized, catalog.reader, path_id, type_index, text_type, int(ClassIDType.TextAsset),
        ClassIDType.TextAsset, 0, len(data), None, None, data)
    pointer = {"m_FileID": 0, "m_PathID": path_id}
    tree["m_Container"].append((address, dict(preloadIndex=len(tree["m_PreloadTable"]), preloadSize=1, asset=pointer)))
    tree["m_PreloadTable"].append(pointer)

tree["m_Container"].sort(key=lambda entry: entry[0])
catalog.save_typetree(tree)
output = assets / "rtlc.bundle"
output.write_bytes(bundle.save(packer="lz4"))
check = UnityPy.load(str(output))
objects = {o.path_id: o for o in check.objects}
result = next(o.read_typetree() for o in objects.values() if o.type == ClassIDType.AssetBundle)
addresses = dict(result["m_Container"])
for path in inputs:
    address = "assets/" + path.relative_to(assets).as_posix().lower() + ".bytes"
    obj = objects[addresses[address]["asset"]["m_PathID"]]
    assert obj.read().m_Script.encode("utf-8", "surrogateescape") == path.read_bytes(), address
assert objects[addresses["assets/slippysloppy.ttf"]["asset"]["m_PathID"]].type == ClassIDType.Font
print(f"{output}: {len(inputs)} verified byte assets and one Font")
