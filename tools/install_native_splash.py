"""Install the localized native startup logo, with a backup for restoration."""
import argparse
from pathlib import Path
import shutil

from PIL import Image
import UnityPy

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("game", type=Path, help="BirdGut installation directory (game must be closed)")
parser.add_argument("backup", type=Path, help="Path for the original .resS backup")
args = parser.parse_args()
data = args.game.resolve() / "BirdGut_Data"
serialized = data / "globalgamemanagers.assets"
stream = data / "globalgamemanagers.assets.resS"
source = Path(__file__).resolve().parents[1] / "assets" / "textures" / "NarcissismSplash.png"

def load_logo():
    env = UnityPy.load(str(serialized))
    return next(o.read() for o in env.objects if o.type.name == "Texture2D"
                and o.path_id == 3 and o.read().m_Name == "NarcissismSplash")

logo = load_logo()
info = logo.m_StreamData
if logo.m_TextureFormat != 4 or logo.m_MipCount != 1 or info.path != stream.name:
    raise SystemExit("Unexpected format for the native splash texture.")
with Image.open(source) as image:
    replacement = image.convert("RGBA")
if replacement.size != (logo.m_Width, logo.m_Height):
    raise SystemExit("Splash replacement dimensions do not match.")
pixels = replacement.transpose(Image.Transpose.FLIP_TOP_BOTTOM).tobytes()
if len(pixels) != info.size or info.offset + info.size > stream.stat().st_size:
    raise SystemExit("Unexpected splash stream range.")
with stream.open("rb") as file:
    file.seek(info.offset)
    original = file.read(info.size)
if original == pixels:
    print("The native splash is already localized.")
    raise SystemExit(0)
args.backup.parent.mkdir(parents=True, exist_ok=True)
with stream.open("rb") as src, args.backup.open("xb") as dst:
    shutil.copyfileobj(src, dst)

def write_block(payload):
    with stream.open("r+b") as file:
        file.seek(info.offset)
        file.write(payload)

try:
    write_block(pixels)
    if load_logo().image.convert("RGBA").tobytes() != replacement.tobytes():
        raise RuntimeError("Installed splash did not decode to the expected image.")
except Exception:
    write_block(original)
    raise
print(f"Verified native splash: {replacement.width} x {replacement.height}; backup: {args.backup}")
