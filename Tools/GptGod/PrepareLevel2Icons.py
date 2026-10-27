from pathlib import Path

from PIL import Image
ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets" / "UI" / "Level2Icons"
TARGET = SOURCE / "Prepared"
TARGET.mkdir(parents=True, exist_ok=True)


def prepare(source_path: Path) -> None:
    output = Image.open(source_path).convert("RGBA")

    bounds = output.getchannel("A").getbbox()
    if bounds is not None:
        output = output.crop(bounds)

    scale = min(440 / output.width, 440 / output.height)
    output = output.resize(
        (max(1, round(output.width * scale)), max(1, round(output.height * scale))),
        Image.Resampling.LANCZOS,
    )

    padded = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    padded.alpha_composite(
        output,
        ((512 - output.width) // 2, (512 - output.height) // 2),
    )
    padded.save(TARGET / source_path.name, optimize=True)


for path in sorted(SOURCE.glob("fraud_tool_*.png")):
    prepare(path)

print(f"Prepared {len(list(TARGET.glob('fraud_tool_*.png')))} icons in {TARGET}")
